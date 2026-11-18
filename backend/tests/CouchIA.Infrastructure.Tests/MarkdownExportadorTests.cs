using System.Globalization;
using System.Text;
using CouchIA.Application.Mentor.Services;
using CouchIA.Domain;
using CouchIA.Infrastructure.Exportacao;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace CouchIA.Infrastructure.Tests;

public static class PlanoGeradores
{
    private const string Ids = "abcdefghijklmnopqrstuvwxyz0123456789-";
    private const string Titulos = "abcXYZ019 áçõ[]\\()-_.,:#*";

    private static Gen<T[]> Lista<T>(Gen<T> g, int min, int max) =>
        Gen.Choose(min, max).SelectMany(n => Gen.ArrayOf(g, n));

    private static readonly Gen<RecursoEstudo> Recurso =
        from titulo in Lista(Gen.Elements(Titulos.ToCharArray()), 1, 30).Select(a => new string(a))
        from host in Lista(Gen.Elements("abcdefgh".ToCharArray()), 1, 8).Select(a => new string(a))
        from caminho in Lista(Gen.Elements("abc123/".ToCharArray()), 0, 10).Select(a => new string(a))
        select new RecursoEstudo(titulo, new Uri($"https://{host}.com/{caminho}"));

    private static readonly Gen<HabilidadeId> Habilidade =
        Lista(Gen.Elements(Ids.ToCharArray()), 1, 15).Select(a => new HabilidadeId(new string(a)));

    private static readonly Gen<(int Duracao, HabilidadeId[] Habs, RecursoEstudo[] Recs)> EtapaBase =
        from d in Gen.Choose(1, 12)
        from h in Lista(Habilidade, 1, 5)
        from r in Lista(Recurso, 1, 3)
        select (d, h, r);

    public static readonly Gen<PlanoDesenvolvimento> Plano =
        from area in Lista(Gen.Elements(Ids.ToCharArray()), 1, 15).Select(a => new AreaId(new string(a)))
        from horas in Gen.Choose(1, 60)
        from etapas in Lista(EtapaBase, 1, 6)
        select new PlanoDesenvolvimento(
            Guid.NewGuid(),
            area,
            horas,
            etapas.Select((e, i) => new EtapaPlano(i + 1, e.Habs, e.Duracao, e.Recs)).ToList(),
            etapas.Sum(e => e.Duracao));

    public static Arbitrary<PlanoDesenvolvimento> PlanoArb() => Plano.ToArbitrary();
}

public class MarkdownExportadorTests
{
    /// <summary>Leitor de teste: reconstrói o plano a partir do Markdown exportado.</summary>
    private static (string Area, int Horas, int Total, List<EtapaPlano> Etapas) Ler(string md)
    {
        var inv = CultureInfo.InvariantCulture;
        var linhas = md.Split('\n');
        string area = "";
        int horas = 0, total = 0;
        var etapas = new List<EtapaPlano>();
        int numero = 0, duracao = 0;
        var habs = new List<HabilidadeId>();
        var recs = new List<RecursoEstudo>();
        string secao = "";
        bool aberta = false;

        void Fechar()
        {
            if (aberta) etapas.Add(new EtapaPlano(numero, habs.ToList(), duracao, recs.ToList()));
            habs.Clear();
            recs.Clear();
            aberta = false;
        }

        foreach (var l in linhas)
        {
            if (l.StartsWith("# Plano de Desenvolvimento: ")) area = l["# Plano de Desenvolvimento: ".Length..];
            else if (l.StartsWith("- Disponibilidade semanal: ")) horas = int.Parse(l["- Disponibilidade semanal: ".Length..].Split(' ')[0], inv);
            else if (l.StartsWith("- Duração total: ")) total = int.Parse(l["- Duração total: ".Length..].Split(' ')[0], inv);
            else if (l.StartsWith("## Etapa "))
            {
                Fechar();
                numero = int.Parse(l["## Etapa ".Length..], inv);
                aberta = true;
                secao = "";
            }
            else if (l.StartsWith("- Duração: ")) duracao = int.Parse(l["- Duração: ".Length..].Split(' ')[0], inv);
            else if (l == "### Habilidades") secao = "h";
            else if (l == "### Recursos") secao = "r";
            else if (l.StartsWith("- ") && secao == "h") habs.Add(new HabilidadeId(l[2..]));
            else if (l.StartsWith("- [") && secao == "r") recs.Add(LerRecurso(l[3..]));
        }
        Fechar();
        return (area, horas, total, etapas);
    }

    private static RecursoEstudo LerRecurso(string resto)
    {
        var titulo = new StringBuilder();
        int i = 0;
        for (; i < resto.Length; i++)
        {
            if (resto[i] == '\\') { titulo.Append(resto[++i]); continue; }
            if (resto[i] == ']') break;
            titulo.Append(resto[i]);
        }
        // resto[i] == ']' e resto[i+1] == '('
        var url = resto[(i + 2)..^1];
        return new RecursoEstudo(titulo.ToString(), new Uri(url));
    }

    /// <summary>Feature: ia-mentor-carreira, Property 13: Exportação em Markdown é fiel ao plano.</summary>
    /// <remarks>**Validates: Requirements 5.5**</remarks>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(PlanoGeradores) })]
    public async Task<bool> Exportacao_em_markdown_e_fiel_ao_plano(PlanoDesenvolvimento plano)
    {
        var arquivo = await new MarkdownExportador()
            .ExportarAsync(plano, FormatoExportacao.Markdown, CancellationToken.None);
        var md = new UTF8Encoding(false).GetString(arquivo.Conteudo);

        // Conteúdo presente.
        var presente = md.Contains(plano.Area.Valor)
            && plano.Etapas.All(e => md.Contains($"## Etapa {e.Numero}")
                && e.Habilidades.All(h => md.Contains(h.Valor))
                && md.Contains($"- Duração: {e.DuracaoSemanas} semanas")
                && e.Recursos.All(r => md.Contains(r.Url.OriginalString)));

        // Reler preserva o conteúdo.
        var lido = Ler(md);
        var equivalente = lido.Area == plano.Area.Valor
            && lido.Horas == plano.HorasPorSemana
            && lido.Total == plano.DuracaoTotalSemanas
            && lido.Etapas.Count == plano.Etapas.Count
            && lido.Etapas.Zip(plano.Etapas).All(p =>
                p.First.Numero == p.Second.Numero
                && p.First.DuracaoSemanas == p.Second.DuracaoSemanas
                && p.First.Habilidades.SequenceEqual(p.Second.Habilidades)
                && p.First.Recursos.Select(r => (r.Titulo, r.Url.OriginalString))
                    .SequenceEqual(p.Second.Recursos.Select(r => (r.Titulo, r.Url.OriginalString))));

        return presente && equivalente && arquivo.ContentType.StartsWith("text/markdown");
    }
}
