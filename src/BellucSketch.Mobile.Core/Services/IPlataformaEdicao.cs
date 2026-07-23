using BellucSketch.Domain.Enums;

namespace BellucSketch.Mobile.Services;

/// <summary>
/// Diz ao <see cref="BellucSketch.Mobile.ViewModels.PlantaViewModel"/> (compartilhado entre o app Android e
/// a Web) se um tipo de edição de Camada pode ser aplicado direto no mestre (Android, sempre) ou
/// precisa virar uma proposta pendente de aprovação de um técnico no Android (Web, só pra edições
/// que mexem no traço em si — desenhar por cima/apagar; visibilidade, opacidade, bloqueio e ordem
/// são livres por não serem alterações críticas). Cada app registra sua própria implementação na
/// injeção de dependência.
/// </summary>
public interface IPlataformaEdicao
{
    bool PrecisaAprovacao(TipoOperacaoEdicaoPendente tipoOperacao);

    /// <summary>
    /// Pede ao usuário quem é o responsável pela edição e o motivo — só chamado quando
    /// <see cref="PrecisaAprovacao"/> retorna true. Retorna null se o usuário cancelar, caso em que a
    /// edição não deve nem ser aplicada nem solicitada.
    /// </summary>
    Task<(string Responsavel, string Motivo)?> PedirJustificativaAsync();
}
