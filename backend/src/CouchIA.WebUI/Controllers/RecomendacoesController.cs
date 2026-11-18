using CouchIA.Application.Recomendacoes.Commands;
using CouchIA.Application.Recomendacoes.Queries;
using CouchIA.Application.Recomendacoes.ViewModels;
using CouchIA.Application.Sessoes.Queries;
using CouchIA.WebUI.Contratos;
using Microsoft.AspNetCore.Mvc;

namespace CouchIA.WebUI.Controllers;

/// <summary>Recomendações, feedback e mapa de habilidades. A Sessao vem do cookie.</summary>
public sealed class RecomendacoesController : ApiControllerBase
{
    [HttpGet("~/api/sessoes/atual/recomendacoes")]
    public async Task<ActionResult<IReadOnlyList<RecomendacaoDto>>> Listar(CancellationToken cancellationToken)
    {
        var vm = await Mediator.Send(new RecomendacaoListarQuery(ExigirSessaoId()), cancellationToken);
        return Ok(Mapear(vm));
    }

    [HttpPut("~/api/sessoes/atual/recomendacoes/{areaId}/feedback")]
    public async Task<ActionResult<FeedbackDto>> RegistrarFeedback(
        string areaId, [FromBody] FeedbackRegistrarRequisicao requisicao, CancellationToken cancellationToken)
    {
        var sessaoId = ExigirSessaoId();
        await Mediator.Send(
            new FeedbackRegistrarCommand(sessaoId, areaId, requisicao.Tipo!.Value, requisicao.Motivo),
            cancellationToken);

        // O feedback gravado (com data) é o último da área; o comando substitui o anterior.
        var sessao = await Mediator.Send(new SessaoObterQuery(sessaoId), cancellationToken);
        var feedback = sessao.Feedbacks.Last(f => f.Area.Valor == areaId);
        return Ok(MapeadorContratos.Feedback(feedback));
    }

    [HttpGet("~/api/areas/{areaId}/habilidades")]
    public async Task<ActionResult<MapaHabilidadesVm>> ObterMapa(string areaId, CancellationToken cancellationToken) =>
        Ok(await Mediator.Send(new MapaHabilidadesObterQuery(ExigirSessaoId(), areaId), cancellationToken));

    private static IReadOnlyList<RecomendacaoDto> Mapear(RecomendacoesVm vm) =>
        vm.Itens.Select(i => new RecomendacaoDto(i.AreaId, i.Nome, i.Pontuacao, i.Justificativa)).ToList();
}
