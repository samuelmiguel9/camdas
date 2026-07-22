using Camdas.Mobile.ViewModels;

namespace Camdas.Mobile.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.LoginRealizado += async (_, _) => await Shell.Current.GoToAsync(nameof(ProjetosPage));
    }
}
