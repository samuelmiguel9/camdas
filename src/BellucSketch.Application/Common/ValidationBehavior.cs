using FluentValidation;
using MediatR;

namespace BellucSketch.Application.Common;

/// <summary>
/// Roda todos os validadores FluentValidation registrados para o Command/Query antes do handler.
/// Registrado como pipeline behavior aberto do MediatR na composição de DI da Api.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!validators.Any())
            return await next();

        var contexto = new ValidationContext<TRequest>(request);
        var falhas = new List<FluentValidation.Results.ValidationFailure>();

        foreach (var validator in validators)
        {
            var resultado = await validator.ValidateAsync(contexto, cancellationToken);
            falhas.AddRange(resultado.Errors);
        }

        if (falhas.Count != 0)
            throw new ValidationException(falhas);

        return await next();
    }
}
