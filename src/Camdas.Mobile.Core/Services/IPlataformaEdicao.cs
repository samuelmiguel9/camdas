namespace Camdas.Mobile.Services;

/// <summary>
/// Diz ao <see cref="Camdas.Mobile.ViewModels.PlantaViewModel"/> (compartilhado entre o app Android e
/// a Web) se esta plataforma pode aplicar edições de Camada direto no mestre (Android) ou precisa
/// registrá-las como pendentes de aprovação de um técnico no Android (Web, auxiliar). Cada app
/// registra sua própria implementação na injeção de dependência.
/// </summary>
public interface IPlataformaEdicao
{
    bool ExigeAprovacao { get; }

    /// <summary>
    /// Pede ao usuário quem é o responsável pela edição e o motivo — só chamado quando
    /// <see cref="ExigeAprovacao"/> é true. Retorna null se o usuário cancelar, caso em que a
    /// edição não deve nem ser aplicada nem solicitada.
    /// </summary>
    Task<(string Responsavel, string Motivo)?> PedirJustificativaAsync();
}
