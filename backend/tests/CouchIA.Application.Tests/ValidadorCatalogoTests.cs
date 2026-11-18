using CouchIA.Application.Mentor.Services;
using CouchIA.Domain;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace CouchIA.Application.Tests;

public class ValidadorCatalogoTests
{
    private enum Violacao
    {
        NomeHabilidadeVazio, NomeHabilidadeLongo, DescricaoLonga, DescricaoQuatroFrases, DescricaoVazia,
        HorasZero, SemRecursos, AutoReferencia, PreRequisitoInexistente, PreRequisitoDuplicado,
        OnzePreRequisitos, Ciclo, IdDuplicado,
        NomeAreaVazio, NomeAreaDuplicado, DescricaoAreaLonga, AreaSemHabilidades, AreaComHabilidadeInexistente
    }

    private static readonly Violacao[] Todas = Enum.GetValues<Violacao>();

    private static Habilidade H(int i, string? nome = null, string? desc = null, int horas = 10,
        IReadOnlyList<RecursoEstudo>? recursos = null, IReadOnlyList<HabilidadeId>? pre = null)
        => new(new HabilidadeId($"h{i}"), nome ?? $"Habilidade {i}", desc ?? "Descricao valida.", horas,
            recursos ?? new[] { new RecursoEstudo("Curso", new Uri("https://exemplo.com/a")) },
            pre ?? Array.Empty<HabilidadeId>());

    // Catálogo válido: pré-requisitos só apontam para índices menores (grafo acíclico), sem repetição.
    private static (List<AreaAtuacao> Areas, List<Habilidade> Habs) CatalogoValido(Random rnd)
    {
        var n = rnd.Next(2, 9);
        var habs = new List<Habilidade>();
        for (var i = 0; i < n; i++)
        {
            var pre = Enumerable.Range(0, i).Where(_ => rnd.Next(3) == 0).Take(10)
                .Select(j => new HabilidadeId($"h{j}")).ToList();
            var frases = rnd.Next(1, 4);
            var desc = string.Join(" ", Enumerable.Range(1, frases).Select(f => $"Frase {f}."));
            habs.Add(H(i, nome: new string('n', rnd.Next(1, 101)), desc: desc, horas: rnd.Next(1, 501),
                recursos: Enumerable.Range(0, rnd.Next(1, 4))
                    .Select(r => new RecursoEstudo($"R{r}", new Uri($"https://exemplo.com/{r}"))).ToList(),
                pre: pre));
        }

        var areas = new List<AreaAtuacao>();
        var qtdAreas = rnd.Next(1, 4);
        for (var a = 0; a < qtdAreas; a++)
        {
            var itens = habs.Where(_ => rnd.Next(2) == 0).ToList();
            if (itens.Count == 0) itens.Add(habs[0]);
            areas.Add(new AreaAtuacao(new AreaId($"a{a}"), $"Area {a}", new string('d', rnd.Next(1, 1001)),
                itens.Select(h => new HabilidadeNaArea(h.Id, PrioridadeHabilidade.Essencial)).ToList()));
        }
        return (areas, habs);
    }

    private static Catalogo Montar(List<AreaAtuacao> areas, List<Habilidade> habs) => new(areas, habs);

    // Aplica a violação e devolve (tipo esperado, ids de habilidade aceitos, id de área esperado).
    private static (TipoErroCatalogo Tipo, string[]? Habs, string? Area) Injetar(
        Violacao v, List<AreaAtuacao> areas, List<Habilidade> habs, Random rnd)
    {
        var k = rnd.Next(habs.Count);
        var alvo = habs[k];
        var id = alvo.Id.Valor;
        var ids = new[] { id };

        switch (v)
        {
            case Violacao.NomeHabilidadeVazio:
                habs[k] = alvo with { Nome = "   " };
                return (TipoErroCatalogo.NomeHabilidadeInvalido, ids, null);
            case Violacao.NomeHabilidadeLongo:
                habs[k] = alvo with { Nome = new string('x', 101) };
                return (TipoErroCatalogo.NomeHabilidadeInvalido, ids, null);
            case Violacao.DescricaoLonga:
                habs[k] = alvo with { Descricao = new string('x', 301) };
                return (TipoErroCatalogo.DescricaoHabilidadeInvalida, ids, null);
            case Violacao.DescricaoQuatroFrases:
                habs[k] = alvo with { Descricao = "Um. Dois. Tres. Quatro." };
                return (TipoErroCatalogo.DescricaoHabilidadeInvalida, ids, null);
            case Violacao.DescricaoVazia:
                habs[k] = alvo with { Descricao = "" };
                return (TipoErroCatalogo.DescricaoHabilidadeInvalida, ids, null);
            case Violacao.HorasZero:
                habs[k] = alvo with { HorasEstimadas = rnd.Next(2) == 0 ? 0 : 501 };
                return (TipoErroCatalogo.HorasEstimadasInvalidas, ids, null);
            case Violacao.SemRecursos:
                habs[k] = alvo with { Recursos = Array.Empty<RecursoEstudo>() };
                return (TipoErroCatalogo.QuantidadeRecursosInvalida, ids, null);
            case Violacao.AutoReferencia:
                habs[k] = alvo with { PreRequisitos = alvo.PreRequisitos.Append(alvo.Id).ToList() };
                return (TipoErroCatalogo.PreRequisitoAutoReferencia, ids, null);
            case Violacao.PreRequisitoInexistente:
                habs[k] = alvo with { PreRequisitos = alvo.PreRequisitos.Append(new HabilidadeId("inexistente")).ToList() };
                return (TipoErroCatalogo.PreRequisitoInexistente, ids, null);
            case Violacao.PreRequisitoDuplicado:
            {
                var outro = habs[(k + 1) % habs.Count].Id;
                if (outro == alvo.Id) outro = habs[(k + 2) % habs.Count].Id;
                // Remove o par antes para evitar repetição acidental além da injetada.
                var pre = alvo.PreRequisitos.Where(p => p != outro).Append(outro).Append(outro).ToList();
                habs[k] = alvo with { PreRequisitos = pre };
                // Pode também gerar ciclo, mas o erro de duplicidade deve estar presente na habilidade alvo.
                return (TipoErroCatalogo.PreRequisitoDuplicado, ids, null);
            }
            case Violacao.OnzePreRequisitos:
                habs[k] = alvo with
                {
                    PreRequisitos = Enumerable.Range(0, 11).Select(i => new HabilidadeId($"x{i}")).ToList()
                };
                return (TipoErroCatalogo.QuantidadePreRequisitosInvalida, ids, null);
            case Violacao.Ciclo:
            {
                // h1 já pode depender de h0; força h1 -> h0 e h0 -> h1.
                var h0 = habs[0];
                var h1 = habs[1];
                if (!h1.PreRequisitos.Contains(h0.Id))
                    habs[1] = h1 with { PreRequisitos = h1.PreRequisitos.Append(h0.Id).ToList() };
                habs[0] = h0 with { PreRequisitos = h0.PreRequisitos.Append(h1.Id).ToList() };
                return (TipoErroCatalogo.CicloDeDependencia, new[] { h0.Id.Valor, h1.Id.Valor }, null);
            }
            case Violacao.IdDuplicado:
                habs.Add(alvo);
                return (TipoErroCatalogo.IdentificadorHabilidadeDuplicado, ids, null);
        }

        var ak = rnd.Next(areas.Count);
        var area = areas[ak];
        var areaId = area.Id.Valor;
        switch (v)
        {
            case Violacao.NomeAreaVazio:
                areas[ak] = area with { Nome = "" };
                return (TipoErroCatalogo.NomeAreaInvalido, null, areaId);
            case Violacao.NomeAreaDuplicado:
                if (areas.Count < 2)
                    areas.Add(new AreaAtuacao(new AreaId("extra"), "Outra", "d", area.Habilidades));
                var segunda = areas.Count - 1 == ak ? 0 : areas.Count - 1;
                areas[segunda] = areas[segunda] with { Nome = areas[ak == segunda ? 0 : ak].Nome };
                // O validador reporta a segunda ocorrência na ordem de iteração; basta que seja uma das duas.
                return (TipoErroCatalogo.NomeAreaDuplicado, null, null);
            case Violacao.DescricaoAreaLonga:
                areas[ak] = area with { Descricao = new string('d', 1001) };
                return (TipoErroCatalogo.DescricaoAreaInvalida, null, areaId);
            case Violacao.AreaSemHabilidades:
                areas[ak] = area with { Habilidades = Array.Empty<HabilidadeNaArea>() };
                return (TipoErroCatalogo.QuantidadeHabilidadesAreaInvalida, null, areaId);
            default:
                areas[ak] = area with
                {
                    Habilidades = area.Habilidades
                        .Append(new HabilidadeNaArea(new HabilidadeId("fantasma"), PrioridadeHabilidade.Essencial)).ToList()
                };
                return (TipoErroCatalogo.HabilidadeDaAreaInexistente, null, areaId);
        }
    }

    // Feature: ia-mentor-carreira, Property 15: Validação do catálogo
    // **Validates: Requirements 4.5, 6.1, 6.2, 6.3, 6.4**
    [Property(MaxTest = 200)]
    public Property Catalogo_valido_e_aceito()
    {
        return Prop.ForAll(Gen.Choose(0, int.MaxValue).ToArbitrary(), seed =>
        {
            var (areas, habs) = CatalogoValido(new Random(seed));
            var r = ValidadorCatalogo.Validar(Montar(areas, habs));
            return r.EhValido && r.Erros.Count == 0;
        });
    }

    // Feature: ia-mentor-carreira, Property 15: Validação do catálogo
    // **Validates: Requirements 4.5, 6.1, 6.2, 6.3, 6.4**
    [Property(MaxTest = 300)]
    public Property Catalogo_com_uma_violacao_e_rejeitado_identificando_habilidade_e_tipo()
    {
        var gen = Gen.Choose(0, int.MaxValue)
            .SelectMany(seed => Gen.Choose(0, Todas.Length - 1).Select(vi => (seed, vi)));

        return Prop.ForAll(gen.ToArbitrary(), x =>
        {
            var rnd = new Random(x.seed);
            var (areas, habs) = CatalogoValido(rnd);
            var (tipo, habsEsperadas, areaEsperada) = Injetar(Todas[x.vi], areas, habs, rnd);

            var r = ValidadorCatalogo.Validar(Montar(areas, habs));

            return !r.EhValido && r.Erros.Any(e =>
                e.Tipo == tipo
                && (habsEsperadas is null || (e.HabilidadeId is not null && habsEsperadas.Contains(e.HabilidadeId)))
                && (areaEsperada is null || e.AreaId == areaEsperada));
        });
    }
}
