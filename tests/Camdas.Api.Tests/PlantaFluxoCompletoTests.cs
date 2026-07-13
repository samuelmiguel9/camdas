using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Camdas.Contracts;
using Camdas.Domain.Entities;
using Camdas.Domain.Enums;
using Camdas.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Camdas.Api.Tests;

/// <summary>
/// Testes de integração ponta a ponta via <see cref="WebApplicationFactory{Program}"/>: sobem a Api
/// real (controllers, MediatR, autenticação JWT, middleware de erros) contra um banco Sqlite
/// in-memory, exercitando o fluxo completo — importar planta, desenhar (raster) sobre uma
/// camada — além dos principais casos de erro (401/404/400).
/// </summary>
public sealed class PlantaFluxoCompletoTests : IClassFixture<CustomWebApplicationFactory>
{
    // Espelha exatamente a configuração JSON da Api (Program.cs): PropertyNameCaseInsensitive (via
    // JsonSerializerDefaults.Web) + enums como string.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly CustomWebApplicationFactory _factory;

    public PlantaFluxoCompletoTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task<Usuario> SemearUsuarioAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CamdasDbContext>();
        await db.Database.EnsureCreatedAsync();

        var usuario = new Usuario("Ana Souza", $"ana{Guid.NewGuid()}@empresa.com");
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();

        return usuario;
    }

    /// <summary>
    /// Obtém o token pelo próprio endpoint de emissão (/api/auth/dev-token) em vez de instanciar
    /// JwtTokenGenerator manualmente no teste — assim o token sempre vem exatamente do mesmo
    /// pipeline (config + DI) que o servidor em teste está rodando, sem risco de divergência.
    /// </summary>
    private async Task<HttpClient> ClienteAutenticadoComoAsync(Usuario usuario)
    {
        var cliente = _factory.CreateClient();

        var respostaToken = await cliente.PostAsJsonAsync("/api/auth/dev-token", new { usuarioId = usuario.Id });
        respostaToken.EnsureSuccessStatusCode();
        var tokenDto = await respostaToken.Content.ReadFromJsonAsync<EmitirTokenResponse>(JsonOptions);

        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenDto!.Token);
        return cliente;
    }

    private static MultipartFormDataContent CriarFormularioImportacao(Guid projetoId, MemoryStream conteudo, string nome = "Casa Alfa")
    {
        return new MultipartFormDataContent
        {
            { new StringContent(projetoId.ToString()), "projetoId" },
            { new StringContent(nome), "nome" },
            { new StringContent(nameof(TipoArquivoOrigem.Imagem)), "tipoArquivoOrigem" },
            { new StreamContent(conteudo), "arquivo", "planta.png" },
        };
    }

    [Fact]
    public async Task Fluxo_completo_importar_e_desenhar_sobre_camada()
    {
        var usuario = await SemearUsuarioAsync();
        var cliente = await ClienteAutenticadoComoAsync(usuario);

        var respostaProjeto = await cliente.PostAsJsonAsync(
            "/api/projetos", new { nome = "Residência Alfa", descricao = (string?)null });
        respostaProjeto.EnsureSuccessStatusCode();
        var projeto = await respostaProjeto.Content.ReadFromJsonAsync<ProjetoDto>(JsonOptions);
        projeto.Should().NotBeNull();

        var bytesPlanta = new byte[] { 1, 2, 3, 4 };
        using var conteudoArquivo = new MemoryStream(bytesPlanta);
        using var formulario = CriarFormularioImportacao(projeto!.Id, conteudoArquivo);

        var respostaPlanta = await cliente.PostAsync("/api/plantas", formulario);
        respostaPlanta.EnsureSuccessStatusCode();
        var planta = await respostaPlanta.Content.ReadFromJsonAsync<PlantaDto>(JsonOptions);
        planta.Should().NotBeNull();
        planta!.Nome.Should().Be("Casa Alfa");
        planta.Camadas.Should().BeEmpty();

        var plantasDoProjeto = await cliente.GetFromJsonAsync<List<PlantaDto>>(
            $"/api/projetos/{projeto.Id}/plantas", JsonOptions);
        plantasDoProjeto.Should().ContainSingle(p => p.Id == planta.Id);

        // GET .../arquivo devolve exatamente os bytes salvos na importação.
        var bytesArquivo = await cliente.GetByteArrayAsync($"/api/plantas/{planta.Id}/arquivo");
        bytesArquivo.Should().Equal(bytesPlanta);

        // Camada é criada livremente pelo usuário — só o nome, sem cor própria (cor é só do traço).
        var respostaCamada = await cliente.PostAsJsonAsync(
            $"/api/plantas/{planta.Id}/camadas", new { nome = "Hidráulica" });
        respostaCamada.EnsureSuccessStatusCode();
        var camada = await respostaCamada.Content.ReadFromJsonAsync<CamadaDto>(JsonOptions);
        camada!.Nome.Should().Be("Hidráulica");
        camada.Ordem.Should().Be(1);
        camada.TemImagemRaster.Should().BeFalse();

        // Desenho livre: PUT salva o traço da camada, GET devolve exatamente o que foi enviado.
        var bytesTraco = new byte[] { 9, 9, 9 };
        using var conteudoTraco = new MemoryStream(bytesTraco);
        using var formularioImagem = new MultipartFormDataContent
        {
            { new StreamContent(conteudoTraco), "arquivo", "traco.png" },
        };
        var respostaImagem = await cliente.PutAsync($"/api/plantas/{planta.Id}/camadas/{camada.Id}/imagem", formularioImagem);
        respostaImagem.EnsureSuccessStatusCode();
        var camadaComTraco = await respostaImagem.Content.ReadFromJsonAsync<CamadaDto>(JsonOptions);
        camadaComTraco!.TemImagemRaster.Should().BeTrue();

        var bytesTracoBaixados = await cliente.GetByteArrayAsync($"/api/plantas/{planta.Id}/camadas/{camada.Id}/imagem");
        bytesTracoBaixados.Should().Equal(bytesTraco);

        var historico = await cliente.GetFromJsonAsync<List<HistoricoDto>>($"/api/plantas/{planta.Id}/historico", JsonOptions);
        historico!.Select(h => h.Acao).Should().ContainInOrder(
            TipoAcaoHistorico.PlantaImportada,
            TipoAcaoHistorico.CamadaAdicionada,
            TipoAcaoHistorico.CamadaImagemAtualizada);
    }

    [Fact]
    public async Task Deve_reordenar_camadas_e_renumerar_em_ordem_crescente()
    {
        var usuario = await SemearUsuarioAsync();
        var cliente = await ClienteAutenticadoComoAsync(usuario);

        var respostaProjeto = await cliente.PostAsJsonAsync("/api/projetos", new { nome = "Projeto Ordem", descricao = (string?)null });
        var projeto = await respostaProjeto.Content.ReadFromJsonAsync<ProjetoDto>(JsonOptions);

        using var conteudoArquivo = new MemoryStream([1, 2, 3, 4]);
        using var formulario = CriarFormularioImportacao(projeto!.Id, conteudoArquivo);
        var respostaPlanta = await cliente.PostAsync("/api/plantas", formulario);
        var planta = await respostaPlanta.Content.ReadFromJsonAsync<PlantaDto>(JsonOptions);

        var camada1 = await (await cliente.PostAsJsonAsync($"/api/plantas/{planta!.Id}/camadas", new { nome = "Primeira" }))
            .Content.ReadFromJsonAsync<CamadaDto>(JsonOptions);
        var camada2 = await (await cliente.PostAsJsonAsync($"/api/plantas/{planta.Id}/camadas", new { nome = "Segunda" }))
            .Content.ReadFromJsonAsync<CamadaDto>(JsonOptions);
        var camada3 = await (await cliente.PostAsJsonAsync($"/api/plantas/{planta.Id}/camadas", new { nome = "Terceira" }))
            .Content.ReadFromJsonAsync<CamadaDto>(JsonOptions);

        var respostaOrdem = await cliente.PutAsJsonAsync(
            $"/api/plantas/{planta.Id}/camadas/ordem",
            new { ordemDosIds = new[] { camada3!.Id, camada1!.Id, camada2!.Id } });
        respostaOrdem.EnsureSuccessStatusCode();

        var camadasReordenadas = await respostaOrdem.Content.ReadFromJsonAsync<List<CamadaDto>>(JsonOptions);
        camadasReordenadas!.OrderBy(c => c.Ordem).Select(c => c.Id).Should().Equal(camada3.Id, camada1.Id, camada2.Id);
        camadasReordenadas.Single(c => c.Id == camada3.Id).Ordem.Should().Be(1);
        camadasReordenadas.Single(c => c.Id == camada1.Id).Ordem.Should().Be(2);
        camadasReordenadas.Single(c => c.Id == camada2.Id).Ordem.Should().Be(3);
    }

    [Fact]
    public async Task Camada_sem_imagem_retorna_404()
    {
        var usuario = await SemearUsuarioAsync();
        var cliente = await ClienteAutenticadoComoAsync(usuario);

        var respostaProjeto = await cliente.PostAsJsonAsync("/api/projetos", new { nome = "Projeto X", descricao = (string?)null });
        var projeto = await respostaProjeto.Content.ReadFromJsonAsync<ProjetoDto>(JsonOptions);

        using var conteudoArquivo = new MemoryStream([1, 2, 3, 4]);
        using var formulario = CriarFormularioImportacao(projeto!.Id, conteudoArquivo);
        var respostaPlanta = await cliente.PostAsync("/api/plantas", formulario);
        var planta = await respostaPlanta.Content.ReadFromJsonAsync<PlantaDto>(JsonOptions);

        var respostaCamada = await cliente.PostAsJsonAsync(
            $"/api/plantas/{planta!.Id}/camadas", new { nome = "Elétrica" });
        var camada = await respostaCamada.Content.ReadFromJsonAsync<CamadaDto>(JsonOptions);

        var resposta = await cliente.GetAsync($"/api/plantas/{planta.Id}/camadas/{camada!.Id}/imagem");

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Obter_projeto_inexistente_retorna_404()
    {
        var usuario = await SemearUsuarioAsync();
        var cliente = await ClienteAutenticadoComoAsync(usuario);

        var resposta = await cliente.GetAsync($"/api/projetos/{Guid.NewGuid()}");

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Sem_token_retorna_401()
    {
        var cliente = _factory.CreateClient();

        var resposta = await cliente.GetAsync("/api/projetos");

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
