using Camdas.Mobile.Services;
using Camdas.Mobile.ViewModels;

namespace Camdas.Mobile.Views;

public partial class LoginPage : ContentPage
{
    private readonly ResolvedorEnderecoApi _resolvedor;
    private bool _resolucaoExecutada;

    public LoginPage(LoginViewModel viewModel, ResolvedorEnderecoApi resolvedor)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _resolvedor = resolvedor;
        viewModel.LoginRealizado += async (_, _) => await Shell.Current.GoToAsync(nameof(ProjetosPage));
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_resolucaoExecutada)
            return;
        _resolucaoExecutada = true;

        if (await _resolvedor.ResolverAsync())
            return;

        var ip = await DisplayPromptAsync(
            "Servidor não encontrado",
            "Nenhum endereço salvo respondeu nesta rede. Informe o IP (e porta) do servidor:",
            placeholder: "192.168.0.50:5080");
        if (string.IsNullOrWhiteSpace(ip))
            return;

        var nome = await DisplayPromptAsync(
            "Nome do endereço",
            "Como quer chamar esse endereço, para reconhecer depois? (ex.: Casa, Trabalho, Cliente X)",
            placeholder: "Trabalho");
        if (string.IsNullOrWhiteSpace(nome))
            nome = ip;

        await _resolvedor.ConfigurarNovoEnderecoAsync(nome, ip);
    }
}
