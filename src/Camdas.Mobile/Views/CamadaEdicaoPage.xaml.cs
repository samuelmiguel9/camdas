using Camdas.Mobile.ViewModels;

namespace Camdas.Mobile.Views;

[QueryProperty(nameof(PlantaId), "plantaId")]
[QueryProperty(nameof(CamadaId), "camadaId")]
public partial class CamadaEdicaoPage : ContentPage
{
    private readonly CamadaEdicaoViewModel _viewModel;
    private bool _zoomAjustadoNoCarregamento;

    public string PlantaId { get; set; } = string.Empty;
    public string CamadaId { get; set; } = string.Empty;

    public CamadaEdicaoPage(CamadaEdicaoViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        viewModel.CamadaSalva += async (_, _) => await Shell.Current.GoToAsync("..");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (Guid.TryParse(PlantaId, out var plantaId) && Guid.TryParse(CamadaId, out var camadaId))
            await _viewModel.CarregarAsync(plantaId, camadaId);

        // Primeiro carregamento: ajusta o zoom pra caber na tela (planta costuma ser maior que o
        // aparelho) — o traço continua sendo gravado na resolução nativa da imagem base
        // independente do zoom, ver comentário em PlantaCanvasView.UsarResolucaoNativa.
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

    private void OnCorClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string cor })
            _viewModel.EscolherCorCommand.Execute(cor);
    }

    private void OnLimparClicked(object? sender, EventArgs e) => Canvas.LimparCamadaAtiva();
}
