using Camdas.Mobile.ViewModels;
using Camdas.Contracts;

namespace Camdas.Mobile.Views;

[QueryProperty(nameof(PlantaId), "plantaId")]
public partial class PlantaPage : ContentPage
{
    private readonly PlantaViewModel _viewModel;
    private bool _zoomAjustadoNoCarregamento;

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

    private void AtualizarZoom(float zoom)
    {
        if (_viewModel.ImagemBase is not { } imagemBase)
            return;

        zoom = Math.Clamp(zoom, (float)ZoomSlider.Minimum, (float)ZoomSlider.Maximum);

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

    private async void OnOpacidadeSliderDragCompleted(object? sender, EventArgs e)
    {
        if (sender is not Slider slider || slider.BindingContext is not CamadaDto camada)
            return;

        await _viewModel.DefinirOpacidadeAsync(camada, slider.Value);
    }

    private async void OnAbrirHistoricoClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync($"{nameof(HistoricoPage)}?plantaId={PlantaId}");

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

}
