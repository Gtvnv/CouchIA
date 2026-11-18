using CouchIA.Application.Common.Exceptions;
using CouchIA.Application.Common.Interfaces;
using CouchIA.Application.Sessoes.ViewModels;
using CouchIA.Domain;
using MediatR;

namespace CouchIA.Application.Sessoes.Queries;

/// <summary>Restaura histórico, perfil, recomendações e planos da Sessao (Req. 7.2).</summary>
public sealed record SessaoObterQuery(SessaoId SessaoId) : IRequest<SessaoVm>;

public sealed class SessaoObterQueryHandler : IRequestHandler<SessaoObterQuery, SessaoVm>
{
    private readonly ISessaoRepository _repositorio;

    public SessaoObterQueryHandler(ISessaoRepository repositorio) => _repositorio = repositorio;

    public async Task<SessaoVm> Handle(SessaoObterQuery request, CancellationToken cancellationToken)
    {
        var sessao = await _repositorio.ObterAsync(request.SessaoId, cancellationToken)
            ?? throw new SessaoNaoEncontradaException();

        return SessaoVm.De(sessao);
    }
}
