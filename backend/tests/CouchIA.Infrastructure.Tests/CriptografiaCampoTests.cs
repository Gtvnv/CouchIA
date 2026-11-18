using System.Security.Cryptography;
using CouchIA.Infrastructure.Criptografia;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace CouchIA.Infrastructure.Tests;

public class CriptografiaCampoTests
{
    private static readonly CriptografiaCampoAesGcm Cripto = new(RandomNumberGenerator.GetBytes(32));

    // Texto arbitrário (inclui vazio, acentos, emoji); evita surrogates soltos, que não sobrevivem a UTF-8.
    private static Gen<string> Texto(int min, int max)
    {
        var chars = Gen.OneOf(
            Gen.Choose(0x20, 0x7E).Select(i => char.ConvertFromUtf32(i)),
            Gen.Choose(0xA0, 0x2FF).Select(i => char.ConvertFromUtf32(i)),
            Gen.Choose(0x4E00, 0x4E80).Select(i => char.ConvertFromUtf32(i)),
            Gen.Choose(0x1F600, 0x1F64F).Select(i => char.ConvertFromUtf32(i)),
            Gen.Elements("\n\r\t\0"));
        return Gen.Choose(min, max)
            .SelectMany(n => Gen.ArrayOf(chars, n))
            .Select(a => string.Concat(a));
    }

    // Feature: ia-mentor-carreira, Property 20: Criptografia de campo
    // **Validates: Requirements 7.6**
    [Property(MaxTest = 100)]
    public Property Ida_e_volta_devolve_o_texto_original()
    {
        return Prop.ForAll(Texto(0, 200).ToArbitrary(), texto =>
            Cripto.Descriptografar(Cripto.Criptografar(texto)) == texto);
    }

    // Feature: ia-mentor-carreira, Property 20: Criptografia de campo
    // **Validates: Requirements 7.6**
    [Property(MaxTest = 100)]
    public Property Valor_armazenado_nao_contem_o_texto_plano()
    {
        // mínimo de 8 caracteres evita coincidência casual de substring curta no base64 aleatório
        return Prop.ForAll(Texto(8, 200).ToArbitrary(), texto =>
        {
            var cifrado = Cripto.Criptografar(texto);
            var bytes = Convert.FromBase64String(cifrado);
            var plano = System.Text.Encoding.UTF8.GetBytes(texto);
            return !cifrado.Contains(texto, StringComparison.Ordinal)
                && bytes.AsSpan().IndexOf(plano) < 0;
        });
    }

    // Feature: ia-mentor-carreira, Property 20: Criptografia de campo
    // **Validates: Requirements 7.6**
    [Property(MaxTest = 100)]
    public Property Dois_cifrados_do_mesmo_texto_diferem()
    {
        return Prop.ForAll(Texto(0, 200).ToArbitrary(), texto =>
            Cripto.Criptografar(texto) != Cripto.Criptografar(texto));
    }
}
