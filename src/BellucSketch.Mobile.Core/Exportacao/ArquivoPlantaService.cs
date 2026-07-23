using System.Text.Json;
using BellucSketch.Contracts;
using SkiaSharp;

namespace BellucSketch.Mobile.Exportacao;

/// <summary>
/// Lógica pura (sem MAUI/Android) de exportar/importar o arquivo de projeto — testável isoladamente.
/// </summary>
public static class ArquivoPlantaService
{
    /// <summary>Versão do formato do arquivo em si (não a versão do app) — permite detectar um
    /// arquivo de uma versão futura/incompatível do formato sem tentar interpretá-lo às cegas.</summary>
    public const string FormatoVersaoAtual = "1";

    public static byte[] Exportar(
        PlantaDto planta, SKBitmap imagemBase, IReadOnlyDictionary<Guid, SKBitmap> imagensPorCamada)
    {
        using var dadosBase = imagemBase.Encode(SKEncodedImageFormat.Png, 100);

        var camadas = planta.Camadas
            .OrderBy(c => c.Ordem)
            .Select(c =>
            {
                byte[]? imagemPng = null;
                if (imagensPorCamada.TryGetValue(c.Id, out var bitmap))
                {
                    using var dados = bitmap.Encode(SKEncodedImageFormat.Png, 100);
                    imagemPng = dados.ToArray();
                }

                return new ArquivoCamadaDto(c.Nome, c.Visivel, c.Bloqueada, c.BloqueioAlpha, c.Ordem, c.Opacidade, imagemPng);
            })
            .ToList();

        var arquivo = new ArquivoPlantaDto(
            FormatoVersaoAtual, planta.Nome, planta.Descricao, planta.NomeCliente, dadosBase.ToArray(), camadas);

        return JsonSerializer.SerializeToUtf8Bytes(arquivo);
    }

    public static ArquivoPlantaDto Importar(byte[] jsonBytes)
    {
        ArquivoPlantaDto? arquivo;
        try
        {
            arquivo = JsonSerializer.Deserialize<ArquivoPlantaDto>(jsonBytes);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Arquivo de projeto inválido ou corrompido.", ex);
        }

        if (arquivo is null || string.IsNullOrWhiteSpace(arquivo.FormatoVersao) || arquivo.ImagemBasePng.Length == 0)
            throw new InvalidOperationException("Arquivo de projeto inválido ou corrompido.");

        if (arquivo.FormatoVersao != FormatoVersaoAtual)
            throw new InvalidOperationException(
                $"Arquivo de projeto em formato não reconhecido (versão {arquivo.FormatoVersao}) — atualize o app.");

        return arquivo;
    }
}
