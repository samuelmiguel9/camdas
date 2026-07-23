using BellucSketch.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BellucSketch.Mobile.ViewModels;

/// <summary>
/// Login por Id de usuário (sem senha) — espelha o endpoint provisório <c>POST /api/auth/dev-token</c>
/// da Api (ver RELATORIO.md, Fase 4: ainda não existe login por credencial no backend).
/// </summary>
public partial class LoginViewModel(IApiClient apiClient, ITokenStore tokenStore) : BaseViewModel
{
    [ObservableProperty]
    private string _usuarioId = string.Empty;

    public event EventHandler? LoginRealizado;

    [RelayCommand]
    private async Task EntrarAsync()
    {
        if (!Guid.TryParse(UsuarioId, out var id))
        {
            MensagemErro = "Informe um Id de usuário válido (Guid).";
            return;
        }

        EstaCarregando = true;
        MensagemErro = null;
        try
        {
            var token = await apiClient.LoginDevAsync(id);
            await tokenStore.SalvarTokenAsync(token);
            LoginRealizado?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            MensagemErro = $"Não foi possível entrar: {ex.Message}";
        }
        finally
        {
            EstaCarregando = false;
        }
    }
}
