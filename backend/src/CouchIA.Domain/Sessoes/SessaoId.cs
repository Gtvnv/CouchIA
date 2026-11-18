using System.Security.Cryptography;

namespace CouchIA.Domain;

/// <summary>Identificador de Sessão: 256 bits aleatórios codificados em Base64Url (43 caracteres, sem padding).</summary>
public readonly record struct SessaoId
{
    public const int TamanhoEmBytes = 32;
    public const int TamanhoEmCaracteres = 43;

    public string Valor { get; }

    private SessaoId(string valor) => Valor = valor;

    public static SessaoId Gerar()
    {
        var bytes = RandomNumberGenerator.GetBytes(TamanhoEmBytes);
        return new SessaoId(Codificar(bytes));
    }

    public static bool TryParse(string? texto, out SessaoId id)
    {
        id = default;
        if (texto is null || texto.Length != TamanhoEmCaracteres)
            return false;

        foreach (var c in texto)
        {
            var valido = c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_';
            if (!valido) return false;
        }

        // 43 caracteres = 258 bits; os 2 bits finais precisam ser zero para a representação ser canônica.
        var decodificado = Decodificar(texto);
        if (decodificado is null || decodificado.Length != TamanhoEmBytes || Codificar(decodificado) != texto)
            return false;

        id = new SessaoId(texto);
        return true;
    }

    public static SessaoId Criar(string texto) =>
        TryParse(texto, out var id)
            ? id
            : throw new ArgumentException("Identificador de sessão inválido.", nameof(texto));

    public byte[] ObterBytes() => Decodificar(Valor ?? throw new InvalidOperationException("SessaoId não inicializado."))!;

    public override string ToString() => Valor;

    private static string Codificar(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[]? Decodificar(string texto)
    {
        var base64 = texto.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
        try { return Convert.FromBase64String(base64); }
        catch (FormatException) { return null; }
    }
}
