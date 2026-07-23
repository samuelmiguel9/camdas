using SkiaSharp;

namespace BellucSketch.Mobile.Services;

/// <summary>Reconhecimento de texto (OCR) usado pela ferramenta de cota — implementado por
/// <c>OcrTextoService</c> (Android, via Google ML Kit) e por <c>OcrServicoWeb</c> (Web, via
/// Tesseract.js rodando no navegador). Ambas as implementações rodam localmente (sem depender de
/// um serviço de nuvem/chave de API), só o motor por trás muda por plataforma.</summary>
public interface IOcrService
{
    Task<string> ReconhecerAsync(SKBitmap recorte);
}
