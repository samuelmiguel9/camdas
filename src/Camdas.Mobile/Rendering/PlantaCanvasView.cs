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
/// (rabiscar/escrever na camada ativa) e zoom/pan — desenho e pan nunca competem pelo mesmo toque,
/// só um dos dois fica ativo por vez (<see cref="ModoPan"/>).
///
/// Quando <see cref="UsarResolucaoNativa"/> é true (PlantaPage e CamadaEdicaoPage), o bitmap de cada
/// camada é criado no tamanho nativo da imagem base — não no tamanho da tela do aparelho — então o
/// traço fica na mesma proporção da planta independente da resolução de quem desenhou; <see
/// cref="Zoom"/> só controla a transformação visual (canvas.Scale), sem afetar essa resolução.
/// Mantido `false` por padrão pra não alterar o comportamento de quem ainda não usa zoom.
/// </summary>
public sealed class PlantaCanvasView : SKCanvasView
{
    /// <summary>Uma ação de desenho (traço ou texto), guardada pra permitir desfazer/refazer sem
    /// apagar a camada inteira — o bitmap é reconstruído do zero reaplicando as ações restantes,
    /// já que o desenho é raster (não dá pra "remover" uma pincelada específica de outro jeito).</summary>
    private abstract record AcaoDesenho;
    private sealed record AcaoTraco(List<SKPoint> Pontos, string Cor, float Espessura, bool Apagar) : AcaoDesenho;
    private sealed record AcaoTexto(string Texto, SKPoint Posicao, string Cor, float TamanhoFonte) : AcaoDesenho;

    private readonly List<AcaoDesenho> _historico = [];
    private readonly Stack<AcaoDesenho> _desfeitas = [];
    private AcaoTraco? _tracoEmAndamento;

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
        nameof(EspessuraTraco), typeof(float), typeof(PlantaCanvasView), defaultValue: 3f);

    public static readonly BindableProperty ModoApagarProperty = BindableProperty.Create(
        nameof(ModoApagar), typeof(bool), typeof(PlantaCanvasView), defaultValue: false);

    /// <summary>Quando true, um toque no canvas não desenha — dispara <see cref="SolicitarTexto"/>
    /// pra a Page pedir o texto (a Page decide como pedir: DisplayPromptAsync, etc.) e chamar
    /// <see cref="AdicionarTexto"/> de volta.</summary>
    public static readonly BindableProperty ModoTextoProperty = BindableProperty.Create(
        nameof(ModoTexto), typeof(bool), typeof(PlantaCanvasView), defaultValue: false);

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

    /// <summary>Deslocamento visual (pixels de tela) aplicado antes do <see cref="Zoom"/> — permite
    /// "arrastar" a planta quando o zoom deixa o conteúdo maior que a tela, sem precisar de um
    /// ScrollView ao redor do canvas (que competia com o gesto de desenhar). Só é alterado enquanto
    /// <see cref="ModoPan"/> está ligado — ver comentário lá.</summary>
    public static readonly BindableProperty PanXProperty = BindableProperty.Create(
        nameof(PanX), typeof(float), typeof(PlantaCanvasView),
        defaultValue: 0f,
        propertyChanged: (bindable, _, _) => ((PlantaCanvasView)bindable).InvalidateSurface());

    public static readonly BindableProperty PanYProperty = BindableProperty.Create(
        nameof(PanY), typeof(float), typeof(PlantaCanvasView),
        defaultValue: 0f,
        propertyChanged: (bindable, _, _) => ((PlantaCanvasView)bindable).InvalidateSurface());

    /// <summary>Quando true, arrastar o dedo move a visualização (PanX/PanY) em vez de desenhar — modo
    /// explícito, ligado/desligado por um ícone na barra de ferramentas (ver CamadaEdicaoPage), pra não
    /// competir com o gesto de desenho: só um dos dois funciona por vez, nunca os dois ao mesmo toque.</summary>
    public static readonly BindableProperty ModoPanProperty = BindableProperty.Create(
        nameof(ModoPan), typeof(bool), typeof(PlantaCanvasView), defaultValue: false);

    private SKBitmap? _imagemBaseEscalada;
    private SKSizeI _tamanhoImagemBaseEscalada;
    private SKPoint? _ultimoPontoToque;
    private SKPoint? _ultimoPontoPan;

    public event EventHandler? DesenhoAlterado;

    public PlantaCanvasView()
    {
        EnableTouchEvents = true;
        Touch += OnTouch;

        // Crash real reproduzido num Galaxy Tab A com S Pen (Android 8.1): pairar a caneta perto da
        // tela sem tocar (ACTION_HOVER_ENTER) derruba o app com SIGSEGV nativo dentro de
        // libSkiaSharp.so — parece um bug da própria lib ao processar hover em versões antigas do
        // Android, não algo que dá pra evitar só no C# do nosso OnTouch (o crash acontece antes de
        // chegar lá). O app nunca usou hover pra nada, então suprimimos no listener nativo do Android
        // (consumindo o evento, sem repassar pra frente) assim que o Handler for criado.
        HandlerChanged += (_, _) =>
        {
            if (Handler?.PlatformView is Android.Views.View view)
                view.SetOnHoverListener(new SupressorDeHover());
        };
    }

    private sealed class SupressorDeHover : Java.Lang.Object, Android.Views.View.IOnHoverListener
    {
        public bool OnHover(Android.Views.View? v, Android.Views.MotionEvent? e) => true;
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

    public bool ModoTexto
    {
        get => (bool)GetValue(ModoTextoProperty);
        set => SetValue(ModoTextoProperty, value);
    }

    /// <summary>Ponto (resolução nativa da imagem base) onde o usuário tocou em modo texto.</summary>
    public event EventHandler<SKPoint>? SolicitarTexto;

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

    public float PanX
    {
        get => (float)GetValue(PanXProperty);
        set => SetValue(PanXProperty, value);
    }

    public float PanY
    {
        get => (float)GetValue(PanYProperty);
        set => SetValue(PanYProperty, value);
    }

    public bool ModoPan
    {
        get => (bool)GetValue(ModoPanProperty);
        set => SetValue(ModoPanProperty, value);
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
        _historico.Clear();
        _desfeitas.Clear();

        DesenhoAlterado?.Invoke(this, EventArgs.Empty);
        InvalidateSurface();
    }

    public bool TemAcaoParaDesfazer => _historico.Count > 0;
    public bool TemAcaoParaRefazer => _desfeitas.Count > 0;

    public void DesfazerUltimaAcao()
    {
        if (_historico.Count == 0)
            return;

        var acao = _historico[^1];
        _historico.RemoveAt(_historico.Count - 1);
        _desfeitas.Push(acao);
        RedesenharDoHistorico();
    }

    public void RefazerAcao()
    {
        if (_desfeitas.Count == 0)
            return;

        var acao = _desfeitas.Pop();
        _historico.Add(acao);
        RedesenharDoHistorico();
    }

    /// <summary>Texto ainda "solto" na tela — movível até o usuário confirmar, quando então é
    /// desenhado (raster) na camada ativa igual a um traço normal. Coordenadas sempre na resolução
    /// nativa da imagem base, mesmo espaço usado pelo restante do desenho.</summary>
    private sealed class ElementoPendente
    {
        public required string Texto;
        public string Cor = "#000000";
        public float TamanhoFonte;
        public SKRect Retangulo;
    }

    private ElementoPendente? _elementoPendente;
    private SKPoint? _ultimoPontoElementoPendente;

    public bool TemElementoPendente => _elementoPendente is not null;

    /// <summary>Começa a posicionar um texto — aparece "solto" (com moldura) no ponto tocado, pra
    /// mover antes de confirmar. Ver <see cref="ConfirmarElementoPendente"/>.</summary>
    public void IniciarTextoPendente(string texto, string cor, float tamanhoFonte, SKPoint posicaoInicial)
    {
        using var fonte = new SKFont(SKTypeface.Default, tamanhoFonte);
        var largura = Math.Max(20f, fonte.MeasureText(texto));
        var altura = tamanhoFonte * 1.3f;

        _elementoPendente = new ElementoPendente
        {
            Texto = texto,
            Cor = cor,
            TamanhoFonte = tamanhoFonte,
            Retangulo = new SKRect(posicaoInicial.X, posicaoInicial.Y - altura, posicaoInicial.X + largura, posicaoInicial.Y),
        };
        InvalidateSurface();
    }

    /// <summary>Desenha o texto pendente (na posição atual) na camada ativa, igual a um traço normal,
    /// e encerra o modo de posicionamento.</summary>
    public void ConfirmarElementoPendente()
    {
        if (_elementoPendente is not { } pendente)
            return;

        AdicionarTexto(pendente.Texto, new SKPoint(pendente.Retangulo.Left, pendente.Retangulo.Bottom), pendente.Cor, pendente.TamanhoFonte);

        _elementoPendente = null;
        InvalidateSurface();
    }

    /// <summary>Descarta o elemento pendente sem desenhar nada na camada.</summary>
    public void CancelarElementoPendente()
    {
        _elementoPendente = null;
        _ultimoPontoElementoPendente = null;
        InvalidateSurface();
    }

    private void DesenharElementoPendente(SKCanvas canvas, ElementoPendente pendente)
    {
        using var paintTexto = new SKPaint { IsAntialias = true, Color = SKColor.Parse(pendente.Cor) };
        using var fonte = new SKFont(SKTypeface.Default, pendente.TamanhoFonte);
        canvas.DrawText(pendente.Texto, pendente.Retangulo.Left, pendente.Retangulo.Bottom, fonte, paintTexto);

        // Espessura dividida pelo Zoom pra moldura ter sempre o mesmo tamanho na tela, independente
        // do nível de zoom (o canvas já está escalado por Zoom neste ponto).
        var escala = Math.Max(Zoom, 0.05f);
        using var borda = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f / escala, Color = SKColors.DeepSkyBlue };
        canvas.DrawRect(pendente.Retangulo, borda);
    }

    /// <summary>Toque enquanto há um elemento pendente: arrasta pra mover — nunca desenha na camada
    /// nem aciona modo texto/pan enquanto está posicionando.</summary>
    private void TratarTouchElementoPendente(ElementoPendente pendente, SKTouchEventArgs e)
    {
        var ponto = ConverterTelaParaNativo(e.Location);

        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
                // Mesmo que o toque caia um pouco fora da moldura exata (medida de texto imprecisa,
                // dedo grosso), ainda tratamos como "pegar" o elemento — sem isto, um toque levemente
                // fora do retângulo não iniciava o arrasto, dando a impressão de que ele "não é
                // arrastável".
                _ultimoPontoElementoPendente = ponto;

                // Sem isto, o Android entende o arrasto do dedo como gesto de rolagem do ScrollView pai
                // (PlantaScroll) a partir do primeiro Moved, e o elemento pendente trava no ponto
                // inicial — mesmo problema (e mesma correção) já documentado no toque de desenho normal.
                (Handler?.PlatformView as Android.Views.View)?.Parent?.RequestDisallowInterceptTouchEvent(true);
                break;

            case SKTouchAction.Moved when _ultimoPontoElementoPendente is { } ultimo:
                var deslocX = ponto.X - ultimo.X;
                var deslocY = ponto.Y - ultimo.Y;
                pendente.Retangulo = SKRect.Create(
                    pendente.Retangulo.Left + deslocX, pendente.Retangulo.Top + deslocY,
                    pendente.Retangulo.Width, pendente.Retangulo.Height);
                _ultimoPontoElementoPendente = ponto;
                InvalidateSurface();
                break;

            case SKTouchAction.Released:
            case SKTouchAction.Cancelled:
                (Handler?.PlatformView as Android.Views.View)?.Parent?.RequestDisallowInterceptTouchEvent(false);
                _ultimoPontoElementoPendente = null;
                break;
        }

        e.Handled = true;
    }

    /// <summary>Converte um ponto em coordenadas de tela pra coordenadas nativas da imagem base —
    /// mesma conta usada no início de <see cref="OnTouch"/>, extraída pra reuso pelo elemento pendente.</summary>
    private SKPoint ConverterTelaParaNativo(SKPoint pontoTela) =>
        UsarResolucaoNativa && Zoom > 0
            ? new SKPoint((pontoTela.X - PanX) / Zoom, (pontoTela.Y - PanY) / Zoom)
            : pontoTela;

    /// <summary>Escreve texto na camada ativa na posição indicada (coordenadas na resolução nativa
    /// da imagem base) — entra no histórico igual a um traço, pra poder desfazer.</summary>
    public void AdicionarTexto(string texto, SKPoint posicao, string cor, float tamanhoFonte)
    {
        if (CamadaAtivaId is not { } camadaId || ImagensPorCamada is null || string.IsNullOrWhiteSpace(texto))
            return;

        var acao = new AcaoTexto(texto, posicao, cor, tamanhoFonte);
        var bitmap = ObterOuCriarBitmapDaCamada(camadaId);
        using (var canvas = new SKCanvas(bitmap))
            DesenharAcao(canvas, acao);

        _historico.Add(acao);
        _desfeitas.Clear();

        DesenhoAlterado?.Invoke(this, EventArgs.Empty);
        InvalidateSurface();
    }

    private void RedesenharDoHistorico()
    {
        if (CamadaAtivaId is not { } camadaId || ImagensPorCamada is null)
            return;

        var bitmap = ObterOuCriarBitmapDaCamada(camadaId);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        foreach (var acao in _historico)
            DesenharAcao(canvas, acao);

        DesenhoAlterado?.Invoke(this, EventArgs.Empty);
        InvalidateSurface();
    }

    private static void DesenharAcao(SKCanvas canvas, AcaoDesenho acao)
    {
        switch (acao)
        {
            case AcaoTraco traco:
                DesenharTraco(canvas, traco);
                break;
            case AcaoTexto texto:
                using (var paint = new SKPaint { IsAntialias = true, Color = SKColor.Parse(texto.Cor) })
                using (var fonte = new SKFont(SKTypeface.Default, texto.TamanhoFonte))
                    canvas.DrawText(texto.Texto, texto.Posicao.X, texto.Posicao.Y, fonte, paint);
                break;
        }
    }

    private static void DesenharTraco(SKCanvas canvas, AcaoTraco traco)
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

        // Durante transições de foco/layout (teclado abrindo, hover da caneta, fragment sendo
        // recriado) o Android pode colapsar a view pra 0x0 por um instante antes de estabilizar no
        // tamanho final. Sem este guard, ObterImagemBaseEscalada chamava ImagemBase.Resize com
        // destino 0x0, que gera um SKBitmap com buffer de pixels inválido — e o DrawBitmap seguinte
        // crasha nativamente dentro do SkiaSharp (sk_image_new_from_bitmap, null pointer dereference)
        // ao tentar empacotar esse bitmap como SKImage. Ver captura_log4.txt / CRASH_ANALISE.
        if (e.Info.Width <= 0 || e.Info.Height <= 0)
            return;

        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.White);

        var imagensPorCamada = ImagensPorCamada as IReadOnlyDictionary<Guid, SKBitmap>;

        if (UsarResolucaoNativa && ImagemBase is not null)
        {
            // Desenha tudo em resolução nativa e deixa a transformação do canvas escalar — mantém a
            // planta base e o traço de cada camada alinhados entre si em qualquer nível de zoom, ao
            // contrário de redimensionar cada bitmap individualmente.
            canvas.Save();
            canvas.Translate(PanX, PanY);
            canvas.Scale(Zoom);
            if (Camadas is { Count: > 0 } camadasComZoom)
                PlantaOverlayRenderer.Desenhar(canvas, camadasComZoom, ImagemBase, imagensPorCamada);
            else if (PlantaOverlayRenderer.PodeDesenhar(ImagemBase))
                canvas.DrawBitmap(ImagemBase, 0, 0);

            if (_elementoPendente is { } pendente)
                DesenharElementoPendente(canvas, pendente);

            canvas.Restore();
            return;
        }

        var imagemBaseEscalada = ObterImagemBaseEscalada(e.Info);

        if (Camadas is { Count: > 0 } camadas)
            PlantaOverlayRenderer.Desenhar(canvas, camadas, imagemBaseEscalada, imagensPorCamada);
        else if (PlantaOverlayRenderer.PodeDesenhar(imagemBaseEscalada))
            canvas.DrawBitmap(imagemBaseEscalada, 0, 0);
    }

    private SKBitmap? ObterImagemBaseEscalada(SKImageInfo info)
    {
        if (ImagemBase is null || info.Width <= 0 || info.Height <= 0)
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
        if (e.ActionType is not (SKTouchAction.Pressed or SKTouchAction.Moved or SKTouchAction.Released or SKTouchAction.Cancelled))
            return;

        // Elemento pendente (texto/imagem sendo posicionado) tem prioridade total sobre qualquer
        // outro modo — nunca desenha na camada nem aciona pan/texto enquanto está sendo posicionado.
        if (_elementoPendente is { } pendente)
        {
            TratarTouchElementoPendente(pendente, e);
            return;
        }

        // Modo pan explícito (ícone na barra de ferramentas, ver CamadaEdicaoPage) — separado do
        // desenho de propósito: sem ScrollView ao redor do canvas (competia com o próprio gesto de
        // desenhar), arrastar só move a visualização quando esse modo está ligado; nunca ao mesmo
        // tempo que o lápis desenha.
        if (ModoPan)
        {
            GerenciarPan(e);
            return;
        }

        if (e.ActionType == SKTouchAction.Cancelled)
            return;

        if (CamadaAtivaId is not { } camadaId || ImagensPorCamada is null)
            return;

        var camadaAtiva = Camadas?.FirstOrDefault(c => c.Id == camadaId);
        if (camadaAtiva is null || camadaAtiva.Bloqueada)
            return;

        // Em resolução nativa, o toque chega em pixels de tela (0..CanvasSize, que já inclui o Zoom
        // pelo WidthRequest/HeightRequest da Page) — precisa descontar o Pan e dividir por Zoom pra
        // achar o ponto correspondente no bitmap nativo da camada, senão o traço fica na
        // posição/escala erradas.
        var ponto = ConverterTelaParaNativo(e.Location);

        if (ModoTexto)
        {
            if (e.ActionType == SKTouchAction.Pressed)
                SolicitarTexto?.Invoke(this, ponto);

            e.Handled = true;
            return;
        }

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
                // O canvas normalmente fica dentro de um ScrollView (zoom/pan — ver
                // CamadaEdicaoPage/PlantaPage). Sem isto, o Android entende um arrasto do dedo como
                // gesto de rolagem do ScrollView pai a partir do primeiro Moved, e o traço trava no
                // ponto inicial. Pedimos ao pai pra não interceptar até soltar o dedo.
                (Handler?.PlatformView as Android.Views.View)?.Parent?.RequestDisallowInterceptTouchEvent(true);
                canvasBitmap.DrawCircle(ponto, EspessuraTraco / 2, paint);
                _ultimoPontoToque = ponto;
                _tracoEmAndamento = new AcaoTraco([ponto], CorTraco, EspessuraTraco, ModoApagar);
                _desfeitas.Clear();
                break;
            case SKTouchAction.Moved when _ultimoPontoToque is { } ultimo:
                canvasBitmap.DrawLine(ultimo, ponto, paint);
                _ultimoPontoToque = ponto;
                _tracoEmAndamento?.Pontos.Add(ponto);
                break;
            case SKTouchAction.Released:
                (Handler?.PlatformView as Android.Views.View)?.Parent?.RequestDisallowInterceptTouchEvent(false);
                _ultimoPontoToque = null;
                if (_tracoEmAndamento is not null)
                {
                    _historico.Add(_tracoEmAndamento);
                    _tracoEmAndamento = null;
                }
                DesenhoAlterado?.Invoke(this, EventArgs.Empty);
                break;
        }

        e.Handled = true;
        InvalidateSurface();
    }

    /// <summary>Com <see cref="ModoPan"/> ligado, qualquer arrasto de um dedo só move a visualização
    /// (PanX/PanY) — não desenha nada, o lápis fica desativado enquanto esse modo está ligado.</summary>
    private void GerenciarPan(SKTouchEventArgs e)
    {
        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
                _ultimoPontoPan = e.Location;
                break;
            case SKTouchAction.Moved when _ultimoPontoPan is { } ultimo:
                PanX += e.Location.X - ultimo.X;
                PanY += e.Location.Y - ultimo.Y;
                _ultimoPontoPan = e.Location;
                break;
            case SKTouchAction.Released:
            case SKTouchAction.Cancelled:
                _ultimoPontoPan = null;
                break;
        }

        e.Handled = true;
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
            if (PlantaOverlayRenderer.PodeDesenhar(existente))
                canvasNovo.DrawBitmap(existente, new SKRect(0, 0, largura, altura));
        }

        existente?.Dispose();
        dicionario[camadaId] = novo;
        return novo;
    }
}
