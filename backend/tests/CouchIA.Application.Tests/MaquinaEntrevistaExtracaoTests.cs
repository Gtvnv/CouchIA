using CouchIA.Application.Mentor.Services;
using CouchIA.Domain;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace CouchIA.Application.Tests;

public class MaquinaEntrevistaExtracaoTests
{
    private static readonly char[] Letras = { 'a', 'B', 'z', '1', 'é' };

    private static Gen<string> GeradorTextoUtil()
        => Gen.Choose(1, 20).SelectMany(n => Gen.ArrayOf(Gen.Elements(Letras), n).Select(cs => new string(cs)));

    private static PerfilUsuario PerfilBase() => new(
        new List<string> { "base" },
        "exp-base",
        "formacao-base",
        "estilo-base",
        10,
        "objetivos-base",
        new HashSet<HabilidadeId>());

    private static Entrevista EntrevistaEmAndamento() => MaquinaEntrevista.Iniciar(MaquinaEntrevista.Nova());

    // Projeção do valor de cada categoria no perfil.
    private static object? Campo(PerfilUsuario p, CategoriaEntrevista c) => c switch
    {
        CategoriaEntrevista.Interesses => string.Join("|", p.Interesses),
        CategoriaEntrevista.ExperienciaPrevia => p.ExperienciaPrevia,
        CategoriaEntrevista.Formacao => p.Formacao,
        CategoriaEntrevista.EstiloDeTrabalho => p.EstiloDeTrabalho,
        CategoriaEntrevista.DisponibilidadeSemanal => p.HorasEstudoPorSemana,
        CategoriaEntrevista.Objetivos => p.Objetivos,
        _ => null
    };

    private static ExtracaoEntrevista ExtracaoUtilizavel(CategoriaEntrevista c, string texto, int horas) => c switch
    {
        CategoriaEntrevista.Interesses => new ExtracaoEntrevista(c, true, Interesses: new[] { texto }),
        CategoriaEntrevista.DisponibilidadeSemanal => new ExtracaoEntrevista(c, true, HorasEstudoPorSemana: horas),
        _ => new ExtracaoEntrevista(c, true, Texto: texto)
    };

    // Feature: ia-mentor-carreira, Property 6: Extração atualiza o perfil
    // **Validates: Requirements 2.3, 5.6**
    [Property(MaxTest = 100)]
    public Property Resposta_utilizavel_atualiza_so_o_campo_da_categoria()
    {
        var gen =
            from c in Gen.Elements(MaquinaEntrevista.TodasCategorias.ToArray())
            from t in GeradorTextoUtil()
            from h in Gen.Choose(1, 168)
            select (c, t, h);

        return Prop.ForAll(gen.ToArbitrary(), x =>
        {
            var perfil = PerfilBase();
            var r = MaquinaEntrevista.AplicarExtracao(EntrevistaEmAndamento(), perfil, ExtracaoUtilizavel(x.c, x.t, x.h));

            var respondida = r.Acao == AcaoEntrevista.RespostaAplicada
                && r.Entrevista.Categorias[x.c] == EstadoCategoria.Respondida;
            var esperado = x.c == CategoriaEntrevista.DisponibilidadeSemanal ? (object)x.h : x.t;
            var atualizou = x.c == CategoriaEntrevista.DisponibilidadeSemanal
                ? Equals(Campo(r.Perfil, x.c), esperado)
                : Equals(Campo(r.Perfil, x.c), x.t);
            var demaisIguais = MaquinaEntrevista.TodasCategorias
                .Where(c => c != x.c)
                .All(c => Equals(Campo(r.Perfil, c), Campo(perfil, c)));
            var demaisCategoriasPendentes = MaquinaEntrevista.TodasCategorias
                .Where(c => c != x.c)
                .All(c => r.Entrevista.Categorias[c] == EstadoCategoria.Pendente);

            return respondida && atualizou && demaisIguais && demaisCategoriasPendentes;
        });
    }

    // Feature: ia-mentor-carreira, Property 6: Extração atualiza o perfil
    // **Validates: Requirements 2.3, 5.6**
    [Property(MaxTest = 100)]
    public Property Disponibilidade_aceita_se_e_somente_se_inteiro_entre_1_e_168()
    {
        var gen = Gen.OneOf(Gen.Choose(-10, 10), Gen.Choose(160, 180), Gen.Choose(-500, 500));
        return Prop.ForAll(gen.ToArbitrary(), h =>
        {
            var perfil = PerfilBase();
            var r = MaquinaEntrevista.AplicarExtracao(
                EntrevistaEmAndamento(), perfil,
                new ExtracaoEntrevista(CategoriaEntrevista.DisponibilidadeSemanal, true, HorasEstudoPorSemana: h));

            var aceita = r.Acao == AcaoEntrevista.RespostaAplicada;
            var valida = h >= 1 && h <= 168;
            var perfilOk = aceita ? r.Perfil.HorasEstudoPorSemana == h : r.Perfil.HorasEstudoPorSemana == perfil.HorasEstudoPorSemana;
            return aceita == valida && perfilOk;
        });
    }

    [Fact]
    public void Disponibilidade_sem_valor_e_limites_exatos()
    {
        var c = CategoriaEntrevista.DisponibilidadeSemanal;
        var vazio = MaquinaEntrevista.AplicarExtracao(EntrevistaEmAndamento(), PerfilBase(), new ExtracaoEntrevista(c, true));
        Assert.Equal(AcaoEntrevista.Reformular, vazio.Acao);
        foreach (var h in new[] { 1, 168 })
        {
            Assert.Equal(AcaoEntrevista.RespostaAplicada,
                MaquinaEntrevista.AplicarExtracao(EntrevistaEmAndamento(), PerfilBase(), new ExtracaoEntrevista(c, true, HorasEstudoPorSemana: h)).Acao);
        }
        foreach (var h in new[] { 0, 169 })
        {
            Assert.NotEqual(AcaoEntrevista.RespostaAplicada,
                MaquinaEntrevista.AplicarExtracao(EntrevistaEmAndamento(), PerfilBase(), new ExtracaoEntrevista(c, true, HorasEstudoPorSemana: h)).Acao);
        }
    }
}
