using BellucSketch.Application.Abstractions;
using BellucSketch.Application.Projetos;
using BellucSketch.Domain.Entities;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BellucSketch.Application.Tests.Projetos;

public class CriarProjetoCommandHandlerTests
{
    [Fact]
    public async Task Deve_criar_projeto_associando_ao_usuario_autenticado()
    {
        var projetoRepository = Substitute.For<IProjetoRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var usuarioContext = Substitute.For<IUsuarioContext>();
        var clock = Substitute.For<IClock>();

        var usuarioId = Guid.NewGuid();
        var agora = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        usuarioContext.UsuarioId.Returns(usuarioId);
        clock.AgoraUtc.Returns(agora);

        var handler = new CriarProjetoCommandHandler(projetoRepository, usuarioContext, unitOfWork, clock);

        var resultado = await handler.Handle(new CriarProjetoCommand("Residência Alfa", "Reforma completa"), CancellationToken.None);

        resultado.Nome.Should().Be("Residência Alfa");
        resultado.CriadoPorId.Should().Be(usuarioId);
        resultado.DataCriacao.Should().Be(agora);
        projetoRepository.Received(1).Adicionar(Arg.Any<Projeto>());
        await unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
