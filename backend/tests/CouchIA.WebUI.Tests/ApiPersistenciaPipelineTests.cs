using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using CouchIA.Application.Mentor.Services;

namespace CouchIA.WebUI.Tests;

/// <summary>
/// Persistência cifrada (leitura SQL crua, sem conversores do EF) e ordem do pipeline MediatR (validação antes do handler).
/// Validates: Requirements 7.6
/// </summary>
[Collection(ApiCollection.Nome)]
public sealed class ApiPersistenciaPipelineTests
{
    private readonly ApiFixture _api;

    public ApiPersistenciaPipelineTests(ApiFixture api) => _api = api;

    private LlmFalso Llm => (LlmFalso)_api.Factory.Services.GetRequiredService<ILlmClient>();

    private static async Task EnviarAsync(HttpClient http, string texto)
    {
        var r = await http.PostAsJsonAsync("/api/sessoes/atual/mensagens", new { clientMessageId = Guid.NewGuid(), texto });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        await r.Content.ReadAsStringAsync();
    }

    private async Task<List<string>> LerColunaCrua(string tabela, string coluna)
    {
        await using var conexao = new NpgsqlConnection(_api.ConnectionString);
        await conexao.OpenAsync();
        await using var cmd = new NpgsqlCommand($"SELECT {coluna}::text FROM {tabela} WHERE {coluna} IS NOT NULL", conexao);
        await using var leitor = await cmd.ExecuteReaderAsync();
        var valores = new List<string>();
        while (await leitor.ReadAsync()) valores.Add(leitor.GetString(0));
        return valores;
    }

    private async Task<long> Contar(string tabela)
    {
        await using var conexao = new NpgsqlConnection(_api.ConnectionString);
        await conexao.OpenAsync();
        await using var cmd = new NpgsqlCommand($"SELECT count(*) FROM {tabela}", conexao);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task ColunasSensiveis_NoBanco_NaoContemTextoPuro()
    {
        var http = _api.Factory.CriarClienteHttps();
        await http.PostAsync("/api/sessoes", null);
        for (var i = 0; i < 6; i++) await EnviarAsync(http, i == 0 ? "Olá" : "Gosto de backend");
        Assert.Equal(HttpStatusCode.OK, (await http.PostAsync("/api/sessoes/atual/entrevista/encerrar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/api/sessoes/atual/recomendacoes")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await http.PostAsJsonAsync("/api/sessoes/atual/planos", new { areaId = "backend" })).StatusCode);

        // O perfil fica na coluna sessoes.perfil.
        var colunas = new[]
        {
            ("mensagens", "texto"), ("sessoes", "perfil"), ("recomendacoes", "justificativa"), ("planos", "conteudo")
        };
        var proibidos = new[] { "Gosto de backend", "backend", "Olá", LlmFalso.TextoResposta, "Interesse em" };

        foreach (var (tabela, coluna) in colunas)
        {
            var valores = await LerColunaCrua(tabela, coluna);
            Assert.NotEmpty(valores);
            foreach (var valor in valores)
            {
                Assert.False(string.IsNullOrWhiteSpace(valor));
                foreach (var texto in proibidos)
                    Assert.False(valor.Contains(texto, StringComparison.OrdinalIgnoreCase),
                        $"{tabela}.{coluna} contém texto puro '{texto}'.");
            }
        }
    }

    public static IEnumerable<object[]> MensagensInvalidas() => new[]
    {
        new object[] { "" },
        new object[] { "   " },
        new object[] { new string('a', 2001) }
    };

    [Theory]
    [MemberData(nameof(MensagensInvalidas))]
    public async Task MensagemInvalida_Retorna400_SemChamarLlmNemPersistir(string texto)
    {
        var http = _api.Factory.CriarClienteHttps();
        await http.PostAsync("/api/sessoes", null);
        var chamadasAntes = Llm.Chamadas;
        var mensagensAntes = await Contar("mensagens");

        var resposta = await http.PostAsJsonAsync("/api/sessoes/atual/mensagens",
            new { clientMessageId = Guid.NewGuid(), texto });

        await AssertProblema400(resposta);
        Assert.Equal(chamadasAntes, Llm.Chamadas);
        Assert.Equal(mensagensAntes, await Contar("mensagens"));
        var atual = await http.GetFromJsonAsync<JsonElement>("/api/sessoes/atual");
        Assert.Equal(0, atual.GetProperty("historico").GetArrayLength());
    }

    [Fact]
    public async Task ClientMessageIdVazio_Retorna400_SemChamarLlmNemPersistir()
    {
        var http = _api.Factory.CriarClienteHttps();
        await http.PostAsync("/api/sessoes", null);
        var chamadasAntes = Llm.Chamadas;
        var mensagensAntes = await Contar("mensagens");

        var resposta = await http.PostAsJsonAsync("/api/sessoes/atual/mensagens",
            new { clientMessageId = Guid.Empty, texto = "Olá" });

        await AssertProblema400(resposta);
        Assert.Equal(chamadasAntes, Llm.Chamadas);
        Assert.Equal(mensagensAntes, await Contar("mensagens"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(169)]
    public async Task DisponibilidadeInvalida_NoPlano_Retorna400_SemPersistir(int horas)
    {
        var http = _api.Factory.CriarClienteHttps();
        await http.PostAsync("/api/sessoes", null);
        var chamadasAntes = Llm.Chamadas;
        var planosAntes = await Contar("planos");

        var resposta = await http.PostAsJsonAsync("/api/sessoes/atual/planos",
            new { areaId = "backend", horasPorSemana = horas });

        await AssertProblema400(resposta);
        Assert.Equal(chamadasAntes, Llm.Chamadas);
        Assert.Equal(planosAntes, await Contar("planos"));
    }

    private static async Task AssertProblema400(HttpResponseMessage resposta)
    {
        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType!.MediaType);
        var problema = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, problema.GetProperty("status").GetInt32());
        Assert.True(problema.TryGetProperty("title", out _));
    }
}
