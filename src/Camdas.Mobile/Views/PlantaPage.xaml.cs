using System.ComponentModel;
using Camdas.Mobile.ViewModels;
using Camdas.Contracts;
using SkiaSharp;

namespace Camdas.Mobile.Views;

[QueryProperty(nameof(PlantaId), "plantaId")]
public partial class PlantaPage : ContentPage
{
    private readonly PlantaViewModel _viewModel;
    private bool _zoomAjustadoNoCarregamento;
    private CamadaDto? _camadaArrastada;

    public string PlantaId { get; set; } = string.Empty;

    public PlantaPage(PlantaViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;

        // Entrar/sair do modo de edição (ex.: tocar numa camada dispara SelecionarCamadaCommand no
        // XAML, sem passar pelo code-behind) precisa reconfigurar a UI de desenho — reagimos aqui.
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Canvas.SolicitarTexto += OnCanvasSolicitarTexto;

        // Ao entrar na edição, a barra de ferramentas aparece e o Grid só termina de redistribuir as
        // linhas (e portanto o tamanho real do ScrollView) depois do PropertyChanged já ter disparado
        // — reaplica o tamanho do canvas quando o layout do ScrollView de fato mudar.
        PlantaScroll.SizeChanged += (_, _) =>
        {
            if (_viewModel.ModoEdicao)
                AjustarCanvasParaViewport();
        };

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
    /// desenhar/borracha), troca a rolagem do ScrollView (desenhar dentro dele viraria rolagem) e
    /// reaplica o zoom no layout correspondente (inflar+rolar na visualização; transform interno na
    /// edição). Canvas.ModoPan entra ligado (ajuste/pinça por padrão, pedido do usuário) só ao ENTRAR
    /// na edição — ao sair, volta pra false, porque fora da edição quem manda no arrasto é o
    /// ScrollView nativo (Orientation="Both"); deixar ModoPan ligado ali interceptaria o toque antes
    /// da rolagem nativa acontecer.</summary>
    private void AplicarModoEdicaoUi()
    {
        Canvas.ModoTexto = false;
        Canvas.ModoPan = _viewModel.ModoEdicao;
        AtualizarDestaqueBotao(BotaoTexto, false);
        AtualizarDestaqueBotao(BotaoDesenhar, false);
        AtualizarDestaqueBorracha(false);

        PlantaScroll.Orientation = _viewModel.ModoEdicao ? ScrollOrientation.Neither : ScrollOrientation.Both;
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

        // Primeiro carregamento: começa com a planta inteira visível na tela (em vez do tamanho
        // nativo, que geralmente é maior que a tela e cortava a visualização — bug reportado).
        if (!_zoomAjustadoNoCarregamento && _viewModel.ImagemBase is not null)
        {
            _zoomAjustadoNoCarregamento = true;
            AjustarZoomParaTela();
        }
    }

    // O SKCanvasView aqui é inflado pra imagemBase.Width×zoom (pra rolar dentro do ScrollView), e o
    // Android aloca uma superfície bitmap desse tamanho. Acima de ~100 MB o Android recusa desenhar
    // (RecordingCanvas.throwIfCannotDraw: "trying to draw too large bitmap") e derruba o app — era o
    // que acontecia ao dar zoom alto numa planta grande. Mantemos um teto conservador de bytes e de
    // dimensão por lado, e limitamos o zoom pra nunca ultrapassá-lo (o desenho segue em resolução
    // nativa; só não dá pra ampliar a superfície visível além do que o Android aguenta).
    private const long OrcamentoBytesSuperficie = 64L * 1024 * 1024;
    private const int DimensaoMaximaSuperficie = 8000;

    private float ZoomMaximoSeguro(int largura, int altura)
    {
        var area = (double)largura * altura;
        if (area <= 0)
            return (float)ZoomSlider.Maximum;

        var porBytes = Math.Sqrt(OrcamentoBytesSuperficie / (4.0 * area));
        var porDimensao = (double)DimensaoMaximaSuperficie / Math.Max(largura, altura);
        return (float)Math.Min(porBytes, porDimensao);
    }

    /// <summary>Dá ao canvas o tamanho da área visível do ScrollView (o SKCanvasView não tem tamanho
    /// próprio quando hospedado num ScrollView — sem isto ele colapsa pra 0 e a planta não aparece).
    /// Chamado ao entrar na edição e sempre que o ScrollView mudar de tamanho (a barra de ferramentas
    /// aparecendo/sumindo redistribui as linhas do Grid).</summary>
    private void AjustarCanvasParaViewport()
    {
        if (PlantaScroll.Width > 0)
            Canvas.WidthRequest = PlantaScroll.Width;
        if (PlantaScroll.Height > 0)
            Canvas.HeightRequest = PlantaScroll.Height;
    }

    private void AtualizarZoom(float zoom)
    {
        if (_viewModel.ImagemBase is not { } imagemBase)
            return;

        Canvas.UsarResolucaoNativa = true;

        if (_viewModel.ModoEdicao)
        {
            // Edição: canvas do tamanho da área visível (o zoom só afeta o Scale/Translate internos do
            // OnPaintSurface — igual à antiga tela isolada). Um SKCanvasView dentro do ScrollView não
            // tem tamanho próprio; sem fixar o tamanho da viewport ele colapsa pra 0 e a planta some.
            // Sem inflar, a superfície nunca estoura o limite do Android, então o zoom vai até o máximo
            // do slider; o deslocamento é pelo modo pan (✋), já que a rolagem do ScrollView fica off.
            zoom = Math.Clamp(zoom, (float)ZoomSlider.Minimum, (float)ZoomSlider.Maximum);
            AjustarCanvasParaViewport();
            Canvas.PanX = 0;
            Canvas.PanY = 0;
        }
        else
        {
            // Visualização: infla o canvas (imagem×zoom) pra rolar dentro do ScrollView. Limita o zoom
            // pra a superfície não passar do teto do Android (ver ZoomMaximoSeguro).
            var zoomMaximo = Math.Min((float)ZoomSlider.Maximum, ZoomMaximoSeguro(imagemBase.Width, imagemBase.Height));
            zoom = Math.Clamp(zoom, (float)ZoomSlider.Minimum, zoomMaximo);
            Canvas.WidthRequest = imagemBase.Width * zoom;
            Canvas.HeightRequest = imagemBase.Height * zoom;
        }

        Canvas.Zoom = zoom;
        Canvas.AtualizarPreview();

        ZoomSlider.Value = zoom;
        ZoomLabel.Text = $"{(int)Math.Round(zoom * 100)}%";
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

    private void OnZoomSliderChanged(object? sender, ValueChangedEventArgs e) => AtualizarZoom((float)e.NewValue);

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

    /// <summary>Zoom por pinça (dois dedos) durante a edição — só age com ModoEdicao ligado, pra não
    /// interferir em nada fora da edição (fora dela quem cuida do zoom/rolagem é o ScrollView nativo).
    /// Canvas.EmGestoDePinca fica ligado do Started ao Completed/Canceled — enquanto isso, o toque de
    /// um dedo só (GerenciarPan) é ignorado no Canvas, senão os dois sistemas de gesto brigavam pela
    /// mesma sequência de toque (bug reportado: "solta no meio do caminho" durante a pinça).</summary>
    private void OnPinchUpdated(object? sender, PinchGestureUpdatedEventArgs e)
    {
        if (!_viewModel.ModoEdicao)
            return;

        switch (e.Status)
        {
            case GestureStatus.Started:
                Canvas.EmGestoDePinca = true;
                break;
            case GestureStatus.Running when e.Scale > 0:
                AtualizarZoomPinca((float)e.Scale, e.ScaleOrigin);
                break;
            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                Canvas.EmGestoDePinca = false;
                break;
        }
    }

    /// <summary>Ajusta o Zoom durante a pinça mantendo o ponto sob os dedos fixo na tela (ancora em
    /// e.ScaleOrigin, relativo 0..1 da área do Canvas) — ao contrário de AtualizarZoom (usado pelos
    /// botões/slider), NÃO reseta PanX/PanY: resetar a cada evento de pinça (que dispara várias vezes
    /// por segundo) fazia a visualização "saltar" de volta pra um ponto fixo a cada atualização,
    /// impedindo qualquer ajuste (bug reportado: "vai pra um ponto fixo da planta, não consigo
    /// ajustar"). A conta é a de zoom-em-torno-de-um-ponto padrão: acha o ponto do conteúdo (native)
    /// sob os dedos com o zoom antigo, e desloca o Pan pra esse mesmo ponto continuar sob os dedos com
    /// o zoom novo.</summary>
    private void AtualizarZoomPinca(float fatorEscala, Point origemRelativa)
    {
        if (_viewModel.ImagemBase is null || Canvas.Width <= 0 || Canvas.Height <= 0)
            return;

        var zoomAntigo = Canvas.Zoom;
        var novoZoom = Math.Clamp(zoomAntigo * fatorEscala, (float)ZoomSlider.Minimum, (float)ZoomSlider.Maximum);
        if (novoZoom == zoomAntigo)
            return;

        var pontoTelaX = (float)(origemRelativa.X * Canvas.Width);
        var pontoTelaY = (float)(origemRelativa.Y * Canvas.Height);
        var pontoConteudoX = (pontoTelaX - Canvas.PanX) / zoomAntigo;
        var pontoConteudoY = (pontoTelaY - Canvas.PanY) / zoomAntigo;

        Canvas.PanX += pontoConteudoX * (zoomAntigo - novoZoom);
        Canvas.PanY += pontoConteudoY * (zoomAntigo - novoZoom);
        Canvas.Zoom = novoZoom;

        ZoomSlider.Value = novoZoom;
        ZoomLabel.Text = $"{(int)Math.Round(novoZoom * 100)}%";
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
        MostrarBarraPosicionamento();
    }

    private void MostrarBarraPosicionamento()
    {
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
}
