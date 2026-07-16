using Camdas.Domain.Common;
using Camdas.Domain.Enums;

namespace Camdas.Domain.Entities;

/// <summary>
/// Edição proposta por um usuário da Web numa Camada, aguardando aprovação de um técnico no
/// Android (mestre). É uma entidade independente (não faz parte do agregado Planta) porque precisa
/// ser consultada isoladamente — um técnico revisa uma fila de edições pendentes sem carregar a
/// Planta inteira, do mesmo jeito que <see cref="HistoricoAlteracao"/> já funciona.
///
/// Enquanto pendente, a Camada em si permanece inalterada (o mestre não muda sozinho): a proposta
/// só é aplicada à Planta/Camada quando <see cref="Aprovar"/> é chamado pelo caso de uso de
/// aprovação, que também é responsável por interpretar <see cref="DadosDepoisJson"/> de acordo com
/// <see cref="TipoOperacao"/> e executar a mudança real no agregado.
/// </summary>
public sealed class EdicaoPendenteCamada : Entity
{
    public Guid PlantaId { get; private set; }

    /// <summary>Nulo apenas para <see cref="TipoOperacaoEdicaoPendente.Reordenar"/>, que se aplica a
    /// todas as camadas da planta de uma vez.</summary>
    public Guid? CamadaId { get; private set; }

    public TipoOperacaoEdicaoPendente TipoOperacao { get; private set; }

    /// <summary>Estado relevante antes da proposta, capturado no momento da solicitação — permite ao
    /// técnico comparar "de/para" ao revisar, sem precisar reconstruir o estado anterior.</summary>
    public string? DadosAntesJson { get; private set; }

    /// <summary>Estado proposto (ex.: {"opacidade":0.5} ou {"ordemDosIds":[...]}). Vazio ("{}") para
    /// operações que não carregam dado extra (visibilidade/bloqueio são só um toggle; exclusão não
    /// precisa de payload).</summary>
    public string DadosDepoisJson { get; private set; } = null!;

    /// <summary>Nome do técnico responsável pela edição, informado livremente por quem estiver
    /// usando a Web (ela não exige necessariamente um técnico específico logado por dispositivo).</summary>
    public string Responsavel { get; private set; } = null!;

    public string Motivo { get; private set; } = null!;
    public DateTime DataSolicitacao { get; private set; }

    public StatusEdicaoPendente Status { get; private set; }
    public DateTime? DataResposta { get; private set; }
    public string? MotivoRejeicao { get; private set; }

    private EdicaoPendenteCamada()
    {
    } // EF Core

    public EdicaoPendenteCamada(
        Guid plantaId,
        Guid? camadaId,
        TipoOperacaoEdicaoPendente tipoOperacao,
        string dadosDepoisJson,
        string responsavel,
        string motivo,
        DateTime dataSolicitacao,
        string? dadosAntesJson = null)
    {
        if (plantaId == Guid.Empty)
            throw new DomainException("Edição pendente precisa referenciar uma planta válida.");
        if (camadaId is null && tipoOperacao != TipoOperacaoEdicaoPendente.Reordenar)
            throw new DomainException("Edição pendente precisa referenciar uma camada, exceto ao reordenar.");
        if (string.IsNullOrWhiteSpace(dadosDepoisJson))
            throw new DomainException("Edição pendente precisa descrever o estado proposto.");
        if (string.IsNullOrWhiteSpace(responsavel))
            throw new DomainException("Edição pendente precisa de um responsável.");
        if (string.IsNullOrWhiteSpace(motivo))
            throw new DomainException("Edição pendente precisa de um motivo.");

        PlantaId = plantaId;
        CamadaId = camadaId;
        TipoOperacao = tipoOperacao;
        DadosDepoisJson = dadosDepoisJson;
        DadosAntesJson = dadosAntesJson;
        Responsavel = responsavel;
        Motivo = motivo;
        DataSolicitacao = dataSolicitacao;
        Status = StatusEdicaoPendente.Pendente;
    }

    public void Aprovar(DateTime dataResposta)
    {
        GarantirPendente();
        Status = StatusEdicaoPendente.Aprovada;
        DataResposta = dataResposta;
    }

    public void Rejeitar(string motivoRejeicao, DateTime dataResposta)
    {
        if (string.IsNullOrWhiteSpace(motivoRejeicao))
            throw new DomainException("Rejeição precisa de um motivo.");

        GarantirPendente();
        Status = StatusEdicaoPendente.Rejeitada;
        MotivoRejeicao = motivoRejeicao;
        DataResposta = dataResposta;
    }

    private void GarantirPendente()
    {
        if (Status != StatusEdicaoPendente.Pendente)
            throw new DomainException("Esta edição já foi respondida.");
    }
}
