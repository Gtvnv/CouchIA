using System.Security.Cryptography;
using CouchIA.Application.Mentor.Services;
using CouchIA.Infrastructure.Criptografia;
using CouchIA.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace CouchIA.WebUI.Tests;

/// <summary>
/// LLM determinístico. Streaming (conversa): texto + <c>&lt;&lt;EXTRACAO&gt;&gt;</c> + JSON utilizável para qualquer categoria.
/// Completar (recomendações): JSON válido com três áreas do catálogo, justificativas que citam o interesse "backend".
/// </summary>
public sealed class LlmFalso : ILlmClient
{
    public const string TextoResposta = "Entendi. Conte mais sobre você.";
    public const string Interesse = "backend";

    private const string Extracao =
        "{\"utilizavel\":true,\"interesses\":[\"" + Interesse + "\"],\"texto\":\"" + Interesse + " com C#\",\"horasEstudoPorSemana\":20}";

    private int _chamadas;

    /// <summary>Quantidade de chamadas recebidas (completar + fluxo).</summary>
    public int Chamadas => Volatile.Read(ref _chamadas);

    public Task<string> CompletarAsync(RequisicaoLlm requisicao, CancellationToken ct)
    {
        Interlocked.Increment(ref _chamadas);
        return Task.FromResult(
            "{\"recomendacoes\":[" +
            $"{{\"area\":\"backend\",\"pontuacao\":90,\"justificativa\":\"Interesse em {Interesse} combina com a área.\"}}," +
            $"{{\"area\":\"frontend\",\"pontuacao\":70,\"justificativa\":\"Parte do interesse em {Interesse} se aplica.\"}}," +
            $"{{\"area\":\"dados\",\"pontuacao\":50,\"justificativa\":\"O interesse em {Interesse} toca em dados.\"}}" +
            "]}");
    }

    public async IAsyncEnumerable<string> CompletarEmFluxoAsync(
        RequisicaoLlm requisicao, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        Interlocked.Increment(ref _chamadas);
        yield return "Entendi. ";
        await Task.Yield();
        yield return "Conte mais sobre você.\n<<EXTR";
        yield return "ACAO>>" + Extracao;
    }
}

/// <summary>PostgreSQL real (Testcontainers) com migrations aplicadas e a API hospedada em memória com o LLM falso.</summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private readonly Dictionary<string, string?> _anteriores = new();

    public const string ChaveBase64Teste = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=";

    public ApiFactory Factory { get; private set; } = default!;
    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using (var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(ConnectionString).Options,
            new CriptografiaCampoAesGcm(RandomNumberGenerator.GetBytes(32))))
        {
            await db.Database.MigrateAsync();
        }

        // O Program lê a configuração de forma antecipada; variáveis de ambiente são vistas por todo o pipeline.
        Definir("ConnectionStrings__Default", ConnectionString);
        Definir("Criptografia__ChaveBase64", ChaveBase64Teste);
        Definir("LimiteRequisicoes__LimitePorIp", "1000000");
        Definir("LimiteRequisicoes__LimitePorSessao", "1000000");
        Definir("HTTPS_PORT", "443");

        Factory = new ApiFactory();
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null) await Factory.DisposeAsync();
        foreach (var (chave, valor) in _anteriores)
            Environment.SetEnvironmentVariable(chave, valor);
        await _container.DisposeAsync();
    }

    private void Definir(string chave, string valor)
    {
        _anteriores[chave] = Environment.GetEnvironmentVariable(chave);
        Environment.SetEnvironmentVariable(chave, valor);
    }

    public ApplicationDbContext NovoContexto() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(ConnectionString).Options,
            new CriptografiaCampoAesGcm(Convert.FromBase64String(ChaveBase64Teste)));

    public sealed class ApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ILlmClient>();
                services.AddSingleton<ILlmClient, LlmFalso>();
            });
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<HttpClient, JarraDeCookie> Jarras = new();

        /// <summary>Cookie de sessão atualmente guardado pelo cliente (null se ausente ou removido pelo servidor).</summary>
        public static string? CookieDe(HttpClient http) => Jarras.TryGetValue(http, out var j) ? j.Valor : null;

        /// <summary>
        /// Cliente HTTPS que guarda o cookie de sessão (respeitando Secure e a remoção pelo servidor) e não segue redirecionamentos.
        /// </summary>
        public HttpClient CriarClienteHttps()
        {
            var jarra = new JarraDeCookie();
            var http = CreateDefaultClient(new Uri("https://localhost"), jarra);
            Jarras.Add(http, jarra);
            return http;
        }
    }

    /// <summary>Guarda e reenvia o cookie de sessão apenas em https, como um navegador faria com um cookie Secure.</summary>
    public sealed class JarraDeCookie : DelegatingHandler
    {
        private const string Nome = "couchia_sessao";

        public string? Valor { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (Valor is not null && request.RequestUri!.Scheme == Uri.UriSchemeHttps && !request.Headers.Contains("Cookie"))
                request.Headers.Add("Cookie", $"{Nome}={Valor}");

            var resposta = await base.SendAsync(request, ct);

            if (resposta.Headers.TryGetValues("Set-Cookie", out var cookies))
            {
                foreach (var c in cookies.Where(c => c.StartsWith(Nome + "=", StringComparison.Ordinal)))
                {
                    var partes = c.Split(';', StringSplitOptions.TrimEntries);
                    var valor = partes[0][(Nome.Length + 1)..];
                    var expirado = valor.Length == 0
                        || partes.Any(p => p.Equals("max-age=0", StringComparison.OrdinalIgnoreCase))
                        || partes.Any(p => p.StartsWith("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase));
                    var seguro = partes.Any(p => p.Equals("secure", StringComparison.OrdinalIgnoreCase));
                    if (!seguro || request.RequestUri!.Scheme != Uri.UriSchemeHttps) continue;
                    Valor = expirado ? null : valor;
                }
            }

            return resposta;
        }
    }
}

[CollectionDefinition(Nome)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Nome = "Api";
}
