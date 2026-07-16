using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace Camdas.Mobile;

// WindowSoftInputMode = AdjustPan: sem isto (padrão do Android costuma ser "resize"), abrir o
// teclado on-screen redimensiona a Activity inteira — inclusive o PlantaCanvasView (SKCanvasView).
// Crash real reproduzido num Galaxy Tab A (Android 8.1): abrir o teclado (ex.: nomear uma camada
// nova) derrubava o app com SIGSEGV nativo dentro de libSkiaSharp.so, sempre no mesmo instante do
// "startInputInner" — o resize forçado da Activity parece recriar a superfície nativa do canvas
// enquanto o Skia ainda está desenhando nela. AdjustPan evita o resize: o teclado só sobrepõe a
// tela, sem forçar o layout/canvas a recalcular.
[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop,
    WindowSoftInputMode = SoftInput.AdjustPan,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
}
