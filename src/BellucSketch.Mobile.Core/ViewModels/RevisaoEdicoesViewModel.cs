using System.Collections.ObjectModel;
using BellucSketch.Contracts;
using BellucSketch.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BellucSketch.Mobile.ViewModels;

/// <summary>
/// Fila de edições propostas pela Web (visibilidade/opacidade/bloqueio/ordem/exclusão de Camada)
/// aguardando aprovação do técnico. Aprovar aplica a mudança de verdade na Planta/Camada; rejeitar
/// não muda nada — o Android continua sendo o mestre em ambos os casos.
/// </summary>
public partial class RevisaoEdicoesViewModel(IApiClient apiClient) : BaseViewModel
{
    [ObservableProperty]
    private Guid _plantaId;

    public ObservableCollection<EdicaoPendenteDto> Itens { get; } = [];

    public async Task CarregarAsync(Guid plantaId)
    {
        PlantaId = plantaId;
        EstaCarregando = true;
        MensagemErro = null;
        try
        {
            var itens = await apiClient.ListarEdicoesPendentesAsync(plantaId);
            Itens.Clear();
            foreach (var item in itens)
                Itens.Add(item);
        }
        catch (Exception ex)
        {
            MensagemErro = $"Não foi possível carregar as edições pendentes: {ex.Message}";
        }
        finally
        {
            EstaCarregando = false;
        }
    }

    public async Task AprovarAsync(EdicaoPendenteDto edicao)
    {
        try
        {
            await apiClient.AprovarEdicaoCamadaAsync(PlantaId, edicao.Id);
            Itens.Remove(edicao);
        }
        catch (Exception ex)
        {
            MensagemErro = $"Não foi possível aprovar a edição: {ex.Message}";
        }
    }

    public async Task RejeitarAsync(EdicaoPendenteDto edicao, string motivo)
    {
        try
        {
            await apiClient.RejeitarEdicaoCamadaAsync(PlantaId, edicao.Id, motivo);
            Itens.Remove(edicao);
        }
        catch (Exception ex)
        {
            MensagemErro = $"Não foi possível rejeitar a edição: {ex.Message}";
        }
    }

    [RelayCommand]
    private Task RecarregarAsync() => CarregarAsync(PlantaId);
}
