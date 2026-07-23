using BellucSketch.Application.Abstractions;
using BellucSketch.Application.Common;
using BellucSketch.Contracts;
using BellucSketch.Domain.Entities;
using BellucSketch.Domain.Enums;
using MediatR;

namespace BellucSketch.Application.Camadas;

public sealed record DefinirOpacidadeCamadaCommand(Guid PlantaId, Guid CamadaId, double Opacidade) : IRequest<CamadaDto>;

public sealed class DefinirOpacidadeCamadaCommandHandler(
    IPlantaRepository plantaRepository,
    IHistoricoRepository historicoRepository,
    IUsuarioContext usuarioContext,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<DefinirOpacidadeCamadaCommand, CamadaDto>
{
    public async Task<CamadaDto> Handle(DefinirOpacidadeCamadaCommand request, CancellationToken cancellationToken)
    {
        var planta = await plantaRepository.ObterPorIdAsync(request.PlantaId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Planta '{request.PlantaId}' não encontrada.");

        planta.DefinirOpacidadeCamada(request.CamadaId, request.Opacidade);

        historicoRepository.Adicionar(new HistoricoAlteracao(
            nameof(Camada),
            request.CamadaId,
            TipoAcaoHistorico.CamadaOpacidadeAlterada,
            usuarioContext.UsuarioId,
            clock.AgoraUtc,
            planta.Id));

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        return planta.Camadas.First(c => c.Id == request.CamadaId).ParaDto();
    }
}
