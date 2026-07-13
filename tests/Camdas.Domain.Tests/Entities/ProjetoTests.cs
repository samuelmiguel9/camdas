using Camdas.Domain.Common;
using Camdas.Domain.Entities;
using Camdas.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Camdas.Domain.Tests.Entities;

public class ProjetoTests
{
    [Fact]
    public void Deve_criar_projeto_ativo_por_padrao()
    {
        var projeto = new Projeto("Residência Alfa", "Reforma completa", Guid.NewGuid(), DateTime.UtcNow);

        projeto.Status.Should().Be(StatusProjeto.Ativo);
    }

    [Fact]
    public void Nao_deve_criar_projeto_sem_responsavel()
    {
        var criar = () => new Projeto("Residência Alfa", null, Guid.Empty, DateTime.UtcNow);

        criar.Should().Throw<DomainException>();
    }

    [Fact]
    public void Nao_deve_arquivar_projeto_ja_arquivado()
    {
        var projeto = new Projeto("Residência Alfa", null, Guid.NewGuid(), DateTime.UtcNow);
        projeto.Arquivar();

        var arquivarNovamente = () => projeto.Arquivar();

        arquivarNovamente.Should().Throw<DomainException>();
    }

    [Fact]
    public void Deve_reativar_projeto_arquivado()
    {
        var projeto = new Projeto("Residência Alfa", null, Guid.NewGuid(), DateTime.UtcNow);
        projeto.Arquivar();

        projeto.Reativar();

        projeto.Status.Should().Be(StatusProjeto.Ativo);
    }
}
