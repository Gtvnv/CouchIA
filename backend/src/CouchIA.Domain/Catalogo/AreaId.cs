namespace CouchIA.Domain;

public readonly record struct AreaId
{
    public string Valor { get; }

    public AreaId(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            throw new ArgumentException("O identificador da área é obrigatório.", nameof(valor));
        Valor = valor;
    }

    public override string ToString() => Valor;
}
