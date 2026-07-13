using Camdas.Application.Abstractions;
using Camdas.Application.Common;
using Camdas.Contracts;
using Camdas.Domain.Entities;
using Camdas.Domain.Enums;
using FluentValidation;
using MediatR;

namespace Camdas.Application.Camadas;

public sealed record ReordenarCamadasCommand(Guid PlantaId, IReadOnlyList<Guid> OrdemDosIds) : IRequest<IReadOnlyList<CamadaDto>>;

public sealed class ReordenarCamadasCommandValidator : AbstractValidator<ReordenarCamadasCommand>
{
    public ReordenarCamadasCommandValidator()
    {
        RuleFor(c => c.PlantaId).NotEmpty();
        RuleFor(c => c.OrdemDosIds).NotEmpty();
    }
}

public sealed class ReordenarCamadasCommandHandler(
    IPlantaRepository plantaRepository,
    IHistoricoRepository historicoRepository,
    IUsuarioContext usuarioContext,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<ReordenarCamadasCommand, IReadOnlyList<CamadaDto>>
{
    public async Task<IReadOnlyList<CamadaDto>> Handle(ReordenarCamadasCommand request, CancellationToken cancellationToken)
    {
        var planta = await plantaRepository.ObterPorIdAsync(request.PlantaId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Planta '{request.PlantaId}' não encontrada.");

        planta.ReordenarCamadas(request.OrdemDosIds);

        historicoRepository.Adicionar(new HistoricoAlteracao(
            nameof(Planta),
            planta.Id,
            TipoAcaoHistorico.CamadaReordenada,
            usuarioContext.UsuarioId,
            clock.AgoraUtc,
            planta.Id));

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        return planta.Camadas.Select(c => c.ParaDto()).ToList();
    }
}
