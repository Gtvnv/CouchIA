using CouchIA.Application.Mentor.Services;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace CouchIA.Application.Tests;

public class ValidadorMensagemTests
{
    private static readonly char[] Alfabeto = { 'a', 'Z', '1', 'é', ' ', ' ', '\t', '\n', '\r' };

    // Strings de 0 a 2100 caracteres, com muitos espaços em branco para cobrir o caso "só whitespace".
    private static Gen<string> GeradorMensagem()
    {
        var comprimento = Gen.OneOf(Gen.Choose(0, 5), Gen.Choose(1995, 2005), Gen.Choose(0, 2100));
        return comprimento.SelectMany(n =>
            Gen.ArrayOf(Gen.Elements(Alfabeto), n).Select(cs => new string(cs)));
    }

    // Feature: ia-mentor-carreira, Property 2: Validação de mensagem
    // **Validates: Requirements 1.6, 2.8**
    [Property(MaxTest = 100)]
    public Property Aceita_se_e_somente_se_trim_nao_vazio_e_tamanho_entre_1_e_2000()
    {
        return Prop.ForAll(GeradorMensagem().ToArbitrary(), s =>
        {
            var esperado = s.Trim().Length > 0 && s.Length >= 1 && s.Length <= 2000;
            return ValidadorMensagem.EhValida(s) == esperado;
        });
    }

    [Fact]
    public void Nulo_vazio_e_whitespace_sao_rejeitados_e_limites_sao_respeitados()
    {
        Assert.False(ValidadorMensagem.EhValida(null));
        Assert.False(ValidadorMensagem.EhValida(""));
        Assert.False(ValidadorMensagem.EhValida("   \t\n"));
        Assert.True(ValidadorMensagem.EhValida("a"));
        Assert.True(ValidadorMensagem.EhValida(new string('a', 2000)));
        Assert.False(ValidadorMensagem.EhValida(new string('a', 2001)));
    }
}
