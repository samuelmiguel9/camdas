using System.Collections.ObjectModel;
using Camdas.Contracts;
using Camdas.Mobile.Rendering;
using Camdas.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp;

namespace Camdas.Mobile.ViewModels;

/// <summary>
/// Tela de edição isolada de UMA camada por vez: mostra só a imagem base + o traço dessa camada
/// (nenhuma outra camada aparece), pra desenhar sem distração. Ao salvar, volta pra tela principal
/// da planta, que recarrega e mostra todas as camadas juntas.
/// </summary>
public partial class CamadaEdicaoViewModel(IApiClient apiClient, IArmazenamentoRascunho armazenamentoRascunho) : BaseViewModel
{
    private Guid _plantaId;

    [ObservableProperty]
    private CamadaDto? _camada;

    [ObservableProperty]
    private SKBitmap? _imagemBase;

    [ObservableProperty]
    private string _corTraco = "#000000";

    [ObservableProperty]
    private float _espessuraTraco = 3f;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EspessuraMaxima))]
    private bool _modoApagar;

    /// <summary>A borracha usa uma escala bem maior que o traço — apagar uma área grande de uma vez
    /// exige uma espessura muito acima do que faz sentido pra desenhar linhas finas.</summary>
    public float EspessuraMaxima => ModoApagar ? 120f : 24f;

    /// <summary>Evita deixar EspessuraTraco acima do novo Maximum ao sair do modo borracha (o Slider
    /// não aceita Value > Maximum).</summary>
    partial void OnModoApagarChanged(bool value)
    {
        if (EspessuraTraco > EspessuraMaxima)
            EspessuraTraco = EspessuraMaxima;
    }

    /// <summary>Sempre com 0 ou 1 item (a própria camada) — formato exigido por PlantaCanvasView.</summary>
    public ObservableCollection<CamadaDto> Camadas { get; } = [];

    public Dictionary<Guid, SKBitmap> ImagensPorCamada { get; } = [];

    public event EventHandler? CamadaSalva;

    public async Task CarregarAsync(Guid plantaId, Guid camadaId)
    {
        _plantaId = plantaId;
        EstaCarregando = true;
        MensagemErro = null;
        try
        {
            var planta = await apiClient.ObterPlantaAsync(plantaId);
            var camada = planta.Camadas.First(c => c.Id == camadaId);
            Camada = camada;
            Camadas.Clear();
            Camadas.Add(camada);

            ImagemBase?.Dispose();
            var bytesBase = await apiClient.ObterArquivoPlantaAsync(plantaId);
            ImagemBase = BitmapDecodificacao.DecodificarLimitado(bytesBase);

            foreach (var bitmap in ImagensPorCamada.Values)
                bitmap.Dispose();
            ImagensPorCamada.Clear();

            if (camada.TemImagemRaster)
            {
                var bytesCamada = await apiClient.ObterImagemCamadaAsync(plantaId, camadaId);
                var bitmap = BitmapDecodificacao.DecodificarLimitado(bytesCamada);
                if (bitmap is not null)
                    ImagensPorCamada[camadaId] = bitmap;
            }

            // Rascunho local mais recente que o que está no servidor (ex.: o app fechou antes de
            // "Salvar camada") — carrega por cima, sem perguntar, pra não perder o trabalho.
            var rascunho = await armazenamentoRascunho.CarregarAsync(camadaId);
            if (rascunho is { Length: > 0 })
            {
                if (ImagensPorCamada.Remove(camadaId, out var bitmapAnterior))
                    bitmapAnterior.Dispose();
                ImagensPorCamada[camadaId] = SKBitmap.Decode(rascunho);
            }
        }
        catch (Exception ex)
        {
            MensagemErro = $"Não foi possível carregar a camada: {ex.Message}";
        }
        finally
        {
            EstaCarregando = false;
        }
    }

    /// <summary>Chamado a cada alteração no canvas (Canvas.DesenhoAlterado) — guarda uma cópia local
    /// pra não perder o traço se o app fechar antes de "Salvar camada" mandar pro servidor.</summary>
    public async Task SalvarRascunhoAsync()
    {
        if (Camada is null || !ImagensPorCamada.TryGetValue(Camada.Id, out var bitmap))
            return;

        // Encode direto do SKBitmap (não via SKImage.FromBitmap) — evita sk_image_new_from_bitmap,
        // ponto de crash nativo (SIGSEGV) reproduzido no Galaxy Tab A.
        using var dados = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        await armazenamentoRascunho.SalvarAsync(Camada.Id, dados.ToArray());
    }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        if (Camada is null || !ImagensPorCamada.TryGetValue(Camada.Id, out var bitmap))
        {
            MensagemErro = "Nada foi desenhado nesta camada ainda.";
            return;
        }

        EstaCarregando = true;
        MensagemErro = null;
        try
        {
            using var dados = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = dados.AsStream();

            await apiClient.AtualizarImagemCamadaAsync(_plantaId, Camada.Id, stream);
            armazenamentoRascunho.Remover(Camada.Id);
            CamadaSalva?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            MensagemErro = $"Não foi possível salvar a camada: {ex.Message}";
        }
        finally
        {
            EstaCarregando = false;
        }
    }

    [RelayCommand]
    private void EscolherCor(string cor) => CorTraco = cor;

    [RelayCommand]
    private void AlternarModoApagar() => ModoApagar = !ModoApagar;
}
