using Android.Graphics;
using Android.Gms.Tasks;
using Java.Nio;
using SkiaSharp;
using Xamarin.Google.MLKit.Vision.Common;
using Xamarin.Google.MLKit.Vision.Text;
using Xamarin.Google.MLKit.Vision.Text.Latin;

namespace BellucSketch.Mobile.Services;

/// <summary>
/// Reconhecimento de texto (OCR) da ferramenta de cota — roda dentro do próprio processo do app via
/// Google ML Kit, modelo embutido no apk (pacote "Bundled": ver BellucSketch.Mobile.csproj), sem depender
/// de internet nem de download via Play Services no primeiro uso. O reconhecedor (<see
/// cref="_reconhecedor"/>) é caro de criar e não muda de configuração durante a execução do app, por
/// isso vive uma única vez (singleton, ver MauiProgram).
/// </summary>
public sealed class OcrTextoService : IOcrService
{
    private readonly ITextRecognizer _reconhecedor = TextRecognition.GetClient(TextRecognizerOptions.DefaultOptions!);

    /// <summary>Recorta (ver PlantaCanvasView.RecortarComposicaoNativa) e reconhece o texto contido no
    /// bitmap — precisa ser Rgba8888 (mesmo layout de bytes usado por Bitmap.Config.Argb8888 no
    /// Android), senão a conversão abaixo produz cores erradas e o OCR lê ruído.</summary>
    public Task<string> ReconhecerAsync(SKBitmap recorte)
    {
        if (recorte.ColorType != SKColorType.Rgba8888)
            throw new ArgumentException("O recorte precisa estar em SKColorType.Rgba8888.", nameof(recorte));

        using var bitmapAndroid = ConverterParaBitmapAndroid(recorte);
        var imagemEntrada = InputImage.FromBitmap(bitmapAndroid, 0);

        var conclusao = new TaskCompletionSource<string>();
        _reconhecedor.Process(imagemEntrada)
            .AddOnSuccessListener(new ListenerSucesso(texto => conclusao.TrySetResult(texto)))
            .AddOnFailureListener(new ListenerFalha(erro => conclusao.TrySetException(erro)));
        return conclusao.Task;
    }

    private static Bitmap ConverterParaBitmapAndroid(SKBitmap origem)
    {
        var bitmap = Bitmap.CreateBitmap(origem.Width, origem.Height, Bitmap.Config.Argb8888!)
            ?? throw new InvalidOperationException("Não foi possível criar o bitmap Android pro OCR.");
        bitmap.CopyPixelsFromBuffer(ByteBuffer.Wrap(origem.Bytes));
        return bitmap;
    }

    private sealed class ListenerSucesso(Action<string> aoConcluir) : Java.Lang.Object, IOnSuccessListener
    {
        public void OnSuccess(Java.Lang.Object? resultado) =>
            aoConcluir((resultado as Text)?.GetText() ?? string.Empty);
    }

    private sealed class ListenerFalha(Action<Java.Lang.Exception> aoFalhar) : Java.Lang.Object, IOnFailureListener
    {
        public void OnFailure(Java.Lang.Exception erro) => aoFalhar(erro);
    }
}
