using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using CouchIA.Domain;

namespace CouchIA.Application.Mentor.Services;

public sealed record ResultadoDesserializacao(Catalogo? Catalogo, IReadOnlyList<ErroCarga> Erros)
{
    public bool Sucesso => Catalogo is not null && Erros.Count == 0;
}

/// <summary>
/// JSON UTF-8 do catálogo. A desserialização é manual para localizar o campo ou a posição da falha.
/// Não valida regras do catálogo (isso é do <see cref="ValidadorCatalogo"/>).
/// </summary>
public static class CatalogoSerializer
{
    private static readonly JsonWriterOptions OpcoesEscrita = new()
    {
        Indented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly JsonDocumentOptions OpcoesLeitura = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = 32
    };

    public static string Serializar(Catalogo catalogo) =>
        new UTF8Encoding(false).GetString(SerializarUtf8(catalogo));

    public static byte[] SerializarUtf8(Catalogo catalogo)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms, OpcoesEscrita))
        {
            w.WriteStartObject();

            w.WriteStartArray("areas");
            foreach (var a in catalogo.Areas)
            {
                w.WriteStartObject();
                w.WriteString("id", a.Id.Valor);
                w.WriteString("nome", a.Nome);
                w.WriteString("descricao", a.Descricao);
                w.WriteStartArray("habilidades");
                foreach (var h in a.Habilidades)
                {
                    w.WriteStartObject();
                    w.WriteString("id", h.Habilidade.Valor);
                    w.WriteString("prioridade", NomePrioridade(h.Prioridade));
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                w.WriteEndObject();
            }
            w.WriteEndArray();

            w.WriteStartArray("habilidades");
            foreach (var h in catalogo.Habilidades)
            {
                w.WriteStartObject();
                w.WriteString("id", h.Id.Valor);
                w.WriteString("nome", h.Nome);
                w.WriteString("descricao", h.Descricao);
                w.WriteNumber("horasEstimadas", h.HorasEstimadas);
                w.WriteStartArray("recursos");
                foreach (var r in h.Recursos)
                {
                    w.WriteStartObject();
                    w.WriteString("titulo", r.Titulo);
                    w.WriteString("url", r.Url.OriginalString);
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                w.WriteStartArray("preRequisitos");
                foreach (var p in h.PreRequisitos) w.WriteStringValue(p.Valor);
                w.WriteEndArray();
                w.WriteEndObject();
            }
            w.WriteEndArray();

            w.WriteEndObject();
        }
        return ms.ToArray();
    }

    public static ResultadoDesserializacao Desserializar(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Falha(new ErroCarga(TipoFalhaCarga.JsonMalformado, null, 1, 0, "O JSON do catálogo está vazio."));

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json, OpcoesLeitura);
        }
        catch (JsonException ex)
        {
            return Falha(new ErroCarga(TipoFalhaCarga.JsonMalformado, ex.Path,
                ex.LineNumber is null ? null : ex.LineNumber + 1, ex.BytePositionInLine,
                $"JSON malformado: {ex.Message}"));
        }

        using (doc)
        {
            var erros = new List<ErroCarga>();
            var raiz = doc.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object)
                return Falha(Campo("$", "O catálogo deve ser um objeto JSON."));

            var areas = new List<AreaAtuacao>();
            var habilidades = new List<Habilidade>();

            if (Lista(raiz, "areas", "areas", erros) is { } listaAreas)
            {
                var i = 0;
                foreach (var el in listaAreas.EnumerateArray())
                {
                    var a = LerArea(el, $"areas[{i++}]", erros);
                    if (a is not null) areas.Add(a);
                }
            }

            if (Lista(raiz, "habilidades", "habilidades", erros) is { } listaHab)
            {
                var i = 0;
                foreach (var el in listaHab.EnumerateArray())
                {
                    var h = LerHabilidade(el, $"habilidades[{i++}]", erros);
                    if (h is not null) habilidades.Add(h);
                }
            }

            return erros.Count > 0
                ? new ResultadoDesserializacao(null, erros)
                : new ResultadoDesserializacao(new Catalogo(areas, habilidades), Array.Empty<ErroCarga>());
        }
    }

    private static AreaAtuacao? LerArea(JsonElement el, string caminho, List<ErroCarga> erros)
    {
        if (el.ValueKind != JsonValueKind.Object)
        {
            erros.Add(Campo(caminho, "Esperado um objeto."));
            return null;
        }
        var antes = erros.Count;
        var id = Texto(el, "id", caminho, erros);
        var nome = Texto(el, "nome", caminho, erros);
        var descricao = Texto(el, "descricao", caminho, erros);
        var itens = new List<HabilidadeNaArea>();
        if (Lista(el, "habilidades", caminho + ".habilidades", erros) is { } lista)
        {
            var i = 0;
            foreach (var item in lista.EnumerateArray())
            {
                var c = $"{caminho}.habilidades[{i++}]";
                if (item.ValueKind != JsonValueKind.Object)
                {
                    erros.Add(Campo(c, "Esperado um objeto."));
                    continue;
                }
                var hid = Texto(item, "id", c, erros);
                var prio = Texto(item, "prioridade", c, erros);
                if (hid is null || prio is null) continue;
                if (!TentarPrioridade(prio, out var p))
                {
                    erros.Add(Campo(c + ".prioridade", $"Prioridade desconhecida '{prio}'."));
                    continue;
                }
                itens.Add(new HabilidadeNaArea(new HabilidadeId(hid), p));
            }
        }
        if (erros.Count > antes || id is null || nome is null || descricao is null) return null;
        return new AreaAtuacao(new AreaId(id), nome, descricao, itens);
    }

    private static Habilidade? LerHabilidade(JsonElement el, string caminho, List<ErroCarga> erros)
    {
        if (el.ValueKind != JsonValueKind.Object)
        {
            erros.Add(Campo(caminho, "Esperado um objeto."));
            return null;
        }
        var antes = erros.Count;
        var id = Texto(el, "id", caminho, erros);
        var nome = Texto(el, "nome", caminho, erros);
        var descricao = Texto(el, "descricao", caminho, erros);

        var horas = 0;
        if (!el.TryGetProperty("horasEstimadas", out var h))
            erros.Add(Campo($"{caminho}.horasEstimadas", "Campo obrigatório ausente."));
        else if (h.ValueKind != JsonValueKind.Number || !h.TryGetInt32(out horas))
            erros.Add(Campo($"{caminho}.horasEstimadas", "Esperado um número inteiro."));

        var recursos = new List<RecursoEstudo>();
        if (Lista(el, "recursos", caminho + ".recursos", erros) is { } lr)
        {
            var i = 0;
            foreach (var r in lr.EnumerateArray())
            {
                var c = $"{caminho}.recursos[{i++}]";
                if (r.ValueKind != JsonValueKind.Object)
                {
                    erros.Add(Campo(c, "Esperado um objeto."));
                    continue;
                }
                var titulo = Texto(r, "titulo", c, erros);
                var url = Texto(r, "url", c, erros);
                if (titulo is null || url is null) continue;
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    erros.Add(Campo(c + ".url", "URL inválida."));
                    continue;
                }
                recursos.Add(new RecursoEstudo(titulo, uri));
            }
        }

        var pre = new List<HabilidadeId>();
        if (Lista(el, "preRequisitos", caminho + ".preRequisitos", erros) is { } lp)
        {
            var i = 0;
            foreach (var p in lp.EnumerateArray())
            {
                var c = $"{caminho}.preRequisitos[{i++}]";
                if (p.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(p.GetString()))
                {
                    erros.Add(Campo(c, "Esperado um identificador de habilidade não vazio."));
                    continue;
                }
                pre.Add(new HabilidadeId(p.GetString()!));
            }
        }

        if (erros.Count > antes || id is null || nome is null || descricao is null) return null;
        return new Habilidade(new HabilidadeId(id), nome, descricao, horas, recursos, pre);
    }

    private static string? Texto(JsonElement el, string nome, string caminho, List<ErroCarga> erros)
    {
        var c = $"{caminho}.{nome}";
        if (!el.TryGetProperty(nome, out var v))
        {
            erros.Add(Campo(c, "Campo obrigatório ausente."));
            return null;
        }
        if (v.ValueKind != JsonValueKind.String)
        {
            erros.Add(Campo(c, "Esperado um texto."));
            return null;
        }
        var s = v.GetString();
        // Identificadores não podem ser vazios (os tipos de domínio recusam); demais textos vazios
        // chegam ao ValidadorCatalogo, que reporta o limite violado.
        if (nome == "id" && string.IsNullOrWhiteSpace(s))
        {
            erros.Add(Campo(c, "O identificador não pode ser vazio."));
            return null;
        }
        return s;
    }

    private static JsonElement? Lista(JsonElement el, string nome, string caminho, List<ErroCarga> erros)
    {
        if (!el.TryGetProperty(nome, out var v))
        {
            erros.Add(Campo(caminho, "Campo obrigatório ausente."));
            return null;
        }
        if (v.ValueKind != JsonValueKind.Array)
        {
            erros.Add(Campo(caminho, "Esperada uma lista."));
            return null;
        }
        return v;
    }

    private static ErroCarga Campo(string campo, string detalhe) =>
        new(TipoFalhaCarga.CampoAusenteOuInvalido, campo, null, null, detalhe);

    private static ResultadoDesserializacao Falha(ErroCarga erro) =>
        new(null, new[] { erro });

    private static string NomePrioridade(PrioridadeHabilidade p) => p switch
    {
        PrioridadeHabilidade.Essencial => "essencial",
        PrioridadeHabilidade.Importante => "importante",
        _ => "desejavel"
    };

    private static bool TentarPrioridade(string texto, out PrioridadeHabilidade p)
    {
        switch (texto.Trim().ToLowerInvariant())
        {
            case "essencial": p = PrioridadeHabilidade.Essencial; return true;
            case "importante": p = PrioridadeHabilidade.Importante; return true;
            case "desejavel": case "desejável": p = PrioridadeHabilidade.Desejavel; return true;
            default: p = default; return false;
        }
    }
}
