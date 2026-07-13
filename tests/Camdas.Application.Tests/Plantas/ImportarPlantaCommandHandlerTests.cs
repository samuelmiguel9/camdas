using Camdas.Application.Abstractions;
using Camdas.Application.Common;
using Camdas.Application.Plantas;
using Camdas.Domain.Entities;
using Camdas.Domain.Enums;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Camdas.Application.Tests.Plantas;

public class ImportarPlantaCommandHandlerTests
{
    [Fact]
    public async Task Deve_importar_planta_e_registrar_historico()
    {
        var projetoId = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();

        var projetoRepository = Substitute.For<IProjetoRepository>();
        projetoRepository.ObterPorIdAsync(projetoId, Arg.Any<CancellationToken>())
            .Returns(new Projeto("Residência Alfa", null, usuarioId, DateTime.UtcNow));

        var plantaRepository = Substitute.For<IPlantaRepository>();
        var historicoRepository = Substitute.For<IHistoricoRepository>();

        var arquivoStorage = Substitute.For<IArquivoStorage>();
        arquivoStorage.SalvarAsync("planta.png", Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns("/intranet/plantas/planta.png");

        var usuarioContext = Substitute.For<IUsuarioContext>();
        usuarioContext.UsuarioId.Returns(usuarioId);
        var clock = Substitute.For<IClock>();
        clock.AgoraUtc.Returns(DateTime.UtcNow);
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var conversorPdf = Substitute.For<IConversorPdfParaImagem>();

        var handler = new ImportarPlantaCommandHandler(
            projetoRepository, plantaRepository, historicoRepository, arquivoStorage, conversorPdf, usuarioContext, unitOfWork, clock);

        using var conteudo = new MemoryStream();
        var resultado = await handler.Handle(
            new ImportarPlantaCommand(projetoId, "Residência Alfa", "Reforma completa", "João Silva", TipoArquivoOrigem.Imagem, "planta.png", conteudo),
            CancellationToken.None);

        resultado.Nome.Should().Be("Residência Alfa");
        resultado.Camadas.Should().BeEmpty();
        resultado.CaminhoArquivoOriginal.Should().Be("/intranet/plantas/planta.png");
        plantaRepository.Received(1).Adicionar(Arg.Any<Planta>());
        historicoRepository.Received(1).Adicionar(Arg.Is<HistoricoAlteracao>(h => h.Acao == TipoAcaoHistorico.PlantaImportada));
        await unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        await conversorPdf.DidNotReceive().ConverterPrimeiraPaginaAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deve_converter_pdf_para_imagem_antes_de_salvar()
    {
        var projetoId = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();

        var projetoRepository = Substitute.For<IProjetoRepository>();
        projetoRepository.ObterPorIdAsync(projetoId, Arg.Any<CancellationToken>())
            .Returns(new Projeto("Residência Alfa", null, usuarioId, DateTime.UtcNow));

        using var imagemConvertida = new MemoryStream();
        var conversorPdf = Substitute.For<IConversorPdfParaImagem>();
        conversorPdf.ConverterPrimeiraPaginaAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(imagemConvertida);

        var arquivoStorage = Substitute.For<IArquivoStorage>();
        arquivoStorage.SalvarAsync("planta.png", imagemConvertida, Arg.Any<CancellationToken>())
            .Returns("/intranet/plantas/planta.png");

        var usuarioContext = Substitute.For<IUsuarioContext>();
        usuarioContext.UsuarioId.Returns(usuarioId);
        var clock = Substitute.For<IClock>();
        clock.AgoraUtc.Returns(DateTime.UtcNow);

        var handler = new ImportarPlantaCommandHandler(
            projetoRepository,
            Substitute.For<IPlantaRepository>(),
            Substitute.For<IHistoricoRepository>(),
            arquivoStorage,
            conversorPdf,
            usuarioContext,
            Substitute.For<IUnitOfWork>(),
            clock);

        using var conteudoPdf = new MemoryStream();
        var resultado = await handler.Handle(
            new ImportarPlantaCommand(projetoId, "Residência Alfa", null, null, TipoArquivoOrigem.Pdf, "planta.pdf", conteudoPdf),
            CancellationToken.None);

        resultado.CaminhoArquivoOriginal.Should().Be("/intranet/plantas/planta.png");
        await conversorPdf.Received(1).ConverterPrimeiraPaginaAsync(conteudoPdf, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deve_falhar_quando_projeto_nao_existe()
    {
        var projetoRepository = Substitute.For<IProjetoRepository>();
        projetoRepository.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Projeto?)null);

        var handler = new ImportarPlantaCommandHandler(
            projetoRepository,
            Substitute.For<IPlantaRepository>(),
            Substitute.For<IHistoricoRepository>(),
            Substitute.For<IArquivoStorage>(),
            Substitute.For<IConversorPdfParaImagem>(),
            Substitute.For<IUsuarioContext>(),
            Substitute.For<IUnitOfWork>(),
            Substitute.For<IClock>());

        using var conteudo = new MemoryStream();
        var acao = () => handler.Handle(
            new ImportarPlantaCommand(Guid.NewGuid(), "Residência Alfa", null, null, TipoArquivoOrigem.Pdf, "a.pdf", conteudo),
            CancellationToken.None);

        await acao.Should().ThrowAsync<RecursoNaoEncontradoException>();
    }
}
