using Camdas.Application.Abstractions;
using Camdas.Application.Common;
using Camdas.Contracts;
using Camdas.Domain.Entities;
using Camdas.Domain.Enums;
using FluentValidation;
using MediatR;

namespace Camdas.Application.Plantas;

public sealed record ImportarPlantaCommand(
    Guid ProjetoId,
    string Nome,
    string? Descricao,
    string? NomeCliente,
    TipoArquivoOrigem TipoArquivoOrigem,
    string NomeArquivo,
    Stream Conteudo) : IRequest<PlantaDto>;

public sealed class ImportarPlantaCommandValidator : AbstractValidator<ImportarPlantaCommand>
{
    public ImportarPlantaCommandValidator()
    {
        RuleFor(c => c.ProjetoId).NotEmpty();
        RuleFor(c => c.Nome).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Descricao).MaximumLength(2000);
        RuleFor(c => c.NomeCliente).MaximumLength(200);
        RuleFor(c => c.NomeArquivo).NotEmpty().MaximumLength(260);
        RuleFor(c => c.Conteudo).NotNull();
    }
}

public sealed class ImportarPlantaCommandHandler(
    IProjetoRepository projetoRepository,
    IPlantaRepository plantaRepository,
    IHistoricoRepository historicoRepository,
    IArquivoStorage arquivoStorage,
    IConversorPdfParaImagem conversorPdfParaImagem,
    IUsuarioContext usuarioContext,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<ImportarPlantaCommand, PlantaDto>
{
    public async Task<PlantaDto> Handle(ImportarPlantaCommand request, CancellationToken cancellationToken)
    {
        _ = await projetoRepository.ObterPorIdAsync(request.ProjetoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Projeto '{request.ProjetoId}' não encontrado.");

        // O app sempre desenha sobre uma imagem raster — um PDF importado é convertido (primeira
        // página) antes de ser salvo. TipoArquivoOrigem continua registrando o formato original
        // que o usuário enviou, mesmo que o arquivo armazenado seja o PNG resultante.
        var (nomeParaSalvar, conteudoParaSalvar) = request.TipoArquivoOrigem == TipoArquivoOrigem.Pdf
            ? (Path.ChangeExtension(request.NomeArquivo, ".png"),
               await conversorPdfParaImagem.ConverterPrimeiraPaginaAsync(request.Conteudo, cancellationToken))
            : (request.NomeArquivo, request.Conteudo);

        var caminhoArquivo = await arquivoStorage.SalvarAsync(nomeParaSalvar, conteudoParaSalvar, cancellationToken);

        var planta = new Planta(
            request.ProjetoId,
            usuarioContext.UsuarioId,
            request.Nome,
            request.Descricao,
            request.NomeCliente,
            request.TipoArquivoOrigem,
            caminhoArquivo,
            clock.AgoraUtc);

        plantaRepository.Adicionar(planta);
        historicoRepository.Adicionar(new HistoricoAlteracao(
            nameof(Planta),
            planta.Id,
            TipoAcaoHistorico.PlantaImportada,
            usuarioContext.UsuarioId,
            clock.AgoraUtc,
            planta.Id));

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        return planta.ParaDto();
    }
}
