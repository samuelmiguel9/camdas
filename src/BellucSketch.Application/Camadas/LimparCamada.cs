using BellucSketch.Application.Abstractions;
using BellucSketch.Application.Common;
using BellucSketch.Contracts;
using BellucSketch.Domain.Entities;
using BellucSketch.Domain.Enums;
using MediatR;

namespace BellucSketch.Application.Camadas;

public sealed record LimparCamadaCommand(Guid PlantaId, Guid CamadaId) : IRequest<CamadaDto>;

public sealed class LimparCamadaCommandHandler(
    IPlantaRepository plantaRepository,
    IHistoricoRepository historicoRepository,
    IArquivoStorage arquivoStorage,
    IUsuarioContext usuarioContext,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<LimparCamadaCommand, CamadaDto>
{
    public async Task<CamadaDto> Handle(LimparCamadaCommand request, CancellationToken cancellationToken)
    {
        var planta = await plantaRepository.ObterPorIdAsync(request.PlantaId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Planta '{request.PlantaId}' não encontrada.");

        // Guardado ANTES de limpar — Planta.LimparCamada zera a referência (ImagemRasterCaminho =
        // null), então sem isto o PNG antigo ficaria órfão no armazenamento sem NENHUMA referência
        // que permitisse achá-lo/apagá-lo depois.
        var caminhoAnterior = planta.Camadas.First(c => c.Id == request.CamadaId).ImagemRasterCaminho;

        planta.LimparCamada(request.CamadaId);

        historicoRepository.Adicionar(new HistoricoAlteracao(
            nameof(Camada),
            request.CamadaId,
            TipoAcaoHistorico.CamadaLimpada,
            usuarioContext.UsuarioId,
            clock.AgoraUtc,
            planta.Id));

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        if (caminhoAnterior is not null)
            await arquivoStorage.ExcluirAsync(caminhoAnterior, cancellationToken);

        return planta.Camadas.First(c => c.Id == request.CamadaId).ParaDto();
    }
}
