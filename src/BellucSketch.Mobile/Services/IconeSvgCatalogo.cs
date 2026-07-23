using Svg.Skia;
using SkiaSharp;

namespace BellucSketch.Mobile.Services;

/// <summary>
/// Catálogo dos ícones técnicos padronizados (água fria/quente, esgoto, interruptor, ponto de gás,
/// tomada simples/dupla) usados pela ferramenta de ícones — SVGs embutidos como MauiAsset em
/// Resources/Raw/Icones. Carrega e faz o parse de cada um sob demanda (só quando o usuário abre o
/// menu ou escolhe um pela primeira vez) e cacheia o <see cref="SKPicture"/> resultante em memória,
/// já que reabrir/reparsear o SVG a cada toque seria desperdício — os 7 nunca mudam durante a
/// execução do app.
/// </summary>
public sealed class IconeSvgCatalogo
{
    /// <summary>Nome do arquivo (sem extensão) → título amigável mostrado no menu de escolha.</summary>
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

    // Guarda o SKSvg em si (não só o SKPicture) e NUNCA descarta — Dispose() do SKSvg também
    // descarta o SKPicture que ele criou, então um `using var svg = new SKSvg()` invalidava o
    // Picture assim que o método retornava. O Picture ficava com o handle nativo morto, e a
    // primeira leitura dele (CullRect, ou desenhar) crashava nativo (SIGSEGV dentro de
    // sk_picture_get_cull_rect, confirmado via logcat). Como os 7 ícones nunca mudam durante a
    // execução do app, manter o SKSvg vivo pra sempre (igual ficaria em cache de qualquer forma) é
    // seguro e não vaza nada relevante.
    private readonly Dictionary<string, SKSvg> _cache = [];

    public async Task<SKPicture> ObterAsync(string nomeArquivo)
    {
        if (_cache.TryGetValue(nomeArquivo, out var existente))
            return existente.Picture!;

        await using var stream = await FileSystem.OpenAppPackageFileAsync($"Icones/{nomeArquivo}.svg");
        var svg = new SKSvg();
        var picture = svg.Load(stream) ?? throw new InvalidOperationException($"Não foi possível carregar o ícone '{nomeArquivo}'.");

        _cache[nomeArquivo] = svg;
        return picture;
    }
}
