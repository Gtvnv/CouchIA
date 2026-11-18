using MediatR.Pipeline;
using Microsoft.Extensions.Logging;

namespace CouchIA.Application.Common.Behaviours;

/// <summary>Registra apenas o nome do request. O conteúdo não é logado (respostas do usuário são dados sensíveis).</summary>
public sealed class LoggingBehaviour<TRequest> : IRequestPreProcessor<TRequest>
    where TRequest : notnull
{
    private readonly ILogger<TRequest> _logger;

    public LoggingBehaviour(ILogger<TRequest> logger) => _logger = logger;

    public Task Process(TRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Requisição recebida: {Requisicao}", typeof(TRequest).Name);
        return Task.CompletedTask;
    }
}
