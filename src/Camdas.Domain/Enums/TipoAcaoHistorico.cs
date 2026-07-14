namespace Camdas.Domain.Enums;

/// <summary>
/// Ações auditáveis registradas em HistoricoAlteracao. A gravação do histórico é responsabilidade
/// da camada de Aplicação (casos de uso), que registra uma entrada após cada operação relevante
/// no agregado Planta.
/// </summary>
public enum TipoAcaoHistorico
{
    PlantaImportada,
    CamadaAdicionada,
    CamadaRemovida,
    CamadaVisibilidadeAlterada,
    CamadaBloqueada,
    CamadaDesbloqueada,
    CamadaImagemAtualizada,
    CamadaReordenada,
    CamadaOpacidadeAlterada,
    CamadaAlphaBloqueado,
    CamadaAlphaDesbloqueado,
    CamadaLimpada,
    CamadaDuplicada
}
