using Camdas.Application.Abstractions;
using Camdas.Application.Common;
using Camdas.Contracts;
using Camdas.Domain.Entities;
using Camdas.Domain.Enums;
using FluentValidation;
using MediatR;

namespace Camdas.Application.Camadas;

public sealed record AtualizarImagemCamadaCommand(
    Guid PlantaId,
    Guid CamadaId,
    string NomeArquivo,
    Stream Conteudo) : IRequest<CamadaDto>;

public sealed class AtualizarImagemCamadaCommandValidator : AbstractValidator<AtualizarImagemCamadaCommand>
{
    public AtualizarImagemCamadaCommandValidator()
    {
        RuleFor(c => c.PlantaId).NotEmpty();
        RuleFor(c => c.CamadaId).NotEmpty();
        RuleFor(c => c.NomeArquivo).NotEmpty().MaximumLength(260);
        RuleFor(c => c.Conteudo).NotNull();
    }
}

public sealed class AtualizarImagemCamadaCommandHandler(
    IPlantaRepository plantaRepository,
    IHistoricoRepository historicoRepository,
    IArquivoStorage arquivoStorage,
    IUsuarioContext usuarioContext,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<AtualizarImagemCamadaCommand, CamadaDto>
{
    public async Task<CamadaDto> Handle(AtualizarImagemCamadaCommand request, CancellationToken cancellationToken)
    {
        var planta = await plantaRepository.ObterPorIdAsync(request.PlantaId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Planta '{request.PlantaId}' não encontrada.");

        var caminho = await arquivoStorage.SalvarAsync(request.NomeArquivo, request.Conteudo, cancellationToken);

        planta.AtualizarImagemRasterDaCamada(request.CamadaId, caminho);

        historicoRepository.Adicionar(new HistoricoAlteracao(
            nameof(Camada),
            request.CamadaId,
            TipoAcaoHistorico.CamadaImagemAtualizada,
            usuarioContext.UsuarioId,
            clock.AgoraUtc,
            planta.Id));

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        return planta.Camadas.First(c => c.Id == request.CamadaId).ParaDto();
    }
}
