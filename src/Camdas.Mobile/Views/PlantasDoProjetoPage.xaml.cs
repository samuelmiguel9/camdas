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
        var origem = await DisplayActionSheet(
            "Nova planta — de onde?", "Cancelar", null,
            "Câmera", "Galeria", "Arquivo (PDF ou imagem)", "Projeto exportado (.json)");
        if (origem is null || origem == "Cancelar")
            return;

        if (origem == "Projeto exportado (.json)")
        {
            await ImportarArquivoDeProjetoAsync();
            return;
        }

        FileResult? arquivo = origem switch
        {
            "Câmera" => await CapturarFotoAsync(),
            "Galeria" => await MediaPicker.Default.PickPhotoAsync(),
            _ => await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Selecione o PDF ou imagem da planta",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    [DevicePlatform.Android] = ["application/pdf", "image/png", "image/jpeg"],
                }),
            }),
        };

        if (arquivo is null)
            return;

        var nome = await DisplayPromptAsync("Nova planta", "Nome da planta");
        if (string.IsNullOrWhiteSpace(nome))
            return;

        var descricao = await DisplayPromptAsync("Nova planta", "Breve descrição (opcional)");
        var nomeCliente = await DisplayPromptAsync("Nova planta", "Nome do cliente (opcional)");

        var tipo = arquivo.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            ? TipoArquivoOrigem.Pdf
            : TipoArquivoOrigem.Imagem;

        await using var conteudo = await arquivo.OpenReadAsync();
        await _viewModel.ImportarAsync(nome, descricao, nomeCliente, tipo, arquivo.FileName, conteudo);
    }

    /// <summary>Abre um arquivo .json exportado por <see cref="PlantaPage.OnExportarProjetoClicked"/>
    /// (em outro dispositivo ou neste mesmo) e recria a planta com todas as camadas — ver
    /// PlantasDoProjetoViewModel.ImportarArquivoDeProjetoAsync.</summary>
    private async Task ImportarArquivoDeProjetoAsync()
    {
        var arquivo = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Selecione o arquivo de projeto (.json)",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                // Compartilhado por outro app (WhatsApp, Drive, e-mail...), o .json costuma chegar
                // marcado como application/json — mas alguns apps retransmitem como octet-stream
                // genérico; aceitamos os dois pra não travar na hora de selecionar.
                [DevicePlatform.Android] = ["application/json", "application/octet-stream"],
            }),
        });

        if (arquivo is null)
            return;

        await using var conteudo = await arquivo.OpenReadAsync();
        using var memoria = new MemoryStream();
        await conteudo.CopyToAsync(memoria);

        await _viewModel.ImportarArquivoDeProjetoAsync(memoria.ToArray());
    }

    /// <summary>Câmera pode não existir/estar disponível (emulador sem câmera virtual, por
    /// exemplo) — <see cref="MediaPicker.IsCaptureSupported"/> evita uma exceção nesse caso.</summary>
    private async Task<FileResult?> CapturarFotoAsync()
    {
        if (!MediaPicker.Default.IsCaptureSupported)
        {
            await DisplayAlert("Câmera", "Este aparelho não tem câmera disponível.", "OK");
            return null;
        }

        return await MediaPicker.Default.CapturePhotoAsync();
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
