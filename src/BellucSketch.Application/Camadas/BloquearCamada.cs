using BellucSketch.Application.Abstractions;
using BellucSketch.Application.Common;
using BellucSketch.Contracts;
using BellucSketch.Domain.Entities;
using BellucSketch.Domain.Enums;
using MediatR;

namespace BellucSketch.Application.Camadas;

public sealed record BloquearCamadaCommand(Guid PlantaId, Guid CamadaId) : IRequest<CamadaDto>;

public sealed class BloquearCamadaCommandHandler(
    IPlantaRepository plantaRepository,
    IHistoricoRepository historicoRepository,
    IUsuarioContext usuarioContext,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<BloquearCamadaCommand, CamadaDto>
{
    public async Task<CamadaDto> Handle(BloquearCamadaCommand request, CancellationToken cancellationToken)
    {
        var planta = await plantaRepository.ObterPorIdAsync(request.PlantaId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Planta '{request.PlantaId}' não encontrada.");

        planta.BloquearCamada(request.CamadaId);

        historicoRepository.Adicionar(new HistoricoAlteracao(
            nameof(Camada),
            request.CamadaId,
            TipoAcaoHistorico.CamadaBloqueada,
            usuarioContext.UsuarioId,
            clock.AgoraUtc,
            planta.Id));

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        return planta.Camadas.First(c => c.Id == request.CamadaId).ParaDto();
    }
}
