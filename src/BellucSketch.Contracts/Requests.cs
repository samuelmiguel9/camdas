using BellucSketch.Domain.Enums;

namespace BellucSketch.Contracts;

public sealed record CriarProjetoRequest(string Nome, string? Descricao);

public sealed record RenomearProjetoRequest(string Nome);

public sealed record CriarCamadaRequest(string Nome);

public sealed record ReordenarCamadasRequest(IReadOnlyList<Guid> OrdemDosIds);

public sealed record DefinirOpacidadeCamadaRequest(double Opacidade);

public sealed record EmitirTokenRequest(Guid UsuarioId);

public sealed record EmitirTokenResponse(string Token);

public sealed record CriarUsuarioRequest(string Nome, string Email);

public sealed record SolicitarEdicaoCamadaRequest(
    Guid? CamadaId,
    TipoOperacaoEdicaoPendente TipoOperacao,
    string DadosDepoisJson,
    string Responsavel,
    string Motivo);

public sealed record RejeitarEdicaoCamadaRequest(string Motivo);

/// <summary>
/// Nomes dos campos do formulário multipart usado por POST /api/plantas — evita strings mágicas
/// duplicadas entre a Api ([FromForm(Name = ...)]) e um futuro cliente HTTP do app Mobile.
/// </summary>
public static class ImportarPlantaCampos
{
    public const string ProjetoId = "projetoId";
    public const string Nome = "nome";
    public const string Descricao = "descricao";
    public const string NomeCliente = "nomeCliente";
    public const string TipoArquivoOrigem = "tipoArquivoOrigem";
    public const string Arquivo = "arquivo";
}
