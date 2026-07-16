using Camdas.Contracts;
using Camdas.Domain.Enums;

namespace Camdas.Mobile.Services;

/// <summary>
/// Cliente tipado do Camdas.Api — cada método corresponde a um endpoint. Usa exclusivamente tipos de
/// Camdas.Contracts/Camdas.Domain (enums), nunca Application/Infrastructure.
/// </summary>
public interface IApiClient
{
    Task<string> LoginDevAsync(Guid usuarioId, CancellationToken ct = default);

    Task<ProjetoDto> CriarProjetoAsync(CriarProjetoRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ProjetoDto>> ListarProjetosAsync(CancellationToken ct = default);
    Task<ProjetoDto> ObterProjetoAsync(Guid projetoId, CancellationToken ct = default);
    Task<ProjetoDto> RenomearProjetoAsync(Guid projetoId, string nome, CancellationToken ct = default);
    Task RemoverProjetoAsync(Guid projetoId, CancellationToken ct = default);
    Task<IReadOnlyList<PlantaDto>> ListarPlantasDoProjetoAsync(Guid projetoId, CancellationToken ct = default);

    Task<PlantaDto> ImportarPlantaAsync(
        Guid projetoId, string nome, string? descricao, string? nomeCliente, TipoArquivoOrigem tipo,
        string nomeArquivo, Stream conteudo, CancellationToken ct = default);
    Task<PlantaDto> ObterPlantaAsync(Guid plantaId, CancellationToken ct = default);
    Task<byte[]> ObterArquivoPlantaAsync(Guid plantaId, CancellationToken ct = default);
    Task RemoverPlantaAsync(Guid plantaId, CancellationToken ct = default);

    Task<CamadaDto> CriarCamadaAsync(Guid plantaId, string nome, CancellationToken ct = default);
    Task RemoverCamadaAsync(Guid plantaId, Guid camadaId, CancellationToken ct = default);
    Task<IReadOnlyList<CamadaDto>> ReordenarCamadasAsync(Guid plantaId, IReadOnlyList<Guid> ordemDosIds, CancellationToken ct = default);
    Task<CamadaDto> AlternarVisibilidadeCamadaAsync(Guid plantaId, Guid camadaId, CancellationToken ct = default);
    Task<CamadaDto> DefinirOpacidadeCamadaAsync(Guid plantaId, Guid camadaId, double opacidade, CancellationToken ct = default);
    Task<CamadaDto> BloquearCamadaAsync(Guid plantaId, Guid camadaId, CancellationToken ct = default);
    Task<CamadaDto> DesbloquearCamadaAsync(Guid plantaId, Guid camadaId, CancellationToken ct = default);
    Task<CamadaDto> BloquearAlphaCamadaAsync(Guid plantaId, Guid camadaId, CancellationToken ct = default);
    Task<CamadaDto> DesbloquearAlphaCamadaAsync(Guid plantaId, Guid camadaId, CancellationToken ct = default);
    Task<CamadaDto> LimparCamadaAsync(Guid plantaId, Guid camadaId, CancellationToken ct = default);
    Task<CamadaDto> DuplicarCamadaAsync(Guid plantaId, Guid camadaId, CancellationToken ct = default);
    Task<byte[]> ObterImagemCamadaAsync(Guid plantaId, Guid camadaId, CancellationToken ct = default);
    Task<CamadaDto> AtualizarImagemCamadaAsync(Guid plantaId, Guid camadaId, Stream conteudoPng, CancellationToken ct = default);

    Task<IReadOnlyList<HistoricoDto>> ObterHistoricoAsync(Guid plantaId, CancellationToken ct = default);

    Task<EdicaoPendenteDto> SolicitarEdicaoCamadaAsync(
        Guid plantaId, Guid? camadaId, TipoOperacaoEdicaoPendente tipoOperacao, string dadosDepoisJson,
        string responsavel, string motivo, CancellationToken ct = default);
    Task<IReadOnlyList<EdicaoPendenteDto>> ListarEdicoesPendentesAsync(Guid plantaId, CancellationToken ct = default);
    Task<CamadaDto?> AprovarEdicaoCamadaAsync(Guid plantaId, Guid edicaoId, CancellationToken ct = default);
    Task RejeitarEdicaoCamadaAsync(Guid plantaId, Guid edicaoId, string motivo, CancellationToken ct = default);
}
