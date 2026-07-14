using Camdas.Domain.Enums;
using Camdas.Mobile.ViewModels;

namespace Camdas.Mobile.Views;

[QueryProperty(nameof(ProjetoId), "projetoId")]
public partial class PlantasDoProjetoPage : ContentPage
{
    private readonly PlantasDoProjetoViewModel _viewModel;

    public string ProjetoId { get; set; } = string.Empty;

    public PlantasDoProjetoPage(PlantasDoProjetoViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        viewModel.PlantaSelecionada += async (_, planta) =>
            await Shell.Current.GoToAsync($"{nameof(PlantaPage)}?plantaId={planta.Id}");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (Guid.TryParse(ProjetoId, out var projetoId))
            await _viewModel.CarregarAsync(projetoId);
    }

    private async void OnImportarClicked(object? sender, EventArgs e)
    {
        var nome = await DisplayPromptAsync("Nova planta", "Nome da planta");
        if (string.IsNullOrWhiteSpace(nome))
            return;

        var descricao = await DisplayPromptAsync("Nova planta", "Breve descrição (opcional)");
        var nomeCliente = await DisplayPromptAsync("Nova planta", "Nome do cliente (opcional)");

        var arquivo = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Selecione o PDF ou imagem da planta",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                [DevicePlatform.Android] = ["application/pdf", "image/png", "image/jpeg"],
            }),
        });

        if (arquivo is null)
            return;

        var tipo = arquivo.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            ? TipoArquivoOrigem.Pdf
            : TipoArquivoOrigem.Imagem;

        await using var conteudo = await arquivo.OpenReadAsync();
        await _viewModel.ImportarAsync(nome, descricao, nomeCliente, tipo, arquivo.FileName, conteudo);
    }

    private async void OnExcluirPlantaClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: PlantaListItemViewModel item })
            return;

        var confirmar = await DisplayAlert(
            "Excluir planta", $"Excluir '{item.Planta.Nome}'? Isso apaga também todas as camadas dela, sem volta.", "Excluir", "Cancelar");
        if (!confirmar)
            return;

        await _viewModel.RemoverAsync(item);
    }
}
