using Camdas.Application.Abstractions;
using Camdas.Application.Common;
using Camdas.Contracts;
using MediatR;

namespace Camdas.Application.Projetos;

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
