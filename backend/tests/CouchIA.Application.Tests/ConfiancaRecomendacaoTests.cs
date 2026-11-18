using CouchIA.Application.Mentor.Services;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace CouchIA.Application.Tests;

public class ConfiancaRecomendacaoTests
{
    // Feature: ia-mentor-carreira, Property 8: Aviso de baixa confiança
    // **Validates: Requirements 3.6**
    [Property(MaxTest = 100)]
    public Property BaixaConfianca_se_e_somente_se_menos_de_3_categorias()
    {
        return Prop.ForAll(Gen.Choose(0, 10).ToArbitrary(), n =>
            ConfiancaRecomendacao.BaixaConfianca(n) == (n < 3));
    }

    [Fact]
    public void Limites_0_2_3_e_10()
    {
        Assert.True(ConfiancaRecomendacao.BaixaConfianca(0));
        Assert.True(ConfiancaRecomendacao.BaixaConfianca(2));
        Assert.False(ConfiancaRecomendacao.BaixaConfianca(3));
        Assert.False(ConfiancaRecomendacao.BaixaConfianca(10));
    }
}
