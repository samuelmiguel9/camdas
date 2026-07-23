using BellucSketch.Application.Abstractions;
using BellucSketch.Application.Common;
using BellucSketch.Contracts;
using BellucSketch.Domain.Enums;
using MediatR;

namespace BellucSketch.Application.Plantas;

public sealed record ObterPlantaQuery(Guid PlantaId) : IRequest<PlantaDto>;

public sealed class ObterPlantaQueryHandler(
    IPlantaRepository plantaRepository,
    IEdicaoPendenteRepository edicaoPendenteRepository,
    IHistoricoRepository historicoRepository,
    IUsuarioRepository usuarioRepository)
    : IRequestHandler<ObterPlantaQuery, PlantaDto>
{
    public async Task<PlantaDto> Handle(ObterPlantaQuery request, CancellationToken cancellationToken)
    {
        var planta = await plantaRepository.ObterPorIdAsync(request.PlantaId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Planta '{request.PlantaId}' não encontrada.");

        var edicoesPendentes = await edicaoPendenteRepository.ListarPendentesPorPlantaAsync(planta.Id, cancellationToken);

        // Resumo de "criado em/por" e "modificado em/por" — derivado do histórico de auditoria já
        // existente (HistoricoAlteracao), sem precisar guardar isso de novo em Planta: a criação é o
        // registro PlantaImportada (sempre o primeiro); a última modificação é só o registro mais
        // recente entre TODOS os desta planta (que pode ser o próprio PlantaImportada, se nada mais
        // aconteceu ainda). Histórico de plantas anteriores a este recurso pode não ter nada — nesse
        // caso os campos ficam null e a UI simplesmente não mostra a linha.
        var historico = await historicoRepository.ListarPorPlantaAsync(planta.Id, cancellationToken);
        var criacao = historico
            .Where(h => h.Acao == TipoAcaoHistorico.PlantaImportada)
            .MinBy(h => h.DataHora);
        var ultimaModificacao = historico.MaxBy(h => h.DataHora);

        Guid? criadoPorId = criacao?.UsuarioId;
        Guid? modificadoPorId = ultimaModificacao?.UsuarioId;

        var idsParaResolver = new[] { criadoPorId, modificadoPorId }.Where(id => id is not null).Select(id => id!.Value).Distinct();
        var nomesPorUsuarioId = new Dictionary<Guid, string>();
        foreach (var usuarioId in idsParaResolver)
        {
            var usuario = await usuarioRepository.ObterPorIdAsync(usuarioId, cancellationToken);
            if (usuario is not null)
                nomesPorUsuarioId[usuarioId] = usuario.Nome;
        }

        return planta.ParaDto() with
        {
            EdicoesPendentes = edicoesPendentes.Select(e => e.ParaDto()).ToList(),
            CriadoPorId = criadoPorId,
            NomeCriador = criadoPorId is { } id1 ? nomesPorUsuarioId.GetValueOrDefault(id1) : null,
            UltimaModificacaoEm = ultimaModificacao?.DataHora,
            UltimaModificacaoPorId = modificadoPorId,
            NomeUltimoModificador = modificadoPorId is { } id2 ? nomesPorUsuarioId.GetValueOrDefault(id2) : null,
        };
    }
}
