using BellucSketch.Application.Abstractions;
using BellucSketch.Application.Common;
using BellucSketch.Contracts;
using MediatR;

namespace BellucSketch.Application.Projetos;

public sealed record ObterProjetoQuery(Guid ProjetoId) : IRequest<ProjetoDto>;

public sealed class ObterProjetoQueryHandler(IProjetoRepository projetoRepository)
    : IRequestHandler<ObterProjetoQuery, ProjetoDto>
{
    public async Task<ProjetoDto> Handle(ObterProjetoQuery request, CancellationToken cancellationToken)
    {
        var projeto = await projetoRepository.ObterPorIdAsync(request.ProjetoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Projeto '{request.ProjetoId}' não encontrado.");

        return projeto.ParaDto();
    }
}
