using BellucSketch.Application.Abstractions;
using BellucSketch.Application.Common;
using BellucSketch.Domain.Entities;
using BellucSketch.Domain.Enums;
using MediatR;

namespace BellucSketch.Application.Camadas;

public sealed record RemoverCamadaCommand(Guid PlantaId, Guid CamadaId) : IRequest;

public sealed class RemoverCamadaCommandHandler(
    IPlantaRepository plantaRepository,
    IHistoricoRepository historicoRepository,
    IArquivoStorage arquivoStorage,
    IUsuarioContext usuarioContext,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<RemoverCamadaCommand>
{
    public async Task Handle(RemoverCamadaCommand request, CancellationToken cancellationToken)
    {
        var planta = await plantaRepository.ObterPorIdAsync(request.PlantaId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Planta '{request.PlantaId}' não encontrada.");

        // Guardado ANTES de remover — a camada (e a referência ao arquivo dela) deixa de existir no
        // banco depois de RemoverCamada, então sem isto o PNG dela ficaria órfão pra sempre.
        var caminhoImagem = planta.Camadas.First(c => c.Id == request.CamadaId).ImagemRasterCaminho;

        planta.RemoverCamada(request.CamadaId);

        historicoRepository.Adicionar(new HistoricoAlteracao(
            nameof(Camada),
            request.CamadaId,
            TipoAcaoHistorico.CamadaRemovida,
            usuarioContext.UsuarioId,
            clock.AgoraUtc,
            planta.Id));

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        if (caminhoImagem is not null)
            await arquivoStorage.ExcluirAsync(caminhoImagem, cancellationToken);
    }
}
