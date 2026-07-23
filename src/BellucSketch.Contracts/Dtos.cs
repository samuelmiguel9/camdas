using BellucSketch.Domain.Enums;

namespace BellucSketch.Contracts;

public sealed record ProjetoDto(
    Guid Id,
    string Nome,
    string? Descricao,
    Guid CriadoPorId,
    DateTime DataCriacao,
    StatusProjeto Status);

public sealed record CamadaDto(
    Guid Id,
    Guid PlantaId,
    string Nome,
    bool Visivel,
    bool Bloqueada,
    bool BloqueioAlpha,
    int Ordem,
    bool TemImagemRaster,
    double Opacidade);

public sealed record PlantaDto(
    Guid Id,
    Guid ProjetoId,
    string Nome,
    string? Descricao,
    string? NomeCliente,
    TipoArquivoOrigem TipoArquivoOrigem,
    string CaminhoArquivoOriginal,
    DateTime DataImportacao,
    IReadOnlyList<CamadaDto> Camadas,
    IReadOnlyList<EdicaoPendenteDto> EdicoesPendentes);

public sealed record UsuarioDto(Guid Id, string Nome, string Email, bool Ativo);

public sealed record HistoricoDto(
    Guid Id,
    string EntidadeTipo,
    Guid EntidadeId,
    Guid? PlantaId,
    TipoAcaoHistorico Acao,
    Guid UsuarioId,
    DateTime DataHora,
    string? DadosAnterioresJson,
    string? DadosNovosJson);

public sealed record EdicaoPendenteDto(
    Guid Id,
    Guid PlantaId,
    Guid? CamadaId,
    TipoOperacaoEdicaoPendente TipoOperacao,
    string? DadosAntesJson,
    string DadosDepoisJson,
    string Responsavel,
    string Motivo,
    DateTime DataSolicitacao,
    StatusEdicaoPendente Status,
    DateTime? DataResposta,
    string? MotivoRejeicao);
