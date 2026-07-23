using BellucSketch.Application.Abstractions;
using BellucSketch.Contracts;
using MediatR;

namespace BellucSketch.Application.EdicoesPendentes;

public sealed record ListarEdicoesPendentesQuery(Guid PlantaId) : IRequest<IReadOnlyList<EdicaoPendenteDto>>;

public sealed class ListarEdicoesPendentesQueryHandler(IEdicaoPendenteRepository edicaoPendenteRepository)
    : IRequestHandler<ListarEdicoesPendentesQuery, IReadOnlyList<EdicaoPendenteDto>>
{
    public async Task<IReadOnlyList<EdicaoPendenteDto>> Handle(ListarEdicoesPendentesQuery request, CancellationToken cancellationToken)
    {
        var edicoes = await edicaoPendenteRepository.ListarPendentesPorPlantaAsync(request.PlantaId, cancellationToken);
        return edicoes.Select(e => e.ParaDto()).ToList();
    }
}
