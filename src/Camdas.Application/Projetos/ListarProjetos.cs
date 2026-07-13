using Camdas.Application.Abstractions;
using Camdas.Contracts;
using MediatR;

namespace Camdas.Application.Projetos;

public sealed record ListarProjetosQuery : IRequest<IReadOnlyList<ProjetoDto>>;

public sealed class ListarProjetosQueryHandler(IProjetoRepository projetoRepository)
    : IRequestHandler<ListarProjetosQuery, IReadOnlyList<ProjetoDto>>
{
    public async Task<IReadOnlyList<ProjetoDto>> Handle(ListarProjetosQuery request, CancellationToken cancellationToken)
    {
        var projetos = await projetoRepository.ListarAsync(cancellationToken);
        return projetos.Select(p => p.ParaDto()).ToList();
    }
}
