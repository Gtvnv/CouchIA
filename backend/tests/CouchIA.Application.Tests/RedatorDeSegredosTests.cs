using CouchIA.Application.Mentor.Services;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace CouchIA.Application.Tests;

public class RedatorDeSegredosTests
{
    private static readonly char[] AlfabetoSegredo =
        "abcXYZ0189_-/+=.:@!#$%".ToCharArray();

    private static readonly char[] AlfabetoTexto =
        "abc XYZ019\n;=,.-".ToCharArray();

    private static Gen<string> GeradorSegredo() =>
        Gen.Choose(8, 40).SelectMany(n =>
            Gen.ArrayOf(Gen.Elements(AlfabetoSegredo), n).Select(cs => new string(cs)));

    private static Gen<string> GeradorTexto() =>
        Gen.Choose(0, 30).SelectMany(n =>
            Gen.ArrayOf(Gen.Elements(AlfabetoTexto), n).Select(cs => new string(cs)));

    // Feature: ia-mentor-carreira, Property 23: Segredos não vazam
    // **Validates: Requirements 8.6**
    [Property(MaxTest = 100)]
    public Property Saida_nao_contem_nenhum_segredo_configurado()
    {
        var gen =
            from chaveApi in GeradorSegredo()
            from conexao in GeradorSegredo()
            from antes in GeradorTexto()
            from meio in GeradorTexto()
            from depois in GeradorTexto()
            select (chaveApi, conexao, texto: antes + chaveApi + meio + conexao + depois);

        return Prop.ForAll(gen.ToArbitrary(), t =>
        {
            var redator = new RedatorDeSegredos(new[] { t.chaveApi, t.conexao });
            var saida = redator.Redigir(t.texto);
            return !saida.Contains(t.chaveApi, StringComparison.Ordinal)
                && !saida.Contains(t.conexao, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Mascara_chave_sk_e_connection_string_e_preserva_texto_sem_segredo()
    {
        var redator = new RedatorDeSegredos(new[] { "minha-chave-secreta" });
        var saida = redator.Redigir("k=minha-chave-secreta Host=db.interno;Password=abc123;ok");
        Assert.DoesNotContain("minha-chave-secreta", saida);
        Assert.DoesNotContain("abc123", saida);
        Assert.DoesNotContain("db.interno", saida);
        Assert.Equal("texto normal", redator.Redigir("texto normal"));
        Assert.Equal(string.Empty, redator.Redigir(null));
    }
}
