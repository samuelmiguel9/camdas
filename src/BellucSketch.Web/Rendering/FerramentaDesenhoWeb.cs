namespace BellucSketch.Web.Rendering;

/// <summary>Ferramenta ativa na barra de edição da Web — equivalente aos botões de
/// PlantaPage.xaml (Android), mas como um único seletor em vez de vários modos booleanos
/// independentes (ModoTexto/ModoSelecaoCota etc.).</summary>
public enum FerramentaDesenhoWeb
{
    Lapis,
    Texto,
    Icone,
    Cota,
}

/// <summary>Estilo de traço da ferramenta de lápis — mesmas opções de <c>EstiloTraco</c>
/// (PlantaCanvasView, Android), redeclaradas aqui porque o Android é o único a poder referenciar
/// aquele enum (fica num assembly net9.0-android).</summary>
public enum EstiloTracoWeb
{
    Livre,
    RetaContinua,
    RetaPontilhada,
    RetaTracejada,
}
