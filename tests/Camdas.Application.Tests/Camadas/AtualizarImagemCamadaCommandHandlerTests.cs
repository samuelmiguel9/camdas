using Camdas.Application.Abstractions;
using Camdas.Application.Camadas;
using Camdas.Domain.Common;
using Camdas.Domain.Entities;
using Camdas.Domain.Enums;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Camdas.Application.Tests.Camadas;

public class AtualizarImagemCamadaCommandHandlerTests
{
    private static Planta NovaPlantaComCamadas(Guid usuarioId) => new(
        Guid.NewGuid(), usuarioId, "Casa Alfa", null, null, TipoArquivoOrigem.Imagem, "/intranet/plantas/a.png", DateTime.UtcNow);

    [Fact]
    public async Task Deve_salvar_arquivo_atualizar_camada_e_registrar_historico()
    {
        var usuarioId = Guid.NewGuid();
        var planta = NovaPlantaComCamadas(usuarioId);
        var camadaId = planta.AdicionarCamada("Hidráulica").Id;

        var plantaRepository = Substitute.For<IPlantaRepository>();
        plantaRepository.ObterPorIdAsync(planta.Id, Arg.Any<CancellationToken>()).Returns(planta);

        var arquivoStorage = Substitute.For<IArquivoStorage>();
        arquivoStorage.SalvarAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns("/intranet/camadas/traco.png");

        var historicoRepository = Substitute.For<IHistoricoRepository>();
        var usuarioContext = Substitute.For<IUsuarioContext>();
        usuarioContext.UsuarioId.Returns(usuarioId);
        var clock = Substitute.For<IClock>();
        clock.AgoraUtc.Returns(DateTime.UtcNow);
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var handler = new AtualizarImagemCamadaCommandHandler(
            plantaRepository, historicoRepository, arquivoStorage, usuarioContext, unitOfWork, clock);

        using var conteudo = new MemoryStream([1, 2, 3]);
        var comando = new AtualizarImagemCamadaCommand(planta.Id, camadaId, "traco.png", conteudo);

        var resultado = await handler.Handle(comando, CancellationToken.None);

        resultado.TemImagemRaster.Should().BeTrue();
        historicoRepository.Received(1).Adicionar(Arg.Is<HistoricoAlteracao>(h => h.Acao == TipoAcaoHistorico.CamadaImagemAtualizada));
        await unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deve_propagar_excecao_de_dominio_quando_camada_esta_bloqueada()
    {
        var usuarioId = Guid.NewGuid();
        var planta = NovaPlantaComCamadas(usuarioId);
        var camadaId = planta.AdicionarCamada("Hidráulica").Id;
        planta.BloquearCamada(camadaId);

        var plantaRepository = Substitute.For<IPlantaRepository>();
        plantaRepository.ObterPorIdAsync(planta.Id, Arg.Any<CancellationToken>()).Returns(planta);

        var arquivoStorage = Substitute.For<IArquivoStorage>();
        arquivoStorage.SalvarAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns("/intranet/camadas/traco.png");

        var handler = new AtualizarImagemCamadaCommandHandler(
            plantaRepository,
            Substitute.For<IHistoricoRepository>(),
            arquivoStorage,
            Substitute.For<IUsuarioContext>(),
            Substitute.For<IUnitOfWork>(),
            Substitute.For<IClock>());

        using var conteudo = new MemoryStream([1, 2, 3]);
        var comando = new AtualizarImagemCamadaCommand(planta.Id, camadaId, "traco.png", conteudo);

        var acao = () => handler.Handle(comando, CancellationToken.None);

        await acao.Should().ThrowAsync<DomainException>();
    }
}
