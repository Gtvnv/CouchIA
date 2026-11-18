using CouchIA.Application.Mentor.Services;
using CouchIA.Domain;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace CouchIA.Application.Tests;

public class PlanoEstruturaTests
{
    private static readonly AreaId Area = new("area-teste");

    private sealed record Entrada(Catalogo Catalogo, IReadOnlySet<HabilidadeId> Dominadas, int Horas);

    private sealed record Semente(int Horas, int Prioridade, bool Dominada, int[] Pre);

    private static Gen<Semente> GeradorSemente() =>
        Gen.Choose(1, 500).SelectMany(horas =>
        Gen.Choose(0, 2).SelectMany(prio =>
        Gen.Elements(true, false, false).SelectMany(dom =>
        Gen.ArrayOf(Gen.Choose(0, 1000), 3).Select(pre => new Semente(horas, prio, dom, pre)))));

    private static Gen<Entrada> GeradorEntrada() =>
        Gen.Choose(1, 30).SelectMany(n =>
        Gen.ArrayOf(GeradorSemente(), n).SelectMany(sementes =>
        Gen.Choose(1, 168).SelectMany(horas =>
        Gen.ArrayOf(Gen.Choose(0, 1_000_000), n).Select(chaves => Montar(sementes, chaves, horas)))));

    private static Entrada Montar(Semente[] sementes, int[] chaves, int horas)
    {
        var ids = sementes.Select((_, i) => new HabilidadeId($"h{i}")).ToArray();
        var habilidades = sementes.Select((s, i) =>
        {
            // Pré-requisitos só para índices anteriores: grafo acíclico.
            var pre = i == 0
                ? new List<HabilidadeId>()
                : s.Pre.Select(p => ids[p % i]).Distinct().ToList();
            return new Habilidade(ids[i], $"H{i}", "Descrição.", s.Horas,
                new[] { new RecursoEstudo("R", new Uri($"https://exemplo.com/{i}")) }, pre);
        }).ToList();

        // Ordem da área embaralhada para não coincidir com a ordem topológica.
        var naArea = Enumerable.Range(0, sementes.Length)
            .OrderBy(i => chaves[i]).ThenBy(i => i)
            .Select(i => new HabilidadeNaArea(ids[i], (PrioridadeHabilidade)sementes[i].Prioridade))
            .ToList();

        var catalogo = new Catalogo(
            new[] { new AreaAtuacao(Area, "Área", "Descrição", naArea) },
            habilidades);
        var dominadas = sementes.Select((s, i) => (s, i)).Where(x => x.s.Dominada)
            .Select(x => ids[x.i]).ToHashSet();
        return new Entrada(catalogo, dominadas, horas);
    }

    // Feature: ia-mentor-carreira, Property 16: Estrutura do plano (etapas em ordem topológica, até 20 etapas, cobertura das pendentes)
    // **Validates: Requirements 5.1, 5.2, 5.3**
    [Property(MaxTest = 100)]
    public Property Plano_gerado_tem_ordem_topologica_ate_20_etapas_e_cobre_as_pendentes()
    {
        return Prop.ForAll(GeradorEntrada().ToArbitrary(), e =>
        {
            var resultado = PlanoDesenvolvimentoService.Gerar(e.Catalogo, Area, e.Dominadas, e.Horas);
            if (!resultado.Sucesso) return true; // limites excedidos: outras propriedades cobrem

            var plano = resultado.Plano!;
            if (plano.Etapas.Count < 1 || plano.Etapas.Count > PlanoDesenvolvimentoService.EtapasMaximo) return false;

            var posicao = new Dictionary<HabilidadeId, (int Etapa, int Indice)>();
            foreach (var etapa in plano.Etapas)
                for (var k = 0; k < etapa.Habilidades.Count; k++)
                    if (!posicao.TryAdd(etapa.Habilidades[k], (etapa.Numero, k))) return false; // duplicada

            var pendentes = e.Catalogo.Areas[0].Habilidades
                .Select(h => h.Habilidade).Where(h => !e.Dominadas.Contains(h)).ToHashSet();
            if (!posicao.Keys.ToHashSet().SetEquals(pendentes)) return false;

            foreach (var h in e.Catalogo.Habilidades.Where(h => posicao.ContainsKey(h.Id)))
                foreach (var pre in h.PreRequisitos.Where(posicao.ContainsKey))
                    if (posicao[pre].CompareTo(posicao[h.Id]) >= 0) return false;

            return true;
        });
    }

    [Fact]
    public void Cadeia_de_prerequisitos_declarada_em_ordem_inversa_sai_em_ordem_topologica()
    {
        var a = new HabilidadeId("a");
        var b = new HabilidadeId("b");
        var rec = new[] { new RecursoEstudo("R", new Uri("https://exemplo.com/r")) };
        var catalogo = new Catalogo(
            new[]
            {
                new AreaAtuacao(Area, "Área", "D", new[]
                {
                    new HabilidadeNaArea(b, PrioridadeHabilidade.Essencial),
                    new HabilidadeNaArea(a, PrioridadeHabilidade.Desejavel)
                })
            },
            new[]
            {
                new Habilidade(a, "A", "D.", 10, rec, Array.Empty<HabilidadeId>()),
                new Habilidade(b, "B", "D.", 10, rec, new[] { a })
            });

        var resultado = PlanoDesenvolvimentoService.Gerar(catalogo, Area, new HashSet<HabilidadeId>(), 10);

        Assert.True(resultado.Sucesso);
        Assert.Equal(new[] { a, b }, resultado.Plano!.Etapas.SelectMany(x => x.Habilidades).ToArray());
    }
}
