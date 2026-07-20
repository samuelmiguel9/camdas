using Camdas.Mobile.ViewModels;
using Camdas.Contracts;

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
        viewModel.CamadaSelecionadaParaEdicao += async (_, camada) =>
            await Shell.Current.GoToAsync($"{nameof(CamadaEdicaoPage)}?plantaId={PlantaId}&camadaId={camada.Id}");
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

    private void AtualizarZoom(float zoom)
    {
        if (_viewModel.ImagemBase is not { } imagemBase)
            return;

        var zoomMaximo = Math.Min((float)ZoomSlider.Maximum, ZoomMaximoSeguro(imagemBase.Width, imagemBase.Height));
        zoom = Math.Clamp(zoom, (float)ZoomSlider.Minimum, zoomMaximo);

        Canvas.UsarResolucaoNativa = true;
        Canvas.Zoom = zoom;
        Canvas.WidthRequest = imagemBase.Width * zoom;
        Canvas.HeightRequest = imagemBase.Height * zoom;
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

    /// <summary>Guarda a camada da linha que começou a ser arrastada (a recognizer herda o
    /// BindingContext do item da CollectionView, igual ao slider de opacidade acima).</summary>
    private void OnCamadaDragStarting(object? sender, DragStartingEventArgs e)
    {
        if (sender is DragGestureRecognizer { BindingContext: CamadaDto camada })
            _camadaArrastada = camada;
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

    /// <summary>Soltar uma camada na lixeira ao lado da lista exclui ela (com confirmação, mesmo
    /// texto do botão "Excluir camada" do menu de opções).</summary>
    private async void OnCamadaDropNaLixeira(object? sender, DropEventArgs e)
    {
        var camada = _camadaArrastada;
        _camadaArrastada = null;
        if (camada is null)
            return;

        var confirmar = await DisplayAlert(
            "Excluir camada", $"Excluir a camada '{camada.Nome}'? O traço dela some, sem volta.", "Excluir", "Cancelar");
        if (!confirmar)
            return;

        await _viewModel.RemoverCamadaAsync(camada);
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

    private async void OnNovaCamadaClicked(object? sender, EventArgs e)
    {
        var nome = await DisplayPromptAsync("Nova camada", "Nome da camada");
        if (string.IsNullOrWhiteSpace(nome))
            return;

        await _viewModel.CriarCamadaAsync(nome);

        // Criada, entra direto na edição isolada dela (sem as outras camadas aparecendo).
        var camadaCriada = _viewModel.CamadaAtiva;
        if (camadaCriada is not null)
            await Shell.Current.GoToAsync($"{nameof(CamadaEdicaoPage)}?plantaId={PlantaId}&camadaId={camadaCriada.Id}");
    }

    /// <summary>Cria uma camada nova a partir de uma foto/imagem (galeria ou câmera), em vez de
    /// desenhar à mão — ver PlantaViewModel.AdicionarImagemComoCamadaAsync.</summary>
    private async void OnAdicionarImagemClicked(object? sender, EventArgs e)
    {
        var origem = await DisplayActionSheet("Adicionar imagem — de onde?", "Cancelar", null, "Câmera", "Galeria");
        if (origem is null || origem == "Cancelar")
            return;

        FileResult? arquivo = origem == "Câmera"
            ? await CapturarFotoAsync()
            : await MediaPicker.Default.PickPhotoAsync();

        if (arquivo is null)
            return;

        var nome = await DisplayPromptAsync("Nova camada", "Nome da camada", initialValue: "Imagem");
        if (string.IsNullOrWhiteSpace(nome))
            return;

        using var conteudo = await arquivo.OpenReadAsync();
        await _viewModel.AdicionarImagemComoCamadaAsync(nome, conteudo);
        Canvas.AtualizarPreview();
    }

    private async Task<FileResult?> CapturarFotoAsync()
    {
        if (!MediaPicker.Default.IsCaptureSupported)
        {
            await DisplayAlert("Câmera", "Este aparelho não tem câmera disponível.", "OK");
            return null;
        }

        return await MediaPicker.Default.CapturePhotoAsync();
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
}
