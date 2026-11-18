using CouchIA.Application.Mentor.Services;
using CouchIA.Domain;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace CouchIA.Application.Tests;

public class PlanoDuracaoTests
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
            var pre = i == 0
                ? new List<HabilidadeId>()
                : s.Pre.Select(p => ids[p % i]).Distinct().ToList();
            return new Habilidade(ids[i], $"H{i}", "Descrição.", s.Horas,
                new[] { new RecursoEstudo("R", new Uri($"https://exemplo.com/{i}")) }, pre);
        }).ToList();

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

    // Feature: ia-mentor-carreira, Property 17: Duração do plano (etapa = ceil(horas/disponibilidade), total = soma, até 104 semanas)
    // **Validates: Requirements 5.4, 5.5**
    [Property(MaxTest = 100)]
    public Property Duracao_da_etapa_e_ceil_das_horas_e_total_e_a_soma_ate_104_semanas()
    {
        return Prop.ForAll(GeradorEntrada().ToArbitrary(), e =>
        {
            var resultado = PlanoDesenvolvimentoService.Gerar(e.Catalogo, Area, e.Dominadas, e.Horas);
            if (!resultado.Sucesso) return true;

            var plano = resultado.Plano!;
            var horasPorHabilidade = e.Catalogo.Habilidades.ToDictionary(h => h.Id, h => h.HorasEstimadas);

            foreach (var etapa in plano.Etapas)
            {
                var horas = etapa.Habilidades.Sum(h => horasPorHabilidade[h]);
                var esperado = (int)Math.Ceiling(horas / (double)e.Horas);
                if (etapa.DuracaoSemanas != esperado) return false;
            }

            var soma = plano.Etapas.Sum(x => x.DuracaoSemanas);
            return plano.DuracaoTotalSemanas == soma
                && soma <= PlanoDesenvolvimentoService.DuracaoTotalMaximaSemanas;
        });
    }
}
