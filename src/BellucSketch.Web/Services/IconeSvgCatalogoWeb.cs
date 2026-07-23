using SkiaSharp;
using Svg.Skia;

namespace BellucSketch.Web.Services;

/// <summary>Catálogo dos ícones técnicos padronizados — equivalente ao <c>IconeSvgCatalogo</c> do
/// Android, mas busca cada SVG via <see cref="HttpClient"/> em <c>wwwroot/icones/</c> (publicados a
/// partir da pasta compartilhada <c>assets/icones-tecnicos</c> na raiz do repo — ver
/// BellucSketch.Web.csproj) em vez de <c>FileSystem.OpenAppPackageFileAsync</c> (só existe no MAUI).
/// Mesmo motivo pra nunca descartar o <see cref="SKSvg"/>: descartar invalida o <see cref="SKPicture"/>
/// que ele criou, e os 7 ícones nunca mudam durante a execução do app.</summary>
public sealed class IconeSvgCatalogoWeb(HttpClient httpClient)
{
    public static readonly IReadOnlyList<(string NomeArquivo, string Titulo)> Icones =
    [
        ("agua_fria", "Água fria"),
        ("agua_quente", "Água quente"),
        ("esgoto", "Esgoto"),
        ("interruptor", "Interruptor"),
        ("ponto_de_gas", "Ponto de gás"),
        ("tomada_simples", "Tomada simples"),
        ("tomada_dupla", "Tomada dupla"),
    ];

    private readonly Dictionary<string, SKSvg> _cache = [];

    public async Task<SKPicture> ObterAsync(string nomeArquivo)
    {
        if (_cache.TryGetValue(nomeArquivo, out var existente))
            return existente.Picture!;

        await using var stream = await httpClient.GetStreamAsync($"icones/{nomeArquivo}.svg");
        var svg = new SKSvg();
        var picture = svg.Load(stream) ?? throw new InvalidOperationException($"Não foi possível carregar o ícone '{nomeArquivo}'.");

        _cache[nomeArquivo] = svg;
        return picture;
    }
}
