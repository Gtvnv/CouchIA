using CouchIA.Application.Common.Localization;
using CouchIA.Application.Sessoes.Commands;
using CouchIA.Application.Sessoes.Queries;
using CouchIA.Domain;
using CouchIA.WebUI.Contratos;
using CouchIA.WebUI.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace CouchIA.WebUI.Controllers;

/// <summary>
/// Sessões, mensagens (SSE) e entrevista. A Sessao vem sempre do cookie: nenhuma rota aceita o identificador
/// de outra Sessao.
/// </summary>
public sealed class SessoesController : ApiControllerBase
{
    [HttpPost]
    public async Task<ActionResult<SessaoDto>> Criar(CancellationToken cancellationToken)
    {
        var criada = await Mediator.Send(new SessaoCriarCommand(), cancellationToken);
        var id = SessaoId.Criar(criada.Id);
        DefinirCookieSessao(id);

        var sessao = await Mediator.Send(new SessaoObterQuery(id), cancellationToken);
        return Ok(MapeadorContratos.Sessao(sessao));
    }

    [HttpGet("atual")]
    public async Task<ActionResult<SessaoDto>> Obter(CancellationToken cancellationToken)
    {
        var sessao = await Mediator.Send(new SessaoObterQuery(ExigirSessaoId()), cancellationToken);
        return Ok(MapeadorContratos.Sessao(sessao));
    }

    [HttpDelete("atual")]
    public async Task<IActionResult> Excluir([FromBody] SessaoExcluirRequisicao? corpo, CancellationToken cancellationToken)
    {
        await Mediator.Send(new SessaoExcluirCommand(ExigirSessaoId(), corpo?.Confirmar ?? false), cancellationToken);
        RemoverCookieSessao();
        return NoContent();
    }

    /// <summary>Envia a mensagem e responde em SSE: eventos <c>trecho</c>, <c>final</c> e <c>erro</c>.</summary>
    [HttpPost("atual/mensagens")]
    public async Task EnviarMensagem([FromBody] MensagemEnviarRequisicao requisicao, CancellationToken cancellationToken)
    {
        var sessaoId = ExigirSessaoId();
        var escritor = new EscritorSse(Response);

        try
        {
            await Mediator.Send(
                new MensagemEnviarCommand(
                    sessaoId,
                    requisicao.ClientMessageId,
                    requisicao.Texto,
                    (trecho, ct) => escritor.EscreverAsync(new EventoTrecho(trecho), ct)),
                cancellationToken);

            var sessao = await Mediator.Send(new SessaoObterQuery(sessaoId), cancellationToken);
            var mensagem = sessao.Historico.Last(m => m.Papel == PapelMensagem.Mentor);
            await escritor.EscreverAsync(
                new EventoFinal(
                    MapeadorContratos.Mensagem(mensagem),
                    MapeadorContratos.Entrevista(sessao.Entrevista),
                    MapeadorContratos.Perfil(sessao.Perfil)),
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var (status, chave, _) = ExcecoesMiddleware.Mapear(ex);

            // Erros do cliente (validação, Sessao inexistente) antes do início do fluxo seguem como ProblemDetails.
            if (!escritor.Iniciado && status < StatusCodes.Status500InternalServerError)
                throw;

            var servicos = HttpContext.RequestServices;
            servicos.GetRequiredService<ILogger<SessoesController>>()
                .LogError("Falha ao responder a mensagem: {TipoExcecao} (HTTP {Status})", ex.GetType().Name, status);

            var localizer = servicos.GetRequiredService<IStringLocalizer<MentorLocalization>>();
            await escritor.EscreverAsync(new EventoErro(localizer[$"Problema{chave}Detalhe"].Value), cancellationToken);
        }
    }

    [HttpPost("atual/entrevista/encerrar")]
    public async Task<ActionResult<EntrevistaEncerrarDto>> EncerrarEntrevista(CancellationToken cancellationToken)
    {
        var resultado = await Mediator.Send(new EntrevistaEncerrarCommand(ExigirSessaoId()), cancellationToken);
        return Ok(new EntrevistaEncerrarDto(resultado.CategoriasSemResposta));
    }
}
