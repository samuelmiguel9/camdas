using System.Collections.ObjectModel;
using BellucSketch.Contracts;
using BellucSketch.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BellucSketch.Mobile.ViewModels;

public partial class HistoricoViewModel(IApiClient apiClient) : BaseViewModel
{
    [ObservableProperty]
    private Guid _plantaId;

    public ObservableCollection<HistoricoDto> Itens { get; } = [];

    public async Task CarregarAsync(Guid plantaId)
    {
        PlantaId = plantaId;
        EstaCarregando = true;
        MensagemErro = null;
        try
        {
            var historico = await apiClient.ObterHistoricoAsync(plantaId);
            Itens.Clear();
            foreach (var item in historico)
                Itens.Add(item);
        }
        catch (Exception ex)
        {
            MensagemErro = $"Não foi possível carregar o histórico: {ex.Message}";
        }
        finally
        {
            EstaCarregando = false;
        }
    }

    [RelayCommand]
    private Task RecarregarAsync() => CarregarAsync(PlantaId);
}
