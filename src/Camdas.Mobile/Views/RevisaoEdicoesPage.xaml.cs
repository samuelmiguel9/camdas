using Camdas.Contracts;
using Camdas.Mobile.ViewModels;

namespace Camdas.Mobile.Views;

[QueryProperty(nameof(PlantaId), "plantaId")]
public partial class RevisaoEdicoesPage : ContentPage
{
    private readonly RevisaoEdicoesViewModel _viewModel;

    public string PlantaId { get; set; } = string.Empty;

    public RevisaoEdicoesPage(RevisaoEdicoesViewModel viewModel)
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

    private async void OnAprovarClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: EdicaoPendenteDto edicao })
            return;

        var confirmar = await DisplayAlert(
            "Aprovar edição", $"Aplicar esta mudança ({edicao.TipoOperacao}) na planta?", "Aprovar", "Cancelar");
        if (!confirmar)
            return;

        await _viewModel.AprovarAsync(edicao);
    }

    private async void OnRejeitarClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: EdicaoPendenteDto edicao })
            return;

        var motivo = await DisplayPromptAsync("Rejeitar edição", "Por que esta edição está sendo rejeitada?");
        if (string.IsNullOrWhiteSpace(motivo))
            return;

        await _viewModel.RejeitarAsync(edicao, motivo);
    }
}
