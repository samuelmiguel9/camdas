using Camdas.Domain.Entities;

namespace Camdas.Contracts;

/// <summary>
/// Conversão de entidades de domínio para os DTOs de saída (usada pelos casos de uso da Application
/// ao montar a resposta de cada handler).
/// </summary>
public static class Mapeamentos
{
    public static ProjetoDto ParaDto(this Projeto projeto) =>
        new(projeto.Id, projeto.Nome, projeto.Descricao, projeto.CriadoPorId, projeto.DataCriacao, projeto.Status);

    public static CamadaDto ParaDto(this Camada camada) =>
        new(
            camada.Id,
            camada.PlantaId,
            camada.Nome,
            camada.Visivel,
            camada.Bloqueada,
            camada.BloqueioAlpha,
            camada.Ordem,
            camada.ImagemRasterCaminho is not null,
            camada.Opacidade);

    public static PlantaDto ParaDto(this Planta planta) =>
        new(
            planta.Id,
            planta.ProjetoId,
            planta.Nome,
            planta.Descricao,
            planta.NomeCliente,
            planta.TipoArquivoOrigem,
            planta.CaminhoArquivoOriginal,
            planta.DataImportacao,
            planta.Camadas.Select(c => c.ParaDto()).ToList(),
            Array.Empty<EdicaoPendenteDto>());

    public static UsuarioDto ParaDto(this Usuario usuario) =>
        new(usuario.Id, usuario.Nome, usuario.Email, usuario.Ativo);

    public static HistoricoDto ParaDto(this HistoricoAlteracao historico) =>
        new(
            historico.Id,
            historico.EntidadeTipo,
            historico.EntidadeId,
            historico.PlantaId,
            historico.Acao,
            historico.UsuarioId,
            historico.DataHora,
            historico.DadosAnterioresJson,
            historico.DadosNovosJson);

    public static EdicaoPendenteDto ParaDto(this EdicaoPendenteCamada edicao) =>
        new(
            edicao.Id,
            edicao.PlantaId,
            edicao.CamadaId,
            edicao.TipoOperacao,
            edicao.DadosAntesJson,
            edicao.DadosDepoisJson,
            edicao.Responsavel,
            edicao.Motivo,
            edicao.DataSolicitacao,
            edicao.Status,
            edicao.DataResposta,
            edicao.MotivoRejeicao);
}
