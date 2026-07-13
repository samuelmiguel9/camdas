using CommunityToolkit.Mvvm.ComponentModel;

namespace Camdas.Mobile.ViewModels;

public abstract partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _estaCarregando;

    [ObservableProperty]
    private string? _mensagemErro;
}
