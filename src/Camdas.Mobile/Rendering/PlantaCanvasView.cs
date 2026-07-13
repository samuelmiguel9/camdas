using System.Collections.Specialized;
using Camdas.Contracts;
using Camdas.Mobile.Rendering;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace Camdas.Mobile.Views;

/// <summary>
/// Canvas de desenho da planta: mostra a imagem base + o traço livre (raster) de cada camada
/// visível + as linhas de cota (a lógica de "o que desenhar" vive em
/// <see cref="PlantaOverlayRenderer"/>, Camdas.Mobile.Core, testável sem Android). Esta classe
/// adiciona o que só pode viver aqui: o ciclo de vida do <see cref="SKCanvasView"/>, o toque
/// (rabiscar/escrever na camada ativa), zoom/pan e desfazer traço.
///
/// Quando <see cref="UsarResolucaoNativa"/> é true (PlantaPage e CamadaEdicaoPage), o bitmap de cada
/// camada é criado no tamanho nativo da imagem base — não no tamanho da tela do aparelho — então o
/// traço fica na mesma proporção da planta independente da resolução de quem desenhou; <see
/// cref="Zoom"/> só controla a transformação visual (canvas.Scale), sem afetar essa resolução.
/// Mantido `false` por padrão pra não alterar o comportamento de quem ainda não usa zoom.
/// </summary>
public sealed class PlantaCanvasView : SKCanvasView
{
    /// <summary>Um traço completo (do toque até soltar), guardado pra permitir desfazer sem apagar a
    /// camada inteira — reconstruído reaplicando todos os traços restantes do zero.</summary>
    private sealed record Traco(List<SKPoint> Pontos, string Cor, float Espessura, bool Apagar);

    private readonly List<Traco> _historicoTracos = [];
    private Traco? _tracoEmAndamento;


    public static readonly BindableProperty CamadasProperty = BindableProperty.Create(
        nameof(Camadas), typeof(IReadOnlyList<CamadaDto>), typeof(PlantaCanvasView),
        defaultValue: null,
        propertyChanged: OnCamadasChanged);

    /// <summary>
    /// A lista de camadas normalmente é a mesma instância (ObservableCollection) reaproveitada pela
    /// ViewModel entre recarregamentos — só o *conteúdo* muda (item adicionado, visibilidade
    /// alternada via substituição de item), não a referência. Sem isto, o canvas só redesenhava na
    /// primeira vez que a lista era associada, nunca mais depois (bug real: criar uma camada nova ou
    /// alternar visível/bloqueada não atualizava a composição da planta principal).
    /// </summary>
    private static void OnCamadasChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var view = (PlantaCanvasView)bindable;

        if (oldValue is INotifyCollectionChanged oldColecao)
            oldColecao.CollectionChanged -= view.OnCamadasCollectionChanged;
        if (newValue is INotifyCollectionChanged novaColecao)
            novaColecao.CollectionChanged += view.OnCamadasCollectionChanged;

        view.InvalidateSurface();
    }

    public static readonly BindableProperty ImagemBaseProperty = BindableProperty.Create(
        nameof(ImagemBase), typeof(SKBitmap), typeof(PlantaCanvasView),
        defaultValue: null,
        propertyChanged: (bindable, _, _) =>
        {
            var view = (PlantaCanvasView)bindable;
            view._imagemBaseEscalada?.Dispose();
            view._imagemBaseEscalada = null;
            view.InvalidateSurface();
        });

    public static readonly BindableProperty ImagensPorCamadaProperty = BindableProperty.Create(
        nameof(ImagensPorCamada), typeof(IDictionary<Guid, SKBitmap>), typeof(PlantaCanvasView),
        defaultValue: null,
        propertyChanged: (bindable, _, _) => ((PlantaCanvasView)bindable).InvalidateSurface());

    public static readonly BindableProperty CamadaAtivaIdProperty = BindableProperty.Create(
        nameof(CamadaAtivaId), typeof(Guid?), typeof(PlantaCanvasView), defaultValue: null);

    public static readonly BindableProperty CorTracoProperty = BindableProperty.Create(
        nameof(CorTraco), typeof(string), typeof(PlantaCanvasView), defaultValue: "#000000");

    public static readonly BindableProperty EspessuraTracoProperty = BindableProperty.Create(
        nameof(EspessuraTraco), typeof(float), typeof(PlantaCanvasView), defaultValue: 6f);

    public static readonly BindableProperty ModoApagarProperty = BindableProperty.Create(
        nameof(ModoApagar), typeof(bool), typeof(PlantaCanvasView), defaultValue: false);

    /// <summary>Fator de escala visual aplicado via canvas.Scale quando <see cref="UsarResolucaoNativa"/>
    /// é true — não afeta a resolução em que o traço é armazenado, só o zoom da visualização/edição.</summary>
    public static readonly BindableProperty ZoomProperty = BindableProperty.Create(
        nameof(Zoom), typeof(float), typeof(PlantaCanvasView),
        defaultValue: 1f,
        propertyChanged: (bindable, _, _) => ((PlantaCanvasView)bindable).InvalidateSurface());

    /// <summary>Ver comentário na classe — desliga o redimensionamento "encaixar no canvas" e passa a
    /// desenhar/armazenar tudo na resolução nativa da imagem base, com <see cref="Zoom"/> controlando
    /// só a apresentação.</summary>
    public static readonly BindableProperty UsarResolucaoNativaProperty = BindableProperty.Create(
        nameof(UsarResolucaoNativa), typeof(bool), typeof(PlantaCanvasView),
        defaultValue: false,
        propertyChanged: (bindable, _, _) => ((PlantaCanvasView)bindable).InvalidateSurface());

    private SKBitmap? _imagemBaseEscalada;
    private SKSizeI _tamanhoImagemBaseEscalada;
    private SKPoint? _ultimoPontoToque;

    public event EventHandler? DesenhoAlterado;

    public PlantaCanvasView()
    {
        EnableTouchEvents = true;
        Touch += OnTouch;
    }

    public IReadOnlyList<CamadaDto>? Camadas
    {
        get => (IReadOnlyList<CamadaDto>?)GetValue(CamadasProperty);
        set => SetValue(CamadasProperty, value);
    }

    public SKBitmap? ImagemBase
    {
        get => (SKBitmap?)GetValue(ImagemBaseProperty);
        set => SetValue(ImagemBaseProperty, value);
    }

    public IDictionary<Guid, SKBitmap>? ImagensPorCamada
    {
        get => (IDictionary<Guid, SKBitmap>?)GetValue(ImagensPorCamadaProperty);
        set => SetValue(ImagensPorCamadaProperty, value);
    }

    public Guid? CamadaAtivaId
    {
        get => (Guid?)GetValue(CamadaAtivaIdProperty);
        set => SetValue(CamadaAtivaIdProperty, value);
    }

    public string CorTraco
    {
        get => (string)GetValue(CorTracoProperty);
        set => SetValue(CorTracoProperty, value);
    }

    public float EspessuraTraco
    {
        get => (float)GetValue(EspessuraTracoProperty);
        set => SetValue(EspessuraTracoProperty, value);
    }

    public bool ModoApagar
    {
        get => (bool)GetValue(ModoApagarProperty);
        set => SetValue(ModoApagarProperty, value);
    }

    public float Zoom
    {
        get => (float)GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public bool UsarResolucaoNativa
    {
        get => (bool)GetValue(UsarResolucaoNativaProperty);
        set => SetValue(UsarResolucaoNativaProperty, value);
    }

    /// <summary>
    /// Força o redesenho — usado pela Page depois de recarregar dados que não disparam sozinhos uma
    /// notificação de mudança (ex.: <see cref="ImagensPorCamada"/> é um Dictionary comum, mutado no
    /// lugar, sem INotifyCollectionChanged).
    /// </summary>
    public void AtualizarPreview() => InvalidateSurface();

    private void OnCamadasCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateSurface();

    /// <summary>Apaga (transparente) o bitmap da camada ativa, sem chamar a Api — só ao Salvar.</summary>
    public void LimparCamadaAtiva()
    {
        if (CamadaAtivaId is not { } camadaId || ImagensPorCamada is null)
            return;

        var bitmap = ObterOuCriarBitmapDaCamada(camadaId);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        _historicoTracos.Clear();

        DesenhoAlterado?.Invoke(this, EventArgs.Empty);
        InvalidateSurface();
    }

    /// <summary>
    /// Remove só o último traço (não a camada inteira) — reconstrói o bitmap do zero e reaplica todo
    /// o histórico restante, já que o traço é desenhado direto no bitmap (raster) e não dá pra
    /// "apagar" um traço específico sem redesenhar os demais por cima.
    /// </summary>
    public void DesfazerUltimoTraco()
    {
        if (CamadaAtivaId is not { } camadaId || ImagensPorCamada is null || _historicoTracos.Count == 0)
            return;

        _historicoTracos.RemoveAt(_historicoTracos.Count - 1);

        var bitmap = ObterOuCriarBitmapDaCamada(camadaId);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        foreach (var traco in _historicoTracos)
            DesenharTraco(canvas, traco);

        DesenhoAlterado?.Invoke(this, EventArgs.Empty);
        InvalidateSurface();
    }

    public bool TemTracoParaDesfazer => _historicoTracos.Count > 0;

    private static void DesenharTraco(SKCanvas canvas, Traco traco)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            StrokeWidth = traco.Espessura,
            BlendMode = traco.Apagar ? SKBlendMode.Clear : SKBlendMode.SrcOver,
            Color = traco.Apagar ? SKColors.Transparent : SKColor.Parse(traco.Cor),
        };

        if (traco.Pontos.Count == 1)
        {
            canvas.DrawCircle(traco.Pontos[0], traco.Espessura / 2, paint);
            return;
        }

        for (var i = 1; i < traco.Pontos.Count; i++)
            canvas.DrawLine(traco.Pontos[i - 1], traco.Pontos[i], paint);
    }

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        base.OnPaintSurface(e);

        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.White);

        var imagensPorCamada = ImagensPorCamada as IReadOnlyDictionary<Guid, SKBitmap>;

        if (UsarResolucaoNativa && ImagemBase is not null)
        {
            // Desenha tudo em resolução nativa e deixa a transformação do canvas escalar — mantém a
            // planta base e o traço de cada camada alinhados entre si em qualquer nível de zoom, ao
            // contrário de redimensionar cada bitmap individualmente.
            canvas.Save();
            canvas.Scale(Zoom);
            if (Camadas is { Count: > 0 } camadasComZoom)
                PlantaOverlayRenderer.Desenhar(canvas, camadasComZoom, ImagemBase, imagensPorCamada);
            else
                canvas.DrawBitmap(ImagemBase, 0, 0);
            canvas.Restore();
            return;
        }

        var imagemBaseEscalada = ObterImagemBaseEscalada(e.Info);

        if (Camadas is { Count: > 0 } camadas)
            PlantaOverlayRenderer.Desenhar(canvas, camadas, imagemBaseEscalada, imagensPorCamada);
        else if (imagemBaseEscalada is not null)
            canvas.DrawBitmap(imagemBaseEscalada, 0, 0);
    }

    private SKBitmap? ObterImagemBaseEscalada(SKImageInfo info)
    {
        if (ImagemBase is null)
            return null;

        var tamanhoAlvo = new SKSizeI(info.Width, info.Height);
        if (_imagemBaseEscalada is not null && _tamanhoImagemBaseEscalada == tamanhoAlvo)
            return _imagemBaseEscalada;

        _imagemBaseEscalada?.Dispose();
        _imagemBaseEscalada = ImagemBase.Resize(new SKImageInfo(info.Width, info.Height), SKFilterQuality.Medium);
        _tamanhoImagemBaseEscalada = tamanhoAlvo;
        return _imagemBaseEscalada;
    }

    private void OnTouch(object? sender, SKTouchEventArgs e)
    {
        if (CamadaAtivaId is not { } camadaId || ImagensPorCamada is null)
            return;

        var camadaAtiva = Camadas?.FirstOrDefault(c => c.Id == camadaId);
        if (camadaAtiva is null || camadaAtiva.Bloqueada)
            return;

        if (e.ActionType is not (SKTouchAction.Pressed or SKTouchAction.Moved or SKTouchAction.Released))
            return;

        // Em resolução nativa, o toque chega em pixels de tela (0..CanvasSize, que já inclui o Zoom
        // pelo WidthRequest/HeightRequest da Page) — precisa dividir por Zoom pra achar o ponto
        // correspondente no bitmap nativo da camada, senão o traço fica na posição/escala erradas.
        var ponto = UsarResolucaoNativa && Zoom > 0
            ? new SKPoint(e.Location.X / Zoom, e.Location.Y / Zoom)
            : e.Location;

        var bitmap = ObterOuCriarBitmapDaCamada(camadaId);
        using var canvasBitmap = new SKCanvas(bitmap);
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            StrokeWidth = EspessuraTraco,
            BlendMode = ModoApagar ? SKBlendMode.Clear : SKBlendMode.SrcOver,
            Color = ModoApagar ? SKColors.Transparent : SKColor.Parse(CorTraco),
        };

        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
                canvasBitmap.DrawCircle(ponto, EspessuraTraco / 2, paint);
                _ultimoPontoToque = ponto;
                _tracoEmAndamento = new Traco([ponto], CorTraco, EspessuraTraco, ModoApagar);
                break;
            case SKTouchAction.Moved when _ultimoPontoToque is { } ultimo:
                canvasBitmap.DrawLine(ultimo, ponto, paint);
                _ultimoPontoToque = ponto;
                _tracoEmAndamento?.Pontos.Add(ponto);
                break;
            case SKTouchAction.Released:
                _ultimoPontoToque = null;
                if (_tracoEmAndamento is not null)
                {
                    _historicoTracos.Add(_tracoEmAndamento);
                    _tracoEmAndamento = null;
                }
                DesenhoAlterado?.Invoke(this, EventArgs.Empty);
                break;
        }

        e.Handled = true;
        InvalidateSurface();
    }

    private SKBitmap ObterOuCriarBitmapDaCamada(Guid camadaId)
    {
        var dicionario = ImagensPorCamada!;
        var (largura, altura) = UsarResolucaoNativa && ImagemBase is not null
            ? (Math.Max(1, ImagemBase.Width), Math.Max(1, ImagemBase.Height))
            : (Math.Max(1, (int)CanvasSize.Width), Math.Max(1, (int)CanvasSize.Height));

        if (dicionario.TryGetValue(camadaId, out var existente) &&
            existente.Width == largura && existente.Height == altura)
            return existente;

        var novo = new SKBitmap(largura, altura);
        using (var canvasNovo = new SKCanvas(novo))
        {
            canvasNovo.Clear(SKColors.Transparent);
            if (existente is not null)
                canvasNovo.DrawBitmap(existente, new SKRect(0, 0, largura, altura));
        }

        existente?.Dispose();
        dicionario[camadaId] = novo;
        return novo;
    }
}
