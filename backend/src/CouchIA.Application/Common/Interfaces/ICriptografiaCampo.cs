namespace CouchIA.Application.Common.Interfaces;

/// <summary>Criptografia de campo em repouso (Req. 7.6).</summary>
public interface ICriptografiaCampo
{
    /// <summary>Criptografa o texto com um nonce novo a cada chamada. Devolve texto base64 autocontido.</summary>
    string Criptografar(string textoPlano);

    /// <summary>Descriptografa o valor produzido por <see cref="Criptografar"/>. Lança <see cref="System.Security.Cryptography.CryptographicException"/> se o valor foi adulterado ou a chave é outra.</summary>
    string Descriptografar(string textoCifrado);
}
