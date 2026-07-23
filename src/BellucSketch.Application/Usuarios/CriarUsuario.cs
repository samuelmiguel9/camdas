using BellucSketch.Application.Abstractions;
using BellucSketch.Contracts;
using BellucSketch.Domain.Common;
using BellucSketch.Domain.Entities;
using FluentValidation;
using MediatR;

namespace BellucSketch.Application.Usuarios;

public sealed record CriarUsuarioCommand(string Nome, string Email) : IRequest<UsuarioDto>;

public sealed class CriarUsuarioCommandValidator : AbstractValidator<CriarUsuarioCommand>
{
    public CriarUsuarioCommandValidator()
    {
        RuleFor(c => c.Nome).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Email).NotEmpty().EmailAddress().MaximumLength(200);
    }
}

public sealed class CriarUsuarioCommandHandler(IUsuarioRepository usuarioRepository, IUnitOfWork unitOfWork)
    : IRequestHandler<CriarUsuarioCommand, UsuarioDto>
{
    public async Task<UsuarioDto> Handle(CriarUsuarioCommand request, CancellationToken cancellationToken)
    {
        if (await usuarioRepository.ExisteComEmailAsync(request.Email, cancellationToken))
            throw new DomainException("Já existe um usuário com este e-mail.");

        var usuario = new Usuario(request.Nome, request.Email);

        usuarioRepository.Adicionar(usuario);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        return usuario.ParaDto();
    }
}
