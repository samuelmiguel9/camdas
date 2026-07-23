using BellucSketch.Contracts;
using BellucSketch.Application.Historico;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BellucSketch.Api.Controllers;

[ApiController]
[Route("api/plantas/{plantaId:guid}/historico")]
[Authorize]
public sealed class HistoricoController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<HistoricoDto>>> Obter(Guid plantaId, CancellationToken ct) =>
        Ok(await mediator.Send(new ObterHistoricoDaPlantaQuery(plantaId), ct));
}
