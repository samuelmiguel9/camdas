using BellucSketch.Api.Auth;
using BellucSketch.Application.Abstractions;
using BellucSketch.Application.Usuarios;
using BellucSketch.Contracts;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BellucSketch.Api.Controllers;

/// <summary>
/// ATENÇÃO: emite um token para qualquer UsuarioId existente, sem verificar senha, e permite criar
/// usuário só com nome/e-mail (sem senha). É um placeholder para desenvolvimento/testes até existir
/// um fluxo de login real (credencial + hash de senha) — o domínio Usuario ainda não tem campo de
/// senha (ver TASKS.md, backlog de autenticação). Não usar assim em produção.
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AutenticacaoController(
    IUsuarioRepository usuarioRepository,
    IJwtTokenGenerator tokenGenerator,
    IMediator mediator) : ControllerBase
{
    [HttpPost("dev-token")]
    public async Task<ActionResult<EmitirTokenResponse>> EmitirTokenDeDesenvolvimento(EmitirTokenRequest request, CancellationToken ct)
    {
        var usuario = await usuarioRepository.ObterPorIdAsync(request.UsuarioId, ct);
        if (usuario is null)
            return NotFound();

        var token = tokenGenerator.GerarToken(usuario.Id, usuario.Nome);
        return Ok(new EmitirTokenResponse(token));
    }

    [HttpPost("dev-usuario")]
    public async Task<ActionResult<UsuarioDto>> CriarUsuarioDeDesenvolvimento(CriarUsuarioRequest request, CancellationToken ct)
    {
        var usuario = await mediator.Send(new CriarUsuarioCommand(request.Nome, request.Email), ct);
        return Ok(usuario);
    }
}
