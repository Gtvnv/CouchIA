using System.ComponentModel.DataAnnotations;
using CouchIA.Application.Common.Exceptions;
using CouchIA.Application.Common.Localization;
using CouchIA.Application.Mentor.Services;
using CouchIA.Application.Planos.Commands;
using CouchIA.Application.Planos.Exceptions;
using CouchIA.Application.Planos.Queries;
using CouchIA.Domain;
using CouchIA.WebUI.Contratos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace CouchIA.WebUI.Controllers;

/// <summary>Geração e exportação de planos. A Sessao vem do cookie.</summary>
public sealed class PlanosController : ApiControllerBase
{
    [HttpPost("~/api/sessoes/atual/planos")]
    public async Task<ActionResult<PlanoDto>> Gerar([FromBody] PlanoGerarRequisicao requisicao, CancellationToken cancellationToken)
    {
        var resultado = await Mediator.Send(
            new PlanoGerarCommand(ExigirSessaoId(), new AreaId(requisicao.AreaId.Trim()), requisicao.HorasPorSemana),
            cancellationToken);

        if (resultado.Plano is { } plano)
            return Ok(MapeadorContratos.Plano(plano));

        if (resultado.Status == StatusGeracaoPlano.CatalogoInvalido)
            throw new CatalogoIndisponivelException();

        return PlanoNaoGerado(resultado.Status, resultado.DisponibilidadeMinimaHoras);
    }

    [HttpGet("~/api/sessoes/atual/planos/{id:guid}/exportar")]
    public async Task<IActionResult> Exportar(Guid id, [FromQuery, Required] FormatoArquivoApi? formato, CancellationToken cancellationToken)
    {
        var formatoExportacao = formato == FormatoArquivoApi.Md ? FormatoExportacao.Markdown : FormatoExportacao.Pdf;
        var arquivo = await Mediator.Send(new PlanoExportarQuery(ExigirSessaoId(), id, formatoExportacao), cancellationToken);
        return File(arquivo.Conteudo, arquivo.ContentType, arquivo.NomeArquivo);
    }

    /// <summary>Plano não gerado por regra de negócio (sem pendências, inviável, limites): 422 com explicação localizada.</summary>
    private ObjectResult PlanoNaoGerado(StatusGeracaoPlano status, int? disponibilidadeMinimaHoras)
    {
        var localizer = HttpContext.RequestServices.GetRequiredService<IStringLocalizer<MentorLocalization>>();
        var chave = status switch
        {
            StatusGeracaoPlano.SemHabilidadesPendentes => "PlanoSemHabilidadesPendentes",
            StatusGeracaoPlano.PlanoInviavel => "PlanoInviavel",
            StatusGeracaoPlano.ExcedeLimiteEtapas => "PlanoExcedeLimiteEtapas",
            StatusGeracaoPlano.ExcedeLimiteDuracao => "PlanoExcedeLimiteDuracao",
            _ => "PlanoNaoGerado"
        };

        var problema = new ProblemDetails
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Type = $"https://couchia.app/problemas/{chave}",
            Title = localizer[$"Problema{chave}Titulo"].Value,
            Detail = localizer[$"Problema{chave}Detalhe"].Value,
            Extensions = { ["traceId"] = HttpContext.TraceIdentifier }
        };
        if (disponibilidadeMinimaHoras is { } horas)
            problema.Extensions["disponibilidadeMinimaHoras"] = horas;

        return new ObjectResult(problema)
        {
            StatusCode = StatusCodes.Status422UnprocessableEntity,
            ContentTypes = { "application/problem+json" }
        };
    }
}
