using Camdas.Contracts;
using Camdas.Mobile.Rendering;
using Camdas.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using SkiaSharp;

namespace Camdas.Mobile.ViewModels;

/// <summary>
/// Envolve um <see cref="PlantaDto"/> pra lista de plantas do projeto, carregando sob demanda uma
/// miniatura (PNG cru, sem depender de MAUI aqui) com a imagem base + todas as camadas visíveis já
/// compostas (reaproveita <see cref="PlantaOverlayRenderer"/>, o mesmo usado na tela principal da
/// planta). A conversão pra ImageSource acontece só na camada de UI (Camdas.Mobile), via converter.
/// </summary>
public sealed partial class PlantaListItemViewModel : ObservableObject
{
    private const int TamanhoMiniatura = 160;

    private readonly IApiClient _apiClient;

    public PlantaDto Planta { get; }

    [ObservableProperty]
    private byte[]? _miniaturaPng;

    public PlantaListItemViewModel(PlantaDto planta, IApiClient apiClient)
    {
        Planta = planta;
        _apiClient = apiClient;
    }

    public async Task CarregarMiniaturaAsync()
    {
        try
        {
            using var imagemBase = BitmapDecodificacao.DecodificarLimitado(await _apiClient.ObterArquivoPlantaAsync(Planta.Id));
            if (imagemBase is null)
                return;

            var imagensPorCamada = new Dictionary<Guid, SKBitmap>();
            try
            {
                foreach (var camada in Planta.Camadas.Where(c => c.TemImagemRaster))
                {
                    var bitmap = BitmapDecodificacao.DecodificarLimitado(await _apiClient.ObterImagemCamadaAsync(Planta.Id, camada.Id));
                    if (bitmap is not null)
                        imagensPorCamada[camada.Id] = bitmap;
                }

                using var composta = new SKBitmap(imagemBase.Width, imagemBase.Height);
                using (var canvas = new SKCanvas(composta))
                    PlantaOverlayRenderer.Desenhar(canvas, Planta.Camadas, imagemBase, imagensPorCamada);

                var escala = (float)TamanhoMiniatura / Math.Max(composta.Width, composta.Height);
                using var reduzida = composta.Resize(
                    new SKImageInfo((int)(composta.Width * escala), (int)(composta.Height * escala)),
                    SKFilterQuality.Medium);
                if (reduzida is null)
                    return;

                // Encode direto do SKBitmap (não via SKImage.FromBitmap) — evita sk_image_new_from_bitmap,
                // ponto de crash nativo (SIGSEGV) reproduzido no Galaxy Tab A.
                using var dados = reduzida.Encode(SKEncodedImageFormat.Png, 90);
                MiniaturaPng = dados.ToArray();
            }
            finally
            {
                foreach (var bitmap in imagensPorCamada.Values)
                    bitmap.Dispose();
            }
        }
        catch
        {
            // Miniatura é só um extra visual — se a planta ainda não tem arquivo/camadas legíveis,
            // a lista continua funcionando normalmente sem ela.
        }
    }
}
