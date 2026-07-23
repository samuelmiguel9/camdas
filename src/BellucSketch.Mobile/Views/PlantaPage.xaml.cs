using System.ComponentModel;
using BellucSketch.Mobile.ViewModels;
using BellucSketch.Contracts;
using BellucSketch.Mobile.Services;
using SkiaSharp;

namespace BellucSketch.Mobile.Views;

[QueryProperty(nameof(PlantaId), "plantaId")]
public partial class PlantaPage : ContentPage
{
    private readonly PlantaViewModel _viewModel;
    private readonly IconeSvgCatalogo _iconeCatalogo;
    private readonly OcrTextoService _ocrService;
    private bool _zoomAjustadoNoCarregamento;
    private CamadaDto? _camadaArrastada;

    /// <summary>True desde o último traço/texto/ícone desenhado nesta sessão de edição até o próximo
    /// "Salvar" bem-sucedido — usado por OnVoltarDaEdicaoClicked pra só confirmar a saída quando há
    /// algo de fato não salvo.</summary>
    private bool _temAlteracoesNaoSalvas;

    public string PlantaId { get; set; } = string.Empty;

    public PlantaPage(PlantaViewModel viewModel, IconeSvgCatalogo iconeCatalogo, OcrTextoService ocrService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _iconeCatalogo = iconeCatalogo;
        _ocrService = ocrService;
        BindingContext = viewModel;

        // Entrar/sair do modo de edição (ex.: tocar numa camada dispara SelecionarCamadaCommand no
        // XAML, sem passar pelo code-behind) precisa reconfigurar a UI de desenho — reagimos aqui.
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Canvas.SolicitarTexto += OnCanvasSolicitarTexto;
        Canvas.ZoomPorGestoSolicitado += (_, dados) => AtualizarZoomPinca(dados.FatorEscala, dados.Centro);
        Canvas.IconeTocado += OnCanvasIconeTocado;
        Canvas.SelecaoCotaConcluida += OnCanvasSelecaoCotaConcluida;
        Canvas.IconeEmEdicaoIniciada += (_, _) => MostrarBarraPosicionamento("Arraste o ícone pra posicionar (ou cancele pra deixar como estava)");
        Canvas.TextoEmEdicaoIniciado += (_, _) => MostrarBarraPosicionamento("Arraste o texto pra posicionar (ou cancele pra deixar como estava)");
        Canvas.DesenhoAlterado += (_, _) => _temAlteracoesNaoSalvas = true;
        Canvas.ElementoPendenteArrastando += OnElementoPendenteArrastando;
        Canvas.ElementoPendenteSolto += OnElementoPendenteSolto;

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
        Canvas.ModoSelecaoCota = false;
        Canvas.ModoPan = true;
        AtualizarDestaqueBotao(BotaoTexto, false);
        AtualizarDestaqueBotao(BotaoDesenhar, false);
        AtualizarDestaqueBotao(BotaoCota, false);
        AtualizarDestaqueBorracha(false);
        // Reseta ao entrar (nova sessão de edição, nada desenhado ainda) e ao sair (não deve
        // "vazar" pra próxima vez que entrar em edição, nem pra fora da edição).
        _temAlteracoesNaoSalvas = false;

        // IsVisible da barra de ferramentas não é mais Binding (ver comentário no XAML) — precisa ser
        // reafirmado aqui sempre que ModoEdicao mudar, inclusive ao SAIR da edição (senão ficava presa
        // visível, sobrepondo a barra de Camadas/Salvar na galeria, depois de ter sido escondida uma
        // vez por MostrarBarraPosicionamento nesta mesma sessão da página).
        BarraFerramentasEdicao.IsVisible = _viewModel.ModoEdicao;
        BarraPosicionamento.IsVisible = false;
        MenuLinha.IsVisible = false;

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
            await DisplayAlert("Salvar na galeria", "Planta salva na galeria (Pictures/BellucSketch).", "OK");
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
            var nomeArquivo = $"{_viewModel.Planta!.Nome}.bellucsketch.json";
            var caminho = Path.Combine(FileSystem.CacheDirectory, nomeArquivo);
            await File.WriteAllBytesAsync(caminho, dados);

            await Launcher.Default.OpenAsync(new OpenFileRequest("Projeto BellucSketch", new ReadOnlyFile(caminho)));
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

    /// <summary>Salva o traço da camada ativa SEM sair da edição — separado de "Voltar" de propósito
    /// (bug reportado: o antigo botão único "Concluir" fazia os dois em sequência, e se salvar
    /// demorasse/travasse — rede ruim, servidor "dormindo" no Render — parecia que o botão tinha
    /// "bugado" e nunca mais voltava pra tela anterior). Continua na edição mesmo se der certo, pra
    /// poder salvar de novo depois de continuar desenhando.</summary>
    private async void OnSalvarCamadaClicked(object? sender, EventArgs e)
    {
        var sucesso = await _viewModel.SalvarCamadaAtivaAsync();
        if (_viewModel.MensagemErro is not null)
        {
            await DisplayAlert("Salvar camada", _viewModel.MensagemErro, "OK");
            return;
        }

        if (sucesso)
            _temAlteracoesNaoSalvas = false;
    }

    /// <summary>Sai da edição — sempre funciona, nunca espera a Api (ver comentário em
    /// OnSalvarCamadaClicked). Só confirma antes se houver traço desde o último "Salvar" (ver
    /// _temAlteracoesNaoSalvas), pra não perder desenho sem querer.</summary>
    private async void OnVoltarDaEdicaoClicked(object? sender, EventArgs e)
    {
        if (_temAlteracoesNaoSalvas)
        {
            var confirmar = await DisplayAlert(
                "Sair sem salvar?",
                "Você desenhou algo nesta camada desde o último \"Salvar\". Sair mesmo assim, perdendo essa alteração?",
                "Sair sem salvar", "Cancelar");
            if (!confirmar)
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
        Canvas.ModoSelecaoCota = false;
        Canvas.ModoPan = !Canvas.ModoTexto;
        AtualizarDestaqueBotao(BotaoTexto, Canvas.ModoTexto);
        AtualizarDestaqueBotao(BotaoDesenhar, false);
        AtualizarDestaqueBotao(BotaoCota, false);

        // Sem isto, a borracha continuava ativa por baixo (ModoApagar nunca desligava) mesmo com o
        // botão T destacado — bug reportado: "depois que seleciono a borracha ainda consigo
        // selecionar o lápis/outra ferramenta" (o Cota já fazia esse reset certo, T e Desenhar não).
        if (Canvas.ModoTexto && _viewModel.ModoApagar)
        {
            _viewModel.ModoApagar = false;
            AtualizarDestaqueBorracha(false);
        }
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

        if (desenhoAtivo)
        {
            Canvas.ModoTexto = false;
            Canvas.ModoSelecaoCota = false;
            AtualizarDestaqueBotao(BotaoTexto, false);
            AtualizarDestaqueBotao(BotaoCota, false);

            // Sem isto, a borracha continuava ativa por baixo (ModoApagar nunca desligava) mesmo com
            // o lápis destacado — bug reportado: "depois que seleciono a borracha ainda consigo
            // selecionar o lápis" (o Cota já fazia esse reset certo, Desenhar e Texto não).
            if (_viewModel.ModoApagar)
            {
                _viewModel.ModoApagar = false;
                AtualizarDestaqueBorracha(false);
            }
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
            Canvas.ModoSelecaoCota = false;
            Canvas.ModoPan = false;
            AtualizarDestaqueBotao(BotaoTexto, false);
            AtualizarDestaqueBotao(BotaoDesenhar, false);
            AtualizarDestaqueBotao(BotaoCota, false);
        }
    }

    /// <summary>Alterna a ferramenta de cota: arrastar um retângulo sobre um número/cota impresso na
    /// planta manda aquele recorte pro OCR (ver <see cref="OnCanvasSelecaoCotaConcluida"/>). Desliga
    /// texto/desenho/borracha, igual às outras ferramentas exclusivas entre si.</summary>
    private void OnAlternarModoSelecaoCotaClicked(object? sender, EventArgs e)
    {
        Canvas.ModoSelecaoCota = !Canvas.ModoSelecaoCota;
        Canvas.ModoPan = !Canvas.ModoSelecaoCota;
        AtualizarDestaqueBotao(BotaoCota, Canvas.ModoSelecaoCota);

        if (Canvas.ModoSelecaoCota)
        {
            Canvas.ModoTexto = false;
            AtualizarDestaqueBotao(BotaoTexto, false);
            AtualizarDestaqueBotao(BotaoDesenhar, false);
            if (_viewModel.ModoApagar)
            {
                _viewModel.ModoApagar = false;
                AtualizarDestaqueBorracha(false);
            }
        }
    }

    /// <summary>Altura mínima (pixels nativos) do recorte mandado pro OCR — ver <see
    /// cref="PrepararRecorteParaOcr"/>.</summary>
    private const int AlturaMinimaRecorteOcr = 80;

    /// <summary>ML Kit reconhece mal recortes muito pequenos em pixels nativos — e, contra a intuição,
    /// dar MAIS zoom pra enxergar melhor um número pequeno tende a produzir um recorte nativo MENOR
    /// pro mesmo tamanho visual na tela (o zoom só amplia a exibição — canvas.Scale — não cria pixel
    /// novo nenhum; um arrasto de X pixels de TELA cobre X/Zoom pixels NATIVOS, que encolhe conforme o
    /// zoom sobe). Bug reportado: "quando fica muito perto... a lupa não identifica o texto". Em vez de
    /// pedir pro usuário calibrar o zoom certo, ampliamos o recorte aqui (reamostragem de alta
    /// qualidade) pra uma altura mínima antes do OCR — não inventa detalhe que não existia, mas entrega
    /// ao reconhecedor um tamanho de imagem mais parecido com o que ele foi treinado pra ler.</summary>
    private static SKBitmap PrepararRecorteParaOcr(SKBitmap recorte)
    {
        if (recorte.Height >= AlturaMinimaRecorteOcr)
            return recorte.Copy();

        var fator = (float)AlturaMinimaRecorteOcr / recorte.Height;
        var novaLargura = Math.Max(1, (int)MathF.Round(recorte.Width * fator));
        var ampliado = recorte.Resize(
            new SKImageInfo(novaLargura, AlturaMinimaRecorteOcr, recorte.ColorType, recorte.AlphaType),
            SKFilterQuality.High);
        return ampliado ?? recorte.Copy();
    }

    /// <summary>Recorta a área selecionada, manda pro OCR e mostra o texto reconhecido pra conferir/
    /// editar antes de aplicar — ao confirmar, cobre o número original (cor de fundo detectada, ver
    /// PlantaCanvasView.DetectarCorDeFundo) e escreve o texto no lugar dele, pra parecer uma EDIÇÃO de
    /// verdade da cota (não só um texto novo por cima do antigo, bug reportado). A cobertura/posição/
    /// tamanho do texto novo usam a área EXATA da tinta detectada dentro da seleção (ver
    /// PlantaCanvasView.DetectarAreaTexto), não o retângulo inteiro arrastado (que costuma ter folga ao
    /// redor pra facilitar o toque, e pode conter traço de cota vizinho que não deve ser apagado —
    /// pedido do usuário: "o texto ficar mais ajustado a posição do texto anterior" / "o retângulo
    /// pode mudar para exatamente o texto encontrado"). A cobertura em si é uma máscara, não um
    /// retângulo sólido (ver PlantaCanvasView.CriarMascaraCobertura): pixels vermelhos (linha de cota)
    /// dentro da área ficam de fora, preservados — pedido do usuário: "pixels vermelhos você não tira,
    /// pois fazem parte da linha de cota". Fonte encolhe se necessário pra não estourar a largura da
    /// área detectada. Cor do texto: preta se o usuário aceitou exatamente o que o OCR leu, vermelha se
    /// editou o valor — sinaliza visualmente na planta que aquela cota foi corrigida manualmente.
    /// Continua reaproveitando o mecanismo de posicionar do texto manual (arrastar/girar/A+/A-/
    /// confirmar/cancelar).</summary>
    private async void OnCanvasSelecaoCotaConcluida(object? sender, SKRect retangulo)
    {
        Canvas.ModoSelecaoCota = false;
        Canvas.ModoPan = true;
        AtualizarDestaqueBotao(BotaoCota, false);

        using var recorte = Canvas.RecortarComposicaoNativa(retangulo);
        if (recorte is null)
            return;

        var corFundo = PlantaCanvasView.DetectarCorDeFundo(recorte);
        var areaTextoLocal = PlantaCanvasView.DetectarAreaTexto(recorte, corFundo);
        var areaTextoLocalInt = SKRectI.Round(areaTextoLocal);
        var areaCobertura = new SKRect(
            retangulo.Left + areaTextoLocalInt.Left, retangulo.Top + areaTextoLocalInt.Top,
            retangulo.Left + areaTextoLocalInt.Right, retangulo.Top + areaTextoLocalInt.Bottom);
        // Pixels vermelhos (linha de cota) dentro dessa área ficam de fora da cobertura — ver
        // PlantaCanvasView.CriarMascaraCobertura (pedido do usuário: "pixels vermelhos você não tira,
        // pois fazem parte da linha de cota").
        var mascaraCobertura = PlantaCanvasView.CriarMascaraCobertura(recorte, areaTextoLocalInt, corFundo);

        string textoReconhecido;
        try
        {
            using var recorteParaOcr = PrepararRecorteParaOcr(recorte);
            textoReconhecido = await _ocrService.ReconhecerAsync(recorteParaOcr);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Cota", $"Não foi possível reconhecer o texto: {ex.Message}", "OK");
            return;
        }

        var textoConfirmado = await DisplayPromptAsync(
            "Cota reconhecida", "Confira ou edite o texto reconhecido:", initialValue: textoReconhecido,
            accept: "Adicionar", cancel: "Cancelar");
        if (string.IsNullOrWhiteSpace(textoConfirmado))
            return;

        var corTexto = string.Equals(textoConfirmado.Trim(), textoReconhecido.Trim(), StringComparison.Ordinal)
            ? "#000000"
            : "#FF0000";

        // Parte da altura da área de tinta detectada (aproxima o corpo do texto original) e encolhe se
        // a largura resultante estourar a largura dessa mesma área — sem isto, um texto editado pra
        // ficar mais comprido que o número original vazava pra fora da área coberta.
        var tamanhoFontePorAltura = Math.Max(12f, areaCobertura.Height * 0.85f);
        using var fonteTeste = new SKFont(SKTypeface.Default, tamanhoFontePorAltura);
        var largura = fonteTeste.MeasureText(textoConfirmado);
        var tamanhoFonte = largura > areaCobertura.Width && largura > 0
            ? Math.Max(8f, tamanhoFontePorAltura * (areaCobertura.Width / largura))
            : tamanhoFontePorAltura;

        var posicaoInicial = new SKPoint(areaCobertura.Left, areaCobertura.Bottom);
        Canvas.IniciarTextoPendente(textoConfirmado, corTexto, tamanhoFonte, posicaoInicial, areaCobertura: areaCobertura, mascaraCobertura: mascaraCobertura);
        MostrarBarraPosicionamento("Arraste a cota pra posicionar (ou cancele)");
    }

    /// <summary>Destaque suave (fundo azul bem claro + texto/borda azuis) no lugar do antigo quadrado
    /// preto sólido — pedido do usuário: "esse menu de ferramentas está muito bruto... deixe menos
    /// preto ou um efeito melhor". Mesmo azul (#2E86DE) já usado em "Salvar"/paleta de cores, pra
    /// combinar com o resto da UI em vez de introduzir uma cor nova.</summary>
    private static readonly Color CorFundoFerramentaAtiva = Color.FromArgb("#DCEBFB");
    private static readonly Color CorTextoFerramentaAtiva = Color.FromArgb("#2E86DE");
    private static readonly Color CorTextoFerramentaInativa = Color.FromArgb("#333333");

    private static void AtualizarDestaqueBotao(Button botao, bool ativo)
    {
        botao.BackgroundColor = ativo ? CorFundoFerramentaAtiva : Colors.Transparent;
        botao.TextColor = ativo ? CorTextoFerramentaAtiva : CorTextoFerramentaInativa;
        botao.BorderColor = ativo ? CorTextoFerramentaAtiva : Colors.Transparent;
        botao.BorderWidth = ativo ? 1.5 : 0;
    }

    private void AtualizarDestaqueBorracha(bool ativo)
    {
        BotaoBorracha.BackgroundColor = ativo ? CorFundoFerramentaAtiva : Colors.Transparent;
        BotaoBorracha.BorderColor = ativo ? CorTextoFerramentaAtiva : Colors.Transparent;
        BotaoBorracha.BorderWidth = ativo ? 1.5 : 0;
    }

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
        LixeiraElementoPendente.IsVisible = true;
    }

    private void OcultarBarraPosicionamento()
    {
        BarraPosicionamento.IsVisible = false;
        LixeiraElementoPendente.IsVisible = false;
        AcenderLixeiraElemento(false);
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

    private bool _elementoPendenteSobreLixeira;

    /// <summary>Acompanha o dedo em tempo real (coordenadas de tela) pra saber se o elemento pendente
    /// (texto/ícone sendo posicionado) está sobre a lixeira — acende/apaga visualmente conforme entra/
    /// sai, mesmo princípio do LixeiraDragListener nativo usado pela lixeira de camadas, só que
    /// calculado manualmente aqui porque este arrasto é toque bruto dentro do SKCanvasView, não um
    /// drag-and-drop nativo do Android (não dá pra reaproveitar IOnDragListener).</summary>
    private void OnElementoPendenteArrastando(object? sender, SKPoint pontoTela)
    {
        var sobreLixeira = PontoDeTelaEstaSobreLixeiraElemento(pontoTela);
        if (sobreLixeira == _elementoPendenteSobreLixeira)
            return;

        _elementoPendenteSobreLixeira = sobreLixeira;
        AcenderLixeiraElemento(sobreLixeira);
    }

    /// <summary>Ao soltar o dedo, se estava sobre a lixeira, exclui em vez de deixar posicionado
    /// aguardando Confirmar/Cancelar — pedido do usuário: "deixe que eu possa arrastar o texto ou
    /// ícones também para a lixeira".</summary>
    private void OnElementoPendenteSolto(object? sender, EventArgs e)
    {
        if (_elementoPendenteSobreLixeira)
        {
            Canvas.ExcluirElementoPendente();
            OcultarBarraPosicionamento();
        }

        AcenderLixeiraElemento(false);
        _elementoPendenteSobreLixeira = false;
    }

    private void AcenderLixeiraElemento(bool acesa)
    {
        LixeiraElementoPendente.BackgroundColor = acesa ? Color.FromArgb("#7A3A3A") : Color.FromArgb("#3A2323");
        LixeiraElementoPendente.Stroke = acesa ? Color.FromArgb("#E74C3C") : Color.FromArgb("#1E1E1E");
        LixeiraElementoPendente.Scale = acesa ? 1.15 : 1.0;
    }

    /// <summary>Testa se um ponto de toque (mesma unidade de <see cref="SKTouchEventArgs.Location"/> —
    /// pixels de tela, ver comentário em PlantaCanvasView.OnTouch sobre "0..CanvasSize") cai dentro da
    /// lixeira flutuante — usa geometria nativa do Android (GetLocationOnScreen) porque o Canvas (
    /// dentro de um ScrollView) e a lixeira (flutuando por cima do Grid) não compartilham um sistema
    /// de coordenadas MAUI simples o bastante pra comparar direto (Bounds/Frame não refletem o
    /// deslocamento de rolagem). GetLocationOnScreen já devolve pixels físicos, igual ao touch —
    /// nenhuma conversão de densidade é necessária.</summary>
    private bool PontoDeTelaEstaSobreLixeiraElemento(SKPoint pontoTelaCanvas)
    {
        if (!LixeiraElementoPendente.IsVisible)
            return false;
        if (Canvas.Handler?.PlatformView is not Android.Views.View viewCanvas)
            return false;
        if (LixeiraElementoPendente.Handler?.PlatformView is not Android.Views.View viewLixeira)
            return false;

        var origemCanvas = new int[2];
        viewCanvas.GetLocationOnScreen(origemCanvas);
        var origemLixeira = new int[2];
        viewLixeira.GetLocationOnScreen(origemLixeira);

        var pontoAbsolutoX = origemCanvas[0] + pontoTelaCanvas.X;
        var pontoAbsolutoY = origemCanvas[1] + pontoTelaCanvas.Y;

        return pontoAbsolutoX >= origemLixeira[0] && pontoAbsolutoX <= origemLixeira[0] + viewLixeira.Width
            && pontoAbsolutoY >= origemLixeira[1] && pontoAbsolutoY <= origemLixeira[1] + viewLixeira.Height;
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

    // --- Ferramenta de lápis: estilo do traço (reta contínua/pontilhada/tracejada/livre) ---

    private void OnAbrirMenuLinhaClicked(object? sender, EventArgs e) => MenuLinha.IsVisible = true;

    private void OnFecharMenuLinhaTapped(object? sender, TappedEventArgs e) => MenuLinha.IsVisible = false;

    private void OnFecharMenuLinhaClicked(object? sender, EventArgs e) => MenuLinha.IsVisible = false;

    /// <summary>Escolher um estilo no menu já liga o modo de desenhar com esse estilo (mesmo efeito de
    /// tocar o lápis), desligando as outras ferramentas exclusivas — evita ter que tocar o lápis de
    /// novo depois de escolher no menu.</summary>
    private void OnEscolherEstiloLinhaClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: string estiloTexto } || !Enum.TryParse<EstiloTraco>(estiloTexto, out var estilo))
            return;

        MenuLinha.IsVisible = false;
        Canvas.EstiloTraco = estilo;
        Canvas.ModoPan = false;
        Canvas.ModoTexto = false;
        Canvas.ModoSelecaoCota = false;
        AtualizarDestaqueBotao(BotaoDesenhar, true);
        AtualizarDestaqueBotao(BotaoTexto, false);
        AtualizarDestaqueBotao(BotaoCota, false);

        if (_viewModel.ModoApagar)
        {
            _viewModel.ModoApagar = false;
            AtualizarDestaqueBorracha(false);
        }
    }
}
