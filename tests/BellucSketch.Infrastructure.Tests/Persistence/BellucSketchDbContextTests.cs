using BellucSketch.Domain.Entities;
using BellucSketch.Domain.Enums;
using BellucSketch.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BellucSketch.Infrastructure.Tests.Persistence;

/// <summary>
/// Testes de "smoke" do mapeamento EF Core (banco InMemory) — não substituem testes de integração
/// com um banco relacional real (migrations, Fase 3 mais adiante), mas provam que a conversão de
/// CorHex e as coleções por backing field (Camadas) realmente persistem e recarregam corretamente.
/// </summary>
public class BellucSketchDbContextTests
{
    private static BellucSketchDbContext CriarContexto(string nomeBanco) =>
        new(new DbContextOptionsBuilder<BellucSketchDbContext>().UseInMemoryDatabase(nomeBanco).Options);

    [Fact]
    public async Task Deve_salvar_e_recarregar_planta_com_camadas()
    {
        var nomeBanco = Guid.NewGuid().ToString();
        var projetoId = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();

        Guid plantaId;
        Guid camadaId;

        await using (var contexto = CriarContexto(nomeBanco))
        {
            var planta = new Planta(
                projetoId, usuarioId, "Casa Alfa", null, null, TipoArquivoOrigem.Imagem, "/intranet/plantas/a.png", DateTime.UtcNow);
            var camada = planta.AdicionarCamada("Hidráulica");

            plantaId = planta.Id;
            camadaId = camada.Id;

            contexto.Plantas.Add(planta);
            await contexto.SaveChangesAsync();
        }

        await using var leitura = CriarContexto(nomeBanco);
        var plantaCarregada = await leitura.Plantas
            .Include(p => p.Camadas)
            .SingleAsync(p => p.Id == plantaId);

        plantaCarregada.Camadas.Should().HaveCount(1);

        var camadaCarregada = plantaCarregada.Camadas.Single(c => c.Id == camadaId);
        camadaCarregada.Nome.Should().Be("Hidráulica");
    }

    [Fact]
    public async Task Deve_salvar_e_recarregar_usuario_e_projeto_preservando_enums()
    {
        var nomeBanco = Guid.NewGuid().ToString();
        Guid usuarioId;
        Guid projetoId;

        await using (var contexto = CriarContexto(nomeBanco))
        {
            var usuario = new Usuario("Ana Souza", "ana@empresa.com");
            var projeto = new Projeto("Residência Alfa", "Reforma completa", usuario.Id, DateTime.UtcNow);
            projeto.Arquivar();

            usuarioId = usuario.Id;
            projetoId = projeto.Id;

            contexto.Usuarios.Add(usuario);
            contexto.Projetos.Add(projeto);
            await contexto.SaveChangesAsync();
        }

        await using var leitura = CriarContexto(nomeBanco);
        var usuarioCarregado = await leitura.Usuarios.SingleAsync(u => u.Id == usuarioId);
        var projetoCarregado = await leitura.Projetos.SingleAsync(p => p.Id == projetoId);

        usuarioCarregado.Nome.Should().Be("Ana Souza");
        projetoCarregado.Status.Should().Be(StatusProjeto.Arquivado);
    }

    [Fact]
    public async Task Deve_salvar_e_recarregar_historico_vinculado_a_planta()
    {
        var nomeBanco = Guid.NewGuid().ToString();
        var plantaId = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();

        await using (var contexto = CriarContexto(nomeBanco))
        {
            var historico = new HistoricoAlteracao(
                nameof(Planta), plantaId, TipoAcaoHistorico.PlantaImportada, usuarioId, DateTime.UtcNow, plantaId);

            contexto.HistoricosDeAlteracao.Add(historico);
            await contexto.SaveChangesAsync();
        }

        await using var leitura = CriarContexto(nomeBanco);
        var historicoCarregado = await leitura.HistoricosDeAlteracao.SingleAsync(h => h.PlantaId == plantaId);

        historicoCarregado.Acao.Should().Be(TipoAcaoHistorico.PlantaImportada);
    }
}
