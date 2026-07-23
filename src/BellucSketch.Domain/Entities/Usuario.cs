using BellucSketch.Domain.Common;

namespace BellucSketch.Domain.Entities;

public sealed class Usuario : Entity
{
    public string Nome { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public bool Ativo { get; private set; }

    private Usuario()
    {
    } // EF Core

    public Usuario(string nome, string email)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("Nome do usuário é obrigatório.");
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new DomainException("E-mail do usuário é inválido.");

        Nome = nome;
        Email = email;
        Ativo = true;
    }

    public void Desativar() => Ativo = false;

    public void Ativar() => Ativo = true;
}
