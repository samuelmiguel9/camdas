using BellucSketch.Domain.Entities;

namespace BellucSketch.Application.Abstractions;

public interface IEdicaoPendenteRepository
{
    void Adicionar(EdicaoPendenteCamada edicao);
    Task<EdicaoPendenteCamada?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<EdicaoPendenteCamada>> ListarPendentesPorPlantaAsync(Guid plantaId, CancellationToken cancellationToken);
}
