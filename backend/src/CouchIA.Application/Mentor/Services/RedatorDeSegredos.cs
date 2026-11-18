using System.Text.RegularExpressions;

namespace CouchIA.Application.Mentor.Services;

/// <summary>
/// Remove de logs e respostas os valores de segredos configurados (chave de API, dados de conexão).
/// Além dos valores exatos, mascara padrões conhecidos (chaves "sk-..." e pares de connection string).
/// </summary>
public sealed class RedatorDeSegredos
{
    public const string Mascara = "[REDACTED]";

    private static readonly Regex PadraoChaveApi =
        new(@"\bsk-[A-Za-z0-9_\-]{8,}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex PadraoConexao =
        new(@"(?<chave>\b(?:Password|Pwd|User\s*Id|Uid|Username|Host|Server|Data\s*Source)\s*=)[^;\r\n]*",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly string[] _segredos;

    public RedatorDeSegredos(IEnumerable<string?> segredos)
    {
        ArgumentNullException.ThrowIfNull(segredos);

        // Mais longos primeiro, para que um segredo contido em outro não deixe sobras do maior.
        _segredos = segredos
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!)
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(s => s.Length)
            .ToArray();
    }

    public string Redigir(string? texto)
    {
        if (string.IsNullOrEmpty(texto))
            return texto ?? string.Empty;

        var resultado = texto;

        // Repete até estabilizar: remover um segredo pode juntar partes e formar outro.
        for (var i = 0; i < 10; i++)
        {
            var anterior = resultado;
            foreach (var segredo in _segredos)
            {
                var troca = Mascara.Contains(segredo, StringComparison.Ordinal) ? string.Empty : Mascara;
                resultado = resultado.Replace(segredo, troca, StringComparison.Ordinal);
            }

            if (ReferenceEquals(anterior, resultado) || anterior == resultado)
                break;
        }

        resultado = PadraoChaveApi.Replace(resultado, Mascara);
        resultado = PadraoConexao.Replace(resultado, "${chave}" + Mascara);

        return resultado;
    }
}
