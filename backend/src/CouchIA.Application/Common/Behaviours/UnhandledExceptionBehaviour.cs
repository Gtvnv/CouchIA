using CouchIA.Application.Mentor.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CouchIA.Application.Common.Behaviours;

/// <summary>Registra exceções não tratadas e as relança. A mensagem passa pelo redator para não vazar segredos.</summary>
public sealed class UnhandledExceptionBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<TRequest> _logger;
    private readonly RedatorDeSegredos? _redator;

    public UnhandledExceptionBehaviour(ILogger<TRequest> logger, RedatorDeSegredos? redator = null)
    {
        _logger = logger;
        _redator = redator;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Não registra o objeto da exceção nem o request: ambos podem carregar segredos ou texto do usuário.
            var mensagem = _redator is null ? ex.Message : _redator.Redigir(ex.Message);
            _logger.LogError("Requisição {Requisicao} falhou com {TipoExcecao}: {Mensagem}",
                typeof(TRequest).Name, ex.GetType().Name, mensagem);
            throw;
        }
    }
}
