using Camdas.Domain.Entities;
using Camdas.Domain.Enums;
using Camdas.Infrastructure.Persistence;
using Camdas.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Camdas.Infrastructure.Tests.Repositories;

/// <summary>
/// Diferente do smoke test com o provider InMemory (que não é um banco relacional de verdade),
/// aqui usamos Sqlite para exercitar os repositórios concretos contra um motor relacional real —
/// sem precisar de Docker/servidor externo. A conexão Sqlite ":memory:" precisa ficar aberta durante
/// todo o teste, senão o banco é destruído.
/// </summary>
public sealed class RepositoriosEfCoreTests : IAsyncLifetime
{
    private SqliteConnection _conexao = null!;
    private CamdasDbContext _contexto = null!;

    public async Task InitializeAsync()
    {
        _conexao = new SqliteConnection("DataSource=:memory:");
        await _conexao.OpenAsync();

        var opcoes = new DbContextOptionsBuilder<CamdasDbContext>().UseSqlite(_conexao).Options;
        _contexto = new CamdasDbContext(opcoes);
        await _contexto.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _contexto.DisposeAsync();
        await _conexao.DisposeAsync();
    }

    private CamdasDbContext NovoContexto() =>
        new(new DbContextOptionsBuilder<CamdasDbContext>().UseSqlite(_conexao).Options);

    [Fact]
    public async Task ProjetoRepository_deve_salvar_e_listar()
    {
        var repositorio = new ProjetoRepositoryEfCore(_contexto);
        var unitOfWork = new UnitOfWorkEfCore(_contexto);

        var projeto = new Projeto("Residência Alfa", null, Guid.NewGuid(), DateTime.UtcNow);
        repositorio.Adicionar(projeto);
        await unitOfWork.SalvarAlteracoesAsync(CancellationToken.None);

        var lista = await repositorio.ListarAsync(CancellationToken.None);
        var carregado = await repositorio.ObterPorIdAsync(projeto.Id, CancellationToken.None);

        lista.Should().ContainSingle(p => p.Id == projeto.Id);
        carregado.Should().NotBeNull();
        carregado!.Nome.Should().Be("Residência Alfa");
    }

    [Fact]
    public async Task PlantaRepository_deve_salvar_e_recarregar_agregado_completo()
    {
        var repositorio = new PlantaRepositoryEfCore(_contexto);
        var unitOfWork = new UnitOfWorkEfCore(_contexto);
        var usuarioId = Guid.NewGuid();

        var planta = new Planta(
            Guid.NewGuid(), usuarioId, "Casa Alfa", null, null, TipoArquivoOrigem.Imagem, "/a.png", DateTime.UtcNow);
        var camada = planta.AdicionarCamada("Hidráulica");

        repositorio.Adicionar(planta);
        await unitOfWork.SalvarAlteracoesAsync(CancellationToken.None);

        // Novo DbContext (nova unidade de trabalho) para forçar reler do banco, sem cache do change tracker.
        await using var novoContexto = NovoContexto();
        var novoRepositorio = new PlantaRepositoryEfCore(novoContexto);

        var carregada = await novoRepositorio.ObterPorIdAsync(planta.Id, CancellationToken.None);

        carregada.Should().NotBeNull();
        carregada!.Camadas.Should().HaveCount(1);
        carregada.Camadas.Should().Contain(c => c.Id == camada.Id);

        var porProjeto = await novoRepositorio.ListarPorProjetoAsync(planta.ProjetoId, CancellationToken.None);
        porProjeto.Should().ContainSingle(p => p.Id == planta.Id);
    }

    [Fact]
    public async Task HistoricoRepository_deve_salvar_e_listar_por_planta()
    {
        var repositorio = new HistoricoRepositoryEfCore(_contexto);
        var unitOfWork = new UnitOfWorkEfCore(_contexto);
        var plantaId = Guid.NewGuid();

        repositorio.Adicionar(new HistoricoAlteracao(
            nameof(Planta), plantaId, TipoAcaoHistorico.PlantaImportada, Guid.NewGuid(), DateTime.UtcNow, plantaId));
        await unitOfWork.SalvarAlteracoesAsync(CancellationToken.None);

        var historico = await repositorio.ListarPorPlantaAsync(plantaId, CancellationToken.None);

        historico.Should().ContainSingle();
    }

    [Fact]
    public async Task UsuarioRepository_deve_obter_por_id()
    {
        var usuario = new Usuario("Ana Souza", "ana@empresa.com");
        _contexto.Usuarios.Add(usuario);
        await _contexto.SaveChangesAsync();

        var repositorio = new UsuarioRepositoryEfCore(_contexto);
        var carregado = await repositorio.ObterPorIdAsync(usuario.Id, CancellationToken.None);

        carregado.Should().NotBeNull();
        carregado!.Nome.Should().Be("Ana Souza");
    }
}
