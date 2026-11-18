using CouchIA.Application.Common.Exceptions;
using CouchIA.Application.Common.Interfaces;
using CouchIA.Domain;
using MediatR;

namespace CouchIA.Application.Sessoes.Commands;

/// <summary>
/// Substitui o conjunto de Habilidades que o Usuário informou já dominar (Req. 4.4, 5.3).
/// Altera somente <see cref="PerfilUsuario.HabilidadesDominadas"/>.
/// </summary>
public sealed record DominioAtualizarCommand(
    SessaoId SessaoId,
    IReadOnlyCollection<string> HabilidadesDominadas) : IRequest<IReadOnlyList<string>>;

public sealed class DominioAtualizarCommandHandler : IRequestHandler<DominioAtualizarCommand, IReadOnlyList<string>>
{
    private readonly ISessaoRepository _repositorio;

    public DominioAtualizarCommandHandler(ISessaoRepository repositorio) => _repositorio = repositorio;

    public async Task<IReadOnlyList<string>> Handle(DominioAtualizarCommand request, CancellationToken cancellationToken)
    {
        var sessao = await _repositorio.ObterAsync(request.SessaoId, cancellationToken)
            ?? throw new SessaoNaoEncontradaException();

        var dominadas = request.HabilidadesDominadas
            .Select(h => new HabilidadeId(h.Trim()))
            .ToHashSet();

        sessao = sessao with { Perfil = sessao.Perfil with { HabilidadesDominadas = dominadas } };
        await _repositorio.SalvarAsync(sessao, cancellationToken);

        return dominadas.Select(h => h.Valor).OrderBy(v => v, StringComparer.Ordinal).ToList();
    }
}
