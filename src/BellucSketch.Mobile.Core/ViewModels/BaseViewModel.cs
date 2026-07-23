using CommunityToolkit.Mvvm.ComponentModel;

namespace BellucSketch.Mobile.ViewModels;

public abstract partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _estaCarregando;

    [ObservableProperty]
    private string? _mensagemErro;
}
