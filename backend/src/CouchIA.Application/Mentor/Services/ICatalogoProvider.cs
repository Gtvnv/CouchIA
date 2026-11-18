using CouchIA.Domain;

namespace CouchIA.Application.Mentor.Services;

public enum TipoFalhaCarga
{
    JsonMalformado,
    CampoAusenteOuInvalido,
    ValidacaoDoCatalogo
}

/// <summary>Falha de carga localizada: campo (caminho) ou posição, e o tipo da falha.</summary>
public sealed record ErroCarga(
    TipoFalhaCarga Tipo,
    string? Campo,
    long? Linha,
    long? Posicao,
    string Detalhe,
    ErroCatalogo? ErroValidacao = null);

public sealed record ResultadoCarga(Catalogo? Catalogo, IReadOnlyList<ErroCarga> Erros)
{
    public bool Sucesso => Catalogo is not null && Erros.Count == 0;
}

public interface ICatalogoProvider
{
    /// <summary>Último catálogo carregado com sucesso; null enquanto nenhum foi carregado (modo degradado).</summary>
    Catalogo? Ativo { get; }

    /// <summary>Desserializa e valida; só então substitui o ativo. Em falha mantém o ativo anterior.</summary>
    ResultadoCarga Carregar(string json);
}
