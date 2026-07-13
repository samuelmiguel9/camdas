using System.Collections.ObjectModel;
using Camdas.Contracts;
using Camdas.Domain.Enums;
using Camdas.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Camdas.Mobile.ViewModels;

/// <summary>Lista as plantas de um projeto e permite importar uma nova.</summary>
public partial class PlantasDoProjetoViewModel(IApiClient apiClient) : BaseViewModel
{
    [ObservableProperty]
    private Guid _projetoId;

    public ObservableCollection<PlantaListItemViewModel> Plantas { get; } = [];

    public event EventHandler<PlantaDto>? PlantaSelecionada;

    public async Task CarregarAsync(Guid projetoId)
    {
        ProjetoId = projetoId;
        EstaCarregando = true;
        MensagemErro = null;
        try
        {
            var plantas = await apiClient.ListarPlantasDoProjetoAsync(projetoId);
            Plantas.Clear();
            foreach (var planta in plantas)
                Plantas.Add(new PlantaListItemViewModel(planta, apiClient));

            _ = CarregarMiniaturasAsync();
        }
        catch (Exception ex)
        {
            MensagemErro = $"Não foi possível carregar as plantas: {ex.Message}";
        }
        finally
        {
            EstaCarregando = false;
        }
    }

    /// <summary>Roda em segundo plano, fora do EstaCarregando principal — miniatura é um extra
    /// visual, não deve travar a lista enquanto baixa/compõe cada imagem.</summary>
    private async Task CarregarMiniaturasAsync()
    {
        foreach (var item in Plantas.ToList())
            await item.CarregarMiniaturaAsync();
    }

    public async Task ImportarAsync(
        string nome, string? descricao, string? nomeCliente, TipoArquivoOrigem tipo, string nomeArquivo, Stream conteudo)
    {
        EstaCarregando = true;
        MensagemErro = null;
        try
        {
            var planta = await apiClient.ImportarPlantaAsync(ProjetoId, nome, descricao, nomeCliente, tipo, nomeArquivo, conteudo);
            var item = new PlantaListItemViewModel(planta, apiClient);
            Plantas.Add(item);
            PlantaSelecionada?.Invoke(this, planta);
            _ = item.CarregarMiniaturaAsync();
        }
        catch (Exception ex)
        {
            MensagemErro = $"Não foi possível importar a planta: {ex.Message}";
        }
        finally
        {
            EstaCarregando = false;
        }
    }

    [RelayCommand]
    private void SelecionarPlanta(PlantaListItemViewModel? planta)
    {
        if (planta is not null)
            PlantaSelecionada?.Invoke(this, planta.Planta);
    }
}
