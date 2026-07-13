using Camdas.Application.Abstractions;
using Camdas.Application.Common;
using Camdas.Contracts;
using Camdas.Domain.Entities;
using Camdas.Domain.Enums;
using MediatR;

namespace Camdas.Application.Camadas;

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
