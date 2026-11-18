namespace CouchIA.Application.Planos.Exceptions;

/// <summary>Não há Catálogo_de_Carreiras válido carregado (modo degradado). Permite tentar novamente (Req. 5.8).</summary>
public sealed class CatalogoIndisponivelException : Exception
{
    public CatalogoIndisponivelException() : base("O catálogo de carreiras está indisponível.") { }
}

/// <summary>O plano solicitado não existe na Sessao.</summary>
public sealed class PlanoNaoEncontradoException : Exception
{
    public PlanoNaoEncontradoException() : base("Plano não encontrado.") { }
}
