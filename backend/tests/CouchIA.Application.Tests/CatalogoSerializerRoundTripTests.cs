using CouchIA.Application.Mentor.Services;
using CouchIA.Domain;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace CouchIA.Application.Tests;

public class CatalogoSerializerRoundTripTests
{
    private static readonly string[] Textos =
    {
        "Simples", "Açúcar e ção", "Com \"aspas\" e \\ barra", "Tag <b>&</b>", "Emoji 😀 e 日本語",
        "Linha\ncom quebra", "Tab\tinterna", "  espaços  "
    };

    private static readonly string[] Urls =
    {
        "https://exemplo.com/a", "https://exemplo.com/a?x=1&y=2#frag", "http://exemplo.com:8080/p/q",
        "https://exemplo.com/caminho%20com%20espaco", "https://exemplo.com"
    };

    private static string Texto(Random rnd) => Textos[rnd.Next(Textos.Length)];

    private static Catalogo Gerar(Random rnd)
    {
        var n = rnd.Next(0, 9);
        var habs = new List<Habilidade>();
        for (var i = 0; i < n; i++)
        {
            var pre = Enumerable.Range(0, i).Where(_ => rnd.Next(3) == 0)
                .Select(j => new HabilidadeId($"h{j}")).ToList();
            var recursos = Enumerable.Range(0, rnd.Next(0, 4))
                .Select(_ => new RecursoEstudo(Texto(rnd), new Uri(Urls[rnd.Next(Urls.Length)]))).ToList();
            habs.Add(new Habilidade(new HabilidadeId($"h{i}"), Texto(rnd), Texto(rnd), rnd.Next(1, 501), recursos, pre));
        }

        var prioridades = Enum.GetValues<PrioridadeHabilidade>();
        var areas = new List<AreaAtuacao>();
        for (var a = 0; a < rnd.Next(0, 4); a++)
        {
            var itens = habs.Where(_ => rnd.Next(2) == 0)
                .OrderBy(_ => rnd.Next())
                .Select(h => new HabilidadeNaArea(h.Id, prioridades[rnd.Next(prioridades.Length)])).ToList();
            areas.Add(new AreaAtuacao(new AreaId($"a{a}"), Texto(rnd), Texto(rnd), itens));
        }
        return new Catalogo(areas, habs);
    }

    private static bool Equivalentes(Catalogo x, Catalogo y) =>
        x.Areas.Count == y.Areas.Count && x.Habilidades.Count == y.Habilidades.Count
        && x.Areas.Zip(y.Areas).All(p =>
            p.First.Id == p.Second.Id && p.First.Nome == p.Second.Nome && p.First.Descricao == p.Second.Descricao
            && p.First.Habilidades.SequenceEqual(p.Second.Habilidades))
        && x.Habilidades.Zip(y.Habilidades).All(p =>
            p.First.Id == p.Second.Id && p.First.Nome == p.Second.Nome && p.First.Descricao == p.Second.Descricao
            && p.First.HorasEstimadas == p.Second.HorasEstimadas
            && p.First.PreRequisitos.SequenceEqual(p.Second.PreRequisitos)
            && p.First.Recursos.Count == p.Second.Recursos.Count
            && p.First.Recursos.Zip(p.Second.Recursos).All(r =>
                r.First.Titulo == r.Second.Titulo && r.First.Url.OriginalString == r.Second.Url.OriginalString));

    // Feature: ia-mentor-carreira, Property 28: Round-trip do catálogo
    // **Validates: Requirements 6.8**
    [Property(MaxTest = 200)]
    public Property Serializar_e_desserializar_produz_catalogo_equivalente()
    {
        return Prop.ForAll(Gen.Choose(0, int.MaxValue).ToArbitrary(), seed =>
        {
            var original = Gerar(new Random(seed));
            var resultado = CatalogoSerializer.Desserializar(CatalogoSerializer.Serializar(original));
            return resultado.Sucesso && Equivalentes(original, resultado.Catalogo!);
        });
    }

    // Feature: ia-mentor-carreira, Property 28: Round-trip do catálogo (bytes UTF-8 estáveis)
    // **Validates: Requirements 6.8**
    [Property(MaxTest = 100)]
    public Property Segunda_serializacao_e_identica_a_primeira()
    {
        return Prop.ForAll(Gen.Choose(0, int.MaxValue).ToArbitrary(), seed =>
        {
            var json = CatalogoSerializer.Serializar(Gerar(new Random(seed)));
            var lido = CatalogoSerializer.Desserializar(json);
            return lido.Sucesso && CatalogoSerializer.Serializar(lido.Catalogo!) == json;
        });
    }
}
