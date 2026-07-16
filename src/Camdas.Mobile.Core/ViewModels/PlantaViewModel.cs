using System.Collections.ObjectModel;
using Camdas.Contracts;
using Camdas.Mobile.Rendering;
using Camdas.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp;

namespace Camdas.Mobile.ViewModels;

/// <summary>
/// Tela principal da planta: mostra a composição de todas as camadas visíveis (só leitura — não
/// desenha aqui) e gerencia visibilidade/bloqueio/criação/prioridade de camadas. O desenho em si
/// acontece numa tela isolada por camada (<see cref="CamadaEdicaoViewModel"/>/CamadaEdicaoPage), pra
/// não misturar o traço de uma camada nova com as demais enquanto ela está sendo criada/editada.
/// </summary>
public partial class PlantaViewModel(IApiClient apiClient) : BaseViewModel
{
    [ObservableProperty]
    private PlantaDto? _planta;

    [ObservableProperty]
    private CamadaDto? _camadaAtiva;

    [ObservableProperty]
    private SKBitmap? _imagemBase;

    public ObservableCollection<CamadaDto> Camadas { get; } = [];

    /// <summary>Avisa a Page que o usuário tocou numa camada para abri-la na tela de edição isolada.</summary>
    public event EventHandler<CamadaDto>? CamadaSelecionadaParaEdicao;

    /// <summary>
    /// Bitmap raster de cada camada, por Id. A mesma instância de Dictionary é compartilhada com
    /// PlantaCanvasView (via binding) e mutada por ela a cada traço — esta classe só lê/grava do
    /// zero ao carregar/salvar.
    /// </summary>
    public Dictionary<Guid, SKBitmap> ImagensPorCamada { get; } = [];

    public async Task CarregarAsync(Guid plantaId)
    {
        EstaCarregando = true;
        MensagemErro = null;
        try
        {
            var camadaAtivaAnteriorId = CamadaAtiva?.Id;

            Planta = await apiClient.ObterPlantaAsync(plantaId);
            Camadas.Clear();
            foreach (var camada in Planta.Camadas)
                Camadas.Add(camada);

            CamadaAtiva = Camadas.FirstOrDefault(c => c.Id == camadaAtivaAnteriorId) ?? Camadas.FirstOrDefault();

            await CarregarImagensAsync();
        }
        catch (Exception ex)
        {
            MensagemErro = $"Não foi possível carregar a planta: {ex.Message}";
        }
        finally
        {
            EstaCarregando = false;
        }
    }

    private async Task CarregarImagensAsync()
    {
        if (Planta is null)
            return;

        ImagemBase?.Dispose();
        var bytesBase = await apiClient.ObterArquivoPlantaAsync(Planta.Id);
        ImagemBase = BitmapDecodificacao.DecodificarLimitado(bytesBase);

        foreach (var bitmap in ImagensPorCamada.Values)
            bitmap.Dispose();
        ImagensPorCamada.Clear();

        foreach (var camada in Camadas.Where(c => c.TemImagemRaster))
        {
            var bytesCamada = await apiClient.ObterImagemCamadaAsync(Planta.Id, camada.Id);
            var bitmap = BitmapDecodificacao.DecodificarLimitado(bytesCamada);
            if (bitmap is not null)
                ImagensPorCamada[camada.Id] = bitmap;
        }
    }

    public async Task CriarCamadaAsync(string nome)
    {
        if (Planta is null)
            return;

        EstaCarregando = true;
        MensagemErro = null;
        try
        {
            var camada = await apiClient.CriarCamadaAsync(Planta.Id, nome);
            Camadas.Add(camada);
            CamadaAtiva = camada;
        }
        catch (Exception ex)
        {
            MensagemErro = $"Não foi possível criar a camada: {ex.Message}";
        }
        finally
        {
            EstaCarregando = false;
        }
    }

    /// <summary>
    /// Troca a prioridade da camada com a vizinha imediatamente acima/abaixo na lista e persiste a
    /// nova ordem no servidor, que devolve a lista já renumerada em ordem crescente. Botões
    /// "subir"/"descer" em vez de arrastar-e-soltar — mais confiável em toque no Android.
    /// </summary>
    public async Task MoverCamadaAsync(CamadaDto camada, bool paraCima)
    {
        if (Planta is null)
            return;

        var indice = Camadas.IndexOf(camada);
        var novoIndice = paraCima ? indice - 1 : indice + 1;
        if (indice < 0 || novoIndice < 0 || novoIndice >= Camadas.Count)
            return;

        var ids = Camadas.Select(c => c.Id).ToList();
        (ids[indice], ids[novoIndice]) = (ids[novoIndice], ids[indice]);

        EstaCarregando = true;
        MensagemErro = null;
        try
        {
            var atualizadas = await apiClient.ReordenarCamadasAsync(Planta.Id, ids);
            var camadaAtivaId = CamadaAtiva?.Id;
            Camadas.Clear();
            foreach (var c in atualizadas)
                Camadas.Add(c);
            CamadaAtiva = Camadas.FirstOrDefault(c => c.Id == camadaAtivaId);
        }
        catch (Exception ex)
        {
            MensagemErro = $"Não foi possível reordenar as camadas: {ex.Message}";
        }
        finally
        {
            EstaCarregando = false;
        }
    }

    [RelayCommand]
    private Task MoverCamadaParaCimaAsync(CamadaDto camada) => MoverCamadaAsync(camada, paraCima: true);

    [RelayCommand]
    private Task MoverCamadaParaBaixoAsync(CamadaDto camada) => MoverCamadaAsync(camada, paraCima: false);

    [RelayCommand]
    private void SelecionarCamada(CamadaDto camada)
    {
        CamadaAtiva = camada;
        CamadaSelecionadaParaEdicao?.Invoke(this, camada);
    }

    [RelayCommand]
    private async Task AlternarVisibilidadeAsync(CamadaDto camada)
    {
        if (Planta is null)
            return;

        try
        {
            var atualizada = await apiClient.AlternarVisibilidadeCamadaAsync(Planta.Id, camada.Id);
            SubstituirCamada(atualizada);
        }
        catch (Exception ex)
        {
            MensagemErro = $"Não foi possível alterar a visibilidade: {ex.Message}";
        }
    }

    /// <summary>Chamado ao soltar o slider de opacidade (não a cada tick) — evita spam de requisições.</summary>
    public async Task DefinirOpacidadeAsync(CamadaDto camada, double opacidade)
    {
        if (Planta is null)
            return;

        try
        {
            var atualizada = await apiClient.DefinirOpacidadeCamadaAsync(Planta.Id, camada.Id, opacidade);
            SubstituirCamada(atualizada);
        }
        catch (Exception ex)
        {
            MensagemErro = $"Não foi possível alterar a opacidade: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task AlternarBloqueioAsync(CamadaDto camada)
    {
        if (Planta is null)
            return;

        try
        {
            var atualizada = camada.Bloqueada
                ? await apiClient.DesbloquearCamadaAsync(Planta.Id, camada.Id)
                : await apiClient.BloquearCamadaAsync(Planta.Id, camada.Id);

            SubstituirCamada(atualizada);
        }
        catch (Exception ex)
        {
            MensagemErro = $"Não foi possível alterar o bloqueio: {ex.Message}";
        }
    }

    private void SubstituirCamada(CamadaDto atualizada)
    {
        for (var i = 0; i < Camadas.Count; i++)
        {
            if (Camadas[i].Id == atualizada.Id)
            {
                Camadas[i] = atualizada;
                break;
            }
        }

        if (CamadaAtiva?.Id == atualizada.Id)
            CamadaAtiva = atualizada;
    }
}
