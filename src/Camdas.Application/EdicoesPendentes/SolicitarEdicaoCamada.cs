using System.Text.Json;
using Camdas.Application.Abstractions;
using Camdas.Application.Common;
using Camdas.Contracts;
using Camdas.Domain.Entities;
using Camdas.Domain.Enums;
using FluentValidation;
using MediatR;

namespace Camdas.Application.EdicoesPendentes;

public sealed record SolicitarEdicaoCamadaCommand(
    Guid PlantaId,
    Guid? CamadaId,
    TipoOperacaoEdicaoPendente TipoOperacao,
    string DadosDepoisJson,
    string Responsavel,
    string Motivo) : IRequest<EdicaoPendenteDto>;

public sealed class SolicitarEdicaoCamadaCommandValidator : AbstractValidator<SolicitarEdicaoCamadaCommand>
{
    public SolicitarEdicaoCamadaCommandValidator()
    {
        RuleFor(c => c.PlantaId).NotEmpty();
        RuleFor(c => c.CamadaId).NotEmpty().Unless(c => c.TipoOperacao == TipoOperacaoEdicaoPendente.Reordenar);
        RuleFor(c => c.DadosDepoisJson).NotEmpty();
        RuleFor(c => c.Responsavel).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Motivo).NotEmpty().MaximumLength(500);
    }
}

/// <summary>
/// Caso de uso acionado pela Web: em vez de aplicar a mudança direto na Planta (como as demais
/// operações de Camada fazem), registra uma <see cref="EdicaoPendenteCamada"/> aguardando aprovação
/// de um técnico no Android. A Planta/Camada em si não é alterada aqui — só quando aprovada.
/// </summary>
public sealed class SolicitarEdicaoCamadaCommandHandler(
    IPlantaRepository plantaRepository,
    IEdicaoPendenteRepository edicaoPendenteRepository,
    IHistoricoRepository historicoRepository,
    IUsuarioContext usuarioContext,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<SolicitarEdicaoCamadaCommand, EdicaoPendenteDto>
{
    public async Task<EdicaoPendenteDto> Handle(SolicitarEdicaoCamadaCommand request, CancellationToken cancellationToken)
    {
        var planta = await plantaRepository.ObterPorIdAsync(request.PlantaId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Planta '{request.PlantaId}' não encontrada.");

        string? dadosAntesJson = null;
        Guid entidadeId = planta.Id;
        var entidadeTipo = nameof(Planta);

        if (request.CamadaId is { } camadaId)
        {
            var camada = planta.Camadas.FirstOrDefault(c => c.Id == camadaId)
                ?? throw new RecursoNaoEncontradoException($"Camada '{camadaId}' não encontrada nesta planta.");

            entidadeId = camada.Id;
            entidadeTipo = nameof(Camada);
            dadosAntesJson = request.TipoOperacao switch
            {
                TipoOperacaoEdicaoPendente.AlternarVisibilidade => JsonSerializer.Serialize(new { visivel = camada.Visivel }),
                TipoOperacaoEdicaoPendente.DefinirOpacidade => JsonSerializer.Serialize(new { opacidade = camada.Opacidade }),
                TipoOperacaoEdicaoPendente.AlternarBloqueio => JsonSerializer.Serialize(new { bloqueada = camada.Bloqueada }),
                _ => null
            };
        }
        else
        {
            dadosAntesJson = JsonSerializer.Serialize(new { ordemDosIds = planta.Camadas.Select(c => c.Id).ToList() });
        }

        var edicao = new EdicaoPendenteCamada(
            planta.Id,
            request.CamadaId,
            request.TipoOperacao,
            request.DadosDepoisJson,
            request.Responsavel,
            request.Motivo,
            clock.AgoraUtc,
            dadosAntesJson);

        edicaoPendenteRepository.Adicionar(edicao);

        historicoRepository.Adicionar(new HistoricoAlteracao(
            entidadeTipo,
            entidadeId,
            TipoAcaoHistorico.CamadaEdicaoSolicitada,
            usuarioContext.UsuarioId,
            clock.AgoraUtc,
            planta.Id));

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        return edicao.ParaDto();
    }
}
