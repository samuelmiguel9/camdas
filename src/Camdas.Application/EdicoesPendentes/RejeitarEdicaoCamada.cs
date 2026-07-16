using Camdas.Application.Abstractions;
using Camdas.Application.Common;
using Camdas.Domain.Entities;
using Camdas.Domain.Enums;
using FluentValidation;
using MediatR;

namespace Camdas.Application.EdicoesPendentes;

public sealed record RejeitarEdicaoCamadaCommand(Guid EdicaoId, string Motivo) : IRequest;

public sealed class RejeitarEdicaoCamadaCommandValidator : AbstractValidator<RejeitarEdicaoCamadaCommand>
{
    public RejeitarEdicaoCamadaCommandValidator()
    {
        RuleFor(c => c.Motivo).NotEmpty().MaximumLength(500);
    }
}

/// <summary>Rejeição pelo técnico: nenhuma mudança é aplicada à Planta/Camada, só marca a edição
/// pendente como rejeitada e registra o motivo.</summary>
public sealed class RejeitarEdicaoCamadaCommandHandler(
    IEdicaoPendenteRepository edicaoPendenteRepository,
    IHistoricoRepository historicoRepository,
    IUsuarioContext usuarioContext,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<RejeitarEdicaoCamadaCommand>
{
    public async Task Handle(RejeitarEdicaoCamadaCommand request, CancellationToken cancellationToken)
    {
        var edicao = await edicaoPendenteRepository.ObterPorIdAsync(request.EdicaoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Edição pendente '{request.EdicaoId}' não encontrada.");

        edicao.Rejeitar(request.Motivo, clock.AgoraUtc);

        historicoRepository.Adicionar(new HistoricoAlteracao(
            edicao.CamadaId is not null ? nameof(Camada) : nameof(Planta),
            edicao.CamadaId ?? edicao.PlantaId,
            TipoAcaoHistorico.CamadaEdicaoRejeitada,
            usuarioContext.UsuarioId,
            clock.AgoraUtc,
            edicao.PlantaId));

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);
    }
}
