using BellucSketch.Application.Plantas;
using BellucSketch.Application.Projetos;
using BellucSketch.Contracts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BellucSketch.Api.Controllers;

[ApiController]
[Route("api/projetos")]
[Authorize]
public sealed class ProjetosController(ISender mediator) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ProjetoDto>> Criar(CriarProjetoRequest request, CancellationToken ct)
    {
        var resultado = await mediator.Send(new CriarProjetoCommand(request.Nome, request.Descricao), ct);
        return CreatedAtAction(nameof(Obter), new { projetoId = resultado.Id }, resultado);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProjetoDto>>> Listar(CancellationToken ct) =>
        Ok(await mediator.Send(new ListarProjetosQuery(), ct));

    [HttpGet("{projetoId:guid}")]
    public async Task<ActionResult<ProjetoDto>> Obter(Guid projetoId, CancellationToken ct) =>
        Ok(await mediator.Send(new ObterProjetoQuery(projetoId), ct));

    [HttpGet("{projetoId:guid}/plantas")]
    public async Task<ActionResult<IReadOnlyList<PlantaDto>>> ListarPlantas(Guid projetoId, CancellationToken ct) =>
        Ok(await mediator.Send(new ListarPlantasPorProjetoQuery(projetoId), ct));

    [HttpPut("{projetoId:guid}")]
    public async Task<ActionResult<ProjetoDto>> Renomear(Guid projetoId, RenomearProjetoRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new RenomearProjetoCommand(projetoId, request.Nome), ct));

    [HttpDelete("{projetoId:guid}")]
    public async Task<IActionResult> Remover(Guid projetoId, CancellationToken ct)
    {
        await mediator.Send(new RemoverProjetoCommand(projetoId), ct);
        return NoContent();
    }
}
