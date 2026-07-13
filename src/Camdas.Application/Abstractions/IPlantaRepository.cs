using Camdas.Domain.Entities;

namespace Camdas.Application.Abstractions;

/// <summary>
/// Planta é o agregado raiz: carregar por Id traz consigo Camadas e Cotas. Não existem
/// repositórios separados para essas entidades filhas — respeitar o agregado é responsabilidade
/// da implementação concreta (Infrastructure/EF Core).
/// </summary>
public interface IPlantaRepository
{
    Task<Planta?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Planta>> ListarPorProjetoAsync(Guid projetoId, CancellationToken cancellationToken);
    void Adicionar(Planta planta);
    void Remover(Planta planta);
}
