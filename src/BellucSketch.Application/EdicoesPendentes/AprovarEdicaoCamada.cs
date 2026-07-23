using System.Text.Json;
using BellucSketch.Application.Abstractions;
using BellucSketch.Application.Common;
using BellucSketch.Contracts;
using BellucSketch.Domain.Entities;
using BellucSketch.Domain.Enums;
using MediatR;

namespace BellucSketch.Application.EdicoesPendentes;

public sealed record AprovarEdicaoCamadaCommand(Guid EdicaoId) : IRequest<CamadaDto?>;

/// <summary>
/// Aprovação de uma edição solicitada pela Web (acionada pelo técnico no Android, que segue sendo o
/// mestre): interpreta <see cref="EdicaoPendenteCamada.DadosDepoisJson"/> conforme
/// <see cref="EdicaoPendenteCamada.TipoOperacao"/> e só então aplica a mudança de verdade na Planta,
/// registrando o mesmo tipo de histórico que a operação direta equivalente já gera.
/// </summary>
public sealed class AprovarEdicaoCamadaCommandHandler(
    IPlantaRepository plantaRepository,
    IEdicaoPendenteRepository edicaoPendenteRepository,
    IHistoricoRepository historicoRepository,
    IUsuarioContext usuarioContext,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<AprovarEdicaoCamadaCommand, CamadaDto?>
{
    private sealed record DadosOpacidade(double Opacidade);

    private sealed record DadosReordenar(IReadOnlyList<Guid> OrdemDosIds);

    public async Task<CamadaDto?> Handle(AprovarEdicaoCamadaCommand request, CancellationToken cancellationToken)
    {
        var edicao = await edicaoPendenteRepository.ObterPorIdAsync(request.EdicaoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Edição pendente '{request.EdicaoId}' não encontrada.");

        var planta = await plantaRepository.ObterPorIdAsync(edicao.PlantaId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Planta '{edicao.PlantaId}' não encontrada.");

        TipoAcaoHistorico acaoAplicada;
        Guid entidadeId = planta.Id;
        var entidadeTipo = nameof(Planta);

        switch (edicao.TipoOperacao)
        {
            case TipoOperacaoEdicaoPendente.AlternarVisibilidade:
                planta.AlternarVisibilidadeCamada(edicao.CamadaId!.Value);
                acaoAplicada = TipoAcaoHistorico.CamadaVisibilidadeAlterada;
                entidadeId = edicao.CamadaId.Value;
                entidadeTipo = nameof(Camada);
                break;

            case TipoOperacaoEdicaoPendente.DefinirOpacidade:
                var dadosOpacidade = JsonSerializer.Deserialize<DadosOpacidade>(edicao.DadosDepoisJson)
                    ?? throw new RecursoNaoEncontradoException("Edição pendente sem opacidade proposta válida.");
                planta.DefinirOpacidadeCamada(edicao.CamadaId!.Value, dadosOpacidade.Opacidade);
                acaoAplicada = TipoAcaoHistorico.CamadaOpacidadeAlterada;
                entidadeId = edicao.CamadaId.Value;
                entidadeTipo = nameof(Camada);
                break;

            case TipoOperacaoEdicaoPendente.AlternarBloqueio:
                var camada = planta.Camadas.First(c => c.Id == edicao.CamadaId!.Value);
                if (camada.Bloqueada)
                {
                    planta.DesbloquearCamada(camada.Id);
                    acaoAplicada = TipoAcaoHistorico.CamadaDesbloqueada;
                }
                else
                {
                    planta.BloquearCamada(camada.Id);
                    acaoAplicada = TipoAcaoHistorico.CamadaBloqueada;
                }

                entidadeId = camada.Id;
                entidadeTipo = nameof(Camada);
                break;

            case TipoOperacaoEdicaoPendente.Reordenar:
                var dadosReordenar = JsonSerializer.Deserialize<DadosReordenar>(edicao.DadosDepoisJson)
                    ?? throw new RecursoNaoEncontradoException("Edição pendente sem ordem proposta válida.");
                planta.ReordenarCamadas(dadosReordenar.OrdemDosIds);
                acaoAplicada = TipoAcaoHistorico.CamadaReordenada;
                break;

            case TipoOperacaoEdicaoPendente.Excluir:
                planta.RemoverCamada(edicao.CamadaId!.Value);
                acaoAplicada = TipoAcaoHistorico.CamadaRemovida;
                entidadeId = edicao.CamadaId.Value;
                entidadeTipo = nameof(Camada);
                break;

            default:
                throw new RecursoNaoEncontradoException($"Tipo de operação '{edicao.TipoOperacao}' não suportado.");
        }

        edicao.Aprovar(clock.AgoraUtc);

        historicoRepository.Adicionar(new HistoricoAlteracao(
            entidadeTipo,
            entidadeId,
            acaoAplicada,
            usuarioContext.UsuarioId,
            clock.AgoraUtc,
            planta.Id));

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        return edicao.TipoOperacao == TipoOperacaoEdicaoPendente.Excluir
            ? null
            : planta.Camadas.FirstOrDefault(c => c.Id == edicao.CamadaId)?.ParaDto();
    }
}
