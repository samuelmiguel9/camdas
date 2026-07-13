using Camdas.Application.Abstractions;
using Camdas.Contracts;
using MediatR;

namespace Camdas.Application.Historico;

public sealed record ObterHistoricoDaPlantaQuery(Guid PlantaId) : IRequest<IReadOnlyList<HistoricoDto>>;

public sealed class ObterHistoricoDaPlantaQueryHandler(IHistoricoRepository historicoRepository)
    : IRequestHandler<ObterHistoricoDaPlantaQuery, IReadOnlyList<HistoricoDto>>
{
    public async Task<IReadOnlyList<HistoricoDto>> Handle(ObterHistoricoDaPlantaQuery request, CancellationToken cancellationToken)
    {
        // Ordem cronológica (mais antigo primeiro) — o histórico é lido como a linha do tempo de
        // como a planta chegou ao estado atual, não como um feed "mais recente primeiro".
        var historico = await historicoRepository.ListarPorPlantaAsync(request.PlantaId, cancellationToken);
        return historico.OrderBy(h => h.DataHora).Select(h => h.ParaDto()).ToList();
    }
}
