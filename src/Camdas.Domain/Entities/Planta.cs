using Camdas.Domain.Common;
using Camdas.Domain.Enums;

namespace Camdas.Domain.Entities;

/// <summary>
/// Agregado raiz do domínio. Toda leitura/alteração de Camadas passa por aqui — o construtor de
/// Camada é "internal" justamente para impedir que Application/Infrastructure/Api/Mobile a
/// manipulem sem respeitar as invariantes da Planta.
/// </summary>
public sealed class Planta : Entity
{
    private readonly List<Camada> _camadas = new();

    public Guid ProjetoId { get; private set; }
    public string Nome { get; private set; } = null!;
    public string? Descricao { get; private set; }
    public string? NomeCliente { get; private set; }
    public TipoArquivoOrigem TipoArquivoOrigem { get; private set; }
    public string CaminhoArquivoOriginal { get; private set; } = null!;
    public DateTime DataImportacao { get; private set; }

    public IReadOnlyCollection<Camada> Camadas => _camadas.OrderBy(c => c.Ordem).ToList();

    private Planta()
    {
    } // EF Core

    public Planta(
        Guid projetoId,
        Guid importadoPorId,
        string nome,
        string? descricao,
        string? nomeCliente,
        TipoArquivoOrigem tipoArquivoOrigem,
        string caminhoArquivoOriginal,
        DateTime dataImportacao)
    {
        if (projetoId == Guid.Empty)
            throw new DomainException("Planta precisa estar vinculada a um projeto.");
        if (importadoPorId == Guid.Empty)
            throw new DomainException("Planta precisa registrar quem realizou a importação.");
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("Nome da planta é obrigatório.");
        if (string.IsNullOrWhiteSpace(caminhoArquivoOriginal))
            throw new DomainException("Caminho do arquivo original é obrigatório.");

        ProjetoId = projetoId;
        Nome = nome;
        Descricao = descricao;
        NomeCliente = nomeCliente;
        TipoArquivoOrigem = tipoArquivoOrigem;
        CaminhoArquivoOriginal = caminhoArquivoOriginal;
        DataImportacao = dataImportacao;
    }

    /// <summary>Planta importa sem nenhuma camada — o usuário cria todas manualmente.</summary>
    public Camada AdicionarCamada(string nome)
    {
        var camada = new Camada(Id, nome, _camadas.Count + 1);
        _camadas.Add(camada);
        return camada;
    }

    /// <summary>
    /// Redefine a prioridade/ordem de exibição das camadas — <paramref name="ordemDosIds"/> precisa
    /// conter exatamente os Ids das camadas existentes, na nova ordem desejada (índice 0 vira
    /// prioridade 1, e assim por diante).
    /// </summary>
    public void ReordenarCamadas(IReadOnlyList<Guid> ordemDosIds)
    {
        if (ordemDosIds.Count != _camadas.Count ||
            ordemDosIds.Distinct().Count() != _camadas.Count ||
            !ordemDosIds.All(id => _camadas.Any(c => c.Id == id)))
            throw new DomainException("A nova ordem precisa conter exatamente as camadas existentes desta planta.");

        for (var i = 0; i < ordemDosIds.Count; i++)
            ObterCamada(ordemDosIds[i]).DefinirOrdem(i + 1);
    }

    public void RemoverCamada(Guid camadaId) => _camadas.Remove(ObterCamada(camadaId));

    public void AlternarVisibilidadeCamada(Guid camadaId) => ObterCamada(camadaId).AlternarVisibilidade();

    public void DefinirOpacidadeCamada(Guid camadaId, double opacidade) => ObterCamada(camadaId).DefinirOpacidade(opacidade);

    public void BloquearCamada(Guid camadaId) => ObterCamada(camadaId).Bloquear();

    public void DesbloquearCamada(Guid camadaId) => ObterCamada(camadaId).Desbloquear();

    /// <summary>
    /// Atualiza o traço livre (raster) da camada com o PNG salvo em <paramref name="caminho"/>.
    /// Respeita o mesmo bloqueio de camada usado por Cotas.
    /// </summary>
    public void AtualizarImagemRasterDaCamada(Guid camadaId, string caminho) =>
        ObterCamada(camadaId).AtualizarImagemRaster(caminho);

    private Camada ObterCamada(Guid camadaId)
    {
        var camada = _camadas.FirstOrDefault(c => c.Id == camadaId);
        if (camada is null)
            throw new DomainException("Camada não encontrada nesta planta.");

        return camada;
    }
}
