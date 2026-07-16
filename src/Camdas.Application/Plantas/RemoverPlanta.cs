using Camdas.Application.Abstractions;
using Camdas.Application.Common;
using MediatR;

namespace Camdas.Application.Plantas;

public sealed record RemoverPlantaCommand(Guid PlantaId) : IRequest;

public sealed class RemoverPlantaCommandHandler(
    IPlantaRepository plantaRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<RemoverPlantaCommand>
{
    public async Task Handle(RemoverPlantaCommand request, CancellationToken cancellationToken)
    {
        var planta = await plantaRepository.ObterPorIdAsync(request.PlantaId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Planta '{request.PlantaId}' não encontrada.");

        plantaRepository.Remover(planta);

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);
    }
}
