using Camdas.Application.Abstractions;
using Camdas.Application.Common;
using Camdas.Contracts;
using MediatR;

namespace Camdas.Application.Plantas;

public sealed record ObterPlantaQuery(Guid PlantaId) : IRequest<PlantaDto>;

public sealed class ObterPlantaQueryHandler(IPlantaRepository plantaRepository)
    : IRequestHandler<ObterPlantaQuery, PlantaDto>
{
    public async Task<PlantaDto> Handle(ObterPlantaQuery request, CancellationToken cancellationToken)
    {
        var planta = await plantaRepository.ObterPorIdAsync(request.PlantaId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Planta '{request.PlantaId}' não encontrada.");

        return planta.ParaDto();
    }
}
