namespace CouchIA.Application.Mentor.Services;

/// <summary>Lançada quando o LLM não responde dentro do tempo limite.</summary>
public sealed class LlmTimeoutException : Exception
{
    public LlmTimeoutException() : base("O LLM não respondeu dentro do tempo limite.") { }

    public LlmTimeoutException(Exception innerException)
        : base("O LLM não respondeu dentro do tempo limite.", innerException) { }
}

/// <summary>Porta de acesso ao LLM. A implementação concreta fica em Infrastructure.</summary>
public interface ILlmClient
{
    /// <summary>Tempo limite sem resposta (Req. 1.5).</summary>
    static readonly TimeSpan TempoLimite = TimeSpan.FromSeconds(30);

    /// <summary>Completa a requisição e devolve o texto bruto. Lança <see cref="LlmTimeoutException"/> após <see cref="TempoLimite"/>.</summary>
    Task<string> CompletarAsync(RequisicaoLlm requisicao, CancellationToken ct);

    /// <summary>Completa a requisição em fluxo, trecho a trecho. Lança <see cref="LlmTimeoutException"/> após <see cref="TempoLimite"/> sem resposta.</summary>
    IAsyncEnumerable<string> CompletarEmFluxoAsync(RequisicaoLlm requisicao, CancellationToken ct);
}
