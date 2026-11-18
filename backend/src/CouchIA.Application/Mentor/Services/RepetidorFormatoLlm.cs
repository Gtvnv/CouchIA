namespace CouchIA.Application.Mentor.Services;

/// <summary>Resultado da chamada com repetição: valor válido ou erro de formato.</summary>
public sealed record ResultadoFormatoLlm<T>(bool Sucesso, T? Valor, int Chamadas)
{
    public static ResultadoFormatoLlm<T> Ok(T valor, int chamadas) => new(true, valor, chamadas);

    public static ResultadoFormatoLlm<T> FormatoInvalido(int chamadas) => new(false, default, chamadas);
}

/// <summary>
/// Chama o LLM, valida o formato da saída e, se inválida, repete a chamada. Faz no máximo
/// <see cref="MaximoChamadas"/> chamadas (a original e uma repetição). Se todas forem inválidas,
/// descarta as respostas e devolve erro, sem tocar em histórico ou perfil (Req. 1.4, 1.5, 3.7).
/// Timeout (<see cref="LlmTimeoutException"/>) e cancelamento propagam sem repetir.
/// </summary>
public sealed class RepetidorFormatoLlm
{
    public const int MaximoChamadas = 2;

    private readonly ILlmClient _llm;

    public RepetidorFormatoLlm(ILlmClient llm)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
    }

    /// <param name="validar">Devolve true e o valor quando a saída bruta está no formato esperado.</param>
    public async Task<ResultadoFormatoLlm<T>> ExecutarAsync<T>(
        RequisicaoLlm requisicao,
        Func<string, (bool Valido, T? Valor)> validar,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(requisicao);
        ArgumentNullException.ThrowIfNull(validar);

        for (var chamada = 1; chamada <= MaximoChamadas; chamada++)
        {
            var bruto = await _llm.CompletarAsync(requisicao, ct);
            var (valido, valor) = validar(bruto);
            if (valido)
            {
                return ResultadoFormatoLlm<T>.Ok(valor!, chamada);
            }
        }

        return ResultadoFormatoLlm<T>.FormatoInvalido(MaximoChamadas);
    }
}
