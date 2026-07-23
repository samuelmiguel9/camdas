using BellucSketch.Mobile.ViewModels;

namespace BellucSketch.Mobile.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.LoginRealizado += async (_, _) => await Shell.Current.GoToAsync(nameof(ProjetosPage));
    }
}
