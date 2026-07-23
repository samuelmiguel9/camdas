using BellucSketch.Mobile.Services;
using Microsoft.JSInterop;
using SkiaSharp;

namespace BellucSketch.Web.Services;

/// <summary>Implementação Web de <see cref="IOcrService"/> — chama o Tesseract.js vendorizado
/// (wwwroot/lib/tesseract, sem CDN) via <c>wwwroot/js/ocr.js</c>. Equivalente ao <c>OcrTextoService</c>
/// do Android (Google ML Kit), mas com um motor OCR diferente por trás — os dois rodam localmente,
/// sem depender de um serviço de nuvem.</summary>
public sealed class OcrServicoWeb(IJSRuntime js) : IOcrService
{
    public async Task<string> ReconhecerAsync(SKBitmap recorte)
    {
        using var dados = recorte.Encode(SKEncodedImageFormat.Png, 100);
        var base64 = Convert.ToBase64String(dados.ToArray());
        return await js.InvokeAsync<string>("camdasOcr.reconhecerAsync", base64);
    }
}
