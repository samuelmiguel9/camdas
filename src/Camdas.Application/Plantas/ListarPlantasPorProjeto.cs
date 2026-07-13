using Camdas.Application.Abstractions;
using Camdas.Contracts;
using MediatR;

namespace Camdas.Application.Plantas;

public sealed record ListarPlantasPorProjetoQuery(Guid ProjetoId) : IRequest<IReadOnlyList<PlantaDto>>;

public sealed class ListarPlantasPorProjetoQueryHandler(IPlantaRepository plantaRepository)
    : IRequestHandler<ListarPlantasPorProjetoQuery, IReadOnlyList<PlantaDto>>
{
    public async Task<IReadOnlyList<PlantaDto>> Handle(ListarPlantasPorProjetoQuery request, CancellationToken cancellationToken)
    {
        var plantas = await plantaRepository.ListarPorProjetoAsync(request.ProjetoId, cancellationToken);
        return plantas.Select(p => p.ParaDto()).ToList();
    }
}
