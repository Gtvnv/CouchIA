using System.Security.Cryptography;
using System.Text;
using CouchIA.Application.Common.Interfaces;

namespace CouchIA.Infrastructure.Criptografia;

/// <summary>
/// AES-256-GCM. Formato do valor: base64(nonce[12] | tag[16] | cifrado).
/// Nonce aleatório (CSPRNG) novo a cada operação de criptografia.
/// </summary>
public sealed class CriptografiaCampoAesGcm : ICriptografiaCampo
{
    public const string ChaveConfig = "Criptografia:ChaveBase64";
    private const int TamanhoChave = 32;
    private const int TamanhoNonce = 12;
    private const int TamanhoTag = 16;

    private readonly byte[] _chave;

    public CriptografiaCampoAesGcm(byte[] chave)
    {
        ArgumentNullException.ThrowIfNull(chave);
        if (chave.Length != TamanhoChave)
            throw new ArgumentException("A chave de criptografia deve ter 32 bytes (AES-256).", nameof(chave));
        _chave = (byte[])chave.Clone();
    }

    /// <summary>Lê a chave (base64, 32 bytes) da configuração/variável de ambiente <c>Criptografia__ChaveBase64</c>.</summary>
    public static CriptografiaCampoAesGcm DeConfiguracao(Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        var base64 = configuration[ChaveConfig];
        if (string.IsNullOrWhiteSpace(base64))
            throw new InvalidOperationException($"Chave de criptografia ausente. Defina '{ChaveConfig}' (variável de ambiente Criptografia__ChaveBase64) com 32 bytes em base64.");

        byte[] chave;
        try { chave = Convert.FromBase64String(base64); }
        catch (FormatException)
        {
            throw new InvalidOperationException($"'{ChaveConfig}' não é base64 válido.");
        }
        if (chave.Length != TamanhoChave)
            throw new InvalidOperationException($"'{ChaveConfig}' deve decodificar para 32 bytes.");
        return new CriptografiaCampoAesGcm(chave);
    }

    public string Criptografar(string textoPlano)
    {
        ArgumentNullException.ThrowIfNull(textoPlano);
        var plano = Encoding.UTF8.GetBytes(textoPlano);
        var saida = new byte[TamanhoNonce + TamanhoTag + plano.Length];
        var nonce = saida.AsSpan(0, TamanhoNonce);
        var tag = saida.AsSpan(TamanhoNonce, TamanhoTag);
        var cifrado = saida.AsSpan(TamanhoNonce + TamanhoTag);

        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(_chave, TamanhoTag);
        aes.Encrypt(nonce, plano, cifrado, tag);
        return Convert.ToBase64String(saida);
    }

    public string Descriptografar(string textoCifrado)
    {
        ArgumentNullException.ThrowIfNull(textoCifrado);
        byte[] dados;
        try { dados = Convert.FromBase64String(textoCifrado); }
        catch (FormatException ex)
        {
            throw new CryptographicException("Valor cifrado inválido.", ex);
        }
        if (dados.Length < TamanhoNonce + TamanhoTag)
            throw new CryptographicException("Valor cifrado inválido.");

        var nonce = dados.AsSpan(0, TamanhoNonce);
        var tag = dados.AsSpan(TamanhoNonce, TamanhoTag);
        var cifrado = dados.AsSpan(TamanhoNonce + TamanhoTag);
        var plano = new byte[cifrado.Length];

        using var aes = new AesGcm(_chave, TamanhoTag);
        aes.Decrypt(nonce, cifrado, tag, plano);
        return Encoding.UTF8.GetString(plano);
    }
}
