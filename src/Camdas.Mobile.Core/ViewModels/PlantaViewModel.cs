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
public partial class PlantaViewModel(IApiClient apiClient, ISalvadorGaleria salvadorGaleria) : BaseViewModel
{
    [ObservableProperty]
    private PlantaDto? _planta;

    [ObservableProperty]
    private CamadaDto? _camadaAtiva;

    [ObservableProperty]
    private SKBitmap? _imagemBase;

    /// <summary>Camada com o menu de opções (opacidade/limpar/bloqueios/duplicar/excluir) aberto — null quando fechado.</summary>
    [ObservableProperty]
    private CamadaDto? _camadaMenuAberta;

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

    /// <summary>Compõe a imagem base + todas as camadas visíveis (mesma lógica de desenho da tela)
    /// num PNG só e salva na galeria do aparelho — "Salvar tudo".</summary>
    public async Task SalvarComposicaoNaGaleriaAsync()
    {
        if (Planta is null || ImagemBase is null)
            return;

        EstaCarregando = true;
        MensagemErro = null;
        try
        {
            using var bitmap = new SKBitmap(ImagemBase.Width, ImagemBase.Height);
            using (var canvas = new SKCanvas(bitmap))
                PlantaOverlayRenderer.Desenhar(canvas, Camadas, ImagemBase, ImagensPorCamada);

            using var imagem = SKImage.FromBitmap(bitmap);
            using var dados = imagem.Encode(SKEncodedImageFormat.Png, 100);

            var nomeArquivo = $"{Planta.Nome}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            await salvadorGaleria.SalvarPngAsync(dados.ToArray(), nomeArquivo);
        }
        catch (Exception ex)
        {
            MensagemErro = $"Não foi possível salvar na galeria: {ex.Message}";
        }
        finally
        {
            EstaCarregando = false;
        }
    }

    /// <summary>
    /// Cria uma camada nova a partir de uma imagem já pronta (galeria/câmera) em vez de traço
    /// desenhado à mão — redimensiona pra bater com o tamanho da planta base, senão a imagem
    /// importada ficaria desalinhada/cortada ao compor com as demais camadas.
    /// </summary>
    public async Task AdicionarImagemComoCamadaAsync(string nome, Stream conteudoImagem)
    {
        if (Planta is null || ImagemBase is null)
            return;

        EstaCarregando = true;
        MensagemErro = null;
        try
        {
            using var bitmapOriginal = SKBitmap.Decode(conteudoImagem);
            if (bitmapOriginal is null)
                throw new InvalidOperationException("Não foi possível ler a imagem selecionada.");

            using var bitmapRedimensionado = bitmapOriginal.Resize(
                new SKImageInfo(ImagemBase.Width, ImagemBase.Height), SKFilterQuality.Medium);
            using var imagem = SKImage.FromBitmap(bitmapRedimensionado ?? bitmapOriginal);
            using var dados = imagem.Encode(SKEncodedImageFormat.Png, 100);
            using var streamPng = dados.AsStream();

            var camada = await apiClient.CriarCamadaAsync(Planta.Id, nome);
            var camadaComImagem = await apiClient.AtualizarImagemCamadaAsync(Planta.Id, camada.Id, streamPng);

            Camadas.Add(camadaComImagem);
            CamadaAtiva = camadaComImagem;
            ImagensPorCamada[camadaComImagem.Id] = SKBitmap.Decode(await apiClient.ObterImagemCamadaAsync(Planta.Id, camadaComImagem.Id));
        }
        catch (Exception ex)
        {
            MensagemErro = $"Não foi possível adicionar a imagem como camada: {ex.Message}";
        }
        finally
        {
            EstaCarregando = false;
        }
    }

    /// <summary>
    /// Move a camada arrastada (<paramref name="origem"/>) pra posição da camada onde ela foi solta
    /// (<paramref name="destino"/>) e persiste a nova ordem no servidor, que devolve a lista já
    /// renumerada em ordem crescente.
    /// </summary>
    public async Task ReordenarArrastandoAsync(CamadaDto origem, CamadaDto destino)
    {
        if (Planta is null)
            return;

        var indiceOrigem = Camadas.IndexOf(origem);
        var indiceDestino = Camadas.IndexOf(destino);
        if (indiceOrigem < 0 || indiceDestino < 0 || indiceOrigem == indiceDestino)
            return;

        var ids = Camadas.Select(c => c.Id).ToList();
        var idOrigem = ids[indiceOrigem];
        ids.RemoveAt(indiceOrigem);
        ids.Insert(indiceDestino, idOrigem);

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
    private void AbrirMenuCamada(CamadaDto camada) => CamadaMenuAberta = camada;

    [RelayCommand]
    private void FecharMenuCamada() => CamadaMenuAberta = null;

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

    public async Task RemoverCamadaAsync(CamadaDto camada)
    {
        if (Planta is null)
            return;

        EstaCarregando = true;
        MensagemErro = null;
        try
        {
            await apiClient.RemoverCamadaAsync(Planta.Id, camada.Id);
            Camadas.Remove(camada);
            ImagensPorCamada.Remove(camada.Id, out var bitmap);
            bitmap?.Dispose();

            if (CamadaAtiva?.Id == camada.Id)
                CamadaAtiva = Camadas.FirstOrDefault();
            if (CamadaMenuAberta?.Id == camada.Id)
                CamadaMenuAberta = null;
        }
        catch (Exception ex)
        {
            MensagemErro = $"Não foi possível excluir a camada: {ex.Message}";
        }
        finally
        {
            EstaCarregando = false;
        }
    }

    /// <summary>Esvazia o traço da camada (fica em branco) sem excluí-la — usado pelo menu de opções da camada.</summary>
    [RelayCommand]
    private async Task LimparCamadaAsync(CamadaDto camada)
    {
        if (Planta is null)
            return;

        try
        {
            var atualizada = await apiClient.LimparCamadaAsync(Planta.Id, camada.Id);
            SubstituirCamada(atualizada);
            if (ImagensPorCamada.Remove(camada.Id, out var bitmap))
                bitmap.Dispose();
        }
        catch (Exception ex)
        {
            MensagemErro = $"Não foi possível limpar a camada: {ex.Message}";
        }
    }

    /// <summary>Cria uma cópia da camada (nome, opacidade, visibilidade e traço) logo abaixo dela.</summary>
    [RelayCommand]
    private async Task DuplicarCamadaAsync(CamadaDto camada)
    {
        if (Planta is null)
            return;

        EstaCarregando = true;
        MensagemErro = null;
        try
        {
            var copia = await apiClient.DuplicarCamadaAsync(Planta.Id, camada.Id);
            Camadas.Add(copia);
            if (copia.TemImagemRaster)
                ImagensPorCamada[copia.Id] = SKBitmap.Decode(await apiClient.ObterImagemCamadaAsync(Planta.Id, copia.Id));

            CamadaMenuAberta = null;
        }
        catch (Exception ex)
        {
            MensagemErro = $"Não foi possível duplicar a camada: {ex.Message}";
        }
        finally
        {
            EstaCarregando = false;
        }
    }

    /// <summary>Bloqueio de opacidade/alpha — impede alterar a transparência do traço já pintado.</summary>
    [RelayCommand]
    private async Task AlternarBloqueioAlphaAsync(CamadaDto camada)
    {
        if (Planta is null)
            return;

        try
        {
            var atualizada = camada.BloqueioAlpha
                ? await apiClient.DesbloquearAlphaCamadaAsync(Planta.Id, camada.Id)
                : await apiClient.BloquearAlphaCamadaAsync(Planta.Id, camada.Id);

            SubstituirCamada(atualizada);
        }
        catch (Exception ex)
        {
            MensagemErro = $"Não foi possível alterar o bloqueio de opacidade: {ex.Message}";
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
        if (CamadaMenuAberta?.Id == atualizada.Id)
            CamadaMenuAberta = atualizada;
    }
}
