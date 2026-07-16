using SkiaSharp;

namespace Camdas.Mobile.Rendering;

/// <summary>
/// Plantas de engenharia (PDF de grande formato) e fotos de câmera podem chegar em resolução muito
/// alta (ex.: uma folha A1 renderizada a 300 DPI passa de 7000x4000px). Manter isso em memória
/// nativa sem redimensionar — sobretudo com <c>PlantaCanvasView.UsarResolucaoNativa</c>, que mantém
/// uma cópia por camada no mesmo tamanho da imagem base — foi a causa real de um crash nativo
/// (SIGSEGV dentro de libSkiaSharp.so, falha de alocação) depois de um tempo de uso normal num
/// aparelho de memória mais limitada. Toda decodificação de imagem vinda da Api passa por aqui.
/// </summary>
public static class BitmapDecodificacao
{
    public const int DimensaoMaximaPadrao = 2500;

    public static SKBitmap? DecodificarLimitado(byte[] bytes, int dimensaoMaxima = DimensaoMaximaPadrao)
    {
        var original = SKBitmap.Decode(bytes);
        if (original is null)
            return null;

        var maiorLado = Math.Max(original.Width, original.Height);
        if (maiorLado <= dimensaoMaxima)
            return original;

        var escala = (float)dimensaoMaxima / maiorLado;
        var reduzida = original.Resize(
            new SKImageInfo((int)(original.Width * escala), (int)(original.Height * escala)),
            SKFilterQuality.Medium);

        if (reduzida is null)
            return original; // Resize falhou — melhor manter a original do que perder a imagem

        original.Dispose();
        return reduzida;
    }
}
