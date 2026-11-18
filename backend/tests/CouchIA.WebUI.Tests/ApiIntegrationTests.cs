using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace CouchIA.WebUI.Tests;

/// <summary>
/// Integração da API com PostgreSQL real e LLM falso.
/// Validates: Requirements 7.7, 8.1, 8.2, 8.3
/// </summary>
[Collection(ApiCollection.Nome)]
public sealed class ApiIntegrationTests
{
    private const string NomeCookie = "couchia_sessao";
    private readonly ApiFixture _api;

    public ApiIntegrationTests(ApiFixture api) => _api = api;

    private static async Task<List<JsonElement>> EnviarMensagemAsync(HttpClient http, string texto)
    {
        var resposta = await http.PostAsJsonAsync("/api/sessoes/atual/mensagens",
            new { clientMessageId = Guid.NewGuid(), texto });
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.StartsWith("text/event-stream", resposta.Content.Headers.ContentType!.MediaType);

        var corpo = await resposta.Content.ReadAsStringAsync();
        Assert.DoesNotContain("<<EXTRACAO>>", corpo);
        return corpo.Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(l => JsonDocument.Parse(l["data: ".Length..]).RootElement.Clone())
            .ToList();
    }

    [Fact]
    public async Task FluxoCompleto_CriaConversaEncerraRecomendaAvaliaMapeiaPlanejaExportaExclui()
    {
        var http = _api.Factory.CriarClienteHttps();

        // Criar sessão.
        var criada = await http.PostAsync("/api/sessoes", null);
        Assert.Equal(HttpStatusCode.OK, criada.StatusCode);
        var sessao = await criada.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, sessao.GetProperty("historico").GetArrayLength());
        Assert.False(sessao.TryGetProperty("id", out _), "O identificador da Sessao não pode vazar no corpo.");

        // Conversar: saudação + respostas às cinco primeiras categorias (até a disponibilidade semanal).
        for (var i = 0; i < 6; i++)
        {
            var eventos = await EnviarMensagemAsync(http, i == 0 ? "Olá" : "Gosto de backend");
            Assert.Contains(eventos, e => e.GetProperty("tipo").GetString() == "trecho");
            var final = eventos[^1];
            Assert.Equal("final", final.GetProperty("tipo").GetString());
            Assert.Equal(LlmFalso.TextoResposta, final.GetProperty("mensagem").GetProperty("texto").GetString());
        }

        var atual = await http.GetFromJsonAsync<JsonElement>("/api/sessoes/atual");
        Assert.Equal(12, atual.GetProperty("historico").GetArrayLength());
        Assert.Equal(20, atual.GetProperty("perfil").GetProperty("horasEstudoPorSemana").GetInt32());
        Assert.Equal("emAndamento", atual.GetProperty("entrevista").GetProperty("estado").GetString());

        // Encerrar a entrevista.
        var encerrar = await http.PostAsync("/api/sessoes/atual/entrevista/encerrar", null);
        Assert.Equal(HttpStatusCode.OK, encerrar.StatusCode);
        var semResposta = (await encerrar.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("categoriasSemResposta");
        Assert.Equal(1, semResposta.GetArrayLength());
        Assert.Equal("objetivos", semResposta[0].GetString());

        // Recomendações.
        var recomendacoes = await http.GetFromJsonAsync<JsonElement>("/api/sessoes/atual/recomendacoes");
        Assert.Equal(new[] { "backend", "frontend", "dados" },
            recomendacoes.EnumerateArray().Select(r => r.GetProperty("areaId").GetString()).ToArray());

        // Feedback "não útil" rebaixa a área na ordem de exibição, sem alterar a pontuação.
        var feedback = await http.PutAsJsonAsync("/api/sessoes/atual/recomendacoes/backend/feedback",
            new { tipo = "naoUtil", motivo = "Prefiro outra área" });
        Assert.Equal(HttpStatusCode.OK, feedback.StatusCode);
        var fb = await feedback.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("naoUtil", fb.GetProperty("tipo").GetString());

        var reordenadas = await http.GetFromJsonAsync<JsonElement>("/api/sessoes/atual/recomendacoes");
        Assert.Equal("backend", reordenadas.EnumerateArray().Last().GetProperty("areaId").GetString());
        Assert.Equal(90, reordenadas.EnumerateArray().Last().GetProperty("pontuacao").GetInt32());

        // Mapa de habilidades.
        var mapa = await http.GetFromJsonAsync<JsonElement>("/api/areas/backend/habilidades");
        Assert.Equal("backend", mapa.GetProperty("areaId").GetString());
        Assert.Equal(9, mapa.GetProperty("habilidades").GetArrayLength());

        // Plano.
        var planoResposta = await http.PostAsJsonAsync("/api/sessoes/atual/planos", new { areaId = "backend" });
        Assert.Equal(HttpStatusCode.OK, planoResposta.StatusCode);
        var plano = await planoResposta.Content.ReadFromJsonAsync<JsonElement>();
        var planoId = plano.GetProperty("id").GetGuid();
        Assert.Equal(20, plano.GetProperty("horasPorSemana").GetInt32());
        Assert.True(plano.GetProperty("etapas").GetArrayLength() > 0);

        // Exportar.
        var md = await http.GetAsync($"/api/sessoes/atual/planos/{planoId}/exportar?formato=md");
        Assert.Equal(HttpStatusCode.OK, md.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(await md.Content.ReadAsStringAsync()));
        var pdf = await http.GetAsync($"/api/sessoes/atual/planos/{planoId}/exportar?formato=pdf");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType!.MediaType);
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString((await pdf.Content.ReadAsByteArrayAsync()).Take(4).ToArray()));

        // Excluir: exige confirmação explícita e remove tudo.
        var antesDeExcluir = await ContarSessoesAsync();
        var semConfirmar = await Excluir(http, confirmar: false);
        Assert.Equal(HttpStatusCode.BadRequest, semConfirmar.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/api/sessoes/atual")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await Excluir(http, confirmar: true)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync("/api/sessoes/atual")).StatusCode);
        Assert.Equal(antesDeExcluir - 1, await ContarSessoesAsync());
    }

    [Fact]
    public async Task Excluir_RemoveOsDadosDoBanco_ECookieAntigoPassaAFalhar()
    {
        var http = _api.Factory.CriarClienteHttps();
        await http.PostAsync("/api/sessoes", null);
        await EnviarMensagemAsync(http, "Olá");
        var cookie = ObterCookieSessao(http);

        var antes = await ContarSessoesAsync();
        var resposta = await Excluir(http, confirmar: true);
        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);

        // Cookie removido no cliente e sessão apagada no banco.
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync("/api/sessoes/atual")).StatusCode);
        Assert.Equal(antes - 1, await ContarSessoesAsync());

        // Reapresentar o cookie antigo também falha: os dados não existem mais.
        var outro = _api.Factory.CriarClienteHttps();
        var requisicao = new HttpRequestMessage(HttpMethod.Get, "/api/sessoes/atual");
        requisicao.Headers.Add("Cookie", $"{NomeCookie}={cookie}");
        Assert.Equal(HttpStatusCode.NotFound, (await outro.SendAsync(requisicao)).StatusCode);
    }

    [Fact]
    public async Task Sessoes_SaoIsoladasEntreSi()
    {
        var a = _api.Factory.CriarClienteHttps();
        var b = _api.Factory.CriarClienteHttps();
        await a.PostAsync("/api/sessoes", null);
        await b.PostAsync("/api/sessoes", null);
        Assert.NotEqual(ObterCookieSessao(a), ObterCookieSessao(b));

        await EnviarMensagemAsync(a, "Mensagem exclusiva da sessão A");

        var sessaoB = await b.GetFromJsonAsync<JsonElement>("/api/sessoes/atual");
        Assert.Equal(0, sessaoB.GetProperty("historico").GetArrayLength());
        var sessaoA = await a.GetFromJsonAsync<JsonElement>("/api/sessoes/atual");
        Assert.Equal(2, sessaoA.GetProperty("historico").GetArrayLength());

        // Excluir B não afeta A.
        Assert.Equal(HttpStatusCode.NoContent, (await Excluir(b, true)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await a.GetAsync("/api/sessoes/atual")).StatusCode);

        // Sem cookie ou com identificador válido porém desconhecido: sessão inexistente, sem vazar dados.
        var anonimo = _api.Factory.CriarClienteHttps();
        Assert.Equal(HttpStatusCode.NotFound, (await anonimo.GetAsync("/api/sessoes/atual")).StatusCode);

        var forjado = Base64Url(RandomNumberGenerator.GetBytes(32));
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/sessoes/atual/recomendacoes");
        req.Headers.Add("Cookie", $"{NomeCookie}={forjado}");
        Assert.Equal(HttpStatusCode.NotFound, (await anonimo.SendAsync(req)).StatusCode);

        // Cookie malformado equivale a Sessao vazia.
        var malformado = new HttpRequestMessage(HttpMethod.Get, "/api/sessoes/atual");
        malformado.Headers.Add("Cookie", $"{NomeCookie}=nao-e-um-id");
        Assert.Equal(HttpStatusCode.NotFound, (await anonimo.SendAsync(malformado)).StatusCode);
    }

    [Fact]
    public async Task Cookie_TemAtributosDeSeguranca()
    {
        var http = _api.Factory.CriarClienteHttps();
        var resposta = await http.PostAsync("/api/sessoes", null);

        var setCookie = Assert.Single(resposta.Headers.GetValues("Set-Cookie"), c => c.StartsWith(NomeCookie + "="));
        var atributos = setCookie.ToLowerInvariant();
        Assert.Contains("httponly", atributos);
        Assert.Contains("secure", atributos);
        Assert.Contains("samesite=strict", atributos);
        Assert.Contains("path=/", atributos);
        Assert.Contains("max-age=", atributos);
    }

    [Fact]
    public async Task Respostas_TemCabecalhosDeSeguranca()
    {
        var http = _api.Factory.CriarClienteHttps();

        // Tanto em sucesso quanto em erro.
        foreach (var resposta in new[]
                 {
                     await http.PostAsync("/api/sessoes", null),
                     await http.GetAsync("/api/sessoes/atual/recomendacoes"),
                     await _api.Factory.CriarClienteHttps().GetAsync("/api/sessoes/atual")
                 })
        {
            var h = resposta.Headers;
            Assert.Equal("nosniff", h.GetValues("X-Content-Type-Options").Single());
            Assert.Equal("DENY", h.GetValues("X-Frame-Options").Single());
            Assert.Equal("no-referrer", h.GetValues("Referrer-Policy").Single());
            Assert.Contains("default-src 'none'", h.GetValues("Content-Security-Policy").Single());
            Assert.Equal("no-store", string.Join(",", h.GetValues("Cache-Control")));
            Assert.Contains("camera=()", h.GetValues("Permissions-Policy").Single());
            Assert.False(h.Contains("Server"), "O cabeçalho Server não deve ser enviado.");
        }
    }

    [Fact]
    public async Task Http_SemTls_EhRedirecionadoParaHttps()
    {
        var http = _api.Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = false
        });

        var resposta = await http.PostAsync("/api/sessoes", null);

        Assert.Equal(HttpStatusCode.TemporaryRedirect, resposta.StatusCode);
        Assert.Equal("https", resposta.Headers.Location!.Scheme);
        // Nenhuma sessão foi criada nem cookie emitido pela requisição em texto claro.
        Assert.False(resposta.Headers.Contains("Set-Cookie"));
    }

    private static async Task<HttpResponseMessage> Excluir(HttpClient http, bool confirmar)
    {
        var req = new HttpRequestMessage(HttpMethod.Delete, "/api/sessoes/atual")
        {
            Content = JsonContent.Create(new { confirmar })
        };
        return await http.SendAsync(req);
    }

    private async Task<int> ContarSessoesAsync()
    {
        await using var db = _api.NovoContexto();
        return await db.Sessoes.CountAsync();
    }

    private static string ObterCookieSessao(HttpClient http) =>
        ApiFixture.ApiFactory.CookieDe(http) ?? throw new InvalidOperationException("Sem cookie de sessão.");

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
