using Camdas.Contracts;
using Camdas.Mobile.Relatorios;
using Camdas.Mobile.ViewModels;

namespace Camdas.Mobile.Views;

public partial class ProjetosPage : ContentPage
{
    private readonly ProjetosViewModel _viewModel;

    public ProjetosPage(ProjetosViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        viewModel.ProjetoSelecionado += async (_, projeto) =>
            await Shell.Current.GoToAsync($"{nameof(PlantasDoProjetoPage)}?projetoId={projeto.Id}");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.CarregarCommand.ExecuteAsync(null);
    }

    private async void OnEditarProjetoClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: ProjetoDto projeto })
            return;

        var novoNome = await DisplayPromptAsync("Editar projeto", "Novo nome", initialValue: projeto.Nome);
        if (string.IsNullOrWhiteSpace(novoNome))
            return;

        await _viewModel.RenomearAsync(projeto, novoNome);
    }

    private async void OnExcluirProjetoClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: ProjetoDto projeto })
            return;

        var confirmar = await DisplayAlert(
            "Excluir projeto", $"Excluir '{projeto.Nome}'? Isso apaga também todas as plantas dele, sem volta.", "Excluir", "Cancelar");
        if (!confirmar)
            return;

        await _viewModel.RemoverAsync(projeto);
    }

    /// <summary>Abre a página do release no navegador do aparelho — o app não baixa/instala a
    /// atualização sozinho, só leva o usuário até onde o `.apk` novo está disponível.</summary>
    private async void OnBaixarAtualizacaoClicked(object? sender, EventArgs e)
    {
        if (_viewModel.UrlAtualizacao is not { } url)
            return;

        try
        {
            await Launcher.Default.OpenAsync(new Uri(url));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Atualização", $"Não foi possível abrir o link: {ex.Message}", "OK");
        }
    }

    /// <summary>Gera o changelog versionado em PDF e abre no visualizador padrão do aparelho — o app
    /// não embute um leitor de PDF próprio, só entrega o arquivo pronto pro Android abrir.</summary>
    private async void OnAbrirRelatorioClicked(object? sender, EventArgs e)
    {
        try
        {
            var bytes = RelatorioPdfService.Gerar();
            var caminho = Path.Combine(FileSystem.CacheDirectory, "camdas-relatorio.pdf");
            await File.WriteAllBytesAsync(caminho, bytes);

            await Launcher.Default.OpenAsync(new OpenFileRequest("Relatório Camdas", new ReadOnlyFile(caminho)));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Relatório", $"Não foi possível abrir o relatório: {ex.Message}", "OK");
        }
    }
}
