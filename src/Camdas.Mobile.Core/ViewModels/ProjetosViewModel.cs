using System.Collections.ObjectModel;
using Camdas.Contracts;
using Camdas.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Camdas.Mobile.ViewModels;

public partial class ProjetosViewModel(IApiClient apiClient, IVerificadorAtualizacao verificadorAtualizacao) : BaseViewModel
{
    public ObservableCollection<ProjetoDto> Projetos { get; } = [];

    [ObservableProperty]
    private string _novoProjetoNome = string.Empty;

    /// <summary>Null enquanto não há atualização — a Page só mostra o aviso quando isto (e
    /// <see cref="UrlAtualizacao"/>) estiverem preenchidos.</summary>
    [ObservableProperty]
    private string? _mensagemAtualizacao;

    [ObservableProperty]
    private string? _urlAtualizacao;

    /// <summary>A navegação em si (Shell.Current.GoToAsync) fica no code-behind da Page — este
    /// ViewModel não conhece MAUI, só avisa "o usuário escolheu este projeto".</summary>
    public event EventHandler<ProjetoDto>? ProjetoSelecionado;

    [RelayCommand]
    private async Task CarregarAsync()
    {
        EstaCarregando = true;
        MensagemErro = null;
        try
        {
            var projetos = await apiClient.ListarProjetosAsync();
            Projetos.Clear();
            foreach (var projeto in projetos)
                Projetos.Add(projeto);
        }
        catch (Exception ex)
        {
            MensagemErro = $"Não foi possível carregar os projetos: {ex.Message}";
        }
        finally
        {
            EstaCarregando = false;
        }

        var atualizacao = await verificadorAtualizacao.VerificarAsync();
        if (atualizacao is not null)
        {
            MensagemAtualizacao = $"Nova versão disponível: {atualizacao.Versao}";
            UrlAtualizacao = atualizacao.UrlDownload;
        }
    }

    [RelayCommand]
    private async Task CriarProjetoAsync()
    {
        if (string.IsNullOrWhiteSpace(NovoProjetoNome))
            return;

        EstaCarregando = true;
        MensagemErro = null;
        try
        {
            var projeto = await apiClient.CriarProjetoAsync(new CriarProjetoRequest(NovoProjetoNome, null));
            Projetos.Add(projeto);
            NovoProjetoNome = string.Empty;
        }
        catch (Exception ex)
        {
            MensagemErro = $"Não foi possível criar o projeto: {ex.Message}";
        }
        finally
        {
            EstaCarregando = false;
        }
    }

    [RelayCommand]
    private void AbrirProjeto(ProjetoDto? projeto)
    {
        if (projeto is not null)
            ProjetoSelecionado?.Invoke(this, projeto);
    }

    public async Task RenomearAsync(ProjetoDto projeto, string novoNome)
    {
        EstaCarregando = true;
        MensagemErro = null;
        try
        {
            var atualizado = await apiClient.RenomearProjetoAsync(projeto.Id, novoNome);
            var indice = Projetos.IndexOf(projeto);
            if (indice >= 0)
                Projetos[indice] = atualizado;
        }
        catch (Exception ex)
        {
            MensagemErro = $"Não foi possível renomear o projeto: {ex.Message}";
        }
        finally
        {
            EstaCarregando = false;
        }
    }

    public async Task RemoverAsync(ProjetoDto projeto)
    {
        EstaCarregando = true;
        MensagemErro = null;
        try
        {
            await apiClient.RemoverProjetoAsync(projeto.Id);
            Projetos.Remove(projeto);
        }
        catch (Exception ex)
        {
            MensagemErro = $"Não foi possível excluir o projeto: {ex.Message}";
        }
        finally
        {
            EstaCarregando = false;
        }
    }
}
