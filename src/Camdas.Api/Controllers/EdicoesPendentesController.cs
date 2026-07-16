using Camdas.Application.EdicoesPendentes;
using Camdas.Contracts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Camdas.Api.Controllers;

/// <summary>
/// Fila de edições pendentes de aprovação: a Web solicita (POST), o técnico no Android revisa
/// (GET) e aprova ou rejeita. Aprovar aplica a mudança de verdade na Planta/Camada — até lá, nada
/// muda no mestre.
/// </summary>
[ApiController]
[Route("api/plantas/{plantaId:guid}/edicoes-pendentes")]
[Authorize]
public sealed class EdicoesPendentesController(ISender mediator) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<EdicaoPendenteDto>> Solicitar(Guid plantaId, SolicitarEdicaoCamadaRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new SolicitarEdicaoCamadaCommand(
            plantaId, request.CamadaId, request.TipoOperacao, request.DadosDepoisJson, request.Responsavel, request.Motivo), ct));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EdicaoPendenteDto>>> Listar(Guid plantaId, CancellationToken ct) =>
        Ok(await mediator.Send(new ListarEdicoesPendentesQuery(plantaId), ct));

    [HttpPost("{edicaoId:guid}/aprovar")]
    public async Task<ActionResult<CamadaDto?>> Aprovar(Guid plantaId, Guid edicaoId, CancellationToken ct) =>
        Ok(await mediator.Send(new AprovarEdicaoCamadaCommand(edicaoId), ct));

    [HttpPost("{edicaoId:guid}/rejeitar")]
    public async Task<IActionResult> Rejeitar(Guid plantaId, Guid edicaoId, RejeitarEdicaoCamadaRequest request, CancellationToken ct)
    {
        await mediator.Send(new RejeitarEdicaoCamadaCommand(edicaoId, request.Motivo), ct);
        return NoContent();
    }
}
