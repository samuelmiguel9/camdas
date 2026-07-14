using Camdas.Application.Camadas;
using Camdas.Contracts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Camdas.Api.Controllers;

/// <summary>
/// Agrupa o arquivo do multipart/form-data de PUT .../imagem num único tipo bindado via [FromForm]
/// — mesmo motivo de <see cref="ImportarPlantaFormulario"/> (Swashbuckle exige isso quando um
/// IFormFile aparece ao lado de outros parâmetros [FromForm]).
/// </summary>
public sealed class AtualizarImagemCamadaFormulario
{
    public IFormFile Arquivo { get; set; } = null!;
}

[ApiController]
[Route("api/plantas/{plantaId:guid}/camadas")]
[Authorize]
public sealed class CamadasController(ISender mediator) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CamadaDto>> Criar(Guid plantaId, CriarCamadaRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new CriarCamadaCommand(plantaId, request.Nome), ct));

    [HttpDelete("{camadaId:guid}")]
    public async Task<IActionResult> Remover(Guid plantaId, Guid camadaId, CancellationToken ct)
    {
        await mediator.Send(new RemoverCamadaCommand(plantaId, camadaId), ct);
        return NoContent();
    }

    [HttpPut("ordem")]
    public async Task<ActionResult<IReadOnlyList<CamadaDto>>> Reordenar(Guid plantaId, ReordenarCamadasRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new ReordenarCamadasCommand(plantaId, request.OrdemDosIds), ct));

    [HttpPatch("{camadaId:guid}/visibilidade")]
    public async Task<ActionResult<CamadaDto>> AlternarVisibilidade(Guid plantaId, Guid camadaId, CancellationToken ct) =>
        Ok(await mediator.Send(new AlternarVisibilidadeCamadaCommand(plantaId, camadaId), ct));

    [HttpPatch("{camadaId:guid}/opacidade")]
    public async Task<ActionResult<CamadaDto>> DefinirOpacidade(
        Guid plantaId, Guid camadaId, DefinirOpacidadeCamadaRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new DefinirOpacidadeCamadaCommand(plantaId, camadaId, request.Opacidade), ct));

    [HttpPost("{camadaId:guid}/bloqueio")]
    public async Task<ActionResult<CamadaDto>> Bloquear(Guid plantaId, Guid camadaId, CancellationToken ct) =>
        Ok(await mediator.Send(new BloquearCamadaCommand(plantaId, camadaId), ct));

    [HttpDelete("{camadaId:guid}/bloqueio")]
    public async Task<ActionResult<CamadaDto>> Desbloquear(Guid plantaId, Guid camadaId, CancellationToken ct) =>
        Ok(await mediator.Send(new DesbloquearCamadaCommand(plantaId, camadaId), ct));

    [HttpPut("{camadaId:guid}/imagem")]
    [RequestSizeLimit(10_000_000)]
    public async Task<ActionResult<CamadaDto>> AtualizarImagem(
        Guid plantaId, Guid camadaId, [FromForm] AtualizarImagemCamadaFormulario formulario, CancellationToken ct)
    {
        await using var conteudo = formulario.Arquivo.OpenReadStream();
        var comando = new AtualizarImagemCamadaCommand(plantaId, camadaId, formulario.Arquivo.FileName, conteudo);
        return Ok(await mediator.Send(comando, ct));
    }

    [HttpGet("{camadaId:guid}/imagem")]
    public async Task<IActionResult> ObterImagem(Guid plantaId, Guid camadaId, CancellationToken ct)
    {
        var conteudo = await mediator.Send(new ObterImagemCamadaQuery(plantaId, camadaId), ct);
        return File(conteudo, "image/png");
    }
}
