using Camdas.Domain.Common;
using Camdas.Domain.Entities;
using Camdas.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Camdas.Domain.Tests.Entities;

public class HistoricoAlteracaoTests
{
    [Fact]
    public void Deve_criar_registro_de_historico_vinculado_a_uma_planta()
    {
        var plantaId = Guid.NewGuid();
        var entidadeId = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();

        var historico = new HistoricoAlteracao(
            nameof(Camada), entidadeId, TipoAcaoHistorico.CamadaAdicionada, usuarioId, DateTime.UtcNow, plantaId);

        historico.PlantaId.Should().Be(plantaId);
        historico.Acao.Should().Be(TipoAcaoHistorico.CamadaAdicionada);
    }

    [Theory]
    [InlineData("", "campo")]
    public void Nao_deve_aceitar_tipo_de_entidade_vazio(string entidadeTipo, string _)
    {
        var criar = () => new HistoricoAlteracao(
            entidadeTipo, Guid.NewGuid(), TipoAcaoHistorico.CamadaAdicionada, Guid.NewGuid(), DateTime.UtcNow);

        criar.Should().Throw<DomainException>();
    }

    [Fact]
    public void Nao_deve_aceitar_entidade_ou_usuario_vazios()
    {
        var semEntidade = () => new HistoricoAlteracao(
            nameof(Camada), Guid.Empty, TipoAcaoHistorico.CamadaAdicionada, Guid.NewGuid(), DateTime.UtcNow);
        var semUsuario = () => new HistoricoAlteracao(
            nameof(Camada), Guid.NewGuid(), TipoAcaoHistorico.CamadaAdicionada, Guid.Empty, DateTime.UtcNow);

        semEntidade.Should().Throw<DomainException>();
        semUsuario.Should().Throw<DomainException>();
    }
}
