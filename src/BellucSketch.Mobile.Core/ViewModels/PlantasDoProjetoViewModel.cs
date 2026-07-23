using System.Collections.ObjectModel;
using BellucSketch.Contracts;
using BellucSketch.Domain.Enums;
using BellucSketch.Mobile.Exportacao;
using BellucSketch.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BellucSketch.Mobile.ViewModels;

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

    /// <summary>Importa um arquivo de projeto exportado por outro dispositivo (ver
    /// PlantaViewModel.ExportarParaArquivo/ArquivoPlantaService) — recria a planta com a imagem base
    /// e todas as camadas (traço, ordem, opacidade, visibilidade, bloqueios), do zero, usando os
    /// mesmos endpoints que criar uma planta/camada manualmente (sem endpoint novo na Api).</summary>
    public async Task ImportarArquivoDeProjetoAsync(byte[] jsonBytes)
    {
        EstaCarregando = true;
        MensagemErro = null;
        try
        {
            var arquivo = ArquivoPlantaService.Importar(jsonBytes);

            using var streamBase = new MemoryStream(arquivo.ImagemBasePng);
            var planta = await apiClient.ImportarPlantaAsync(
                ProjetoId, arquivo.Nome, arquivo.Descricao, arquivo.NomeCliente,
                TipoArquivoOrigem.Imagem, "imagem_base.png", streamBase);

            // Em ordem crescente — CriarCamadaAsync sempre acrescenta ao final, então percorrer já
            // ordenado reproduz a mesma prioridade de camadas do arquivo original, sem precisar de
            // uma chamada extra de reordenar.
            foreach (var camadaArquivo in arquivo.Camadas.OrderBy(c => c.Ordem))
            {
                var camada = await apiClient.CriarCamadaAsync(planta.Id, camadaArquivo.Nome);

                if (camadaArquivo.ImagemPng is { Length: > 0 } imagemPng)
                {
                    using var streamCamada = new MemoryStream(imagemPng);
                    camada = await apiClient.AtualizarImagemCamadaAsync(planta.Id, camada.Id, streamCamada);
                }

                if (Math.Abs(camadaArquivo.Opacidade - camada.Opacidade) > 0.001)
                    await apiClient.DefinirOpacidadeCamadaAsync(planta.Id, camada.Id, camadaArquivo.Opacidade);
                if (!camadaArquivo.Visivel)
                    await apiClient.AlternarVisibilidadeCamadaAsync(planta.Id, camada.Id);
                if (camadaArquivo.Bloqueada)
                    await apiClient.BloquearCamadaAsync(planta.Id, camada.Id);
                if (camadaArquivo.BloqueioAlpha)
                    await apiClient.BloquearAlphaCamadaAsync(planta.Id, camada.Id);
            }

            planta = await apiClient.ObterPlantaAsync(planta.Id);
            var item = new PlantaListItemViewModel(planta, apiClient);
            Plantas.Add(item);
            PlantaSelecionada?.Invoke(this, planta);
            _ = item.CarregarMiniaturaAsync();
        }
        catch (Exception ex)
        {
            MensagemErro = $"Não foi possível importar o arquivo de projeto: {ex.Message}";
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

    public async Task RemoverAsync(PlantaListItemViewModel item)
    {
        EstaCarregando = true;
        MensagemErro = null;
        try
        {
            await apiClient.RemoverPlantaAsync(item.Planta.Id);
            Plantas.Remove(item);
        }
        catch (Exception ex)
        {
            MensagemErro = $"Não foi possível excluir a planta: {ex.Message}";
        }
        finally
        {
            EstaCarregando = false;
        }
    }
}
