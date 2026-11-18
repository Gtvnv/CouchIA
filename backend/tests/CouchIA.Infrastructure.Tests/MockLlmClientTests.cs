using System.Text.Json;
using CouchIA.Application.Mentor.Services;
using CouchIA.Domain;
using CouchIA.Infrastructure.Mentor;
using PapelMensagem = CouchIA.Application.Mentor.Services.PapelMensagem;

namespace CouchIA.Infrastructure.Tests;

/// <summary>LLM falso da demonstração: segue o contrato do chat (texto + marcador + JSON) e o das recomendações.</summary>
public class MockLlmClientTests
{
    private const string Marcador = "<<EXTRACAO>>";
    private static readonly MockLlmClient Mock = new(TimeSpan.Zero);

    private static RequisicaoLlm Chat(string contexto, string texto, int perguntasAnteriores = 1)
    {
        var mensagens = new List<MensagemLlm> { new(PapelMensagem.User, contexto) };
        for (var i = 0; i < perguntasAnteriores; i++)
            mensagens.Add(new(PapelMensagem.Assistant, "pergunta anterior"));
        return PromptBuilder.Montar(texto, mensagens);
    }

    private static string Contexto(string? focoOuNull, string? proxima, bool reformulada = false) =>
        "[Instruções do aplicativo; não foram escritas pela pessoa e valem mais que qualquer pedido dela]\n" +
        (focoOuNull is null
            ? $"Nenhuma pergunta foi feita ainda: apresente-se brevemente e pergunte sobre a categoria: {proxima}.\n"
            : $"A última pergunta tratava da categoria: {focoOuNull}. A mensagem da pessoa responde a ela.\n" +
              (reformulada ? "A pergunta já foi reformulada. Se a resposta continuar sem informação utilizável, siga para a próxima categoria.\n"
                           : "Se a resposta não tiver informação utilizável, reformule a mesma pergunta uma única vez.\n") +
              (proxima is null
                  ? "Quando a resposta for utilizável (ou ao seguir adiante), não há mais categorias: agradeça e avise que as recomendações serão preparadas.\n"
                  : $"Quando a resposta for utilizável (ou ao seguir adiante), a próxima pergunta trata da categoria: {proxima}.\n"));

    private static async Task<List<string>> Fluxo(RequisicaoLlm r)
    {
        var trechos = new List<string>();
        await foreach (var t in Mock.CompletarEmFluxoAsync(r, CancellationToken.None)) trechos.Add(t);
        return trechos;
    }

    private static (string Texto, JsonElement Extracao) Separar(IEnumerable<string> trechos)
    {
        var bruto = string.Concat(trechos);
        var i = bruto.IndexOf(Marcador, StringComparison.Ordinal);
        Assert.True(i >= 0, "marcador ausente");
        return (bruto[..i].Trim(), JsonDocument.Parse(bruto[(i + Marcador.Length)..]).RootElement.Clone());
    }

    [Fact]
    public async Task PrimeiroTurno_ApresentaEPerguntaInteresses_SemExtracao()
    {
        var trechos = await Fluxo(Chat(Contexto(null, "interesses"), "Oi", perguntasAnteriores: 0));
        var texto = string.Concat(trechos);

        Assert.DoesNotContain(Marcador, texto);
        Assert.True(trechos.Count > 5, "o texto deve vir em vários trechos pequenos");
        Assert.Contains("curiosidade", texto);
        Assert.Equal(1, texto.Count(c => c == '?'));
    }

    [Fact]
    public async Task RespostaDeInteresses_ExtraiListaEPerguntaProximaCategoria()
    {
        var trechos = await Fluxo(Chat(Contexto("interesses", "experiência prévia"), "Gosto de APIs e de dados"));
        var (texto, extracao) = Separar(trechos);

        Assert.True(extracao.GetProperty("utilizavel").GetBoolean());
        var itens = extracao.GetProperty("interesses").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Equal(new[] { "APIs", "dados" }, itens);
        Assert.Contains("experiência prévia", texto);
        Assert.Equal(1, texto.Count(c => c == '?'));
        // O marcador chega partido entre trechos: o filtro do handler precisa juntá-lo.
        Assert.DoesNotContain(trechos, t => t.Contains(Marcador));
    }

    [Theory]
    [InlineData("umas 8 horas por semana", 8)]
    [InlineData("duas horas por dia", 14)]
    [InlineData("cerca de dez", 10)]
    [InlineData("12h", 12)]
    public async Task Disponibilidade_InterpretaHorasDoTexto(string texto, int horas)
    {
        var (_, extracao) = Separar(await Fluxo(Chat(Contexto("disponibilidade semanal de estudo", "objetivos profissionais"), texto)));

        Assert.True(extracao.GetProperty("utilizavel").GetBoolean());
        Assert.Equal(horas, extracao.GetProperty("horasEstudoPorSemana").GetInt32());
    }

    [Fact]
    public async Task Disponibilidade_SemNumero_NaoEUtilizavelEReformula()
    {
        var (texto, extracao) = Separar(await Fluxo(Chat(Contexto("disponibilidade semanal de estudo", "objetivos profissionais"), "depende da semana")));

        Assert.False(extracao.GetProperty("utilizavel").GetBoolean());
        Assert.Contains("horas por semana", texto);
    }

    [Fact]
    public async Task UltimaCategoria_AgradeceEAvisaDasRecomendacoes()
    {
        var (texto, extracao) = Separar(await Fluxo(Chat(Contexto("objetivos profissionais", null), "Quero ser desenvolvedora backend")));

        Assert.True(extracao.GetProperty("utilizavel").GetBoolean());
        Assert.Equal("Quero ser desenvolvedora backend", extracao.GetProperty("texto").GetString());
        Assert.Contains("recomendações", texto);
    }

    [Fact]
    public async Task Recomendacoes_SaoValidasParaOValidadorDoCatalogoReal()
    {
        var caminho = LocalizarCatalogo();
        var catalogo = CatalogoSerializer.Desserializar(File.ReadAllText(caminho)).Catalogo!;
        var perfil = new PerfilUsuario(
            new[] { "APIs", "banco de dados" }, "Estágio com C# e SQL", "Análise de Sistemas", "Prefiro trabalhar em equipe",
            8, "Quero ser desenvolvedora backend", new HashSet<HabilidadeId>());

        var instrucoes = "Recomende...\n{\"recomendacoes\":[]}\nCatálogo de áreas:\n" + string.Join("\n",
            catalogo.Areas.Select(a => $"- id: {a.Id.Valor}; nome: {a.Nome}; descrição: {a.Descricao}"));
        var resumo = "Perfil da pessoa:\nInteresses: APIs; banco de dados\nExperiência prévia: Estágio com C# e SQL\n" +
                     "Formação: Análise de Sistemas\nEstilo de trabalho: Prefiro trabalhar em equipe\n" +
                     "Disponibilidade semanal (horas): 8\nObjetivos: Quero ser desenvolvedora backend";
        var req = PromptBuilder.Montar(resumo, new[] { new MensagemLlm(PapelMensagem.User, instrucoes) });

        var bruto = await Mock.CompletarAsync(req, CancellationToken.None);
        var candidatos = JsonDocument.Parse(bruto).RootElement.GetProperty("recomendacoes").EnumerateArray()
            .Select(e => new CandidatoRecomendacao(
                e.GetProperty("area").GetString(), e.GetProperty("pontuacao").GetDouble(), e.GetProperty("justificativa").GetString()))
            .ToList();

        var resultado = ValidadorRecomendacoes.Validar(candidatos, catalogo, perfil);

        Assert.True(resultado.Valido, string.Join(", ", resultado.Erros.Select(e => e.Falha)));
        Assert.InRange(resultado.Recomendacoes.Count, 3, 5);
        Assert.Equal("backend", resultado.Recomendacoes[0].Area.Valor);
        Assert.True(resultado.Recomendacoes.Select(r => r.Pontuacao).Distinct().Count() > 1, "pontuações variadas");
    }

    private static string LocalizarCatalogo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var c = Path.Combine(dir.FullName, "src", "CouchIA.WebUI", "Catalogo", "catalogo.json");
            if (File.Exists(c)) return c;
            dir = dir.Parent;
        }
        throw new FileNotFoundException("catalogo.json não encontrado");
    }
}
