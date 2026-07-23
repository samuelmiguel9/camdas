using System.Collections.Specialized;
using BellucSketch.Contracts;
using BellucSketch.Mobile.Rendering;
using BellucSketch.Mobile.ViewModels;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace BellucSketch.Mobile.Views;

/// <summary>Estilo de traço da ferramenta de lápis (pedido do usuário: "abra mais opções: linha reta
/// contínua, pontilhada, tracejada, desenho livre"). Só os três "Reta*" desenham uma linha reta única
/// (ponto inicial ao final do arrasto, sem curva — ver <see cref="PlantaCanvasView.GerenciarLinhaReta"/>),
/// cada um com o efeito de traço correspondente; <see cref="Livre"/> mantém o desenho a mão livre de
/// sempre (curva suave por pontos capturados, sem efeito de traço, comportamento padrão desde antes
/// desta ferramenta existir).</summary>
public enum EstiloTraco
{
    Livre,
    RetaContinua,
    RetaPontilhada,
    RetaTracejada,
}

/// <summary>
/// Canvas de desenho da planta: mostra a imagem base + o traço livre (raster) de cada camada
/// visível + as linhas de cota (a lógica de "o que desenhar" vive em
/// <see cref="PlantaOverlayRenderer"/>, BellucSketch.Mobile.Core, testável sem Android). Esta classe
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
    private sealed record AcaoTraco(
        List<SKPoint> Pontos, string Cor, float Espessura, bool Apagar, EstiloTraco Estilo = EstiloTraco.Livre) : AcaoDesenho;
    /// <summary>AreaCobertura (coordenadas nativas, sempre sem rotação — cobre o texto ORIGINAL da
    /// planta, que não gira) + MascaraCobertura (bitmap já pronto pra cobrir essa área, ver <see
    /// cref="CriarMascaraCobertura"/>) só são usados pela ferramenta de cota (ver
    /// PlantaPage.OnCanvasSelecaoCotaConcluida): cobrem o número impresso reconhecido pelo OCR antes de
    /// escrever o texto corrigido, pra parecer uma edição de verdade (a cota antiga some) em vez de só
    /// sobrepor texto novo em cima do antigo. A máscara (em vez de um preenchimento sólido) preserva de
    /// propósito qualquer pixel vermelho dentro da área — a linha de cota costuma passar por baixo/
    /// atravessando o número, e um preenchimento sólido apagaria o pedaço dela ali (pedido do usuário:
    /// "pixels vermelhos você não tira, pois fazem parte da linha de cota"). Nulos pro texto manual
    /// normal (ferramenta "T").</summary>
    private sealed record AcaoTexto(
        string Texto, SKPoint Posicao, string Cor, float TamanhoFonte, float RotacaoGraus = 0f,
        SKRect? AreaCobertura = null, SKBitmap? MascaraCobertura = null) : AcaoDesenho;

    private readonly List<AcaoDesenho> _historico = [];
    private readonly Stack<AcaoDesenho> _desfeitas = [];
    private AcaoTraco? _tracoEmAndamento;

    /// <summary>Cópia do bitmap da camada ativa no exato estado em que ela chegou (recém carregada
    /// do servidor com traço de sessões anteriores, ou vazia se for camada nova) — capturada uma vez
    /// ao entrar em edição (ver CamadaAtivaIdProperty), ANTES de qualquer traço desta sessão. Usada
    /// por <see cref="RedesenharDoHistorico"/> como ponto de partida, em vez de assumir bitmap em
    /// branco — sem isto, desfazer limpava o bitmap e redesenhava só as ações desta sessão em
    /// memória, apagando qualquer traço já existente de antes (bug real reportado: "seta voltar
    /// remove tudo como se limpasse a camada inteira").</summary>
    private SKBitmap? _bitmapBaseHistorico;

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

        view.InvalidarComposicaoCache();
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
            view.InvalidarComposicaoCache();
            view.InvalidateSurface();
        });

    public static readonly BindableProperty ImagensPorCamadaProperty = BindableProperty.Create(
        nameof(ImagensPorCamada), typeof(IDictionary<Guid, SKBitmap>), typeof(PlantaCanvasView),
        defaultValue: null,
        propertyChanged: (bindable, _, _) =>
        {
            var view = (PlantaCanvasView)bindable;
            view.InvalidarComposicaoCache();
            view.InvalidateSurface();
        });

    public static readonly BindableProperty CamadaAtivaIdProperty = BindableProperty.Create(
        nameof(CamadaAtivaId), typeof(Guid?), typeof(PlantaCanvasView),
        defaultValue: null,
        propertyChanged: (bindable, _, newValue) =>
        {
            var view = (PlantaCanvasView)bindable;
            // Trocar de camada ativa não deve arrastar o histórico de desfazer/refazer da camada
            // anterior — ele nunca fez sentido pra outra camada (RedesenharDoHistorico sempre
            // reconstrói CamadaAtivaId).
            view._historico.Clear();
            view._desfeitas.Clear();
            view.CapturarBaselineHistorico((Guid?)newValue);
        });

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

    /// <summary>Quando true, arrastar o dedo desenha um retângulo de seleção (em vez de traço/pan) —
    /// ferramenta de cota: o retângulo desenhado é recortado da composição e mandado pro OCR (ver
    /// <see cref="RecortarComposicaoNativa"/>/<see cref="SelecaoCotaConcluida"/>). Desligar o modo (ex.:
    /// trocar de ferramenta) descarta qualquer seleção em andamento.</summary>
    public static readonly BindableProperty ModoSelecaoCotaProperty = BindableProperty.Create(
        nameof(ModoSelecaoCota), typeof(bool), typeof(PlantaCanvasView),
        defaultValue: false,
        propertyChanged: (bindable, _, _) =>
        {
            var view = (PlantaCanvasView)bindable;
            view._selecaoCotaInicio = null;
            view._selecaoCotaRetangulo = null;
            view.InvalidateSurface();
        });

    /// <summary>Estilo de traço usado pela ferramenta de lápis — ver <see cref="EstiloTraco"/>. Trocar
    /// de estilo no meio de um arrasto (raro, o menu de escolha fecha antes de voltar a desenhar) some
    /// com qualquer prévia de linha reta em andamento, pra não desenhar com um estilo diferente do que
    /// a prévia mostrou.</summary>
    public static readonly BindableProperty EstiloTracoProperty = BindableProperty.Create(
        nameof(EstiloTraco), typeof(EstiloTraco), typeof(PlantaCanvasView),
        defaultValue: EstiloTraco.Livre,
        propertyChanged: (bindable, _, _) =>
        {
            var view = (PlantaCanvasView)bindable;
            view._linhaRetaInicio = null;
            view._linhaRetaAtual = null;
            view.InvalidateSurface();
        });

    private SKBitmap? _imagemBaseEscalada;
    private SKSizeI _tamanhoImagemBaseEscalada;
    private readonly Dictionary<long, SKPoint> _ponteirosAtivos = [];
    private float? _distanciaAnteriorPinca;

    /// <summary>Composição (imagem base + camadas visíveis) pré-renderizada num único bitmap, usada
    /// só quando <see cref="UsarResolucaoNativa"/> é true. Sem isto, mexer no zoom/pan da
    /// pré-visualização (CamadaEdicaoPage) disparava um DrawBitmap por camada A CADA FRAME de
    /// arrasto — pesado o bastante pra travar num aparelho mais fraco, mesmo sem nenhum conteúdo
    /// mudando (bug reportado: "opção de mexer na pré-visualização está travando"). Só é invalidada
    /// quando o conteúdo de verdade muda (traço, visibilidade/ordem de camada, imagem base) — nunca
    /// só por zoom/pan.</summary>
    private SKBitmap? _composicaoNativaCache;

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

    public bool ModoSelecaoCota
    {
        get => (bool)GetValue(ModoSelecaoCotaProperty);
        set => SetValue(ModoSelecaoCotaProperty, value);
    }

    public EstiloTraco EstiloTraco
    {
        get => (EstiloTraco)GetValue(EstiloTracoProperty);
        set => SetValue(EstiloTracoProperty, value);
    }

    private SKPoint? _selecaoCotaInicio;
    private SKRect? _selecaoCotaRetangulo;
    private SKPoint? _linhaRetaInicio;
    private SKPoint? _linhaRetaAtual;

    /// <summary>Disparado ao soltar o dedo depois de arrastar um retângulo com <see cref="ModoSelecaoCota"/>
    /// ligado — coordenadas na resolução nativa da imagem base, mesmo espaço usado pelo resto do
    /// desenho. Não dispara para arrastos minúsculos (toque acidental).</summary>
    public event EventHandler<SKRect>? SelecaoCotaConcluida;

    /// <summary>Disparado por <see cref="AtualizarPinca"/> quando dois dedos estão na tela (pinça) —
    /// carrega o fator de escala (multiplicador do zoom atual) desde a última atualização e o ponto
    /// médio atual entre os dois dedos (mesmas coordenadas de <see cref="SKTouchEventArgs.Location"/>,
    /// unidades da view). A Page assina isto pra aplicar o zoom com seus próprios limites (slider),
    /// ancorando nesse ponto — ver comentário em AtualizarPinca sobre por que a pinça é tratada aqui e
    /// não com um PinchGestureRecognizer nativo do MAUI.</summary>
    public event EventHandler<(float FatorEscala, SKPoint Centro)>? ZoomPorGestoSolicitado;

    /// <summary>
    /// Força o redesenho — usado pela Page depois de recarregar dados que não disparam sozinhos uma
    /// notificação de mudança (ex.: <see cref="ImagensPorCamada"/> é um Dictionary comum, mutado no
    /// lugar, sem INotifyCollectionChanged).
    /// </summary>
    public void AtualizarPreview()
    {
        InvalidarComposicaoCache();
        InvalidateSurface();
    }

    private void OnCamadasCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        InvalidarComposicaoCache();
        InvalidateSurface();
    }

    /// <summary>Descarta a composição cacheada (imagem base + camadas) — chamado sempre que o
    /// conteúdo desenhado de fato muda (traço, camada, imagem base). Nunca chamado só por causa de
    /// zoom/pan, que são transformações de visualização, não de conteúdo.</summary>
    private void InvalidarComposicaoCache()
    {
        _composicaoNativaCache?.Dispose();
        _composicaoNativaCache = null;
    }

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
        // O baseline (ver _bitmapBaseHistorico) também precisa esvaziar — senão um desfazer logo
        // depois de "Limpar" traria de volta o traço antigo (pré-limpeza), que não existe mais.
        _bitmapBaseHistorico?.Dispose();
        _bitmapBaseHistorico = null;

        InvalidarComposicaoCache();
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

    /// <summary>Algo ainda "solto" na tela — movível/girável/redimensionável até o usuário confirmar
    /// (texto ou ícone; ver <see cref="ElementoTextoPendente"/>/<see cref="ElementoIconePendente"/>).
    /// Coordenadas sempre na resolução nativa da imagem base, mesmo espaço usado pelo resto do
    /// desenho. Arrastar/girar (<see cref="TratarTouchElementoPendente"/>/<see cref="RotacionarElementoPendente"/>)
    /// são genéricos (só mexem em Retangulo/RotacaoGraus); redimensionar e desenhar variam por
    /// tipo — ver <see cref="RedimensionarElementoPendente"/>/<see cref="DesenharElementoPendente"/>.</summary>
    private abstract class ElementoPendente
    {
        public SKRect Retangulo;
        public float RotacaoGraus;
    }

    private sealed class ElementoTextoPendente : ElementoPendente
    {
        public required string Texto;
        public string Cor = "#000000";
        public float TamanhoFonte;

        /// <summary>Ver comentário em <see cref="AcaoTexto"/> — só preenchidos pela ferramenta de cota.</summary>
        public SKRect? AreaCobertura;
        public SKBitmap? MascaraCobertura;
    }

    private sealed class ElementoIconePendente : ElementoPendente
    {
        public required SKPicture Picture;
        public required string NomeArquivo;
    }

    /// <summary>Um ícone já confirmado/gravado na camada "Ícones" — guardado só em memória, NESTA
    /// sessão do app, pra permitir tocar nele depois (excluir, ou pegar de volta pra mover/girar/
    /// redimensionar — ver <see cref="TratarToqueNaCamadaIcones"/>/<see cref="ExcluirIconeColocado"/>/
    /// <see cref="IniciarEdicaoIconeColocado"/>). Não existe persistência disso no servidor (a camada
    /// só guarda o PNG final) — um ícone de uma sessão anterior (carregado como raster puro) não
    /// aparece aqui, então não pode ser reaproveitado assim; só via "Limpar camada".</summary>
    private sealed record IconeColocado(Guid Id, SKPicture Picture, string NomeArquivo, SKRect Retangulo, float RotacaoGraus);

    private readonly List<IconeColocado> _iconesColocados = [];

    /// <summary>Um texto já confirmado/gravado numa camada — guardado só em memória, NESTA sessão do
    /// app, pra permitir tocar nele depois (segurar ~1s) e pegar de volta pra mover/girar/
    /// redimensionar (ver <see cref="IniciarEdicaoTextoColocado"/>). Ao contrário do ícone, o texto
    /// pode estar em QUALQUER camada (a que estava ativa quando foi escrito) — por isso guarda
    /// CamadaId, além do Acao original (pra remover/repor em _historico sem bagunçar desfazer/
    /// refazer) e o retângulo (hit-test/preview).</summary>
    private sealed record TextoColocado(Guid Id, Guid CamadaId, AcaoTexto Acao, SKRect Retangulo);

    private readonly List<TextoColocado> _textosColocados = [];

    /// <summary>Disparado quando um toque longo (~1s) pega de volta um ícone já colocado pra editar
    /// (ver <see cref="IniciarEdicaoIconeColocado"/>) — a Page assina isto pra mostrar a barra de
    /// posicionamento (girar/A-/A+/confirmar/cancelar), mesma usada ao colocar um ícone novo.</summary>
    public event EventHandler? IconeEmEdicaoIniciada;

    /// <summary>Mesma ideia de <see cref="IconeEmEdicaoIniciada"/>, pro texto — ver
    /// <see cref="IniciarEdicaoTextoColocado"/>.</summary>
    public event EventHandler? TextoEmEdicaoIniciado;

    private ElementoPendente? _elementoPendente;

    /// <summary>Enquanto o elemento pendente atual é um ícone PEGO DE VOLTA (não um novo, colocado
    /// pelo menu) — guarda o estado original (posição/rotação) pra "Cancelar" conseguir regravar
    /// exatamente onde estava, em vez de simplesmente perder o ícone. Null quando o pendente é um
    /// ícone novo (aí cancelar só descarta, sem nada pra restaurar) ou um texto.</summary>
    private IconeColocado? _iconeOriginalEmEdicao;

    /// <summary>Mesma ideia de <see cref="_iconeOriginalEmEdicao"/>, pro texto.</summary>
    private TextoColocado? _textoOriginalEmEdicao;
    private SKPoint? _ultimoPontoElementoPendente;

    public bool TemElementoPendente => _elementoPendente is not null;

    /// <summary>Gira o elemento pendente (texto ou ícone) em passos de 90° (0/90/180/270) em torno do
    /// próprio ponto de ancoragem — sem efeito se não houver elemento pendente. Genérico: só mexe em
    /// RotacaoGraus, o pivô real (canto pro texto, centro pro ícone) é resolvido na hora de desenhar.</summary>
    public void RotacionarElementoPendente()
    {
        if (_elementoPendente is not { } pendente)
            return;

        pendente.RotacaoGraus = (pendente.RotacaoGraus + 90f) % 360f;
        InvalidateSurface();
    }

    private const float TamanhoFontePendenteMinimo = 8f;
    private const float TamanhoFontePendenteMaximo = 300f;
    private const float FatorRedimensionamentoIcone = 1.15f;
    private const float TamanhoIconePendenteMinimo = 16f;
    private const float TamanhoIconePendenteMaximo = 4000f;

    /// <summary>Aumenta/diminui o elemento pendente — texto remede a fonte (delta em pontos,
    /// mantendo o canto inferior-esquerdo fixo, mesmo pivô usado ao confirmar/desenhar); ícone
    /// escala ~15% por chamada mantendo a proporção e o CENTRO fixo (não tem "linha de base" como
    /// texto, então crescer a partir do meio é o que parece natural pra um selo/carimbo). O sinal de
    /// deltaPontos é o que importa pro ícone (positivo cresce, negativo encolhe) — os mesmos botões
    /// A+/A- da barra de posicionamento (que passam +4/-4) servem pros dois tipos sem mudar nada na
    /// Page.</summary>
    public void RedimensionarElementoPendente(float deltaPontos)
    {
        switch (_elementoPendente)
        {
            case ElementoTextoPendente texto:
                RedimensionarTexto(texto, deltaPontos);
                InvalidateSurface();
                break;
            case ElementoIconePendente icone:
                RedimensionarIcone(icone, deltaPontos);
                InvalidateSurface();
                break;
        }
    }

    private static void RedimensionarTexto(ElementoTextoPendente texto, float deltaPontos)
    {
        var novoTamanho = Math.Clamp(texto.TamanhoFonte + deltaPontos, TamanhoFontePendenteMinimo, TamanhoFontePendenteMaximo);
        if (novoTamanho == texto.TamanhoFonte)
            return;

        var ancora = new SKPoint(texto.Retangulo.Left, texto.Retangulo.Bottom);
        using var fonte = new SKFont(SKTypeface.Default, novoTamanho);
        var largura = Math.Max(20f, fonte.MeasureText(texto.Texto));
        var altura = novoTamanho * 1.3f;

        texto.TamanhoFonte = novoTamanho;
        texto.Retangulo = new SKRect(ancora.X, ancora.Y - altura, ancora.X + largura, ancora.Y);
    }

    private static void RedimensionarIcone(ElementoIconePendente icone, float deltaPontos)
    {
        var fator = deltaPontos >= 0 ? FatorRedimensionamentoIcone : 1f / FatorRedimensionamentoIcone;
        var novaLargura = Math.Clamp(icone.Retangulo.Width * fator, TamanhoIconePendenteMinimo, TamanhoIconePendenteMaximo);
        var proporcao = icone.Retangulo.Width > 0 ? icone.Retangulo.Height / icone.Retangulo.Width : 1f;
        var novaAltura = novaLargura * proporcao;

        var centro = new SKPoint(icone.Retangulo.MidX, icone.Retangulo.MidY);
        icone.Retangulo = SKRect.Create(centro.X - novaLargura / 2f, centro.Y - novaAltura / 2f, novaLargura, novaAltura);
    }

    /// <summary>Começa a posicionar um texto — aparece "solto" (com moldura) no ponto tocado, pra
    /// mover antes de confirmar. Ver <see cref="ConfirmarElementoPendente"/>.</summary>
    public void IniciarTextoPendente(
        string texto, string cor, float tamanhoFonte, SKPoint posicaoInicial,
        SKRect? areaCobertura = null, SKBitmap? mascaraCobertura = null)
    {
        using var fonte = new SKFont(SKTypeface.Default, tamanhoFonte);
        var largura = Math.Max(20f, fonte.MeasureText(texto));
        var altura = tamanhoFonte * 1.3f;

        _elementoPendente = new ElementoTextoPendente
        {
            Texto = texto,
            Cor = cor,
            TamanhoFonte = tamanhoFonte,
            Retangulo = new SKRect(posicaoInicial.X, posicaoInicial.Y - altura, posicaoInicial.X + largura, posicaoInicial.Y),
            AreaCobertura = areaCobertura,
            MascaraCobertura = mascaraCobertura,
        };
        InvalidateSurface();
    }

    private const float TamanhoIconePendentePadrao = 64f;

    /// <summary>Começa a posicionar um ícone — aparece "solto" (com moldura) CENTRADO no ponto
    /// tocado (diferente do texto, que ancora no canto — um ícone/selo faz mais sentido centrado no
    /// dedo), mantendo a proporção real do SVG. Ver <see cref="ConfirmarElementoPendente"/>.</summary>
    public void IniciarIconePendente(SKPicture picture, string nomeArquivo, SKPoint posicaoInicial)
    {
        // Travado dentro dos limites da planta (mesma regra do desenho normal — ver OnTouch) — aqui
        // não passa pelo OnTouch (o menu de ícones não parte de um toque no canvas), então precisa
        // do próprio clamp.
        if (ImagemBase is { } imagemBaseClamp)
        {
            posicaoInicial = new SKPoint(
                Math.Clamp(posicaoInicial.X, 0, imagemBaseClamp.Width),
                Math.Clamp(posicaoInicial.Y, 0, imagemBaseClamp.Height));
        }

        var origem = picture.CullRect;
        var proporcao = origem.Width > 0 ? origem.Height / origem.Width : 1f;
        var largura = TamanhoIconePendentePadrao;
        var altura = TamanhoIconePendentePadrao * proporcao;

        _elementoPendente = new ElementoIconePendente
        {
            Picture = picture,
            NomeArquivo = nomeArquivo,
            Retangulo = SKRect.Create(posicaoInicial.X - largura / 2f, posicaoInicial.Y - altura / 2f, largura, altura),
        };
        InvalidateSurface();
    }

    /// <summary>Desenha/grava o elemento pendente (na posição/rotação/tamanho atuais) e encerra o
    /// modo de posicionamento — texto vai pra CamadaAtivaId (igual sempre), ícone vai SEMPRE pra
    /// camada "Ícones" (ver <see cref="AdicionarIcone"/>), não importa qual camada esteja ativa.</summary>
    public void ConfirmarElementoPendente()
    {
        switch (_elementoPendente)
        {
            case ElementoTextoPendente texto:
                AdicionarTexto(
                    texto.Texto, new SKPoint(texto.Retangulo.Left, texto.Retangulo.Bottom),
                    texto.Cor, texto.TamanhoFonte, texto.RotacaoGraus, texto.AreaCobertura, texto.MascaraCobertura);
                break;
            case ElementoIconePendente icone:
                AdicionarIcone(icone);
                break;
            default:
                return;
        }

        // Confirmar grava na posição NOVA — o snapshot da posição antiga (se havia, de um ícone/
        // texto pego de volta) não serve mais pra nada.
        _iconeOriginalEmEdicao = null;
        _textoOriginalEmEdicao = null;
        _elementoPendente = null;
        InvalidateSurface();
    }

    /// <summary>Descarta o elemento pendente. Se for um ícone/texto NOVO (colocado pelo menu/pela
    /// ferramenta T), some sem mais nada. Se for um ícone/texto PEGO DE VOLTA de um já colocado (ver
    /// <see cref="IniciarEdicaoIconeColocado"/>/<see cref="IniciarEdicaoTextoColocado"/>), precisa
    /// regravar exatamente onde/como estava antes — senão "Cancelar" faria o elemento desaparecer da
    /// planta de vez, o que não é "cancelar" coisa nenhuma.</summary>
    public void CancelarElementoPendente()
    {
        if (_iconeOriginalEmEdicao is { } iconeOriginal)
        {
            RegravarIconeNaCamadaIcones(iconeOriginal);
            _iconesColocados.Add(iconeOriginal);
            _iconeOriginalEmEdicao = null;
        }

        if (_textoOriginalEmEdicao is { } textoOriginal)
        {
            RegravarTextoNaCamada(textoOriginal);
            // Reaparece no fim do histórico (não exatamente na posição original entre outras ações)
            // — imperceptível no resultado visual (a composição final é a mesma), só muda a ORDEM em
            // que um desfazer subsequente desfaria as coisas, caso raro de acontecer na prática.
            _historico.Add(textoOriginal.Acao);
            _textosColocados.Add(textoOriginal);
            _textoOriginalEmEdicao = null;
        }

        _elementoPendente = null;
        _ultimoPontoElementoPendente = null;
        InvalidateSurface();
    }

    private void DesenharElementoPendente(SKCanvas canvas, ElementoPendente pendente)
    {
        switch (pendente)
        {
            case ElementoTextoPendente texto:
                DesenharTextoPendente(canvas, texto);
                break;
            case ElementoIconePendente icone:
                DesenharIconePendente(canvas, icone);
                break;
        }
    }

    private void DesenharTextoPendente(SKCanvas canvas, ElementoTextoPendente texto)
    {
        // Preview do "apagar" a cota original (ver AcaoTexto.AreaCobertura/MascaraCobertura) — sempre
        // na posição ORIGINAL da seleção, mesmo que o texto solto já tenha sido arrastado pra outro
        // lugar; é exatamente isso que será pintado ao confirmar.
        if (texto.AreaCobertura is { } areaCobertura && texto.MascaraCobertura is { } mascara)
            canvas.DrawBitmap(mascara, areaCobertura);

        // Mesmo pivô (ponto de ancoragem, canto inferior esquerdo) usado na hora de confirmar — o
        // preview aqui precisa bater exatamente com o resultado final desenhado em AdicionarTexto.
        var pivo = new SKPoint(texto.Retangulo.Left, texto.Retangulo.Bottom);

        canvas.Save();
        if (texto.RotacaoGraus != 0f)
            canvas.RotateDegrees(texto.RotacaoGraus, pivo.X, pivo.Y);

        using var paintTexto = new SKPaint { IsAntialias = true, Color = SKColor.Parse(texto.Cor) };
        using var fonte = new SKFont(SKTypeface.Default, texto.TamanhoFonte);
        canvas.DrawText(texto.Texto, pivo.X, pivo.Y, fonte, paintTexto);

        DesenharMolduraPendente(canvas, texto.Retangulo);
        canvas.Restore();
    }

    private void DesenharIconePendente(SKCanvas canvas, ElementoIconePendente icone)
    {
        // Mesmo pivô (centro do retângulo) usado na hora de confirmar (ver AdicionarIcone) — o
        // preview aqui precisa bater exatamente com o resultado final gravado na camada "Ícones".
        var pivo = new SKPoint(icone.Retangulo.MidX, icone.Retangulo.MidY);

        canvas.Save();
        if (icone.RotacaoGraus != 0f)
            canvas.RotateDegrees(icone.RotacaoGraus, pivo.X, pivo.Y);

        DesenharIconeNoRetangulo(canvas, icone.Picture, icone.Retangulo);
        DesenharMolduraPendente(canvas, icone.Retangulo);
        canvas.Restore();
    }

    // Espessura dividida pelo Zoom pra moldura ter sempre o mesmo tamanho na tela, independente do
    // nível de zoom (o canvas já está escalado por Zoom neste ponto).
    private void DesenharMolduraPendente(SKCanvas canvas, SKRect retangulo)
    {
        var escala = Math.Max(Zoom, 0.05f);
        using var borda = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f / escala, Color = SKColors.DeepSkyBlue };
        canvas.DrawRect(retangulo, borda);
    }

    /// <summary>Desenha o SKPicture do SVG escalado (mantendo a proporção, já resolvida em
    /// IniciarIconePendente) pra caber exatamente no retângulo alvo, na posição/tamanho atuais —
    /// reaproveitado tanto pelo preview (<see cref="DesenharIconePendente"/>) quanto pela gravação
    /// final (<see cref="AdicionarIcone"/>), garantindo que os dois batem pixel a pixel.</summary>
    private static void DesenharIconeNoRetangulo(SKCanvas canvas, SKPicture picture, SKRect retangulo)
    {
        var origem = picture.CullRect;
        canvas.Save();
        canvas.Translate(retangulo.Left, retangulo.Top);
        if (origem.Width > 0 && origem.Height > 0)
            canvas.Scale(retangulo.Width / origem.Width, retangulo.Height / origem.Height);
        canvas.Translate(-origem.Left, -origem.Top);
        canvas.DrawPicture(picture);
        canvas.Restore();
    }

    /// <summary>Grava um ícone (novo ou recolocado após cancelar uma edição) direto na camada
    /// "Ícones" — SEMPRE nela, nunca em CamadaAtivaId, é o motivo de existir a ferramenta (não
    /// depender de qual camada está ativa). Resolvida pelo NOME em <see cref="Camadas"/> (cada
    /// CamadaDto já tem Nome) — a Page/ViewModel garantem que essa camada sempre existe antes do
    /// usuário conseguir abrir o menu de ícones (ver PlantaViewModel.CarregarAsync), então não
    /// precisa criar nada aqui.
    ///
    /// Não entra em _historico/_desfeitas de propósito: desfazer/refazer hoje só sabe reconstruir a
    /// camada ATIVA (RedesenharDoHistorico usa CamadaAtivaId) — colocar o ícone lá faria um
    /// "desfazer" comum tentar repintá-lo em cima da camada errada. Corrigir um ícone errado hoje é
    /// girar/redimensionar antes de confirmar, "Limpar camada" na própria Ícones, ou segurar o dedo
    /// nele (~1s) pra pegar de volta e mover/apagar — ver <see cref="IniciarEdicaoIconeColocado"/>.</summary>
    private void GravarIconeNaCamadaIcones(SKPicture picture, SKRect retangulo, float rotacaoGraus)
    {
        var camadaIcones = Camadas?.FirstOrDefault(c => c.Nome == PlantaViewModel.NomeCamadaIcones);
        if (camadaIcones is null || ImagensPorCamada is null)
            return;

        var bitmap = ObterOuCriarBitmapDaCamada(camadaIcones.Id);
        using var canvas = new SKCanvas(bitmap);

        var pivo = new SKPoint(retangulo.MidX, retangulo.MidY);
        canvas.Save();
        if (rotacaoGraus != 0f)
            canvas.RotateDegrees(rotacaoGraus, pivo.X, pivo.Y);
        DesenharIconeNoRetangulo(canvas, picture, retangulo);
        canvas.Restore();

        InvalidarComposicaoCache();
        DesenhoAlterado?.Invoke(this, EventArgs.Empty);
    }

    private void AdicionarIcone(ElementoIconePendente icone)
    {
        GravarIconeNaCamadaIcones(icone.Picture, icone.Retangulo, icone.RotacaoGraus);
        _iconesColocados.Add(new IconeColocado(Guid.NewGuid(), icone.Picture, icone.NomeArquivo, icone.Retangulo, icone.RotacaoGraus));
    }

    /// <summary>Regrava um ícone existente EXATAMENTE como estava — usado só quando "Cancelar" desfaz
    /// uma edição iniciada por <see cref="IniciarEdicaoIconeColocado"/> (mesmo Id, pra continuar
    /// identificável pra um toque/segurar futuro).</summary>
    private void RegravarIconeNaCamadaIcones(IconeColocado icone) =>
        GravarIconeNaCamadaIcones(icone.Picture, icone.Retangulo, icone.RotacaoGraus);

    /// <summary>Apaga só a área do retângulo de um ícone colocado (transparente, respeitando a
    /// rotação) — usado tanto por <see cref="ExcluirIconeColocado"/> (exclusão definitiva) quanto por
    /// <see cref="IniciarEdicaoIconeColocado"/> (pega de volta pra editar, o retângulo alvo já vai
    /// ficar coberto pelo preview do elemento pendente). De propósito NÃO reconstrói o bitmap inteiro
    /// da camada: um ícone de uma sessão anterior (raster puro, sem entrada em _iconesColocados)
    /// ficaria de fora de uma reconstrução completa e seria apagado sem querer.</summary>
    private void ApagarIconeDoBitmap(IconeColocado icone)
    {
        var camadaIcones = Camadas?.FirstOrDefault(c => c.Nome == PlantaViewModel.NomeCamadaIcones);
        if (camadaIcones is null || ImagensPorCamada is null || !ImagensPorCamada.TryGetValue(camadaIcones.Id, out var bitmap))
            return;

        using var canvas = new SKCanvas(bitmap);
        using var paintApagar = new SKPaint { BlendMode = SKBlendMode.Clear };

        var pivo = new SKPoint(icone.Retangulo.MidX, icone.Retangulo.MidY);
        canvas.Save();
        if (icone.RotacaoGraus != 0f)
            canvas.RotateDegrees(icone.RotacaoGraus, pivo.X, pivo.Y);
        canvas.DrawRect(SKRect.Inflate(icone.Retangulo, 2f, 2f), paintApagar);
        canvas.Restore();

        InvalidarComposicaoCache();
    }

    /// <summary>Disparado quando o usuário TOCA (sem arrastar) num ícone já colocado nesta sessão,
    /// com a camada "Ícones" ativa — a Page assina isto pra perguntar se quer excluir.</summary>
    public event EventHandler<Guid>? IconeTocado;

    private const float ToleranciaTapIcone = 16f;
    private SKPoint? _inicioToqueCamadaIcones;

    /// <summary>Na camada "Ícones", o toque NUNCA desenha raster (ela só existe pra ícones colocados
    /// pela ferramenta própria) — só detecta um TAP (sem arrasto, abaixo de ToleranciaTapIcone) sobre
    /// um ícone rastreado e dispara <see cref="IconeTocado"/>. Chamado de dentro de OnTouch quando a
    /// camada ativa é a Ícones, no lugar do desenho normal.</summary>
    private void TratarToqueNaCamadaIcones(SKTouchEventArgs e, SKPoint ponto)
    {
        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
                _inicioToqueCamadaIcones = ponto;
                break;

            case SKTouchAction.Released:
                if (_inicioToqueCamadaIcones is { } inicio)
                {
                    var dx = ponto.X - inicio.X;
                    var dy = ponto.Y - inicio.Y;
                    if (MathF.Sqrt(dx * dx + dy * dy) <= ToleranciaTapIcone)
                    {
                        var iconeTocado = _iconesColocados.LastOrDefault(i => IconeContemPonto(i, ponto));
                        if (iconeTocado is not null)
                            IconeTocado?.Invoke(this, iconeTocado.Id);
                    }
                }
                _inicioToqueCamadaIcones = null;
                break;

            case SKTouchAction.Cancelled:
                _inicioToqueCamadaIcones = null;
                break;
        }
    }

    /// <summary>Testa se um ponto cai dentro de um retângulo rotacionado em torno de um pivô —
    /// desfaz a rotação (gira o ponto de volta) pra testar contra o retângulo original, sem rotação.
    /// Mesma conta usada tanto pro ícone (pivô = centro) quanto pro texto (pivô = canto
    /// inferior-esquerdo, mesmo usado pra desenhar/girar os dois).</summary>
    private static bool ContidoConsiderandoRotacao(SKRect retangulo, float rotacaoGraus, SKPoint pivo, SKPoint ponto)
    {
        if (rotacaoGraus == 0f)
            return retangulo.Contains(ponto);

        var anguloRad = -rotacaoGraus * MathF.PI / 180f;
        var dx = ponto.X - pivo.X;
        var dy = ponto.Y - pivo.Y;
        var pontoGirado = new SKPoint(
            pivo.X + dx * MathF.Cos(anguloRad) - dy * MathF.Sin(anguloRad),
            pivo.Y + dx * MathF.Sin(anguloRad) + dy * MathF.Cos(anguloRad));

        return retangulo.Contains(pontoGirado);
    }

    private static bool IconeContemPonto(IconeColocado icone, SKPoint ponto) =>
        ContidoConsiderandoRotacao(icone.Retangulo, icone.RotacaoGraus, new SKPoint(icone.Retangulo.MidX, icone.Retangulo.MidY), ponto);

    private static bool TextoContemPonto(TextoColocado texto, SKPoint ponto) =>
        ContidoConsiderandoRotacao(texto.Retangulo, texto.Acao.RotacaoGraus, new SKPoint(texto.Retangulo.Left, texto.Retangulo.Bottom), ponto);

    /// <summary>Exclui um ícone específico colocado nesta sessão — apaga só a área dele (ver
    /// <see cref="ApagarIconeDoBitmap"/>), sem redesenhar mais nada.</summary>
    public void ExcluirIconeColocado(Guid iconeId)
    {
        var indice = _iconesColocados.FindIndex(i => i.Id == iconeId);
        if (indice < 0)
            return;

        var icone = _iconesColocados[indice];
        _iconesColocados.RemoveAt(indice);
        ApagarIconeDoBitmap(icone);

        DesenhoAlterado?.Invoke(this, EventArgs.Empty);
        InvalidateSurface();
    }

    private const double SegundosParaPegarElemento = 1.0;
    private const float ToleranciaMovimentoPegarElemento = 12f;
    private long? _ponteiroPegarElemento;
    private SKPoint? _inicioPegarElemento;
    private int _geracaoToquePegarElemento;

    /// <summary>Detecta um toque parado (~1s) sobre um ícone OU texto já colocado, em QUALQUER modo/
    /// camada ativa — sem precisar ligar o lápis nem selecionar a camada antes (pedido do usuário:
    /// "e se tiver um modo de só eu manter apertado por 2s e aí selecionar o objeto, poder apagar ou
    /// arrastar de novo" — depois estendido pra texto também). Chamado incondicionalmente no início
    /// de OnTouch, como a pinça — só age se o dedo ficar parado (abaixo de
    /// ToleranciaMovimentoPegarElemento) o tempo todo; qualquer arrasto ou soltar antes do tempo
    /// cancela, sem interferir no gesto normal (desenho/pan/pinça). Ícone tem prioridade sobre texto
    /// no raro caso de os dois se sobreporem no mesmo ponto.</summary>
    private void AtualizarDeteccaoPegarElemento(SKTouchEventArgs e, SKPoint pontoNativo)
    {
        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
                _ponteiroPegarElemento = e.Id;
                _inicioPegarElemento = pontoNativo;
                var geracao = ++_geracaoToquePegarElemento;

                Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(SegundosParaPegarElemento), () =>
                {
                    if (geracao != _geracaoToquePegarElemento || _inicioPegarElemento is not { } inicio || _elementoPendente is not null)
                        return;

                    var icone = _iconesColocados.LastOrDefault(i => IconeContemPonto(i, inicio));
                    if (icone is not null)
                    {
                        IniciarEdicaoIconeColocado(icone);
                        return;
                    }

                    var texto = _textosColocados.LastOrDefault(t => TextoContemPonto(t, inicio));
                    if (texto is not null)
                        IniciarEdicaoTextoColocado(texto);
                });
                break;

            case SKTouchAction.Moved:
                if (e.Id == _ponteiroPegarElemento && _inicioPegarElemento is { } inicioMov)
                {
                    var dx = pontoNativo.X - inicioMov.X;
                    var dy = pontoNativo.Y - inicioMov.Y;
                    if (MathF.Sqrt(dx * dx + dy * dy) > ToleranciaMovimentoPegarElemento)
                        CancelarDeteccaoPegarElemento();
                }
                break;

            case SKTouchAction.Released:
            case SKTouchAction.Cancelled:
                if (e.Id == _ponteiroPegarElemento)
                    CancelarDeteccaoPegarElemento();
                break;
        }
    }

    private void CancelarDeteccaoPegarElemento()
    {
        _ponteiroPegarElemento = null;
        _inicioPegarElemento = null;
        _geracaoToquePegarElemento++;
    }

    /// <summary>"Pega de volta" um ícone já gravado: apaga o pixel dele da camada (ver
    /// <see cref="ApagarIconeDoBitmap"/>) e o transforma num elemento pendente na posição/rotação
    /// atuais, reaproveitando a mesma barra de girar/mover/A-/A+/confirmar/cancelar de sempre.
    /// Guarda o estado original em <see cref="_iconeOriginalEmEdicao"/> — se o usuário cancelar, o
    /// ícone volta pro lugar em vez de sumir (ver CancelarElementoPendente).</summary>
    private void IniciarEdicaoIconeColocado(IconeColocado icone)
    {
        _iconesColocados.Remove(icone);
        ApagarIconeDoBitmap(icone);
        _iconeOriginalEmEdicao = icone;

        _elementoPendente = new ElementoIconePendente
        {
            Picture = icone.Picture,
            NomeArquivo = icone.NomeArquivo,
            Retangulo = icone.Retangulo,
            RotacaoGraus = icone.RotacaoGraus,
        };

        DesenhoAlterado?.Invoke(this, EventArgs.Empty);
        InvalidateSurface();
        IconeEmEdicaoIniciada?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Apaga só a área do retângulo de um texto colocado (transparente, respeitando a
    /// rotação em torno do canto inferior-esquerdo — mesmo pivô do desenho/hit-test) — usado só por
    /// <see cref="IniciarEdicaoTextoColocado"/> (pega de volta pra editar).
    ///
    /// Pra texto da ferramenta de cota (com AreaCobertura/MascaraCobertura), o Clear acima também
    /// apaga de volta pra transparente os pixels da MÁSCARA de cobertura nessa mesma região — como
    /// texto e camada ativa ficam por CIMA da imagem base na composição, isso deixava o número
    /// original impresso reaparecer (bug reportado: "quando eu modifico o texto e confirmo, se eu
    /// selecionar novamente o texto da planta que foi apagado volta"; também explicava "voltou a
    /// apagar toda a seleção... não apenas os pixels do texto identificado" — o Clear por retângulo
    /// inteiro, não a máscara precisa). Por isso a máscara é redesenhada aqui em seguida, restaurando
    /// a cobertura antes de qualquer coisa aparecer na tela — o único jeito de reverter isso deve ser
    /// desfazer (↶), nunca só pegar o texto de volta pra editar.</summary>
    private void ApagarTextoDoBitmap(TextoColocado texto)
    {
        if (ImagensPorCamada is null || !ImagensPorCamada.TryGetValue(texto.CamadaId, out var bitmap))
            return;

        using var canvas = new SKCanvas(bitmap);
        using var paintApagar = new SKPaint { BlendMode = SKBlendMode.Clear };

        var pivo = new SKPoint(texto.Retangulo.Left, texto.Retangulo.Bottom);
        canvas.Save();
        if (texto.Acao.RotacaoGraus != 0f)
            canvas.RotateDegrees(texto.Acao.RotacaoGraus, pivo.X, pivo.Y);
        canvas.DrawRect(SKRect.Inflate(texto.Retangulo, 2f, 2f), paintApagar);
        canvas.Restore();

        // Mesmo com a rotação do texto desfeita acima, a máscara em si nunca gira (ver AcaoTexto) —
        // redesenha direto, sem Save/RotateDegrees/Restore.
        if (texto.Acao.AreaCobertura is { } areaCobertura && texto.Acao.MascaraCobertura is { } mascara)
            canvas.DrawBitmap(mascara, areaCobertura);

        InvalidarComposicaoCache();
    }

    /// <summary>Regrava um texto existente EXATAMENTE como estava — usado só quando "Cancelar" desfaz
    /// uma edição iniciada por <see cref="IniciarEdicaoTextoColocado"/>.</summary>
    private void RegravarTextoNaCamada(TextoColocado texto)
    {
        if (ImagensPorCamada is null || !ImagensPorCamada.TryGetValue(texto.CamadaId, out var bitmap))
            return;

        using var canvas = new SKCanvas(bitmap);
        DesenharAcao(canvas, texto.Acao);

        InvalidarComposicaoCache();
        DesenhoAlterado?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>"Pega de volta" um texto já gravado: tira ele de <see cref="_historico"/> (sem
    /// bagunçar desfazer/refazer da camada onde estava — ver comentário em
    /// <see cref="TextoColocado"/>), apaga o pixel dele (<see cref="ApagarTextoDoBitmap"/>) e o
    /// transforma num elemento pendente na posição/rotação atuais, reaproveitando a mesma barra de
    /// girar/mover/A-/A+/confirmar/cancelar de sempre. Guarda o estado original em
    /// <see cref="_textoOriginalEmEdicao"/> — se o usuário cancelar, o texto volta pro lugar em vez
    /// de sumir (ver CancelarElementoPendente).</summary>
    private void IniciarEdicaoTextoColocado(TextoColocado texto)
    {
        _textosColocados.Remove(texto);
        _historico.Remove(texto.Acao);
        ApagarTextoDoBitmap(texto);
        _textoOriginalEmEdicao = texto;

        _elementoPendente = new ElementoTextoPendente
        {
            Texto = texto.Acao.Texto,
            Cor = texto.Acao.Cor,
            TamanhoFonte = texto.Acao.TamanhoFonte,
            Retangulo = texto.Retangulo,
            RotacaoGraus = texto.Acao.RotacaoGraus,
            // Sem isto, confirmar a edição chamava AdicionarTexto com AreaCobertura/MascaraCobertura
            // nulos — perdendo a cobertura da cota pra sempre a partir da primeira reedição (mesmo
            // bug do ApagarTextoDoBitmap acima, na outra ponta: ao GRAVAR de novo).
            AreaCobertura = texto.Acao.AreaCobertura,
            MascaraCobertura = texto.Acao.MascaraCobertura,
        };

        DesenhoAlterado?.Invoke(this, EventArgs.Empty);
        InvalidateSurface();
        TextoEmEdicaoIniciado?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Toque enquanto há um elemento pendente: arrasta pra mover — nunca desenha na camada
    /// nem aciona modo texto/pan enquanto está posicionando.</summary>
    /// <summary>Ponto de tela (mesma unidade de <see cref="SKTouchEventArgs.Location"/>, relativo ao
    /// próprio canvas — NÃO convertido pra coordenada nativa) de cada atualização de arrasto do
    /// elemento pendente. A Page assina isto pra saber, em coordenadas de tela reais, se o dedo está
    /// sobre a lixeira (ver PlantaPage.LixeiraElementoPendente/PontoEstaSobreLixeiraElemento) — pedido
    /// do usuário: "deixe que eu possa arrastar o texto ou ícones também para a lixeira".</summary>
    public event EventHandler<SKPoint>? ElementoPendenteArrastando;

    /// <summary>Disparado ao soltar o dedo (não ao cancelar) com um elemento ainda pendente — a Page
    /// decide, com base no último ponto de <see cref="ElementoPendenteArrastando"/>, se deve excluir
    /// (dedo solto sobre a lixeira, ver <see cref="ExcluirElementoPendente"/>) ou deixar como está,
    /// aguardando Confirmar/Cancelar normalmente.</summary>
    public event EventHandler? ElementoPendenteSolto;

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
                ElementoPendenteArrastando?.Invoke(this, e.Location);
                break;

            case SKTouchAction.Moved when _ultimoPontoElementoPendente is { } ultimo:
                var deslocX = ponto.X - ultimo.X;
                var deslocY = ponto.Y - ultimo.Y;
                pendente.Retangulo = SKRect.Create(
                    pendente.Retangulo.Left + deslocX, pendente.Retangulo.Top + deslocY,
                    pendente.Retangulo.Width, pendente.Retangulo.Height);
                _ultimoPontoElementoPendente = ponto;
                ElementoPendenteArrastando?.Invoke(this, e.Location);
                InvalidateSurface();
                break;

            case SKTouchAction.Released:
                (Handler?.PlatformView as Android.Views.View)?.Parent?.RequestDisallowInterceptTouchEvent(false);
                _ultimoPontoElementoPendente = null;
                ElementoPendenteSolto?.Invoke(this, EventArgs.Empty);
                break;

            case SKTouchAction.Cancelled:
                (Handler?.PlatformView as Android.Views.View)?.Parent?.RequestDisallowInterceptTouchEvent(false);
                _ultimoPontoElementoPendente = null;
                break;
        }

        e.Handled = true;
    }

    /// <summary>Descarta o elemento pendente (novo ou pego de volta pra editar) SEM regravar/restaurar
    /// nada — ao contrário de <see cref="CancelarElementoPendente"/>, que devolve um elemento
    /// existente pro lugar. Usado quando o usuário solta o texto/ícone sobre a lixeira (ver
    /// <see cref="ElementoPendenteSolto"/>): se já era um elemento existente, ele já tinha sido apagado
    /// do bitmap ao ser pego pra editar (<see cref="IniciarEdicaoIconeColocado"/>/<see cref="IniciarEdicaoTextoColocado"/>),
    /// então simplesmente não regravar é o bastante pra excluir de vez.</summary>
    public void ExcluirElementoPendente()
    {
        _iconeOriginalEmEdicao = null;
        _textoOriginalEmEdicao = null;
        _elementoPendente = null;
        _ultimoPontoElementoPendente = null;
        InvalidateSurface();
    }

    /// <summary>Converte um ponto em coordenadas de tela pra coordenadas nativas da imagem base —
    /// mesma conta usada no início de <see cref="OnTouch"/>, extraída pra reuso pelo elemento pendente.</summary>
    private SKPoint ConverterTelaParaNativo(SKPoint pontoTela) =>
        UsarResolucaoNativa && Zoom > 0
            ? new SKPoint((pontoTela.X - PanX) / Zoom, (pontoTela.Y - PanY) / Zoom)
            : pontoTela;

    /// <summary>Tamanho mínimo (nos dois eixos, em pixels nativos) pra considerar a seleção de cota
    /// intencional — evita disparar <see cref="SelecaoCotaConcluida"/> num simples toque acidental.</summary>
    private const float TamanhoMinimoSelecaoCota = 10f;

    private void GerenciarSelecaoCota(SKTouchEventArgs e)
    {
        var ponto = ConverterTelaParaNativo(e.Location);
        if (ImagemBase is { } imagemBase)
        {
            ponto = new SKPoint(
                Math.Clamp(ponto.X, 0, imagemBase.Width),
                Math.Clamp(ponto.Y, 0, imagemBase.Height));
        }

        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
                // Mesmo motivo do desenho normal (ver OnTouch/Pressed mais abaixo): sem isto, o pai
                // (ScrollView/gestos do sistema) pode reivindicar o arrasto como se fosse rolagem
                // assim que detecta movimento, travando o retângulo no ponto inicial.
                (Handler?.PlatformView as Android.Views.View)?.Parent?.RequestDisallowInterceptTouchEvent(true);
                _selecaoCotaInicio = ponto;
                _selecaoCotaRetangulo = SKRect.Create(ponto.X, ponto.Y, 0, 0);
                InvalidateSurface();
                break;

            case SKTouchAction.Moved when _selecaoCotaInicio is { } inicio:
                _selecaoCotaRetangulo = SKRect.Create(
                    Math.Min(inicio.X, ponto.X), Math.Min(inicio.Y, ponto.Y),
                    Math.Abs(ponto.X - inicio.X), Math.Abs(ponto.Y - inicio.Y));
                InvalidateSurface();
                break;

            case SKTouchAction.Released:
            case SKTouchAction.Cancelled:
                (Handler?.PlatformView as Android.Views.View)?.Parent?.RequestDisallowInterceptTouchEvent(false);
                var retanguloFinal = _selecaoCotaRetangulo;
                _selecaoCotaInicio = null;
                _selecaoCotaRetangulo = null;
                InvalidateSurface();

                if (e.ActionType == SKTouchAction.Released
                    && retanguloFinal is { Width: >= TamanhoMinimoSelecaoCota, Height: >= TamanhoMinimoSelecaoCota } retangulo)
                    SelecaoCotaConcluida?.Invoke(this, retangulo);
                break;
        }
    }

    private static void DesenharRetanguloSelecaoCota(SKCanvas canvas, SKRect retangulo)
    {
        using var preenchimento = new SKPaint { Color = new SKColor(33, 150, 243, 40), Style = SKPaintStyle.Fill };
        using var borda = new SKPaint
        {
            Color = SKColors.DodgerBlue,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            PathEffect = SKPathEffect.CreateDash([8f, 6f], 0),
            IsAntialias = true,
        };
        canvas.DrawRect(retangulo, preenchimento);
        canvas.DrawRect(retangulo, borda);
    }

    /// <summary>Ferramenta de lápis em qualquer estilo "reta" (contínua/pontilhada/tracejada, ver
    /// <see cref="EstiloTraco"/>): arrastar desenha uma prévia (ver <see cref="DesenharLinhaRetaPreview"/>)
    /// do ponto inicial até o ponto atual, em vez de acompanhar o dedo com o traço livre de sempre — só
    /// grava de fato na camada ativa (uma única <see cref="AcaoTraco"/> de 2 pontos) ao soltar, igual à
    /// ferramenta de cota grava o retângulo só no Released (ver <see cref="GerenciarSelecaoCota"/>, mesmo
    /// padrão de prévia solta + bake no fim do gesto).</summary>
    private void GerenciarLinhaReta(SKTouchEventArgs e, SKPoint ponto)
    {
        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
                // Mesmo motivo do desenho normal (ver OnTouch/Pressed mais abaixo): sem isto, o pai
                // (ScrollView/gestos do sistema) pode reivindicar o arrasto como se fosse rolagem assim
                // que detecta movimento, travando a linha no ponto inicial.
                (Handler?.PlatformView as Android.Views.View)?.Parent?.RequestDisallowInterceptTouchEvent(true);
                _linhaRetaInicio = ponto;
                _linhaRetaAtual = ponto;
                InvalidateSurface();
                break;

            case SKTouchAction.Moved when _linhaRetaInicio is not null:
                _linhaRetaAtual = ponto;
                InvalidateSurface();
                break;

            case SKTouchAction.Released:
            case SKTouchAction.Cancelled:
                (Handler?.PlatformView as Android.Views.View)?.Parent?.RequestDisallowInterceptTouchEvent(false);
                var inicioFinal = _linhaRetaInicio;
                var fimFinal = _linhaRetaAtual;
                _linhaRetaInicio = null;
                _linhaRetaAtual = null;

                if (e.ActionType == SKTouchAction.Released
                    && inicioFinal is { } inicio && fimFinal is { } fim
                    && CamadaAtivaId is { } camadaId && ImagensPorCamada is not null)
                {
                    var dx = fim.X - inicio.X;
                    var dy = fim.Y - inicio.Y;
                    // Ignora um toque parado/quase parado (sem arrasto de verdade) — evita gravar uma
                    // "linha" de comprimento zero por acidente.
                    if (MathF.Sqrt(dx * dx + dy * dy) >= 2f)
                    {
                        var acao = new AcaoTraco([inicio, fim], CorTraco, EspessuraTraco, Apagar: false, EstiloTraco);
                        var bitmap = ObterOuCriarBitmapDaCamada(camadaId);
                        using (var canvasBitmap = new SKCanvas(bitmap))
                            DesenharTraco(canvasBitmap, acao);

                        _historico.Add(acao);
                        _desfeitas.Clear();
                        InvalidarComposicaoCache();
                        DesenhoAlterado?.Invoke(this, EventArgs.Empty);
                    }
                }

                InvalidateSurface();
                break;
        }
    }

    /// <summary>Prévia da linha reta em andamento (ver <see cref="GerenciarLinhaReta"/>) — usa o mesmo
    /// <see cref="CriarPaintTraco"/> do traço final, pro que o usuário vê arrastando bater exatamente
    /// com o que fica gravado ao soltar.</summary>
    private static void DesenharLinhaRetaPreview(SKCanvas canvas, SKPoint inicio, SKPoint fim, string cor, float espessura, EstiloTraco estilo)
    {
        using var paint = CriarPaintTraco(cor, espessura, false, estilo);
        canvas.DrawLine(inicio, fim, paint);
    }

    /// <summary>Recorta a composição atual (imagem base + camadas) na área indicada (coordenadas
    /// nativas) num bitmap novo, formato Rgba8888 explícito — layout de bytes que
    /// <c>OcrTextoService</c> espera pra converter direto pra um Android Bitmap sem depender do
    /// SKColorType "de plataforma" (que varia). Usado pela ferramenta de cota antes de mandar o
    /// recorte pro OCR; quem chama é responsável por descartar (Dispose) o bitmap retornado.</summary>
    public SKBitmap? RecortarComposicaoNativa(SKRect retangulo)
    {
        var imagensPorCamada = ImagensPorCamada as IReadOnlyDictionary<Guid, SKBitmap>;
        var composicao = ObterComposicaoNativa(imagensPorCamada);
        if (composicao is null)
            return null;

        var subset = SKRectI.Round(retangulo);
        subset.Intersect(new SKRectI(0, 0, composicao.Width, composicao.Height));
        if (subset.Width <= 0 || subset.Height <= 0)
            return null;

        var destino = new SKBitmap(subset.Width, subset.Height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        using var canvasRecorte = new SKCanvas(destino);
        canvasRecorte.Clear(SKColors.White);
        canvasRecorte.DrawBitmap(composicao, subset, new SKRect(0, 0, subset.Width, subset.Height));
        return destino;
    }

    /// <summary>Estima a cor de fundo do recorte (ver <see cref="RecortarComposicaoNativa"/>) pela cor
    /// mais frequente na BORDA do retângulo — a moldura de uma seleção de cota é, quase sempre, bem
    /// maior que o traço fino do número/texto impresso dentro dela, então o fundo real domina a borda
    /// mesmo contando alguns pixels do próprio texto que encostem nela. Usado pela ferramenta de cota
    /// pra cobrir o número original com uma cor parecida com o papel da planta, em vez de branco fixo
    /// (que destoa em PDFs escaneados com fundo levemente bege/cinza).</summary>
    public static SKColor DetectarCorDeFundo(SKBitmap recorte)
    {
        var contagem = new Dictionary<(byte R, byte G, byte B, byte A), int>();

        void Contar(int x, int y)
        {
            var cor = recorte.GetPixel(x, y);
            var chave = (cor.Red, cor.Green, cor.Blue, cor.Alpha);
            contagem[chave] = contagem.GetValueOrDefault(chave) + 1;
        }

        for (var x = 0; x < recorte.Width; x++)
        {
            Contar(x, 0);
            Contar(x, recorte.Height - 1);
        }
        for (var y = 0; y < recorte.Height; y++)
        {
            Contar(0, y);
            Contar(recorte.Width - 1, y);
        }

        if (contagem.Count == 0)
            return SKColors.White;

        var maisFrequente = contagem.MaxBy(par => par.Value).Key;
        return new SKColor(maisFrequente.R, maisFrequente.G, maisFrequente.B, maisFrequente.A);
    }

    /// <summary>Distância (por canal) pra considerar um pixel "tinta" (parte do número/texto impresso),
    /// não fundo — folgada o bastante pra ignorar antialiasing leve entre o traço e o papel, sem
    /// deixar de detectar tinta clara (grafite/tinta desbotada de scan antigo).</summary>
    private const int ToleranciaCorTinta = 40;

    /// <summary>Diferença mínima do canal vermelho acima do verde/azul pra considerar um pixel
    /// "vermelho" — a cor típica da linha de cota nas plantas importadas. Usada tanto pra NÃO inflar o
    /// bounding box do número (ver <see cref="DetectarAreaTexto"/>, a linha às vezes encosta/cruza bem
    /// perto do número) quanto pra preservar esses pixels na hora de cobrir (ver
    /// <see cref="CriarMascaraCobertura"/>) — pedido do usuário: "pixels vermelhos você não tira, pois
    /// fazem parte da linha de cota".</summary>
    private const int ToleranciaVermelho = 40;

    private static bool EhVermelho(SKColor cor) =>
        cor.Red - cor.Green > ToleranciaVermelho && cor.Red - cor.Blue > ToleranciaVermelho;

    /// <summary>Um pixel conta como "tinta" (número/texto impresso) quando destoa de
    /// <paramref name="corFundo"/> além de <see cref="ToleranciaCorTinta"/> e não é vermelho (linha de
    /// cota, ver <see cref="EhVermelho"/> — nunca conta como tinta do número). Critério único
    /// reaproveitado tanto pra achar o contorno do número (<see cref="DetectarAreaTexto"/>) quanto pra
    /// decidir o que de fato cobrir (<see cref="CriarMascaraCobertura"/>) — os dois precisam concordar
    /// no que é "número" pro resultado final bater com a área calculada.</summary>
    private static bool EhTinta(SKColor cor, SKColor corFundo) =>
        (Math.Abs(cor.Red - corFundo.Red) > ToleranciaCorTinta ||
         Math.Abs(cor.Green - corFundo.Green) > ToleranciaCorTinta ||
         Math.Abs(cor.Blue - corFundo.Blue) > ToleranciaCorTinta)
        && !EhVermelho(cor);

    /// <summary>Encontra o retângulo (coordenadas locais do <paramref name="recorte"/>) que envolve só
    /// os pixels de "tinta" (ver <see cref="EhTinta"/>), não o retângulo inteiro que o usuário
    /// arrastou. O usuário costuma arrastar com folga ao redor do número (pra facilitar o toque), e
    /// essa folga pode conter linhas/traços de cota vizinhos — cobrir e reposicionar o texto
    /// substituto com base no retângulo INTEIRO apagava esses traços e deixava a posição/tamanho do
    /// texto novo perceptivelmente diferente do número original (pedido do usuário: cobrir só os
    /// pixels do número, alinhar exatamente com a posição/tamanho do texto anterior). Cai de volta pro
    /// recorte inteiro se nenhum pixel destoar o bastante (seleção vazia/uniforme, ou só linha vermelha
    /// sem nenhum número dentro).</summary>
    public static SKRect DetectarAreaTexto(SKBitmap recorte, SKColor corFundo)
    {
        int minX = recorte.Width, minY = recorte.Height, maxX = -1, maxY = -1;

        for (var y = 0; y < recorte.Height; y++)
        {
            for (var x = 0; x < recorte.Width; x++)
            {
                if (!EhTinta(recorte.GetPixel(x, y), corFundo))
                    continue;

                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }

        if (maxX < minX || maxY < minY)
            return new SKRect(0, 0, recorte.Width, recorte.Height);

        // Pequena folga ao redor da tinta detectada — a área calculada aqui delimita ATÉ ONDE olhar
        // (ver CriarMascaraCobertura, que decide pixel a pixel o que cobrir dentro dela); sem folga, um
        // pixel de antialiasing bem na borda do bounding box ficaria fora da área e nunca seria
        // avaliado/coberto.
        const float Folga = 3f;
        return new SKRect(
            Math.Max(0, minX - Folga), Math.Max(0, minY - Folga),
            Math.Min(recorte.Width, maxX + 1 + Folga), Math.Min(recorte.Height, maxY + 1 + Folga));
    }

    /// <summary>Gera o bitmap usado pra "cobrir" o número original (ver <see cref="AcaoTexto.MascaraCobertura"/>)
    /// — sobrepõe <paramref name="corFundo"/> SÓ nos pixels classificados como tinta do número (ver
    /// <see cref="EhTinta"/>, com uma dilatação de 1px pra pegar a franja de antialiasing ao redor de
    /// cada traço), deixando todo o resto — fundo de verdade, linha de cota, qualquer outro detalhe da
    /// planta que tenha caído dentro da área — absolutamente intocado. Antes cobria a área inteira
    /// (menos os pixels vermelhos) com um preenchimento sólido, que ainda alterava fundo/traços que só
    /// estavam ali por estarem dentro do retângulo, não por serem o número (pedido do usuário: "deixar
    /// com a cor do fundo apenas o texto identificado... basicamente fará a sobreposição em cima do
    /// texto identificado"). <paramref name="origem"/> é a composição já recortada (mesma usada pro
    /// OCR); <paramref name="areaLocal"/> é a área (coordenadas locais de <paramref name="origem"/>)
    /// examinada.</summary>
    public static SKBitmap CriarMascaraCobertura(SKBitmap origem, SKRectI areaLocal, SKColor corFundo)
    {
        var largura = Math.Max(1, areaLocal.Width);
        var altura = Math.Max(1, areaLocal.Height);

        var ehTinta = new bool[largura, altura];
        for (var y = 0; y < altura; y++)
        {
            for (var x = 0; x < largura; x++)
            {
                var origemX = Math.Clamp(areaLocal.Left + x, 0, origem.Width - 1);
                var origemY = Math.Clamp(areaLocal.Top + y, 0, origem.Height - 1);
                ehTinta[x, y] = EhTinta(origem.GetPixel(origemX, origemY), corFundo);
            }
        }

        var mascara = new SKBitmap(largura, altura, SKColorType.Rgba8888, SKAlphaType.Premul);
        for (var y = 0; y < altura; y++)
        {
            for (var x = 0; x < largura; x++)
            {
                // Dilata 1px: cobre também o vizinho imediato de um pixel de tinta, mesmo que ele
                // sozinho não passasse no teste — é a franja de antialiasing entre o traço e o fundo,
                // que sem isto ficaria como um contorno claro/acinzentado do número antigo ao redor do
                // texto novo.
                var cobrir = false;
                for (var dy = -1; dy <= 1 && !cobrir; dy++)
                for (var dx = -1; dx <= 1 && !cobrir; dx++)
                {
                    var vx = x + dx;
                    var vy = y + dy;
                    if (vx >= 0 && vx < largura && vy >= 0 && vy < altura && ehTinta[vx, vy])
                        cobrir = true;
                }

                mascara.SetPixel(x, y, cobrir ? corFundo : SKColors.Transparent);
            }
        }

        return mascara;
    }

    /// <summary>Escreve texto na camada ativa na posição indicada (coordenadas na resolução nativa
    /// da imagem base) — entra no histórico igual a um traço, pra poder desfazer.</summary>
    public void AdicionarTexto(
        string texto, SKPoint posicao, string cor, float tamanhoFonte, float rotacaoGraus = 0f,
        SKRect? areaCobertura = null, SKBitmap? mascaraCobertura = null)
    {
        if (CamadaAtivaId is not { } camadaId || ImagensPorCamada is null || string.IsNullOrWhiteSpace(texto))
            return;

        var acao = new AcaoTexto(texto, posicao, cor, tamanhoFonte, rotacaoGraus, areaCobertura, mascaraCobertura);
        var bitmap = ObterOuCriarBitmapDaCamada(camadaId);
        using (var canvas = new SKCanvas(bitmap))
            DesenharAcao(canvas, acao);

        _historico.Add(acao);
        _desfeitas.Clear();
        _textosColocados.Add(new TextoColocado(Guid.NewGuid(), camadaId, acao, RetanguloDoTexto(acao)));

        InvalidarComposicaoCache();
        DesenhoAlterado?.Invoke(this, EventArgs.Empty);
        InvalidateSurface();
    }

    /// <summary>Calcula o retângulo (canto inferior-esquerdo = pivô) de um AcaoTexto já gravado —
    /// mesma conta usada em IniciarTextoPendente, extraída pra reuso no rastreio de
    /// <see cref="_textosColocados"/> (hit-test do toque longo/preview).</summary>
    private static SKRect RetanguloDoTexto(AcaoTexto acao)
    {
        using var fonte = new SKFont(SKTypeface.Default, acao.TamanhoFonte);
        var largura = Math.Max(20f, fonte.MeasureText(acao.Texto));
        var altura = acao.TamanhoFonte * 1.3f;
        return new SKRect(acao.Posicao.X, acao.Posicao.Y - altura, acao.Posicao.X + largura, acao.Posicao.Y);
    }

    private void RedesenharDoHistorico()
    {
        if (CamadaAtivaId is not { } camadaId || ImagensPorCamada is null)
            return;

        var bitmap = ObterOuCriarBitmapDaCamada(camadaId);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        if (PlantaOverlayRenderer.PodeDesenhar(_bitmapBaseHistorico))
            canvas.DrawBitmap(_bitmapBaseHistorico, 0, 0);
        foreach (var acao in _historico)
            DesenharAcao(canvas, acao);

        InvalidarComposicaoCache();
        DesenhoAlterado?.Invoke(this, EventArgs.Empty);
        InvalidateSurface();
    }

    /// <summary>Tira a cópia do bitmap da camada agora ativa — ver comentário em
    /// <see cref="_bitmapBaseHistorico"/>. Chamado sempre que CamadaAtivaId muda (ver
    /// CamadaAtivaIdProperty).</summary>
    private void CapturarBaselineHistorico(Guid? camadaId)
    {
        _bitmapBaseHistorico?.Dispose();
        _bitmapBaseHistorico = null;

        if (camadaId is not { } id || ImagensPorCamada is null)
            return;

        var atual = ObterOuCriarBitmapDaCamada(id);
        if (PlantaOverlayRenderer.PodeDesenhar(atual))
            _bitmapBaseHistorico = atual.Copy();
    }

    private static void DesenharAcao(SKCanvas canvas, AcaoDesenho acao)
    {
        switch (acao)
        {
            case AcaoTraco traco:
                DesenharTraco(canvas, traco);
                break;
            case AcaoTexto texto:
                if (texto.AreaCobertura is { } areaCobertura && texto.MascaraCobertura is { } mascara)
                    canvas.DrawBitmap(mascara, areaCobertura);

                using (var paint = new SKPaint { IsAntialias = true, Color = SKColor.Parse(texto.Cor) })
                using (var fonte = new SKFont(SKTypeface.Default, texto.TamanhoFonte))
                {
                    if (texto.RotacaoGraus == 0f)
                    {
                        canvas.DrawText(texto.Texto, texto.Posicao.X, texto.Posicao.Y, fonte, paint);
                    }
                    else
                    {
                        // Rotaciona em torno do próprio ponto de ancoragem (base do texto) — mesmo
                        // pivô usado no preview do elemento pendente, pra o resultado final bater
                        // exatamente com o que o usuário viu antes de confirmar.
                        canvas.Save();
                        canvas.RotateDegrees(texto.RotacaoGraus, texto.Posicao.X, texto.Posicao.Y);
                        canvas.DrawText(texto.Texto, texto.Posicao.X, texto.Posicao.Y, fonte, paint);
                        canvas.Restore();
                    }
                }
                break;
        }
    }

    /// <summary>Monta o SKPaint de um traço — reaproveitado tanto pra gravar de fato (<see cref="DesenharTraco"/>)
    /// quanto pra prévia da linha reta em andamento (<see cref="DesenharLinhaRetaPreview"/>), garantindo
    /// que os dois batem pixel a pixel. Apagando, <paramref name="estilo"/> é ignorado de propósito —
    /// borracha sempre "sólida" (Clear), pontilhado/tracejado não fazem sentido num apagar.</summary>
    private static SKPaint CriarPaintTraco(string cor, float espessura, bool apagar, EstiloTraco estilo) => new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round,
        StrokeWidth = espessura,
        BlendMode = apagar ? SKBlendMode.Clear : SKBlendMode.SrcOver,
        Color = apagar ? SKColors.Transparent : SKColor.Parse(cor),
        PathEffect = apagar ? null : CriarEfeitoTraco(estilo, espessura),
    };

    /// <summary>Padrão de traço (ver <see cref="EstiloTraco"/>) — pontilhado usa um "traço" bem curto
    /// (quase um ponto) com StrokeCap.Round (já fixo em <see cref="CriarPaintTraco"/>), que arredonda
    /// cada segmento minúsculo numa bolinha; tracejado usa segmentos bem mais longos, o traço-e-vão
    /// clássico. Nulo (sem efeito) pra linha contínua e pro desenho livre.</summary>
    private static SKPathEffect? CriarEfeitoTraco(EstiloTraco estilo, float espessura) => estilo switch
    {
        EstiloTraco.RetaPontilhada => SKPathEffect.CreateDash([Math.Max(0.5f, espessura * 0.1f), espessura * 2.2f], 0),
        EstiloTraco.RetaTracejada => SKPathEffect.CreateDash([espessura * 3.5f, espessura * 2.2f], 0),
        _ => null,
    };

    // Curva suave: cada trecho é uma Bézier quadrática entre os pontos médios de dois pontos
    // capturados consecutivos, usando o ponto entre eles como controle — em vez de conectar os pontos
    // crus com retas, que deixava o traço visivelmente anguloso em curvas rápidas do dedo (o toque
    // não amostra pontos infinitamente próximos; bug reportado: "desenho livre não faz curvas"). Mesma
    // técnica usada ao vivo em OnTouch, pra redesenho do histórico (desfazer/refazer/recarregar) bater
    // com o que foi desenhado na hora — ver DesenharCaminhoDoTraco.
    private static void DesenharTraco(SKCanvas canvas, AcaoTraco traco)
    {
        using var paint = CriarPaintTraco(traco.Cor, traco.Espessura, traco.Apagar, traco.Estilo);
        DesenharCaminhoDoTraco(canvas, traco.Pontos, paint, traco.Espessura);
    }

    /// <summary>Prévia do traço em andamento, desenhada direto na tela (não no bitmap da camada — ver
    /// comentário em OnTouch). Pra um traço normal, é só <see cref="DesenharTraco"/> mesmo — a cor
    /// aparece igual ao resultado final. Pra borracha, NÃO dá pra usar o mesmo BlendMode.Clear da
    /// gravação de verdade: aqui estaríamos "limpando" pixels da SUPERFÍCIE DE TELA já composta (não
    /// de um bitmap com canal alfa que depois é combinado com outra coisa por baixo), e limpar pra
    /// alfa zero num destino sem nada por baixo pinta preto, não transparente (bug reportado: "a
    /// borracha está com rastro preto, quero transparente"). Em vez de tentar simular o apagar de
    /// verdade aqui, mostra só um traçado indicador (branco translúcido) — o apagar de fato só
    /// acontece mesmo ao soltar o dedo, direto no bitmap real da camada, onde Clear funciona
    /// corretamente.</summary>
    private static void DesenharTracoPreview(SKCanvas canvas, AcaoTraco traco)
    {
        if (!traco.Apagar)
        {
            DesenharTraco(canvas, traco);
            return;
        }

        var tracoIndicador = traco with { Cor = "#FFFFFF", Apagar = false, Estilo = EstiloTraco.Livre };
        using var paint = CriarPaintTraco(tracoIndicador.Cor, tracoIndicador.Espessura, false, EstiloTraco.Livre);
        paint.Color = paint.Color.WithAlpha(160);
        DesenharCaminhoDoTraco(canvas, tracoIndicador.Pontos, paint, tracoIndicador.Espessura);
    }

    /// <summary>Lógica de desenho do caminho (ponto único/reta/curva suave) extraída de
    /// <see cref="DesenharTraco"/> pra reuso em <see cref="DesenharTracoPreview"/>, sem duplicar a
    /// suavização por Bézier.</summary>
    private static void DesenharCaminhoDoTraco(SKCanvas canvas, List<SKPoint> pontos, SKPaint paint, float espessura)
    {
        if (pontos.Count == 1)
        {
            canvas.DrawCircle(pontos[0], espessura / 2, paint);
            return;
        }

        if (pontos.Count == 2)
        {
            canvas.DrawLine(pontos[0], pontos[1], paint);
            return;
        }

        using var caminho = new SKPath();
        caminho.MoveTo(pontos[0]);
        for (var i = 1; i < pontos.Count - 1; i++)
        {
            var pontoMedio = new SKPoint((pontos[i].X + pontos[i + 1].X) / 2f, (pontos[i].Y + pontos[i + 1].Y) / 2f);
            caminho.QuadTo(pontos[i], pontoMedio);
        }
        caminho.LineTo(pontos[^1]);
        canvas.DrawPath(caminho, paint);
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
            // contrário de redimensionar cada bitmap individualmente. A composição em si (imagem base
            // + camadas) vem cacheada de ObterComposicaoNativa — um único DrawBitmap por frame em vez
            // de um por camada, senão mexer no zoom/pan (que chama InvalidateSurface a cada pixel de
            // arrasto) refazia a composição inteira nesse ritmo, travando em aparelhos mais fracos.
            canvas.Save();
            canvas.Translate(PanX, PanY);
            canvas.Scale(Zoom);

            var composicao = ObterComposicaoNativa(imagensPorCamada);
            if (composicao is not null && PlantaOverlayRenderer.PodeDesenhar(composicao))
            {
                // Acima de 100% (Zoom > 1), cada pixel nativo já ocupa mais de um pixel de tela — a
                // suavização padrão (bilinear) borra as bordas do traço/texto impresso bem nesse ponto
                // (bug reportado: "quando fica muito perto está embaçando e a lupa não identifica o
                // texto" — a pessoa dá zoom pra enxergar melhor um número pequeno e o resultado é o
                // oposto). FilterQuality.None (nearest-neighbor) mantém os pixels nítidos/reais em vez
                // de suavizar — mais "serrilhado" de perto, mas sem a mancha borrada, o que ajuda tanto
                // a leitura quanto a mirar a seleção da ferramenta de cota com precisão. Abaixo de 100%
                // (afastado) mantemos suavização (Medium) — nearest nesse sentido gera aliasing feio.
                using var paintComposicao = new SKPaint { FilterQuality = Zoom > 1f ? SKFilterQuality.None : SKFilterQuality.Medium };
                canvas.DrawBitmap(composicao, 0, 0, paintComposicao);
            }

            if (_elementoPendente is { } pendente)
                DesenharElementoPendente(canvas, pendente);

            if (_selecaoCotaRetangulo is { } retanguloSelecao)
                DesenharRetanguloSelecaoCota(canvas, retanguloSelecao);

            if (_linhaRetaInicio is { } linhaInicio && _linhaRetaAtual is { } linhaAtual)
                DesenharLinhaRetaPreview(canvas, linhaInicio, linhaAtual, CorTraco, EspessuraTraco, EstiloTraco);

            // Prévia do traço livre (lápis ou borracha) em andamento — ver comentário em OnTouch sobre
            // por que o traço só é gravado de verdade (e a composição recalculada) ao soltar o dedo.
            if (_tracoEmAndamento is { } tracoEmAndamento)
                DesenharTracoPreview(canvas, tracoEmAndamento);

            canvas.Restore();
            return;
        }

        var imagemBaseEscalada = ObterImagemBaseEscalada(e.Info);

        if (Camadas is { Count: > 0 } camadas)
            PlantaOverlayRenderer.Desenhar(canvas, camadas, imagemBaseEscalada, imagensPorCamada);
        else if (PlantaOverlayRenderer.PodeDesenhar(imagemBaseEscalada))
            canvas.DrawBitmap(imagemBaseEscalada, 0, 0);
    }

    /// <summary>Reaproveita a composição cacheada (ver <see cref="_composicaoNativaCache"/>) se ainda
    /// bater com o tamanho da imagem base, senão desenha tudo de novo uma única vez. Só é invalidada
    /// explicitamente (<see cref="InvalidarComposicaoCache"/>) quando o conteúdo muda de verdade —
    /// aqui só checamos o tamanho como salvaguarda (ex.: imagem base trocada por outra do mesmo
    /// tamanho, caso a invalidação explícita falhe em algum caminho).</summary>
    private SKBitmap? ObterComposicaoNativa(IReadOnlyDictionary<Guid, SKBitmap>? imagensPorCamada)
    {
        if (ImagemBase is not { } imagemBase || !PlantaOverlayRenderer.PodeDesenhar(imagemBase))
            return null;

        if (_composicaoNativaCache is { } cache && cache.Width == imagemBase.Width && cache.Height == imagemBase.Height)
            return cache;

        _composicaoNativaCache?.Dispose();
        var composicao = new SKBitmap(imagemBase.Width, imagemBase.Height);
        using (var canvasComposicao = new SKCanvas(composicao))
        {
            if (Camadas is { Count: > 0 } camadas)
                PlantaOverlayRenderer.Desenhar(canvasComposicao, camadas, imagemBase, imagensPorCamada);
            else
                canvasComposicao.DrawBitmap(imagemBase, 0, 0);
        }

        _composicaoNativaCache = composicao;
        return composicao;
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

        // Ferramenta de cota: modo exclusivo, igual ao lápis — nada mais pode competir pelo toque
        // enquanto está ativa (bug reportado: "trava" quando pinça/pan/pegar-elemento disputavam o
        // mesmo toque no meio do arrasto do retângulo). Por isso checado ANTES do detector de pegar
        // elemento e da pinça, não depois — com esta ferramenta ligada, zoom/pan/pegar-de-volta ficam
        // completamente desligados, só o retângulo de seleção responde ao dedo.
        if (ModoSelecaoCota)
        {
            e.Handled = true;
            GerenciarSelecaoCota(e);
            return;
        }

        // Observa (sem consumir o toque) se o dedo fica parado ~1s sobre um ícone OU texto já
        // colocado, pra "pegar de volta" — funciona em QUALQUER modo/tela, mesmo durante pan/pinça/
        // desenho normal, já que só reage se o toque ficar parado o tempo todo (ver
        // AtualizarDeteccaoPegarElemento).
        AtualizarDeteccaoPegarElemento(e, ConverterTelaParaNativo(e.Location));

        // Pinça (dois dedos) funciona em QUALQUER tela/modo — visualização geral e edição de camada
        // compartilham o mesmo mecanismo (canvas de tamanho fixo + PanX/PanY via GerenciarPan, ver
        // comentário em PlantaPage.AplicarModoEdicaoUi) — sem exigir nenhum modo ligado. Só age de
        // fato quando o SEGUNDO dedo toca a tela; com 0 ou 1 dedo, não faz nada e deixa o toque
        // seguir normalmente (desenho ou GerenciarPan).
        if (AtualizarPinca(e))
        {
            e.Handled = true;
            return;
        }

        // Modo pan explícito (ícone na barra de ferramentas, ver CamadaEdicaoPage) — separado do
        // desenho de propósito: sem ScrollView ao redor do canvas (competia com o próprio gesto de
        // desenhar), arrastar só move a visualização quando esse modo está ligado; nunca ao mesmo
        // tempo que o lápis desenha.
        if (ModoPan)
        {
            e.Handled = true;
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

        // Trava o ponto dentro dos limites da imagem base — o bitmap da camada tem exatamente o
        // tamanho da planta (ver ObterOuCriarBitmapDaCamada), então um toque na margem em volta
        // (visível quando o zoom deixa espaço sobrando, ou fora da planta antes zoom-out) não deve
        // desenhar/escrever nada ali (pedido: "o usuário não pode desenhar fora da planta"). Trava em
        // vez de simplesmente ignorar o toque, pra o traço continuar até a borda em vez de "sumir"
        // assim que o dedo passa da margem.
        if (ImagemBase is { } imagemBase)
        {
            ponto = new SKPoint(
                Math.Clamp(ponto.X, 0, imagemBase.Width),
                Math.Clamp(ponto.Y, 0, imagemBase.Height));
        }

        // A camada "Ícones" nunca recebe traço/texto manual — só existe pra ícones colocados pela
        // ferramenta própria (ver AdicionarIcone). Aqui só detectamos um TAP sobre um ícone já
        // colocado (nesta sessão) pra oferecer excluir; nada mais desenha nela.
        if (camadaAtiva.Nome == PlantaViewModel.NomeCamadaIcones)
        {
            TratarToqueNaCamadaIcones(e, ponto);
            e.Handled = true;
            return;
        }

        if (ModoTexto)
        {
            if (e.ActionType == SKTouchAction.Pressed)
                SolicitarTexto?.Invoke(this, ponto);

            e.Handled = true;
            return;
        }

        // Ferramenta de lápis num estilo "reta" (contínua/pontilhada/tracejada — ver EstiloTraco):
        // desenha só uma linha reta do ponto inicial ao final do arrasto (GerenciarLinhaReta), não o
        // traço livre de sempre. Apagando, ignora o estilo e sempre segue livre (apagar em linha reta
        // não tem utilidade prática) — pedido do usuário: "ferramenta lápis: deixe abrir mais opções:
        // linha reta contínua/pontilhada/tracejada, desenho livre".
        if (EstiloTraco != EstiloTraco.Livre && !ModoApagar)
        {
            GerenciarLinhaReta(e, ponto);
            e.Handled = true;
            InvalidateSurface();
            return;
        }

        // Só acumula os pontos aqui (praticamente grátis) — o traço só é de fato pintado no bitmap da
        // camada (DesenharTraco, com toda a suavização por curva Bézier) UMA VEZ, ao soltar o dedo.
        // Antes desenhava incrementalmente A CADA Moved E invalidava a composição inteira
        // (InvalidarComposicaoCache) a cada amostra de toque — reconstruir a composição inteira
        // (imagem base + todas as camadas) dezenas de vezes por segundo ficou pesado demais depois de
        // subirmos a resolução máxima de 2500 pra 4000px (bug reportado: "os traços estão travando um
        // pouco"). Agora, enquanto o dedo arrasta, a tela mostra a composição (parada, sem o traço
        // novo ainda) + uma prévia leve do traço em andamento desenhada direto por cima (ver
        // OnPaintSurface/_tracoEmAndamento) — só ao soltar é que o traço é gravado de verdade e a
        // composição é recalculada (uma vez só).
        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
                // O canvas normalmente fica dentro de um ScrollView (zoom/pan — ver
                // CamadaEdicaoPage/PlantaPage). Sem isto, o Android entende um arrasto do dedo como
                // gesto de rolagem do ScrollView pai a partir do primeiro Moved, e o traço trava no
                // ponto inicial. Pedimos ao pai pra não interceptar até soltar o dedo.
                (Handler?.PlatformView as Android.Views.View)?.Parent?.RequestDisallowInterceptTouchEvent(true);
                _tracoEmAndamento = new AcaoTraco([ponto], CorTraco, EspessuraTraco, ModoApagar, EstiloTraco.Livre);
                _desfeitas.Clear();
                break;
            case SKTouchAction.Moved when _tracoEmAndamento is not null:
                _tracoEmAndamento.Pontos.Add(ponto);
                break;
            case SKTouchAction.Released:
                (Handler?.PlatformView as Android.Views.View)?.Parent?.RequestDisallowInterceptTouchEvent(false);
                if (_tracoEmAndamento is { } tracoFinal)
                {
                    var bitmap = ObterOuCriarBitmapDaCamada(camadaId);
                    using (var canvasBitmap = new SKCanvas(bitmap))
                        DesenharTraco(canvasBitmap, tracoFinal);

                    _historico.Add(tracoFinal);
                    _tracoEmAndamento = null;
                    InvalidarComposicaoCache();
                    DesenhoAlterado?.Invoke(this, EventArgs.Empty);
                }
                break;
        }

        e.Handled = true;
        InvalidateSurface();
    }

    /// <summary>Reconhece dois dedos na tela e dispara <see cref="ZoomPorGestoSolicitado"/> — chamado
    /// incondicionalmente no início de <see cref="OnTouch"/>, então funciona em QUALQUER tela/modo
    /// (visualização geral com ScrollView nativo, ou edição de camada com <see cref="GerenciarPan"/>),
    /// sem exigir nenhum modo específico ligado. Só devolve true (consome o toque) quando há
    /// exatamente 2 dedos ativos; com 0 ou 1, devolve false sem mexer em nada, deixando o resto de
    /// OnTouch (ou o ScrollView nativo, na visualização) tratar normalmente. Implementado sobre o
    /// toque bruto do SkiaSharp (rastreando cada ponteiro por <see cref="SKTouchEventArgs.Id"/>), não
    /// com um PinchGestureRecognizer nativo do MAUI: as duas abordagens brigavam pela mesma sequência
    /// de toque no Android — o SKCanvasView com EnableTouchEvents já reivindica o fluxo inteiro a
    /// partir do primeiro dedo, então o recognizer nunca reconhecia o segundo dedo de forma confiável
    /// (hora o pan travava depois do zoom, hora a pinça parava de responder por completo — bugs
    /// reportados). Um terceiro dedo é ignorado.</summary>
    private bool AtualizarPinca(SKTouchEventArgs e)
    {
        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
                if (_ponteirosAtivos.Count < 2)
                    _ponteirosAtivos[e.Id] = e.Location;
                if (_ponteirosAtivos.Count == 2)
                    _distanciaAnteriorPinca = DistanciaEntrePonteirosAtivos();
                return _ponteirosAtivos.Count == 2;

            case SKTouchAction.Moved:
                if (_ponteirosAtivos.Count != 2 || !_ponteirosAtivos.ContainsKey(e.Id))
                    return false;

                _ponteirosAtivos[e.Id] = e.Location;
                var distanciaAtual = DistanciaEntrePonteirosAtivos();
                if (_distanciaAnteriorPinca is { } distanciaAnterior && distanciaAnterior > 0)
                    ZoomPorGestoSolicitado?.Invoke(this, (distanciaAtual / distanciaAnterior, CentroEntrePonteirosAtivos()));
                _distanciaAnteriorPinca = distanciaAtual;
                return true;

            case SKTouchAction.Released:
            case SKTouchAction.Cancelled:
                var estavaEmPinca = _ponteirosAtivos.Count == 2;
                _ponteirosAtivos.Remove(e.Id);
                if (_ponteirosAtivos.Count < 2)
                    _distanciaAnteriorPinca = null;
                return estavaEmPinca;

            default:
                return false;
        }
    }

    private float DistanciaEntrePonteirosAtivos()
    {
        var (primeiro, segundo) = ObterParDePonteirosAtivos();
        var dx = primeiro.X - segundo.X;
        var dy = primeiro.Y - segundo.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>Ponto médio entre os dois dedos — usado como âncora do zoom (o que estiver embaixo
    /// dos dedos continua embaixo deles conforme o zoom muda), em vez de um ponto fixo (bug reportado:
    /// "pinça fixa no canto"). Só é confiável agora que a pinça não compete mais com nenhum outro
    /// sistema de gesto pela mesma sequência de toque — ver comentário em AtualizarPinca.</summary>
    private SKPoint CentroEntrePonteirosAtivos()
    {
        var (primeiro, segundo) = ObterParDePonteirosAtivos();
        return new SKPoint((primeiro.X + segundo.X) / 2f, (primeiro.Y + segundo.Y) / 2f);
    }

    private (SKPoint Primeiro, SKPoint Segundo) ObterParDePonteirosAtivos()
    {
        if (_ponteirosAtivos.Count != 2)
            return (default, default);

        using var enumerador = _ponteirosAtivos.Values.GetEnumerator();
        enumerador.MoveNext();
        var primeiro = enumerador.Current;
        enumerador.MoveNext();
        var segundo = enumerador.Current;
        return (primeiro, segundo);
    }

    /// <summary>Com <see cref="ModoPan"/> ligado, arrastar 1 dedo move a visualização (PanX/PanY) —
    /// não desenha nada, o lápis fica desativado enquanto esse modo está ligado. A pinça (2 dedos) é
    /// sempre interceptada antes de chegar aqui (ver <see cref="AtualizarPinca"/> em OnTouch), então
    /// este método só vê, na prática, 0 ou 1 dedo.</summary>
    private void GerenciarPan(SKTouchEventArgs e)
    {
        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
                // O canvas normalmente fica dentro de um ScrollView. Sem isto, o Android entende um
                // arrasto do dedo como gesto de rolagem do ScrollView pai a partir do primeiro Moved, e
                // o pan trava no ponto inicial.
                (Handler?.PlatformView as Android.Views.View)?.Parent?.RequestDisallowInterceptTouchEvent(true);
                break;

            case SKTouchAction.Moved:
                if (_ponteirosAtivos.TryGetValue(e.Id, out var anterior))
                {
                    PanX += e.Location.X - anterior.X;
                    PanY += e.Location.Y - anterior.Y;
                    _ponteirosAtivos[e.Id] = e.Location;
                }
                break;

            case SKTouchAction.Released:
            case SKTouchAction.Cancelled:
                (Handler?.PlatformView as Android.Views.View)?.Parent?.RequestDisallowInterceptTouchEvent(false);
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
