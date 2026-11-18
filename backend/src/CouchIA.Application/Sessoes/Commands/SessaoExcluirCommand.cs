using CouchIA.Application.Common.Interfaces;
using CouchIA.Domain;
using MediatR;

namespace CouchIA.Application.Sessoes.Commands;

/// <summary>Exclui todos os dados da Sessao, de forma irrecuperável (Req. 7.4, 7.5). Exige confirmação explícita.</summary>
public sealed record SessaoExcluirCommand(SessaoId SessaoId, bool Confirmado) : IRequest;

public sealed class SessaoExcluirCommandHandler : IRequestHandler<SessaoExcluirCommand>
{
    private readonly ISessaoRepository _repositorio;

    public SessaoExcluirCommandHandler(ISessaoRepository repositorio) => _repositorio = repositorio;

    public Task Handle(SessaoExcluirCommand request, CancellationToken cancellationToken)
        => _repositorio.ExcluirAsync(request.SessaoId, cancellationToken);
}
