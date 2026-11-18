namespace CouchIA.Application.Common.Exceptions;

/// <summary>A Sessao não existe, foi excluída ou expirou (Req. 7.5).</summary>
public sealed class SessaoNaoEncontradaException : Exception
{
    public SessaoNaoEncontradaException() : base("Sessão não encontrada.") { }
}

/// <summary>
/// O LLM devolveu duas respostas fora do formato esperado; ambas foram descartadas e o perfil
/// não foi alterado (Req. 8.5).
/// </summary>
public sealed class RespostaLlmInvalidaException : Exception
{
    public RespostaLlmInvalidaException() : base("A resposta do serviço de IA não seguiu o formato esperado.") { }
}
