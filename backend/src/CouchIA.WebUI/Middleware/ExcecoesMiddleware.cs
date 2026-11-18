using CouchIA.Application.Common.Exceptions;
using CouchIA.Application.Common.Localization;
using CouchIA.Application.Mentor.Services;
using CouchIA.Application.Planos.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace CouchIA.WebUI.Middleware;

/// <summary>
/// Converte exceções em <see cref="ProblemDetails"/> localizados (pt-BR). Nunca expõe stack trace,
/// mensagem da exceção ou conteúdo bruto do LLM; o 500 é genérico (Req. 8.6).
/// </summary>
public sealed class ExcecoesMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExcecoesMiddleware> _logger;

    public ExcecoesMiddleware(RequestDelegate next, ILogger<ExcecoesMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext contexto, IStringLocalizer<MentorLocalization> localizer)
    {
        try
        {
            await _next(contexto);
        }
        catch (OperationCanceledException) when (contexto.RequestAborted.IsCancellationRequested)
        {
            // Cliente desconectou; não há a quem responder.
        }
        catch (Exception ex)
        {
            if (contexto.Response.HasStarted)
            {
                // Resposta (ex.: SSE) já iniciada: não é possível trocar o status; aborta a conexão.
                _logger.LogError("Falha após o início da resposta: {TipoExcecao}", ex.GetType().Name);
                contexto.Abort();
                return;
            }

            var (status, chave, erros) = Mapear(ex);
            if (status >= 500)
            {
                // Só o tipo da exceção: a mensagem pode conter texto do usuário ou segredos.
                _logger.LogError("Requisição falhou com {TipoExcecao} (HTTP {Status})", ex.GetType().Name, status);
            }

            await ProblemaHttp.EscreverAsync(contexto, localizer, status, chave, erros);
        }
    }

    internal static (int Status, string Chave, IDictionary<string, string[]>? Erros) Mapear(Exception ex) => ex switch
    {
        ValidationException v => (StatusCodes.Status400BadRequest, "Validacao", AgruparErros(v)),
        SessaoNaoEncontradaException => (StatusCodes.Status404NotFound, "SessaoNaoEncontrada", null),
        PlanoNaoEncontradoException => (StatusCodes.Status404NotFound, "PlanoNaoEncontrado", null),
        EntrevistaSemRespostasException => (StatusCodes.Status409Conflict, "EntrevistaSemRespostas", null),
        RespostaLlmInvalidaException => (StatusCodes.Status502BadGateway, "RespostaLlmInvalida", null),
        LlmTimeoutException => (StatusCodes.Status504GatewayTimeout, "LlmTimeout", null),
        RecomendacoesIndisponiveisException => (StatusCodes.Status503ServiceUnavailable, "RecomendacoesIndisponiveis", null),
        MapaHabilidadesIndisponivelException => (StatusCodes.Status503ServiceUnavailable, "MapaIndisponivel", null),
        CatalogoIndisponivelException => (StatusCodes.Status503ServiceUnavailable, "CatalogoIndisponivel", null),
        ExportacaoFalhouException => (StatusCodes.Status500InternalServerError, "ExportacaoFalhou", null),
        UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "AcessoNegado", null),
        BadHttpRequestException b when b.StatusCode == StatusCodes.Status413PayloadTooLarge
            => (StatusCodes.Status413PayloadTooLarge, "CorpoGrande", null),
        BadHttpRequestException or System.Text.Json.JsonException
            => (StatusCodes.Status400BadRequest, "RequisicaoInvalida", null),
        _ => (StatusCodes.Status500InternalServerError, "ErroInterno", null)
    };

    private static IDictionary<string, string[]> AgruparErros(ValidationException ex) =>
        ex.Errors
            .GroupBy(e => string.IsNullOrWhiteSpace(e.PropertyName) ? string.Empty : e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());
}

/// <summary>Escreve <see cref="ProblemDetails"/> (RFC 7807) com título e detalhe localizados.</summary>
public static class ProblemaHttp
{
    public static async Task EscreverAsync(
        HttpContext contexto,
        IStringLocalizer<MentorLocalization> localizer,
        int status,
        string chave,
        IDictionary<string, string[]>? erros = null)
    {
        var problema = erros is null
            ? new ProblemDetails()
            : new ValidationProblemDetails(erros);

        problema.Status = status;
        problema.Type = $"https://couchia.app/problemas/{chave}";
        problema.Title = localizer[$"Problema{chave}Titulo"].Value;
        problema.Detail = localizer[$"Problema{chave}Detalhe"].Value;
        problema.Extensions["traceId"] = contexto.TraceIdentifier;

        contexto.Response.StatusCode = status;
        await contexto.Response.WriteAsJsonAsync(problema, problema.GetType(), options: null, contentType: "application/problem+json");
    }
}
