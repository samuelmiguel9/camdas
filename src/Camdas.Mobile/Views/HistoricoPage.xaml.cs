using Camdas.Mobile.ViewModels;

namespace Camdas.Mobile.Views;

[QueryProperty(nameof(PlantaId), "plantaId")]
public partial class HistoricoPage : ContentPage
{
    private readonly HistoricoViewModel _viewModel;

    public string PlantaId { get; set; } = string.Empty;

    public HistoricoPage(HistoricoViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (Guid.TryParse(PlantaId, out var plantaId))
            await _viewModel.CarregarAsync(plantaId);
    }
}
