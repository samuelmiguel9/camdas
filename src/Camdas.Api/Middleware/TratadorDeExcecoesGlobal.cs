using Camdas.Application.Common;
using Camdas.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Camdas.Api.Middleware;

/// <summary>
/// Mapeia exceções de negócio/aplicação para respostas HTTP consistentes (ProblemDetails), em vez
/// de deixá-las virar 500 genérico. Erros realmente inesperados continuam como 500.
/// </summary>
public sealed class TratadorDeExcecoesGlobal(ILogger<TratadorDeExcecoesGlobal> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problemDetails = exception switch
        {
            DomainException ex => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Regra de negócio violada",
                Detail = ex.Message,
            },
            RecursoNaoEncontradoException ex => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Recurso não encontrado",
                Detail = ex.Message,
            },
            ValidationException ex => CriarProblemaDeValidacao(ex),
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Erro interno",
                Detail = "Ocorreu um erro inesperado.",
            },
        };

        // Erros inesperados (500) vão como Error, com stack trace, para investigação; erros de
        // negócio/validação são esperados do fluxo normal e só entram como Warning (sem poluir os
        // logs de erro com algo que já é tratado e retornado ao cliente).
        if (problemDetails.Status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Erro não tratado em {Metodo} {Caminho}", httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogWarning("{Titulo} em {Metodo} {Caminho}: {Detalhe}", problemDetails.Title, httpContext.Request.Method, httpContext.Request.Path, problemDetails.Detail);
        }

        httpContext.Response.StatusCode = problemDetails.Status!.Value;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private static ProblemDetails CriarProblemaDeValidacao(ValidationException ex)
    {
        var problema = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Erro de validação",
        };

        problema.Extensions["erros"] = ex.Errors.Select(e => new { campo = e.PropertyName, mensagem = e.ErrorMessage });

        return problema;
    }
}
