using BellucSketch.Application.Abstractions;
using BellucSketch.Application.Common;
using BellucSketch.Contracts;
using MediatR;

namespace BellucSketch.Application.Plantas;

public sealed record ObterPlantaQuery(Guid PlantaId) : IRequest<PlantaDto>;

public sealed class ObterPlantaQueryHandler(IPlantaRepository plantaRepository, IEdicaoPendenteRepository edicaoPendenteRepository)
    : IRequestHandler<ObterPlantaQuery, PlantaDto>
{
    public async Task<PlantaDto> Handle(ObterPlantaQuery request, CancellationToken cancellationToken)
    {
        var planta = await plantaRepository.ObterPorIdAsync(request.PlantaId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Planta '{request.PlantaId}' não encontrada.");

        var edicoesPendentes = await edicaoPendenteRepository.ListarPendentesPorPlantaAsync(planta.Id, cancellationToken);

        return planta.ParaDto() with { EdicoesPendentes = edicoesPendentes.Select(e => e.ParaDto()).ToList() };
    }
}
