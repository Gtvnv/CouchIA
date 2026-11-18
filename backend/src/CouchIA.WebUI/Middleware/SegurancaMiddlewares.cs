using CouchIA.Application.Common.Localization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Localization;

namespace CouchIA.WebUI.Middleware;

/// <summary>Cabeçalhos de segurança em todas as respostas. A API só devolve JSON, SSE e arquivos; nada é renderizado.</summary>
public sealed class CabecalhosSegurancaMiddleware
{
    private readonly RequestDelegate _next;
    private readonly bool _relaxarParaSwagger;

    public CabecalhosSegurancaMiddleware(RequestDelegate next, IHostEnvironment ambiente)
    {
        _next = next;
        _relaxarParaSwagger = ambiente.IsDevelopment();
    }

    public Task InvokeAsync(HttpContext contexto)
    {
        contexto.Response.OnStarting(static estado =>
        {
            var (ctx, relaxar) = ((HttpContext, bool))estado;
            var h = ctx.Response.Headers;
            h["X-Content-Type-Options"] = "nosniff";
            h["X-Frame-Options"] = "DENY";
            h["Referrer-Policy"] = "no-referrer";
            h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            h["Cross-Origin-Resource-Policy"] = "same-site";
            if (!(relaxar && ctx.Request.Path.StartsWithSegments("/swagger")))
                h["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
            // Respostas carregam dados pessoais da Sessao: nunca devem ser armazenadas em cache.
            if (!h.ContainsKey("Cache-Control"))
                h["Cache-Control"] = "no-store";
            return Task.CompletedTask;
        }, (contexto, _relaxarParaSwagger));

        return _next(contexto);
    }
}

/// <summary>Opções de limite do corpo da requisição (seção <c>Requisicao</c>).</summary>
public sealed class LimiteCorpoOptions
{
    public const string Secao = "Requisicao";

    /// <summary>Tamanho máximo do corpo em bytes. Padrão 64 KB (mensagens têm até 2.000 caracteres).</summary>
    public long TamanhoMaximoCorpoBytes { get; set; } = 64 * 1024;
}

/// <summary>
/// Limita o tamanho do corpo: recusa por Content-Length (413) e aplica o limite ao servidor
/// para corpos sem Content-Length (ex.: chunked).
/// </summary>
public sealed class LimiteCorpoMiddleware
{
    private readonly RequestDelegate _next;
    private readonly long _limite;

    public LimiteCorpoMiddleware(RequestDelegate next, Microsoft.Extensions.Options.IOptions<LimiteCorpoOptions> options)
    {
        _next = next;
        _limite = options.Value.TamanhoMaximoCorpoBytes;
    }

    public async Task InvokeAsync(HttpContext contexto, IStringLocalizer<MentorLocalization> localizer)
    {
        if (contexto.Request.ContentLength > _limite)
        {
            await ProblemaHttp.EscreverAsync(contexto, localizer, StatusCodes.Status413PayloadTooLarge, "CorpoGrande");
            return;
        }

        var feature = contexto.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (feature is { IsReadOnly: false })
            feature.MaxRequestBodySize = _limite;

        await _next(contexto);
    }
}
