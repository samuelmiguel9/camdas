using Camdas.Domain.Enums;

namespace Camdas.Contracts;

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
    IReadOnlyList<CamadaDto> Camadas);

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
