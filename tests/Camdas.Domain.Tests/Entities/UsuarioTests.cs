using Camdas.Domain.Common;
using Camdas.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Camdas.Domain.Tests.Entities;

public class UsuarioTests
{
    [Fact]
    public void Nao_deve_aceitar_email_sem_arroba()
    {
        var criar = () => new Usuario("Ana Souza", "ana-empresa.com");

        criar.Should().Throw<DomainException>();
    }

    [Fact]
    public void Nao_deve_aceitar_nome_vazio()
    {
        var criar = () => new Usuario("", "ana@empresa.com");

        criar.Should().Throw<DomainException>();
    }

    [Fact]
    public void Deve_permitir_ativar_e_desativar()
    {
        var usuario = new Usuario("Ana Souza", "ana@empresa.com");

        usuario.Desativar();
        usuario.Ativo.Should().BeFalse();

        usuario.Ativar();
        usuario.Ativo.Should().BeTrue();
    }
}
