using Camdas.Application.Abstractions;
using Camdas.Application.Common;
using MediatR;

namespace Camdas.Application.Projetos;

public sealed record RemoverProjetoCommand(Guid ProjetoId) : IRequest;

/// <summary>
/// Exclusão em cascata: apaga o projeto e todas as suas plantas — Camadas/Cotas de cada planta já
/// têm cascade delete configurado no banco (<see cref="Camdas.Infrastructure.Persistence.Configurations.PlantaConfiguration"/>/
/// CamadaConfiguration), então só precisamos remover explicitamente as Plantas (agregados
/// separados do Projeto) antes do Projeto em si.
/// </summary>
public sealed class RemoverProjetoCommandHandler(
    IProjetoRepository projetoRepository,
    IPlantaRepository plantaRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<RemoverProjetoCommand>
{
    public async Task Handle(RemoverProjetoCommand request, CancellationToken cancellationToken)
    {
        var projeto = await projetoRepository.ObterPorIdAsync(request.ProjetoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Projeto '{request.ProjetoId}' não encontrado.");

        var plantas = await plantaRepository.ListarPorProjetoAsync(request.ProjetoId, cancellationToken);
        foreach (var planta in plantas)
            plantaRepository.Remover(planta);

        projetoRepository.Remover(projeto);

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);
    }
}
