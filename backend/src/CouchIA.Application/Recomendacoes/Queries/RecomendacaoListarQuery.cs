using CouchIA.Application.Common.Exceptions;
using CouchIA.Application.Common.Interfaces;
using CouchIA.Application.Mentor.Services;
using CouchIA.Application.Recomendacoes.Commands;
using CouchIA.Application.Recomendacoes.ViewModels;
using CouchIA.Domain;
using MediatR;

namespace CouchIA.Application.Recomendacoes.Queries;

/// <summary>
/// Lista as recomendações na ordem de exibição (feedback aplicado). Se a entrevista terminou
/// (concluída ou encerrada) e ainda não há recomendações, gera-as (Req. 3.1).
/// </summary>
public sealed record RecomendacaoListarQuery(SessaoId SessaoId) : IRequest<RecomendacoesVm>;

public sealed class RecomendacaoListarQueryHandler : IRequestHandler<RecomendacaoListarQuery, RecomendacoesVm>
{
    private readonly ISessaoRepository _repositorio;
    private readonly ICatalogoProvider _catalogo;
    private readonly ISender _sender;

    public RecomendacaoListarQueryHandler(ISessaoRepository repositorio, ICatalogoProvider catalogo, ISender sender)
    {
        _repositorio = repositorio;
        _catalogo = catalogo;
        _sender = sender;
    }

    public async Task<RecomendacoesVm> Handle(RecomendacaoListarQuery request, CancellationToken cancellationToken)
    {
        var sessao = await _repositorio.ObterAsync(request.SessaoId, cancellationToken)
            ?? throw new SessaoNaoEncontradaException();

        var finalizada = sessao.Entrevista.Estado is EstadoEntrevista.Concluida or EstadoEntrevista.Encerrada;
        if (sessao.Recomendacoes.Count == 0 && finalizada)
        {
            return await _sender.Send(new RecomendacoesGerarCommand(request.SessaoId), cancellationToken);
        }

        return RecomendacoesVm.De(sessao, _catalogo.Ativo);
    }
}
