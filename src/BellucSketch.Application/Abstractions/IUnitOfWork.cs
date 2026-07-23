namespace BellucSketch.Application.Abstractions;

/// <summary>
/// Confirma, em uma única transação, todas as alterações feitas nos repositórios durante o
/// tratamento de um caso de uso.
/// </summary>
public interface IUnitOfWork
{
    Task SalvarAlteracoesAsync(CancellationToken cancellationToken);
}
