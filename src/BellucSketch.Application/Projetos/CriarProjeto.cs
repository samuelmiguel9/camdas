using BellucSketch.Application.Abstractions;
using BellucSketch.Contracts;
using BellucSketch.Domain.Entities;
using FluentValidation;
using MediatR;

namespace BellucSketch.Application.Projetos;

public sealed record CriarProjetoCommand(string Nome, string? Descricao) : IRequest<ProjetoDto>;

public sealed class CriarProjetoCommandValidator : AbstractValidator<CriarProjetoCommand>
{
    public CriarProjetoCommandValidator()
    {
        RuleFor(c => c.Nome).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Descricao).MaximumLength(2000);
    }
}

public sealed class CriarProjetoCommandHandler(
    IProjetoRepository projetoRepository,
    IUsuarioContext usuarioContext,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<CriarProjetoCommand, ProjetoDto>
{
    public async Task<ProjetoDto> Handle(CriarProjetoCommand request, CancellationToken cancellationToken)
    {
        var projeto = new Projeto(request.Nome, request.Descricao, usuarioContext.UsuarioId, clock.AgoraUtc);

        projetoRepository.Adicionar(projeto);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        return projeto.ParaDto();
    }
}
