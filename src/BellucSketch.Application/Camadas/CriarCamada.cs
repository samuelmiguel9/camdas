using BellucSketch.Application.Abstractions;
using BellucSketch.Application.Common;
using BellucSketch.Contracts;
using BellucSketch.Domain.Entities;
using BellucSketch.Domain.Enums;
using FluentValidation;
using MediatR;

namespace BellucSketch.Application.Camadas;

public sealed record CriarCamadaCommand(Guid PlantaId, string Nome) : IRequest<CamadaDto>;

public sealed class CriarCamadaCommandValidator : AbstractValidator<CriarCamadaCommand>
{
    public CriarCamadaCommandValidator()
    {
        RuleFor(c => c.PlantaId).NotEmpty();
        RuleFor(c => c.Nome).NotEmpty().MaximumLength(100);
    }
}

public sealed class CriarCamadaCommandHandler(
    IPlantaRepository plantaRepository,
    IHistoricoRepository historicoRepository,
    IUsuarioContext usuarioContext,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<CriarCamadaCommand, CamadaDto>
{
    public async Task<CamadaDto> Handle(CriarCamadaCommand request, CancellationToken cancellationToken)
    {
        var planta = await plantaRepository.ObterPorIdAsync(request.PlantaId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Planta '{request.PlantaId}' não encontrada.");

        var camada = planta.AdicionarCamada(request.Nome);

        historicoRepository.Adicionar(new HistoricoAlteracao(
            nameof(Camada),
            camada.Id,
            TipoAcaoHistorico.CamadaAdicionada,
            usuarioContext.UsuarioId,
            clock.AgoraUtc,
            planta.Id));

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        return camada.ParaDto();
    }
}
