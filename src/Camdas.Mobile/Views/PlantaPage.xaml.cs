using System.ComponentModel;
using Camdas.Mobile.ViewModels;
using Camdas.Contracts;
using Camdas.Mobile.Services;
using SkiaSharp;

namespace Camdas.Mobile.Views;

[QueryProperty(nameof(PlantaId), "plantaId")]
public partial class PlantaPage : ContentPage
{
    private readonly PlantaViewModel _viewModel;
    private readonly IconeSvgCatalogo _iconeCatalogo;
    private bool _zoomAjustadoNoCarregamento;
    private CamadaDto? _camadaArrastada;

    public string PlantaId { get; set; } = string.Empty;

    public PlantaPage(PlantaViewModel viewModel, IconeSvgCatalogo iconeCatalogo)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _iconeCatalogo = iconeCatalogo;
        BindingContext = viewModel;

        // Entrar/sair do modo de edição (ex.: tocar numa camada dispara SelecionarCamadaCommand no
        // XAML, sem passar pelo code-behind) precisa reconfigurar a UI de desenho — reagimos aqui.
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Canvas.SolicitarTexto += OnCanvasSolicitarTexto;
        Canvas.ZoomPorGestoSolicitado += (_, dados) => AtualizarZoomPinca(dados.FatorEscala, dados.Centro);
        Canvas.IconeTocado += OnCanvasIconeTocado;
        Canvas.IconeEmEdicaoIniciada += (_, _) => MostrarBarraPosicionamento("Arraste o ícone pra posicionar (ou cancele pra deixar como estava)");
        Canvas.TextoEmEdicaoIniciado += (_, _) => MostrarBarraPosicionamento("Arraste o texto pra posicionar (ou cancele pra deixar como estava)");

        // O canvas sempre tem o tamanho da área visível do ScrollView (visualização ou edição — ver
        // comentário em AplicarModoEdicaoUi sobre por que os dois modos agora compartilham o mesmo
        // mecanismo de pan/zoom) — reaplica sempre que o layout do ScrollView mudar de tamanho (ex.:
        // a barra de ferramentas de edição aparecendo/sumindo redistribui as linhas do Grid).
        PlantaScroll.SizeChanged += (_, _) => AjustarCanvasParaViewport();

        // Listener nativo Android na lixeira (ver LixeiraDragListener): recebe os eventos reais de
        // entrada/saída do arrasto (o DropGestureRecognizer do MAUI não expõe isso no Android, só
        // Drop) — acende/apaga exatamente quando a camada arrastada está sobre a lixeira.
        BordaLixeira.HandlerChanged += (_, _) =>
        {
            if (BordaLixeira.Handler?.PlatformView is Android.Views.View view)
                view.SetOnDragListener(new LixeiraDragListener(
                    acender: () => AcenderLixeira(true),
                    apagar: () => AcenderLixeira(false),
                    obterCamadaArrastada: () => _camadaArrastada,
                    excluir: ExcluirCamadaArrastadaAsync));
        };
    }

    /// <summary>Recebe os eventos nativos de drag-and-drop do Android na lixeira. O
    /// DropGestureRecognizer do MAUI só expõe "Drop" (sem posição durante o arrasto) — usamos o
    /// listener nativo diretamente pra saber quando a camada está de fato sobre a lixeira
    /// (ActionDragEntered/Exited), sem depender do payload transferido pelo drag (não usamos
    /// DragEvent.ClipData; a camada arrastada já é rastreada em _camadaArrastada).</summary>
    private sealed class LixeiraDragListener(
        Action acender, Action apagar, Func<CamadaDto?> obterCamadaArrastada, Func<CamadaDto, Task> excluir)
        : Java.Lang.Object, Android.Views.View.IOnDragListener
    {
        public bool OnDrag(Android.Views.View? v, Android.Views.DragEvent? e)
        {
            switch (e?.Action)
            {
                case Android.Views.DragAction.Entered:
                    acender();
                    return true;
                case Android.Views.DragAction.Exited:
                case Android.Views.DragAction.Ended:
                    apagar();
                    return true;
                case Android.Views.DragAction.Drop:
                    apagar();
                    if (obterCamadaArrastada() is { } camada)
                        _ = excluir(camada);
                    return true;
                default:
                    // Started/Location: aceita participar do drag em andamento, sem ação própria.
                    return true;
            }
        }
    }

    private async Task ExcluirCamadaArrastadaAsync(CamadaDto camada)
    {
        _camadaArrastada = null;
        var confirmar = await DisplayAlert(
            "Excluir camada", $"Excluir a camada '{camada.Nome}'? O traço dela some, sem volta.", "Excluir", "Cancelar");
        if (!confirmar)
            return;

        await _viewModel.RemoverCamadaAsync(camada);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlantaViewModel.ModoEdicao))
            AplicarModoEdicaoUi();
    }

    /// <summary>Reconfigura a tela ao alternar edição/visualização: desliga os sub-modos (texto/
    /// desenhar/borracha) e reaplica o zoom. Canvas.ModoPan fica sempre ligado (ajuste/pinça por
    /// padrão) em QUALQUER um dos dois modos — antes, a visualização confiava na rolagem nativa do
    /// ScrollView (Orientation="Both") pra arrastar, e só a edição usava PanX/PanY com
    /// Orientation="Neither". O problema: com rolagem nativa ligada, o Android intercepta e "rouba" o
    /// gesto pro ScrollView assim que detecta arrasto — antes que um segundo dedo (pinça) consiga ser
    /// reconhecido pelo nosso código (bug reportado: "pinça não funciona na visualização"). Unificando
    /// os dois modos no mesmo mecanismo (canvas de tamanho fixo + PanX/PanY, rolagem nativa sempre
    /// desligada), a pinça (ver AtualizarPinca/GerenciarPan em PlantaCanvasView) funciona igual nos
    /// dois lugares.</summary>
    private void AplicarModoEdicaoUi()
    {
        Canvas.ModoTexto = false;
        Canvas.ModoPan = true;
        AtualizarDestaqueBotao(BotaoTexto, false);
        AtualizarDestaqueBotao(BotaoDesenhar, false);
        AtualizarDestaqueBorracha(false);

        PlantaScroll.Orientation = ScrollOrientation.Neither;
        AtualizarZoom(Canvas.Zoom <= 0 ? 1f : Canvas.Zoom);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (Guid.TryParse(PlantaId, out var plantaId))
            await _viewModel.CarregarAsync(plantaId);

        // ImagensPorCamada é um Dictionary comum (mutado no lugar) — força o redesenho aqui pra
        // garantir que o traço de cada camada já esteja carregado quando a composição é pintada.
        Canvas.AtualizarPreview();

        // Garante ModoPan/Orientation corretos mesmo na primeiríssima vez que a tela aparece — o
        // PropertyChanged de ModoEdicao (que chama isto normalmente) só dispara numa mudança de
        // verdade, e no primeiro carregamento nunca houve uma.
        AplicarModoEdicaoUi();

        // Primeiro carregamento: começa com a planta inteira visível na tela (em vez do tamanho
        // nativo, que geralmente é maior que a tela e cortava a visualização — bug reportado).
        if (!_zoomAjustadoNoCarregamento && _viewModel.ImagemBase is not null)
        {
            _zoomAjustadoNoCarregamento = true;
            AjustarZoomParaTela();
        }
    }

    /// <summary>Dá ao canvas o tamanho da área visível do ScrollView (o SKCanvasView não tem tamanho
    /// próprio quando hospedado num ScrollView — sem isto ele colapsa pra 0 e a planta não aparece).
    /// Chamado sempre que o ScrollView mudar de tamanho (a barra de ferramentas de edição aparecendo/
    /// sumindo redistribui as linhas do Grid).</summary>
    private void AjustarCanvasParaViewport()
    {
        if (PlantaScroll.Width > 0)
            Canvas.WidthRequest = PlantaScroll.Width;
        if (PlantaScroll.Height > 0)
            Canvas.HeightRequest = PlantaScroll.Height;
    }

    /// <summary>Setado enquanto QUALQUER código daqui muda ZoomSlider.Value programaticamente —
    /// necessário porque atribuir Slider.Value dispara o evento ValueChanged igual um arrasto de
    /// verdade do usuário. Sem esse guard, AtualizarZoomPinca (ancora no ponto médio dos dedos, NÃO
    /// reseta Pan) atualizava o slider só pra manter o rótulo em dia, isso disparava
    /// OnZoomSliderChanged, que chamava ESTE AtualizarZoom — que reseta Canvas.PanX/PanY pra 0 — a
    /// cada evento de pinça, desfazendo a âncora no mesmo instante em que era calculada. Era a causa
    /// raiz real do bug "pinça sempre volta pro canto/ponto fixo" (sobrevivia a toda tentativa
    /// anterior de consertar só a conta da âncora, porque o problema nunca foi a conta).</summary>
    private bool _atualizandoZoomProgramaticamente;

    /// <summary>Canvas sempre do tamanho da área visível (nunca infla pra rolar dentro do ScrollView,
    /// ver comentário em AplicarModoEdicaoUi) — o zoom só afeta o Scale/Translate internos do
    /// OnPaintSurface, igual nos dois modos. Sem inflar, a superfície nunca estoura o limite do
    /// Android (bitmap grande demais), então o zoom vai até o máximo do slider sem precisar de teto
    /// adicional.</summary>
    private void AtualizarZoom(float zoom)
    {
        if (_viewModel.ImagemBase is null)
            return;

        Canvas.UsarResolucaoNativa = true;
        zoom = Math.Clamp(zoom, (float)ZoomSlider.Minimum, (float)ZoomSlider.Maximum);
        AjustarCanvasParaViewport();
        Canvas.PanX = 0;
        Canvas.PanY = 0;
        Canvas.Zoom = zoom;
        Canvas.AtualizarPreview();

        AtualizarSliderELabelSemDispararEvento(zoom);
    }

    /// <summary>Escala a planta pra caber na largura disponível da tela (nunca amplia além de 100%).</summary>
    private void AjustarZoomParaTela()
    {
        if (_viewModel.ImagemBase is not { } imagemBase || imagemBase.Width <= 0)
            return;

        var larguraDisponivel = Width > 0 ? Width - 32 : imagemBase.Width;
        var zoomAjustado = Math.Min(1f, (float)(larguraDisponivel / imagemBase.Width));
        AtualizarZoom(zoomAjustado);
    }

    private void AtualizarSliderELabelSemDispararEvento(float zoom)
    {
        _atualizandoZoomProgramaticamente = true;
        ZoomSlider.Value = zoom;
        _atualizandoZoomProgramaticamente = false;
        ZoomLabel.Text = $"{(int)Math.Round(zoom * 100)}%";
    }

    private void OnZoomSliderChanged(object? sender, ValueChangedEventArgs e)
    {
        if (_atualizandoZoomProgramaticamente)
            return;
        AtualizarZoom((float)e.NewValue);
    }

    private void OnAumentarZoomClicked(object? sender, EventArgs e) => AtualizarZoom(Canvas.Zoom + 0.25f);

    private void OnDiminuirZoomClicked(object? sender, EventArgs e) => AtualizarZoom(Canvas.Zoom - 0.25f);

    private void OnAjustarZoomClicked(object? sender, EventArgs e) => AjustarZoomParaTela();

    private async void OnExcluirCamadaClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: CamadaDto camada })
            return;

        var confirmar = await DisplayAlert(
            "Excluir camada", $"Excluir a camada '{camada.Nome}'? O traço dela some, sem volta.", "Excluir", "Cancelar");
        if (!confirmar)
            return;

        await _viewModel.RemoverCamadaAsync(camada);
    }

    private async void OnOpacidadeSliderDragCompleted(object? sender, EventArgs e)
    {
        if (sender is not Slider slider || slider.BindingContext is not CamadaDto camada)
            return;

        await _viewModel.DefinirOpacidadeAsync(camada, slider.Value);
    }

    /// <summary>Guarda a camada da linha que começou a ser arrastada — a recognizer herda o
    /// BindingContext do item da CollectionView, igual ao slider de opacidade acima. Acender/apagar a
    /// lixeira agora é feito pelo listener nativo (ver LixeiraDragListener), que sabe a posição real.</summary>
    private void OnCamadaDragStarting(object? sender, DragStartingEventArgs e)
    {
        if (sender is DragGestureRecognizer { BindingContext: CamadaDto camada })
            _camadaArrastada = camada;
    }

    /// <summary>Rede de segurança: se o arrasto terminar sem nenhum evento nativo da lixeira ter
    /// disparado (ex.: solto fora de qualquer alvo), garante que ela não fique acesa.</summary>
    private void OnCamadaDropCompleted(object? sender, DropCompletedEventArgs e) => AcenderLixeira(false);

    private void AcenderLixeira(bool acesa)
    {
        BordaLixeira.BackgroundColor = acesa ? Color.FromArgb("#5A2E2E") : Color.FromArgb("#3A2323");
        BordaLixeira.Stroke = acesa ? Color.FromArgb("#E74C3C") : Color.FromArgb("#1E1E1E");
    }

    /// <summary>Soltar uma camada sobre outra move a arrastada pra posição da camada de destino.</summary>
    private async void OnCamadaDrop(object? sender, DropEventArgs e)
    {
        var origem = _camadaArrastada;
        _camadaArrastada = null;
        if (origem is null || sender is not DropGestureRecognizer { BindingContext: CamadaDto destino } || origem.Id == destino.Id)
            return;

        await _viewModel.ReordenarArrastandoAsync(origem, destino);
    }

    /// <summary>Mostra/esconde o painel de camadas — oculto por padrão pra deixar mais espaço pra
    /// planta; a seta no botão (▲/▼) indica o estado atual.</summary>
    private void OnAlternarCamadasClicked(object? sender, EventArgs e)
    {
        PainelCamadas.IsVisible = !PainelCamadas.IsVisible;
        BotaoAlternarCamadas.Text = PainelCamadas.IsVisible ? "▼ Camadas" : "▲ Camadas";
    }

    private async void OnAbrirHistoricoClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync($"{nameof(HistoricoPage)}?plantaId={PlantaId}");

    private async void OnAbrirEdicoesPendentesClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync($"{nameof(RevisaoEdicoesPage)}?plantaId={PlantaId}");

    /// <summary>Hidráulica/Elétrica são camadas pré-definidas com nome fixo — <see
    /// cref="PlantaViewModel.EditarCamada"/> reconhece esses nomes e trava a cor do traço
    /// automaticamente (azul/amarelo), escondendo a paleta (ver <see cref="PlantaViewModel.PermiteEscolherCor"/>).
    /// "Camada livre" é o fluxo antigo: pede o nome e deixa a cor solta.</summary>
    private async void OnNovaCamadaClicked(object? sender, EventArgs e)
    {
        var tipo = await DisplayActionSheet(
            "Nova camada", "Cancelar", null, "Hidráulica (azul)", "Elétrica (amarelo)", "Camada livre");
        if (tipo is null || tipo == "Cancelar")
            return;

        var nome = tipo switch
        {
            "Hidráulica (azul)" => PlantaViewModel.NomeCamadaHidraulica,
            "Elétrica (amarelo)" => PlantaViewModel.NomeCamadaEletrica,
            _ => await DisplayPromptAsync("Nova camada", "Nome da camada"),
        };
        if (string.IsNullOrWhiteSpace(nome))
            return;

        await _viewModel.CriarCamadaAsync(nome);

        // Criada, entra direto na edição inline dela — na própria tela de visualização, com as outras
        // camadas visíveis por baixo (o objetivo desta mudança: desenhar a nova medida se guiando
        // pelas existentes, sem abrir outra aba e sem repetir cota).
        if (_viewModel.CamadaAtiva is { } camadaCriada)
            _viewModel.EditarCamada(camadaCriada);
    }

    /// <summary>Em Android 9 (API 28) ou anterior é preciso pedir a permissão de escrita em tempo de
    /// execução; em versões mais novas (escopo de armazenamento moderno) a MediaStore não exige
    /// isso, o pedido só retorna concedido automaticamente.</summary>
    private async void OnSalvarNaGaleriaClicked(object? sender, EventArgs e)
    {
        var status = await Permissions.RequestAsync<Permissions.StorageWrite>();
        if (status != PermissionStatus.Granted)
        {
            await DisplayAlert("Salvar na galeria", "Permissão de armazenamento negada.", "OK");
            return;
        }

        await _viewModel.SalvarComposicaoNaGaleriaAsync();

        if (_viewModel.MensagemErro is null)
            await DisplayAlert("Salvar na galeria", "Planta salva na galeria (Pictures/Camdas).", "OK");
    }

    /// <summary>Gera o arquivo de projeto (.json — ver PlantaViewModel.ExportarParaArquivo) e abre o
    /// menu de compartilhar do Android, pra mandar pra outro dispositivo por qualquer app (WhatsApp,
    /// Drive, e-mail, Bluetooth...) — mesmo padrão já usado pelo relatório em PDF (ProjetosPage).</summary>
    private async void OnExportarProjetoClicked(object? sender, EventArgs e)
    {
        var dados = _viewModel.ExportarParaArquivo();
        if (dados is null)
        {
            await DisplayAlert("Exportar projeto", _viewModel.MensagemErro ?? "Não foi possível exportar.", "OK");
            return;
        }

        try
        {
            var nomeArquivo = $"{_viewModel.Planta!.Nome}.camdas.json";
            var caminho = Path.Combine(FileSystem.CacheDirectory, nomeArquivo);
            await File.WriteAllBytesAsync(caminho, dados);

            await Launcher.Default.OpenAsync(new OpenFileRequest("Projeto Camdas", new ReadOnlyFile(caminho)));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Exportar projeto", $"Não foi possível compartilhar o arquivo: {ex.Message}", "OK");
        }
    }

    // --- Ferramentas de desenho (modo de edição inline) ---

    private void OnCorClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string cor })
            _viewModel.CorTraco = cor;
    }

    private void OnLimparClicked(object? sender, EventArgs e) => Canvas.LimparCamadaAtiva();

    private void OnDesfazerClicked(object? sender, EventArgs e) => Canvas.DesfazerUltimaAcao();

    private void OnRefazerClicked(object? sender, EventArgs e) => Canvas.RefazerAcao();

    /// <summary>Salva o traço da camada ativa e volta pra visualização. Se o salvamento falhar,
    /// mantém na edição pra tentar de novo (não perde o desenho).</summary>
    private async void OnConcluirEdicaoClicked(object? sender, EventArgs e)
    {
        await _viewModel.SalvarCamadaAtivaAsync();
        if (_viewModel.MensagemErro is not null)
        {
            await DisplayAlert("Salvar camada", _viewModel.MensagemErro, "OK");
            return;
        }

        _viewModel.SairDaEdicao();
    }

    /// <summary>Alterna o "modo texto": tocar no canvas dispara <see cref="PlantaCanvasView.SolicitarTexto"/>
    /// em vez de desenhar; o botão fica destacado enquanto ativo. Ativar texto sempre desliga o pan
    /// (Canvas.ModoPan = false) — senão o toque nunca chegaria em PlantaCanvasView.OnTouch, que confere
    /// ModoPan antes de ModoTexto.</summary>
    private void OnAlternarModoTextoClicked(object? sender, EventArgs e)
    {
        Canvas.ModoTexto = !Canvas.ModoTexto;
        Canvas.ModoPan = !Canvas.ModoTexto;
        AtualizarDestaqueBotao(BotaoTexto, Canvas.ModoTexto);
        AtualizarDestaqueBotao(BotaoDesenhar, false);
    }

    /// <summary>Alterna o "modo desenhar" (lápis): ligado, o toque desenha na camada ativa; desligado
    /// (padrão ao entrar na edição), o toque ajusta a visualização — arrastar com um dedo ou pinça com
    /// dois (<see cref="OnPinchUpdated"/>). Necessário aqui porque a rolagem do ScrollView fica
    /// desligada durante a edição. Internamente é só o inverso de Canvas.ModoPan.</summary>
    private void OnAlternarModoDesenharClicked(object? sender, EventArgs e)
    {
        Canvas.ModoPan = !Canvas.ModoPan;
        var desenhoAtivo = !Canvas.ModoPan;
        AtualizarDestaqueBotao(BotaoDesenhar, desenhoAtivo);

        if (desenhoAtivo && Canvas.ModoTexto)
        {
            Canvas.ModoTexto = false;
            AtualizarDestaqueBotao(BotaoTexto, false);
        }
    }

    /// <summary>Ajusta o Zoom durante a pinça (chamado por Canvas.ZoomPorGestoSolicitado — ver
    /// AtualizarPinca em PlantaCanvasView, que reconhece o gesto de dois dedos em qualquer tela/modo,
    /// os dois agora compartilham o mesmo mecanismo de canvas fixo + PanX/PanY). Ancora no ponto médio
    /// real entre os dois dedos (recebido de Canvas.ZoomPorGestoSolicitado) — o que estiver embaixo
    /// deles continua embaixo deles conforme o zoom muda. Ao contrário de AtualizarZoom, NÃO reseta
    /// PanX/PanY a cada chamada (a pinça dispara isso várias vezes por segundo).</summary>
    private void AtualizarZoomPinca(float fatorEscala, SKPoint centro)
    {
        if (_viewModel.ImagemBase is null || Canvas.Width <= 0 || Canvas.Height <= 0)
            return;

        var zoomAntigo = Canvas.Zoom;
        var novoZoom = Math.Clamp(zoomAntigo * fatorEscala, (float)ZoomSlider.Minimum, (float)ZoomSlider.Maximum);
        if (novoZoom == zoomAntigo)
            return;

        var pontoConteudoX = (centro.X - Canvas.PanX) / zoomAntigo;
        var pontoConteudoY = (centro.Y - Canvas.PanY) / zoomAntigo;

        Canvas.PanX += pontoConteudoX * (zoomAntigo - novoZoom);
        Canvas.PanY += pontoConteudoY * (zoomAntigo - novoZoom);
        Canvas.Zoom = novoZoom;

        AtualizarSliderELabelSemDispararEvento(novoZoom);
    }

    /// <summary>Alterna a borracha (apaga em vez de pintar). Como ela desenha, desliga texto/pan pra
    /// não competirem pelo toque.</summary>
    private void OnAlternarBorrachaClicked(object? sender, EventArgs e)
    {
        _viewModel.ModoApagar = !_viewModel.ModoApagar;
        AtualizarDestaqueBorracha(_viewModel.ModoApagar);

        if (_viewModel.ModoApagar)
        {
            Canvas.ModoTexto = false;
            Canvas.ModoPan = false;
            AtualizarDestaqueBotao(BotaoTexto, false);
            AtualizarDestaqueBotao(BotaoDesenhar, false);
        }
    }

    private static void AtualizarDestaqueBotao(Button botao, bool ativo)
    {
        botao.BackgroundColor = ativo ? Color.FromArgb("#333") : Colors.Transparent;
        botao.TextColor = ativo ? Colors.White : Color.FromArgb("#333");
    }

    private void AtualizarDestaqueBorracha(bool ativo) =>
        BotaoBorracha.BackgroundColor = ativo ? Color.FromArgb("#333") : Colors.Transparent;

    /// <summary>Toque em modo texto: o texto aparece "solto" (movível) no ponto tocado — só vira
    /// traço definitivo na camada quando o usuário tocar em "✓ Confirmar" na barra de posicionamento
    /// (ver <see cref="OnConfirmarElementoPendenteClicked"/>). Pra mover/redimensionar depois de
    /// aplicado, é preciso apagar e adicionar de novo — ver decisão registrada na conversa.</summary>
    private async void OnCanvasSolicitarTexto(object? sender, SKPoint ponto)
    {
        var texto = await DisplayPromptAsync("Adicionar texto", "Escreva o texto:");

        Canvas.ModoTexto = false;
        // Volta pro padrão (ajuste/pan) — enquanto o elemento fica pendente isso não importa (o
        // toque vai todo pro arrasto de posicionamento, PlantaCanvasView.OnTouch confere elemento
        // pendente antes de tudo), mas cobre também o caso de cancelar o prompt (string vazia) sem
        // nunca chegar a mostrar a barra de posicionamento.
        Canvas.ModoPan = true;
        AtualizarDestaqueBotao(BotaoTexto, false);

        if (string.IsNullOrWhiteSpace(texto))
            return;

        Canvas.IniciarTextoPendente(texto, _viewModel.CorTraco, tamanhoFonte: Math.Max(24f, _viewModel.EspessuraTraco * 4), ponto);
        MostrarBarraPosicionamento("Arraste o texto pra posicionar");
    }

    private void MostrarBarraPosicionamento(string legenda)
    {
        LabelPosicionamento.Text = legenda;
        BarraFerramentasEdicao.IsVisible = false;
        BarraPosicionamento.IsVisible = true;
    }

    private void OcultarBarraPosicionamento()
    {
        BarraPosicionamento.IsVisible = false;
        BarraFerramentasEdicao.IsVisible = _viewModel.ModoEdicao;
    }

    private void OnRotacionarElementoPendenteClicked(object? sender, EventArgs e) => Canvas.RotacionarElementoPendente();

    private void OnAumentarTamanhoElementoPendenteClicked(object? sender, EventArgs e) => Canvas.RedimensionarElementoPendente(4f);

    private void OnDiminuirTamanhoElementoPendenteClicked(object? sender, EventArgs e) => Canvas.RedimensionarElementoPendente(-4f);

    private void OnConfirmarElementoPendenteClicked(object? sender, EventArgs e)
    {
        Canvas.ConfirmarElementoPendente();
        OcultarBarraPosicionamento();
    }

    private void OnCancelarElementoPendenteClicked(object? sender, EventArgs e)
    {
        Canvas.CancelarElementoPendente();
        OcultarBarraPosicionamento();
    }

    // --- Ferramenta de ícones ---

    private void OnAbrirMenuIconesClicked(object? sender, EventArgs e) => MenuIcones.IsVisible = true;

    private void OnFecharMenuIconesTapped(object? sender, TappedEventArgs e) => MenuIcones.IsVisible = false;

    private void OnFecharMenuIconesClicked(object? sender, EventArgs e) => MenuIcones.IsVisible = false;

    /// <summary>Escolhido um ícone no menu, carrega o SVG (cacheado depois da primeira vez — ver
    /// IconeSvgCatalogo) e começa o posicionamento, centrado no meio da área visível atual (não tem
    /// um "toque no canvas" de onde partir, diferente do texto — a escolha vem de um menu).</summary>
    private async void OnEscolherIconeClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: string nomeArquivo })
            return;

        MenuIcones.IsVisible = false;

        var picture = await _iconeCatalogo.ObterAsync(nomeArquivo);

        var centroTelaX = (float)(Canvas.Width / 2);
        var centroTelaY = (float)(Canvas.Height / 2);
        var pontoNativo = Canvas.Zoom > 0
            ? new SKPoint((centroTelaX - Canvas.PanX) / Canvas.Zoom, (centroTelaY - Canvas.PanY) / Canvas.Zoom)
            : new SKPoint(centroTelaX, centroTelaY);

        Canvas.IniciarIconePendente(picture, nomeArquivo, pontoNativo);
        MostrarBarraPosicionamento("Arraste o ícone pra posicionar");
    }

    /// <summary>Disparado quando o usuário toca (sem arrastar) num ícone já colocado nesta sessão,
    /// com a camada "Ícones" ativa e o lápis ligado (ver Canvas.TratarToqueNaCamadaIcones) — confirma
    /// antes de excluir só aquele ícone específico.</summary>
    private async void OnCanvasIconeTocado(object? sender, Guid iconeId)
    {
        var confirmar = await DisplayAlert("Excluir ícone", "Excluir esse ícone da planta?", "Excluir", "Cancelar");
        if (confirmar)
            Canvas.ExcluirIconeColocado(iconeId);
    }
}
