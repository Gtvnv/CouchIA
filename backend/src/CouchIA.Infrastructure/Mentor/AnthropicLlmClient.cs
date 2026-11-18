using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using CouchIA.Application.Mentor.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace CouchIA.Infrastructure.Mentor;

/// <summary>Opções do cliente Anthropic. A chave vem somente da configuração (Anthropic:ApiKey).</summary>
public sealed class AnthropicOptions
{
    public const string Secao = "Anthropic";

    public string? ApiKey { get; set; }
    public string BaseUrl { get; set; } = "https://api.anthropic.com/";
    public string Modelo { get; set; } = "claude-sonnet-4-20250514";
    public string VersaoApi { get; set; } = "2023-06-01";
    public int MaxTokens { get; set; } = 2048;
}

/// <summary>Implementação de <see cref="ILlmClient"/> sobre a API Messages da Anthropic.</summary>
public sealed class AnthropicLlmClient : ILlmClient
{
    private readonly HttpClient _http;
    private readonly AnthropicOptions _opcoes;
    private readonly RedatorDeSegredos _redator;
    private readonly ILogger<AnthropicLlmClient> _logger;
    private readonly TimeSpan _tempoLimite;

    public AnthropicLlmClient(
        HttpClient http,
        AnthropicOptions opcoes,
        RedatorDeSegredos redator,
        ILogger<AnthropicLlmClient> logger,
        TimeSpan? tempoLimite = null)
    {
        _http = http;
        _opcoes = opcoes;
        _redator = redator;
        _logger = logger;
        _tempoLimite = tempoLimite ?? ILlmClient.TempoLimite;

        // O timeout geral do HttpClient não pode cortar o fluxo; o limite é aplicado por nós.
        _http.Timeout = Timeout.InfiniteTimeSpan;
        _http.BaseAddress ??= new Uri(opcoes.BaseUrl);
    }

    public async Task<string> CompletarAsync(RequisicaoLlm requisicao, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_tempoLimite);

        try
        {
            using var request = CriarRequest(requisicao, stream: false);
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, cts.Token);
            var corpo = await response.Content.ReadAsStringAsync(cts.Token);
            GarantirSucesso(response, corpo);

            using var doc = JsonDocument.Parse(corpo);
            var sb = new StringBuilder();
            if (doc.RootElement.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
            {
                foreach (var bloco in content.EnumerateArray())
                {
                    if (bloco.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
                        sb.Append(t.GetString());
                }
            }
            return sb.ToString();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Timeout ao chamar o LLM após {Segundos}s.", _tempoLimite.TotalSeconds);
            throw new LlmTimeoutException();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError("Falha de rede ao chamar o LLM: {Erro}", _redator.Redigir(ex.Message));
            throw;
        }
    }

    public async IAsyncEnumerable<string> CompletarEmFluxoAsync(
        RequisicaoLlm requisicao, [EnumeratorCancellation] CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_tempoLimite);

        HttpResponseMessage response;
        StreamReader reader;
        try
        {
            using var request = CriarRequest(requisicao, stream: true);
            response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                var corpo = await response.Content.ReadAsStringAsync(cts.Token);
                try { GarantirSucesso(response, corpo); }
                finally { response.Dispose(); }
            }
            reader = new StreamReader(await response.Content.ReadAsStreamAsync(cts.Token), Encoding.UTF8);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Timeout ao iniciar fluxo do LLM após {Segundos}s.", _tempoLimite.TotalSeconds);
            throw new LlmTimeoutException();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError("Falha de rede ao chamar o LLM: {Erro}", _redator.Redigir(ex.Message));
            throw;
        }

        using (response)
        using (reader)
        {
            while (true)
            {
                string? linha;
                try
                {
                    // Reinicia o limite a cada leitura: o tempo é "sem resposta".
                    cts.CancelAfter(_tempoLimite);
                    linha = await reader.ReadLineAsync(cts.Token);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    _logger.LogWarning("Timeout no fluxo do LLM após {Segundos}s sem dados.", _tempoLimite.TotalSeconds);
                    throw new LlmTimeoutException();
                }

                if (linha is null) yield break;
                if (!linha.StartsWith("data:", StringComparison.Ordinal)) continue;

                var trecho = ExtrairTrecho(linha.AsSpan(5).Trim().ToString());
                if (trecho is { Length: > 0 }) yield return trecho;
            }
        }
    }

    private static string? ExtrairTrecho(string json)
    {
        if (json.Length == 0 || json == "[DONE]") return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var raiz = doc.RootElement;
            if (raiz.TryGetProperty("type", out var tipo) && tipo.GetString() == "content_block_delta"
                && raiz.TryGetProperty("delta", out var delta)
                && delta.TryGetProperty("text", out var texto))
                return texto.GetString();
        }
        catch (JsonException)
        {
            // Evento malformado é ignorado.
        }
        return null;
    }

    private HttpRequestMessage CriarRequest(RequisicaoLlm requisicao, bool stream)
    {
        if (string.IsNullOrWhiteSpace(_opcoes.ApiKey))
            throw new InvalidOperationException("Chave da API Anthropic não configurada (Anthropic:ApiKey).");

        var payload = new
        {
            model = _opcoes.Modelo,
            max_tokens = _opcoes.MaxTokens,
            system = requisicao.PromptSistema,
            stream,
            messages = requisicao.Mensagens.Select(m => new
            {
                role = m.Papel == PapelMensagem.User ? "user" : "assistant",
                content = m.Conteudo
            })
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "v1/messages")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-api-key", _opcoes.ApiKey);
        request.Headers.Add("anthropic-version", _opcoes.VersaoApi);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(stream ? "text/event-stream" : "application/json"));
        return request;
    }

    private void GarantirSucesso(HttpResponseMessage response, string corpo)
    {
        if (response.IsSuccessStatusCode) return;

        _logger.LogError("LLM retornou {Status}: {Corpo}",
            (int)response.StatusCode, _redator.Redigir(corpo));
        throw new HttpRequestException($"Falha ao chamar o LLM (HTTP {(int)response.StatusCode}).", null, response.StatusCode);
    }
}

public static class AnthropicLlmClientExtensions
{
    public static IServiceCollection AddAnthropicLlmClient(this IServiceCollection services, IConfiguration configuration)
    {
        var secao = configuration.GetSection(AnthropicOptions.Secao);
        var padrao = new AnthropicOptions();
        var opcoes = new AnthropicOptions
        {
            ApiKey = secao["ApiKey"],
            BaseUrl = secao["BaseUrl"] ?? padrao.BaseUrl,
            Modelo = secao["Modelo"] ?? padrao.Modelo,
            VersaoApi = secao["VersaoApi"] ?? padrao.VersaoApi,
            MaxTokens = int.TryParse(secao["MaxTokens"], out var max) ? max : padrao.MaxTokens
        };

        services.AddSingleton(opcoes);
        services.TryAddSingleton(new RedatorDeSegredos(new[] { opcoes.ApiKey }));
        services.AddHttpClient<ILlmClient, AnthropicLlmClient>((sp, http) =>
        {
            http.BaseAddress = new Uri(opcoes.BaseUrl);
            http.Timeout = Timeout.InfiniteTimeSpan;
        });
        return services;
    }
}
