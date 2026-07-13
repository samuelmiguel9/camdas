using System.Text.Json;
using Camdas.Mobile.Services;

namespace Camdas.Mobile.Platforms.Services;

/// <summary>
/// Implementação concreta de <see cref="IArmazenamentoEnderecosApi"/> usando <c>Preferences</c> do MAUI
/// Essentials — por isso vive aqui (projeto net8.0-android), não em Camdas.Mobile.Core (biblioteca
/// plain .NET, sem acesso a APIs de plataforma).
/// </summary>
public sealed class ArmazenamentoEnderecosApiPreferences : IArmazenamentoEnderecosApi
{
    private const string ChaveLista = "camdas_enderecos_api";
    private const string ChaveAtivo = "camdas_endereco_ativo";

    private static readonly EnderecoApi EnderecoPadrao = new("Trabalho Starlink", "http://192.168.1.33:5080/");

    public IReadOnlyList<EnderecoApi> Listar()
    {
        var lista = CarregarLista();
        if (lista.Count == 0)
        {
            lista = [EnderecoPadrao];
            SalvarLista(lista);
            Preferences.Default.Set(ChaveAtivo, EnderecoPadrao.Nome);
        }

        var ativo = Preferences.Default.Get(ChaveAtivo, string.Empty);
        return lista.OrderByDescending(e => e.Nome == ativo).ToList();
    }

    public void Salvar(EnderecoApi endereco)
    {
        var lista = CarregarLista().Where(e => e.Nome != endereco.Nome).Append(endereco).ToList();
        SalvarLista(lista);
        Preferences.Default.Set(ChaveAtivo, endereco.Nome);
    }

    public EnderecoApi? ObterAtivo()
    {
        var ativo = Preferences.Default.Get(ChaveAtivo, string.Empty);
        return CarregarLista().FirstOrDefault(e => e.Nome == ativo);
    }

    private static List<EnderecoApi> CarregarLista()
    {
        var json = Preferences.Default.Get(ChaveLista, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
            return [];

        return JsonSerializer.Deserialize<List<EnderecoApi>>(json) ?? [];
    }

    private static void SalvarLista(List<EnderecoApi> lista) =>
        Preferences.Default.Set(ChaveLista, JsonSerializer.Serialize(lista));
}
