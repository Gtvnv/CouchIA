using FluentValidation;
using MediatR;

namespace CouchIA.Application.Common.Behaviours;

/// <summary>Executa todos os validators do request e lança <see cref="ValidationException"/> agregando as falhas antes do handler.</summary>
public sealed class ValidationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehaviour(IEnumerable<IValidator<TRequest>> validators) => _validators = validators;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (_validators.Any())
        {
            var contexto = new ValidationContext<TRequest>(request);
            var resultados = await Task.WhenAll(_validators.Select(v => v.ValidateAsync(contexto, cancellationToken)));
            var falhas = resultados.SelectMany(r => r.Errors).Where(f => f is not null).ToList();

            if (falhas.Count != 0)
                throw new ValidationException(falhas);
        }

        return await next();
    }
}
