using BellucSketch.Application.Abstractions;
using BellucSketch.Application.Common;
using BellucSketch.Contracts;
using FluentValidation;
using MediatR;

namespace BellucSketch.Application.Projetos;

public sealed record RenomearProjetoCommand(Guid ProjetoId, string Nome) : IRequest<ProjetoDto>;

public sealed class RenomearProjetoCommandValidator : AbstractValidator<RenomearProjetoCommand>
{
    public RenomearProjetoCommandValidator()
    {
        RuleFor(c => c.ProjetoId).NotEmpty();
        RuleFor(c => c.Nome).NotEmpty().MaximumLength(200);
    }
}

public sealed class RenomearProjetoCommandHandler(
    IProjetoRepository projetoRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<RenomearProjetoCommand, ProjetoDto>
{
    public async Task<ProjetoDto> Handle(RenomearProjetoCommand request, CancellationToken cancellationToken)
    {
        var projeto = await projetoRepository.ObterPorIdAsync(request.ProjetoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Projeto '{request.ProjetoId}' não encontrado.");

        projeto.Renomear(request.Nome);

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        return projeto.ParaDto();
    }
}
