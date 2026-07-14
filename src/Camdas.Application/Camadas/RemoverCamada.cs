using Camdas.Application.Abstractions;
using Camdas.Application.Common;
using Camdas.Domain.Entities;
using Camdas.Domain.Enums;
using MediatR;

namespace Camdas.Application.Camadas;

public sealed record RemoverCamadaCommand(Guid PlantaId, Guid CamadaId) : IRequest;

public sealed class RemoverCamadaCommandHandler(
    IPlantaRepository plantaRepository,
    IHistoricoRepository historicoRepository,
    IUsuarioContext usuarioContext,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<RemoverCamadaCommand>
{
    public async Task Handle(RemoverCamadaCommand request, CancellationToken cancellationToken)
    {
        var planta = await plantaRepository.ObterPorIdAsync(request.PlantaId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Planta '{request.PlantaId}' não encontrada.");

        planta.RemoverCamada(request.CamadaId);

        historicoRepository.Adicionar(new HistoricoAlteracao(
            nameof(Camada),
            request.CamadaId,
            TipoAcaoHistorico.CamadaRemovida,
            usuarioContext.UsuarioId,
            clock.AgoraUtc,
            planta.Id));

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);
    }
}
