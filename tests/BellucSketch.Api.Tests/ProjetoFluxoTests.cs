using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BellucSketch.Contracts;
using BellucSketch.Domain.Entities;
using BellucSketch.Domain.Enums;
using BellucSketch.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BellucSketch.Api.Tests;

/// <summary>Cobre criar/editar/excluir projeto — inclusive a exclusão em cascata das plantas dele.</summary>
public sealed class ProjetoFluxoTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly CustomWebApplicationFactory _factory;

    public ProjetoFluxoTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task<HttpClient> ClienteAutenticadoAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BellucSketchDbContext>();
        await db.Database.EnsureCreatedAsync();

        var usuario = new Usuario("Ana Souza", $"ana{Guid.NewGuid()}@empresa.com");
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();

        var cliente = _factory.CreateClient();
        var respostaToken = await cliente.PostAsJsonAsync("/api/auth/dev-token", new { usuarioId = usuario.Id });
        var tokenDto = await respostaToken.Content.ReadFromJsonAsync<EmitirTokenResponse>(JsonOptions);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenDto!.Token);
        return cliente;
    }

    [Fact]
    public async Task Deve_renomear_e_excluir_projeto_sem_plantas()
    {
        var cliente = await ClienteAutenticadoAsync();

        var respostaCriar = await cliente.PostAsJsonAsync("/api/projetos", new { nome = "Residência Alfa", descricao = (string?)null });
        var projeto = await respostaCriar.Content.ReadFromJsonAsync<ProjetoDto>(JsonOptions);

        var respostaRenomear = await cliente.PutAsJsonAsync($"/api/projetos/{projeto!.Id}", new { nome = "Residência Beta" });
        respostaRenomear.EnsureSuccessStatusCode();
        var renomeado = await respostaRenomear.Content.ReadFromJsonAsync<ProjetoDto>(JsonOptions);
        renomeado!.Nome.Should().Be("Residência Beta");

        var respostaExcluir = await cliente.DeleteAsync($"/api/projetos/{projeto.Id}");
        respostaExcluir.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var respostaObter = await cliente.GetAsync($"/api/projetos/{projeto.Id}");
        respostaObter.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Excluir_projeto_com_plantas_deve_apagar_tudo_em_cascata()
    {
        var cliente = await ClienteAutenticadoAsync();

        var respostaCriar = await cliente.PostAsJsonAsync("/api/projetos", new { nome = "Residência Gama", descricao = (string?)null });
        var projeto = await respostaCriar.Content.ReadFromJsonAsync<ProjetoDto>(JsonOptions);

        using var conteudoArquivo = new MemoryStream([1, 2, 3, 4]);
        using var formulario = new MultipartFormDataContent
        {
            { new StringContent(projeto!.Id.ToString()), "projetoId" },
            { new StringContent("Casa Gama"), "nome" },
            { new StringContent(nameof(TipoArquivoOrigem.Imagem)), "tipoArquivoOrigem" },
            { new StreamContent(conteudoArquivo), "arquivo", "planta.png" },
        };
        var respostaPlanta = await cliente.PostAsync("/api/plantas", formulario);
        var planta = await respostaPlanta.Content.ReadFromJsonAsync<PlantaDto>(JsonOptions);

        var respostaExcluir = await cliente.DeleteAsync($"/api/projetos/{projeto.Id}");
        respostaExcluir.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var respostaProjeto = await cliente.GetAsync($"/api/projetos/{projeto.Id}");
        respostaProjeto.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var respostaPlantaApagada = await cliente.GetAsync($"/api/plantas/{planta!.Id}");
        respostaPlantaApagada.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
