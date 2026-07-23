using BellucSketch.Domain.Common;
using BellucSketch.Domain.Enums;

namespace BellucSketch.Domain.Entities;

/// <summary>
/// Registro de auditoria de uma ação sobre qualquer entidade do domínio. É criado explicitamente
/// pela camada de Aplicação após cada operação relevante (não via eventos de domínio, para manter
/// o Domínio livre de infraestrutura de mensageria nesta fase do projeto).
/// </summary>
public sealed class HistoricoAlteracao : Entity
{
    public string EntidadeTipo { get; private set; } = null!;
    public Guid EntidadeId { get; private set; }
    public TipoAcaoHistorico Acao { get; private set; }
    public Guid UsuarioId { get; private set; }
    public DateTime DataHora { get; private set; }

    /// <summary>
    /// Planta à qual esta alteração pertence (a própria Planta, ou uma de suas Camadas/Cotas/
    /// Versões/Revisões). Nulo para ações que não pertencem a uma Planta (ex.: gestão de Usuario).
    /// Permite consultar o histórico completo de uma planta em uma única query.
    /// </summary>
    public Guid? PlantaId { get; private set; }

    public string? DadosAnterioresJson { get; private set; }
    public string? DadosNovosJson { get; private set; }

    private HistoricoAlteracao()
    {
    } // EF Core

    public HistoricoAlteracao(
        string entidadeTipo,
        Guid entidadeId,
        TipoAcaoHistorico acao,
        Guid usuarioId,
        DateTime dataHora,
        Guid? plantaId = null,
        string? dadosAnterioresJson = null,
        string? dadosNovosJson = null)
    {
        if (string.IsNullOrWhiteSpace(entidadeTipo))
            throw new DomainException("Tipo da entidade do histórico é obrigatório.");
        if (entidadeId == Guid.Empty)
            throw new DomainException("Histórico precisa referenciar uma entidade válida.");
        if (usuarioId == Guid.Empty)
            throw new DomainException("Histórico precisa registrar o usuário responsável pela ação.");

        EntidadeTipo = entidadeTipo;
        EntidadeId = entidadeId;
        Acao = acao;
        UsuarioId = usuarioId;
        DataHora = dataHora;
        PlantaId = plantaId;
        DadosAnterioresJson = dadosAnterioresJson;
        DadosNovosJson = dadosNovosJson;
    }
}
