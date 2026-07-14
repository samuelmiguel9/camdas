using Camdas.Application.Plantas;
using Camdas.Contracts;
using Camdas.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Camdas.Api.Controllers;

/// <summary>
/// Agrupa os campos do multipart/form-data de POST /api/plantas num único tipo bindado via
/// [FromForm]. Necessário porque o Swashbuckle não gera a documentação Swagger quando um IFormFile
/// aparece ao lado de outros parâmetros [FromForm] soltos na assinatura da action (lança
/// SwaggerGeneratorException em tempo de execução, só ao abrir o Swagger — não é erro de compilação).
/// Os nomes das propriedades já batem com <see cref="ImportarPlantaCampos"/> por convenção
/// (case-insensitive), sem precisar de atributos extras.
/// </summary>
public sealed class ImportarPlantaFormulario
{
    public Guid ProjetoId { get; set; }
    public string Nome { get; set; } = null!;
    public string? Descricao { get; set; }
    public string? NomeCliente { get; set; }
    public TipoArquivoOrigem TipoArquivoOrigem { get; set; }
    public IFormFile Arquivo { get; set; } = null!;
}

[ApiController]
[Route("api/plantas")]
[Authorize]
public sealed class PlantasController(ISender mediator) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(50_000_000)]
    public async Task<ActionResult<PlantaDto>> Importar([FromForm] ImportarPlantaFormulario formulario, CancellationToken ct)
    {
        await using var conteudo = formulario.Arquivo.OpenReadStream();
        var comando = new ImportarPlantaCommand(
            formulario.ProjetoId, formulario.Nome, formulario.Descricao, formulario.NomeCliente,
            formulario.TipoArquivoOrigem, formulario.Arquivo.FileName, conteudo);
        var resultado = await mediator.Send(comando, ct);
        return CreatedAtAction(nameof(Obter), new { plantaId = resultado.Id }, resultado);
    }

    [HttpGet("{plantaId:guid}")]
    public async Task<ActionResult<PlantaDto>> Obter(Guid plantaId, CancellationToken ct) =>
        Ok(await mediator.Send(new ObterPlantaQuery(plantaId), ct));

    [HttpGet("{plantaId:guid}/arquivo")]
    public async Task<IActionResult> ObterArquivo(Guid plantaId, CancellationToken ct)
    {
        var conteudo = await mediator.Send(new ObterArquivoPlantaQuery(plantaId), ct);
        return File(conteudo, "image/png");
    }

    [HttpDelete("{plantaId:guid}")]
    public async Task<IActionResult> Remover(Guid plantaId, CancellationToken ct)
    {
        await mediator.Send(new RemoverPlantaCommand(plantaId), ct);
        return NoContent();
    }
}
