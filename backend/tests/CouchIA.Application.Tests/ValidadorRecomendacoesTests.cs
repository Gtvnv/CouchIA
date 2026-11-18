using CouchIA.Application.Mentor.Services;
using CouchIA.Domain;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace CouchIA.Application.Tests;

public class ValidadorRecomendacoesTests
{
    private static readonly string[] Ids = { "backend", "frontend", "dados", "devops", "qa", "mobile" };

    private static Catalogo Catalogo() => new(
        Ids.Select(id => new AreaAtuacao(new AreaId(id), id, "desc", Array.Empty<HabilidadeNaArea>())).ToList(),
        Array.Empty<Habilidade>());

    private static PerfilUsuario Perfil() => new(
        new[] { "APIs" }, null, null, null, 10, null, new HashSet<HabilidadeId>());

    private static CandidatoRecomendacao Cand(string area, double pont, string just = "Gosta de APIs.")
        => new(area, pont, just);

    // Feature: ia-mentor-carreira, Property 7: Recomendações válidas
    // **Validates: Requirements 3.1, 3.2, 3.3, 3.4**
    [Property]
    public Property Resultado_aceito_satisfaz_todas_as_regras()
    {
        // Escolhe de 2 a 6 itens; índice == Ids.Length representa área fora do catálogo.
        // j: 0 = cita "APIs", 1 = não cita, 2 = vazia, 3 = acima de 500 caracteres.
        var justs = new[] { "Gosta de APIs.", "Sem citar nada.", "  ", "APIs " + new string('x', 500) };
        var item = Gen.Choose(0, Ids.Length).SelectMany(a =>
            Gen.Choose(-5, 105).SelectMany(n => Gen.Frequency(
                new[] { (9, Gen.Constant(0)), (1, Gen.Choose(1, 3)) }).Select(j => (a, n, j))));
        var gen = Gen.Choose(2, 6).SelectMany(n => Gen.ListOf(item).Select(l => l.Take(n).ToList()));

        return Prop.ForAll(gen.ToArbitrary(), itens =>
        {
            var cands = itens
                .Select(x => Cand(x.a < Ids.Length ? Ids[x.a] : "inexistente", x.n, justs[x.j]))
                .ToList();
            var r = ValidadorRecomendacoes.Validar(cands, Catalogo(), Perfil());

            if (!r.Valido) return r.Recomendacoes.Count == 0;

            var lista = r.Recomendacoes;
            return lista.Count is >= 3 and <= 5
                && lista.Select(x => x.Area.Valor).Distinct().Count() == lista.Count
                && lista.All(x => Ids.Contains(x.Area.Valor) && x.Pontuacao is >= 0 and <= 100
                    && x.Justificativa.Length is >= 1 and <= 500
                    && x.Justificativa.Contains("APIs", StringComparison.OrdinalIgnoreCase))
                && lista.Zip(lista.Skip(1), (a, b) => a.Pontuacao >= b.Pontuacao).All(x => x);
        });
    }

    [Fact]
    public void Ordena_decrescente_e_normaliza()
    {
        var r = ValidadorRecomendacoes.Validar(
            new[] { Cand(" backend ", 10, "  gosta de apis "), Cand("dados", 90), Cand("qa", 50) },
            Catalogo(), Perfil());

        Assert.True(r.Valido);
        Assert.Equal(new[] { 90, 50, 10 }, r.Recomendacoes.Select(x => x.Pontuacao));
        Assert.Equal("gosta de apis", r.Recomendacoes[2].Justificativa);
    }

    [Fact]
    public void Rejeita_quantidade_fora_de_3_a_5()
    {
        var dois = new[] { Cand("backend", 1), Cand("dados", 2) };
        var seis = Ids.Select(i => Cand(i, 1)).ToList();
        Assert.False(ValidadorRecomendacoes.Validar(dois, Catalogo(), Perfil()).Valido);
        Assert.False(ValidadorRecomendacoes.Validar(seis, Catalogo(), Perfil()).Valido);
    }

    [Fact]
    public void Rejeita_repeticao_area_fora_do_catalogo_pontuacao_e_justificativa_invalidas()
    {
        var ok = new[] { Cand("dados", 5), Cand("qa", 6) };
        bool Valido(CandidatoRecomendacao c) =>
            ValidadorRecomendacoes.Validar(ok.Append(c).ToList(), Catalogo(), Perfil()).Valido;

        Assert.True(Valido(Cand("backend", 7)));
        Assert.False(Valido(Cand("dados", 7)));                       // repetida
        Assert.False(Valido(Cand("xyz", 7)));                         // fora do catálogo
        Assert.False(Valido(Cand("backend", 101)));
        Assert.False(Valido(Cand("backend", -1)));
        Assert.False(Valido(Cand("backend", 5.5)));                   // não inteira
        Assert.False(Valido(Cand("backend", 7, "   ")));              // vazia
        Assert.False(Valido(Cand("backend", 7, "APIs " + new string('x', 500)))); // > 500
        Assert.False(Valido(Cand("backend", 7, "Sem citar nada.")));  // sem item do perfil
    }
}
