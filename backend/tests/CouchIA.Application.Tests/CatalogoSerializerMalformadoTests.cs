using System.Text.Json.Nodes;
using CouchIA.Application.Mentor.Services;
using CouchIA.Domain;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace CouchIA.Application.Tests;

public class CatalogoSerializerMalformadoTests
{
    private static Catalogo CatalogoBase(int n)
    {
        var habs = Enumerable.Range(0, n).Select(i => new Habilidade(
            new HabilidadeId($"h{i}"), $"Habilidade {i}", "Descricao valida.", 10 + i,
            new[] { new RecursoEstudo("Curso", new Uri($"https://exemplo.com/{i}")) },
            i == 0 ? Array.Empty<HabilidadeId>() : new[] { new HabilidadeId("h0") })).ToList();
        var areas = new List<AreaAtuacao>
        {
            new(new AreaId("a0"), "Area", "Descricao da area.",
                habs.Select(h => new HabilidadeNaArea(h.Id, PrioridadeHabilidade.Essencial)).ToList())
        };
        return new Catalogo(areas, habs);
    }

    private static string JsonCompacto(int n) =>
        JsonNode.Parse(CatalogoSerializer.Serializar(CatalogoBase(n)))!.ToJsonString();

    private static bool IdentificaLocal(ErroCarga e) =>
        !string.IsNullOrWhiteSpace(e.Campo) || e.Linha is not null || e.Posicao is not null;

    // Feature: ia-mentor-carreira, Property 29: JSON malformado ou incompleto
    // **Validates: Requirements 6.5, 6.6**
    [Fact]
    public void Json_integro_e_aceito()
    {
        var r = CatalogoSerializer.Desserializar(JsonCompacto(3));
        Assert.True(r.Sucesso);
    }

    // Feature: ia-mentor-carreira, Property 29: JSON malformado ou incompleto
    // **Validates: Requirements 6.5, 6.6**
    [Property(MaxTest = 200)]
    public Property Json_truncado_falha_com_posicao()
    {
        var gen = Gen.Choose(1, 4).SelectMany(n =>
            Gen.Choose(1, JsonCompacto(n).Length - 1).Select(corte => (n, corte)));
        return Prop.ForAll(gen.ToArbitrary(), x =>
        {
            var json = JsonCompacto(x.n)[..x.corte];
            var r = CatalogoSerializer.Desserializar(json);
            return (!r.Sucesso && r.Catalogo is null && r.Erros.Count > 0
                    && r.Erros.All(e => e.Tipo == TipoFalhaCarga.JsonMalformado && IdentificaLocal(e)))
                .Label($"corte={x.corte}");
        });
    }

    // Feature: ia-mentor-carreira, Property 29: JSON malformado ou incompleto
    // **Validates: Requirements 6.5, 6.6**
    [Property(MaxTest = 200)]
    public Property Json_malformado_falha_com_posicao()
    {
        var gen = Gen.Choose(1, 4).SelectMany(n =>
            Gen.Choose(0, 2).SelectMany(tipo =>
                Gen.Choose(0, int.MaxValue).Select(seed => (n, tipo, seed))));
        return Prop.ForAll(gen.ToArbitrary(), x =>
        {
            var json = JsonCompacto(x.n);
            var rnd = new Random(x.seed);
            string ruim;
            switch (x.tipo)
            {
                case 0: // vírgula sobrando antes de um fechamento
                    var fechamentos = json.Select((c, i) => (c, i)).Where(t => t.c is '}' or ']').ToList();
                    var pos = fechamentos[rnd.Next(fechamentos.Count)].i;
                    ruim = json.Insert(pos, ",");
                    break;
                case 1: // dois-pontos removido
                    var dp = json.Select((c, i) => (c, i)).Where(t => t.c == ':' && t.i > 0 && json[t.i - 1] == '"').ToList();
                    var p = dp[rnd.Next(dp.Count)].i;
                    ruim = json.Remove(p, 1);
                    break;
                default: // lixo após o documento
                    ruim = json + "}";
                    break;
            }
            var r = CatalogoSerializer.Desserializar(ruim);
            return (!r.Sucesso && r.Catalogo is null && r.Erros.Count > 0
                    && r.Erros.All(e => e.Tipo == TipoFalhaCarga.JsonMalformado && IdentificaLocal(e)))
                .Label(ruim);
        });
    }

    private static readonly string[] CamposHabilidade =
        { "id", "nome", "descricao", "horasEstimadas", "recursos", "preRequisitos" };
    private static readonly string[] CamposArea = { "id", "nome", "descricao", "habilidades" };

    // Feature: ia-mentor-carreira, Property 29: JSON malformado ou incompleto
    // **Validates: Requirements 6.5, 6.6**
    [Property(MaxTest = 200)]
    public Property Campo_obrigatorio_ausente_falha_identificando_o_campo()
    {
        var gen = Gen.Choose(1, 4).SelectMany(n =>
            Gen.Choose(0, 3).SelectMany(alvo => // 0 raiz.areas, 1 raiz.habilidades, 2 area, 3 habilidade
                Gen.Choose(0, int.MaxValue).Select(seed => (n, alvo, seed))));
        return Prop.ForAll(gen.ToArbitrary(), x =>
        {
            var rnd = new Random(x.seed);
            var raiz = JsonNode.Parse(JsonCompacto(x.n))!.AsObject();
            string esperado;
            switch (x.alvo)
            {
                case 0:
                    raiz.Remove("areas");
                    esperado = "areas";
                    break;
                case 1:
                    raiz.Remove("habilidades");
                    esperado = "habilidades";
                    break;
                case 2:
                    var campoA = CamposArea[rnd.Next(CamposArea.Length)];
                    raiz["areas"]!.AsArray()[0]!.AsObject().Remove(campoA);
                    esperado = $"areas[0].{campoA}";
                    break;
                default:
                    var idx = rnd.Next(x.n);
                    var campoH = CamposHabilidade[rnd.Next(CamposHabilidade.Length)];
                    raiz["habilidades"]!.AsArray()[idx]!.AsObject().Remove(campoH);
                    esperado = $"habilidades[{idx}].{campoH}";
                    break;
            }
            var r = CatalogoSerializer.Desserializar(raiz.ToJsonString());
            return (!r.Sucesso && r.Catalogo is null
                    && r.Erros.Any(e => e.Tipo == TipoFalhaCarga.CampoAusenteOuInvalido && e.Campo == esperado))
                .Label($"esperado={esperado}");
        });
    }
}
