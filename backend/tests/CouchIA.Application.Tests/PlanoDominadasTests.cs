using CouchIA.Application.Mentor.Services;
using CouchIA.Domain;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace CouchIA.Application.Tests;

public class PlanoDominadasTests
{
    private static readonly AreaId Area = new("area-teste");

    private sealed record Entrada(Catalogo Catalogo, IReadOnlySet<HabilidadeId> Dominadas, IReadOnlySet<HabilidadeId> NaArea);

    private sealed record Semente(bool NaArea, bool Dominada, int[] Pre);

    private static Gen<Semente> GeradorSemente() =>
        Gen.Elements(true, true, false).SelectMany(naArea =>
        Gen.Elements(true, false, false).SelectMany(dom =>
        Gen.ArrayOf(Gen.Choose(0, 1000), 3).Select(pre => new Semente(naArea, dom, pre))));

    private static Gen<Entrada> GeradorEntrada() =>
        Gen.Choose(1, 25).SelectMany(n =>
        Gen.ArrayOf(GeradorSemente(), n).Select(Montar));

    private static Entrada Montar(Semente[] sementes)
    {
        var ids = sementes.Select((_, i) => new HabilidadeId($"h{i}")).ToArray();
        var rec = new[] { new RecursoEstudo("R", new Uri("https://exemplo.com/r")) };
        var habilidades = sementes.Select((s, i) =>
        {
            // Pré-requisitos só para índices anteriores (acíclico); podem apontar para fora da área.
            var pre = i == 0
                ? new List<HabilidadeId>()
                : s.Pre.Select(p => ids[p % i]).Distinct().ToList();
            return new Habilidade(ids[i], $"H{i}", "Descrição.", 1, rec, pre);
        }).ToList();

        var naArea = sementes.Select((s, i) => (s, i)).Where(x => x.s.NaArea)
            .Select(x => new HabilidadeNaArea(ids[x.i], PrioridadeHabilidade.Essencial)).ToList();
        if (naArea.Count == 0)
            naArea.Add(new HabilidadeNaArea(ids[0], PrioridadeHabilidade.Essencial));

        var catalogo = new Catalogo(
            new[] { new AreaAtuacao(Area, "Área", "Descrição", naArea) },
            habilidades);
        var dominadas = sementes.Select((s, i) => (s, i)).Where(x => x.s.Dominada)
            .Select(x => ids[x.i]).ToHashSet();
        return new Entrada(catalogo, dominadas, naArea.Select(x => x.Habilidade).ToHashSet());
    }

    // Feature: ia-mentor-carreira, Property 18: Dominadas e pré-requisitos (dominadas fora do plano; pré-requisito fora do plano gera aviso)
    // **Validates: Requirements 5.7, 5.8**
    [Property(MaxTest = 100)]
    public Property Dominadas_ficam_fora_do_plano_e_prerequisito_fora_do_plano_gera_aviso()
    {
        return Prop.ForAll(GeradorEntrada().ToArbitrary(), e =>
        {
            var resultado = PlanoDesenvolvimentoService.Gerar(e.Catalogo, Area, e.Dominadas, 168);

            var pendentes = e.NaArea.Where(h => !e.Dominadas.Contains(h)).ToHashSet();
            if (pendentes.Count == 0)
                return resultado.Status == StatusGeracaoPlano.SemHabilidadesPendentes;

            // Plano limitado a 20 etapas/104 semanas: com 168 h/semana e 1 h por Habilidade (máx. 25) sempre gera.
            if (!resultado.Sucesso) return false;

            var noPlano = resultado.Plano!.Etapas.SelectMany(x => x.Habilidades).ToList();
            if (noPlano.Any(e.Dominadas.Contains)) return false;
            if (!noPlano.ToHashSet().SetEquals(pendentes)) return false;

            var esperados = e.Catalogo.Habilidades
                .Where(h => pendentes.Contains(h.Id))
                .SelectMany(h => h.PreRequisitos
                    .Where(p => !e.Dominadas.Contains(p) && !pendentes.Contains(p))
                    .Select(p => (h.Id, p)))
                .ToHashSet();
            var obtidos = resultado.Avisos.Select(a => (a.Habilidade, a.PreRequisito)).ToList();

            return obtidos.Count == esperados.Count && obtidos.ToHashSet().SetEquals(esperados);
        });
    }

    [Fact]
    public void Prerequisito_fora_da_area_e_nao_dominado_gera_aviso_e_dominado_nao()
    {
        var a = new HabilidadeId("a");
        var b = new HabilidadeId("b");
        var rec = new[] { new RecursoEstudo("R", new Uri("https://exemplo.com/r")) };
        var catalogo = new Catalogo(
            new[] { new AreaAtuacao(Area, "Área", "D", new[] { new HabilidadeNaArea(b, PrioridadeHabilidade.Essencial) }) },
            new[]
            {
                new Habilidade(a, "A", "D.", 10, rec, Array.Empty<HabilidadeId>()),
                new Habilidade(b, "B", "D.", 10, rec, new[] { a })
            });

        var sem = PlanoDesenvolvimentoService.Gerar(catalogo, Area, new HashSet<HabilidadeId>(), 10);
        var com = PlanoDesenvolvimentoService.Gerar(catalogo, Area, new HashSet<HabilidadeId> { a }, 10);

        Assert.Equal(new AvisoDependencia(b, a), Assert.Single(sem.Avisos));
        Assert.Empty(com.Avisos);
    }
}
