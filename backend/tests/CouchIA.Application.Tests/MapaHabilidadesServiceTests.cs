using CouchIA.Application.Mentor.Services;
using CouchIA.Domain;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace CouchIA.Application.Tests;

public class MapaHabilidadesServiceTests
{
    private sealed record Cenario(Catalogo Catalogo, AreaId Area, HabilidadeId[] Dominadas);

    private static Habilidade H(int i) => new(
        new HabilidadeId($"h{i}"), $"Habilidade {i}", "Descricao valida.", 10,
        new[] { new RecursoEstudo("Curso", new Uri("https://exemplo.com/a")) },
        Array.Empty<HabilidadeId>());

    private static Gen<Cenario> GeradorCenario()
    {
        return Gen.Choose(1, 12).SelectMany(n =>
            Gen.Choose(1, 3).SelectMany(qtdAreas =>
                Gen.Choose(0, int.MaxValue).Select(seed =>
                {
                    var rnd = new Random(seed);
                    var habs = Enumerable.Range(0, n).Select(H).ToList();
                    var prioridades = Enum.GetValues<PrioridadeHabilidade>();
                    var areas = new List<AreaAtuacao>();
                    for (var a = 0; a < qtdAreas; a++)
                    {
                        var itens = habs.Where(_ => rnd.Next(2) == 0).OrderBy(_ => rnd.Next()).ToList();
                        if (itens.Count == 0) itens.Add(habs[rnd.Next(n)]);
                        areas.Add(new AreaAtuacao(
                            new AreaId($"a{a}"), $"Area {a}", "Descricao",
                            itens.Select(h => new HabilidadeNaArea(h.Id, prioridades[rnd.Next(prioridades.Length)])).ToList()));
                    }

                    // Dominadas: subconjunto aleatório do catálogo + ids fora dele.
                    var dominadas = habs.Where(_ => rnd.Next(2) == 0).Select(h => h.Id)
                        .Concat(new[] { new HabilidadeId("fora-do-catalogo") }.Where(_ => rnd.Next(2) == 0))
                        .ToArray();

                    return new Cenario(new Catalogo(areas, habs), areas[rnd.Next(qtdAreas)].Id, dominadas);
                })));
    }

    // Feature: ia-mentor-carreira, Property 9: Mapa de Habilidades
    // **Validates: Requirements 4.1, 4.2, 4.3, 4.4**
    [Property(MaxTest = 100)]
    public Property Mapa_contem_exatamente_as_habilidades_da_area_ordenado_e_com_dominadas_marcadas()
    {
        return Prop.ForAll(GeradorCenario().ToArbitrary(), c =>
        {
            var resultado = MapaHabilidadesService.Montar(c.Catalogo, c.Area, c.Dominadas);
            if (!resultado.Sucesso) return false;

            var area = c.Catalogo.Areas.First(a => a.Id.Equals(c.Area));
            var mapa = resultado.Mapa!.Habilidades;

            // Ordem esperada: Essencial, Importante, Desejável; ordem do catálogo dentro do mesmo nível (estável).
            var esperado = area.Habilidades.Select((v, idx) => (v, idx))
                .OrderBy(x => (int)x.v.Prioridade).ThenBy(x => x.idx)
                .Select(x => x.v).ToList();

            if (mapa.Count != esperado.Count) return false;
            if (!mapa.Select(m => (m.Id, m.Prioridade)).SequenceEqual(esperado.Select(e => (e.Habilidade, e.Prioridade))))
                return false;

            var interseccao = c.Dominadas.Intersect(area.Habilidades.Select(v => v.Habilidade)).ToHashSet();
            var marcadas = mapa.Where(m => m.Dominada).Select(m => m.Id).ToHashSet();
            return marcadas.SetEquals(interseccao)
                && mapa.All(m => m.Dominada == interseccao.Contains(m.Id));
        });
    }

    // Feature: ia-mentor-carreira, Property 9: Mapa de Habilidades
    // **Validates: Requirements 4.1, 4.2, 4.3, 4.4**
    [Property(MaxTest = 100)]
    public Property Marcar_dominadas_nao_altera_posicoes()
    {
        return Prop.ForAll(GeradorCenario().ToArbitrary(), c =>
        {
            var sem = MapaHabilidadesService.Montar(c.Catalogo, c.Area, Array.Empty<HabilidadeId>());
            var com = MapaHabilidadesService.Montar(c.Catalogo, c.Area, c.Dominadas);
            return sem.Sucesso && com.Sucesso
                && sem.Mapa!.Habilidades.Select(m => m.Id).SequenceEqual(com.Mapa!.Habilidades.Select(m => m.Id));
        });
    }

    [Fact]
    public void Area_inexistente_ou_catalogo_nulo_resulta_em_erro()
    {
        var catalogo = new Catalogo(Array.Empty<AreaAtuacao>(), Array.Empty<Habilidade>());
        Assert.Equal(TipoErroMapa.AreaInexistente,
            MapaHabilidadesService.Montar(catalogo, new AreaId("x"), null).Erro);
        Assert.Equal(TipoErroMapa.CatalogoIndisponivel,
            MapaHabilidadesService.Montar(null, new AreaId("x"), null).Erro);
    }
}
