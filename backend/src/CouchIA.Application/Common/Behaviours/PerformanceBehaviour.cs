using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CouchIA.Application.Common.Behaviours;

/// <summary>Mede a duração do request e avisa quando passa do limite.</summary>
public sealed class PerformanceBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public const int LimiteMilissegundos = 500;

    private readonly ILogger<TRequest> _logger;

    public PerformanceBehaviour(ILogger<TRequest> logger) => _logger = logger;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var inicio = Stopwatch.GetTimestamp();
        try
        {
            return await next();
        }
        finally
        {
            var duracao = Stopwatch.GetElapsedTime(inicio);
            if (duracao.TotalMilliseconds > LimiteMilissegundos)
                _logger.LogWarning("Requisição lenta: {Requisicao} levou {Milissegundos} ms",
                    typeof(TRequest).Name, (long)duracao.TotalMilliseconds);
        }
    }
}
