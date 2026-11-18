namespace CouchIA.Domain;

public readonly record struct HabilidadeId
{
    public string Valor { get; }

    public HabilidadeId(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            throw new ArgumentException("O identificador da habilidade é obrigatório.", nameof(valor));
        Valor = valor;
    }

    public override string ToString() => Valor;
}
