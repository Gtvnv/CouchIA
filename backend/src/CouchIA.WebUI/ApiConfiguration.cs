using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using CouchIA.Application.Common.Localization;
using CouchIA.Domain;
using CouchIA.WebUI.Middleware;
using CouchIA.WebUI.Sessao;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace CouchIA.WebUI;

/// <summary>Opções de CORS (seção <c>Cors</c>). Sem origens configuradas, nenhuma origem cruzada é aceita.</summary>
public sealed class CorsOptions
{
    public const string Secao = "Cors";
    public const string Politica = "CouchIA";

    public string[] OrigensPermitidas { get; set; } = Array.Empty<string>();
}

/// <summary>Opções do limite de requisições (seção <c>LimiteRequisicoes</c>).</summary>
public sealed class LimiteRequisicoesOptions
{
    public const string Secao = "LimiteRequisicoes";

    public int JanelaSegundos { get; set; } = 60;
    public int LimitePorIp { get; set; } = 120;
    public int LimitePorSessao { get; set; } = 40;
}

/// <summary>
/// Opções de proxy reverso (seção <c>Proxy</c>). Desligado por padrão: sem proxy confiável, cabeçalhos
/// <c>X-Forwarded-*</c> são ignorados. Ligado (ex.: demonstração com nginx), só são aceitos de <see cref="RedesConfiaveis"/>.
/// </summary>
public sealed class ProxyOptions
{
    public const string Secao = "Proxy";

    public bool ConfiarEmEncaminhamento { get; set; }

    /// <summary>Redes (CIDR) ou IPs de onde o proxy pode falar. Vazio mantém o padrão do framework (somente loopback).</summary>
    public string[] RedesConfiaveis { get; set; } = Array.Empty<string>();
}

public static class ApiConfiguration
{
    public static readonly CultureInfo CulturaPadrao = new("pt-BR");

    public static bool ConfiaEmProxy(IConfiguration configuration) =>
        configuration.GetSection(ProxyOptions.Secao).Get<ProxyOptions>()?.ConfiarEmEncaminhamento ?? false;

    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        if (ConfiaEmProxy(configuration))
        {
            var proxy = configuration.GetSection(ProxyOptions.Secao).Get<ProxyOptions>()!;
            services.Configure<ForwardedHeadersOptions>(o =>
            {
                o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                o.ForwardLimit = 1;
                if (proxy.RedesConfiaveis.Length == 0) return;

                // Somente as redes informadas são confiáveis (substitui o padrão de loopback).
                o.KnownProxies.Clear();
                o.KnownNetworks.Clear();
                foreach (var rede in proxy.RedesConfiaveis)
                {
                    var partes = rede.Split('/', 2);
                    var ip = IPAddress.Parse(partes[0].Trim());
                    if (partes.Length == 2)
                        o.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(ip, int.Parse(partes[1], CultureInfo.InvariantCulture)));
                    else
                        o.KnownProxies.Add(ip);
                }
            });
        }

        services.Configure<SessaoCookieOptions>(configuration.GetSection(SessaoCookieOptions.Secao));
        services.Configure<LimiteCorpoOptions>(configuration.GetSection(LimiteCorpoOptions.Secao));
        services.Configure<LimiteRequisicoesOptions>(configuration.GetSection(LimiteRequisicoesOptions.Secao));
        services.AddSingleton<SessaoCookie>();

        services.Configure<RequestLocalizationOptions>(o =>
        {
            o.DefaultRequestCulture = new RequestCulture(CulturaPadrao);
            o.SupportedCultures = new List<CultureInfo> { CulturaPadrao };
            o.SupportedUICultures = new List<CultureInfo> { CulturaPadrao };
            // Cultura fixa em pt-BR: ignora Accept-Language para manter as mensagens consistentes.
            o.RequestCultureProviders.Clear();
        });

        // Falhas de binding viram ProblemDetails genéricos e localizados, sem ecoar mensagens do framework.
        services.Configure<ApiBehaviorOptions>(o =>
            o.InvalidModelStateResponseFactory = ctx =>
            {
                var localizer = ctx.HttpContext.RequestServices.GetRequiredService<IStringLocalizer<MentorLocalization>>();
                return new ObjectResult(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://couchia.app/problemas/RequisicaoInvalida",
                    Title = localizer["ProblemaRequisicaoInvalidaTitulo"].Value,
                    Detail = localizer["ProblemaRequisicaoInvalidaDetalhe"].Value,
                    Extensions = { ["traceId"] = ctx.HttpContext.TraceIdentifier }
                })
                {
                    StatusCode = StatusCodes.Status400BadRequest,
                    ContentTypes = { "application/problem+json" }
                };
            });

        var origens = configuration.GetSection(CorsOptions.Secao).Get<CorsOptions>()?.OrigensPermitidas ?? Array.Empty<string>();
        services.AddCors(o => o.AddPolicy(CorsOptions.Politica, p =>
        {
            if (origens.Length > 0)
            {
                p.WithOrigins(origens)
                    .WithMethods("GET", "POST", "PUT", "DELETE")
                    .WithHeaders("Content-Type", "Accept")
                    .AllowCredentials();
            }
        }));

        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.GlobalLimiter = CriarLimitador(configuration);
            o.OnRejected = async (ctx, _) =>
            {
                var janela = ctx.HttpContext.RequestServices
                    .GetRequiredService<Microsoft.Extensions.Options.IOptions<LimiteRequisicoesOptions>>().Value.JanelaSegundos;
                ctx.HttpContext.Response.Headers.RetryAfter = janela.ToString(CultureInfo.InvariantCulture);
                var localizer = ctx.HttpContext.RequestServices.GetRequiredService<IStringLocalizer<MentorLocalization>>();
                await ProblemaHttp.EscreverAsync(ctx.HttpContext, localizer, StatusCodes.Status429TooManyRequests, "LimiteRequisicoes");
            };
        });

        return services;
    }

    /// <summary>Limite por IP e por Sessao (cookie válido), encadeados. Protege o custo do LLM (Req. 8.6).</summary>
    private static PartitionedRateLimiter<HttpContext> CriarLimitador(IConfiguration configuration)
    {
        var cfg = configuration.GetSection(LimiteRequisicoesOptions.Secao).Get<LimiteRequisicoesOptions>() ?? new();
        var janela = TimeSpan.FromSeconds(Math.Max(1, cfg.JanelaSegundos));

        FixedWindowRateLimiterOptions Opcoes(int limite) => new()
        {
            PermitLimit = Math.Max(1, limite),
            Window = janela,
            QueueLimit = 0,
            AutoReplenishment = true
        };

        var porIp = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
            RateLimitPartition.GetFixedWindowLimiter(
                "ip:" + (ctx.Connection.RemoteIpAddress?.ToString() ?? "desconhecido"), _ => Opcoes(cfg.LimitePorIp)));

        var porSessao = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        {
            var nome = ctx.RequestServices.GetRequiredService<SessaoCookie>().Nome;
            return ctx.Request.Cookies.TryGetValue(nome, out var valor) && SessaoId.TryParse(valor, out _)
                ? RateLimitPartition.GetFixedWindowLimiter("sessao:" + valor, _ => Opcoes(cfg.LimitePorSessao))
                : RateLimitPartition.GetNoLimiter("sem-sessao");
        });

        return PartitionedRateLimiter.CreateChained(porIp, porSessao);
    }

    /// <summary>Pipeline da API, na ordem de segurança: erros, cabeçalhos, HTTPS/HSTS, CORS, cultura, sessão, limites.</summary>
    public static WebApplication UseApi(this WebApplication app)
    {
        // Encaminhamento primeiro (somente se habilitado): esquema e IP reais precisam valer para HTTPS/HSTS, cookie e limite por IP.
        if (ConfiaEmProxy(app.Configuration))
            app.UseForwardedHeaders();

        // A cultura vem primeiro para que todas as mensagens (inclusive de erro) saiam em pt-BR.
        app.UseRequestLocalization();
        app.UseMiddleware<ExcecoesMiddleware>();
        app.UseMiddleware<CabecalhosSegurancaMiddleware>();

        if (!app.Environment.IsDevelopment())
            app.UseHsts();
        app.UseHttpsRedirection();

        app.UseRouting();
        app.UseCors(CorsOptions.Politica);
        app.UseMiddleware<LimiteCorpoMiddleware>();
        app.UseMiddleware<SessaoMiddleware>();
        app.UseRateLimiter();

        app.UseAuthorization();
        return app;
    }
}
