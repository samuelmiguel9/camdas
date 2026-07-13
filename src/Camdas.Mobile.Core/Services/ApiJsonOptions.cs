using System.Text.Json;
using System.Text.Json.Serialization;

namespace Camdas.Mobile.Services;

/// <summary>
/// Espelha exatamente as opções de JSON da Api (Program.cs): PropertyNameCaseInsensitive (via
/// JsonSerializerDefaults.Web) + enums como string. Ver RELATORIO.md, Fase 4 — sem isso, a
/// desserialização de enums (ex.: "unidade": "Metro") falha silenciosamente.
/// </summary>
internal static class ApiJsonOptions
{
    public static readonly JsonSerializerOptions Padrao = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}
