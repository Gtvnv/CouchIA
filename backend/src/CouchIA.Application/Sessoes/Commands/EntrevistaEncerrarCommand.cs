using CouchIA.Application.Common.Exceptions;
using CouchIA.Application.Common.Interfaces;
using CouchIA.Application.Mentor.Services;
using CouchIA.Application.Recomendacoes.Commands;
using CouchIA.Application.Recomendacoes.ViewModels;
using CouchIA.Domain;
using MediatR;

namespace CouchIA.Application.Sessoes.Commands;

/// <summary>
/// Encerra a entrevista antes de concluí-la, gera as recomendações com o que foi coletado e devolve as
/// categorias sem resposta (Req. 2.5). Sem nenhuma categoria respondida, lança
/// <see cref="EntrevistaSemRespostasException"/> e a entrevista segue ativa (Req. 2.7).
/// </summary>
public sealed record EntrevistaEncerrarCommand(SessaoId SessaoId) : IRequest<EntrevistaEncerradaVm>;

/// <param name="Recomendacoes">Null quando a geração falhou; use <see cref="RecomendacoesFalharam"/> para oferecer nova tentativa (Req. 3.7).</param>
public sealed record EntrevistaEncerradaVm(
    EstadoEntrevista Estado,
    IReadOnlyList<CategoriaEntrevista> CategoriasSemResposta,
    RecomendacoesVm? Recomendacoes,
    bool RecomendacoesFalharam);

public sealed class EntrevistaEncerrarCommandHandler : IRequestHandler<EntrevistaEncerrarCommand, EntrevistaEncerradaVm>
{
    private readonly ISessaoRepository _repositorio;
    private readonly ISender _sender;

    public EntrevistaEncerrarCommandHandler(ISessaoRepository repositorio, ISender sender)
    {
        _repositorio = repositorio;
        _sender = sender;
    }

    public async Task<EntrevistaEncerradaVm> Handle(EntrevistaEncerrarCommand request, CancellationToken cancellationToken)
    {
        var sessao = await _repositorio.ObterAsync(request.SessaoId, cancellationToken)
            ?? throw new SessaoNaoEncontradaException();

        var entrevista = sessao.Entrevista;
        IReadOnlyList<CategoriaEntrevista> semResposta;

        if (entrevista.Estado is EstadoEntrevista.Concluida or EstadoEntrevista.Encerrada)
        {
            // Já finalizada: não altera o estado; apenas devolve o resultado atual.
            semResposta = CategoriasSemResposta(entrevista);
        }
        else
        {
            var resultado = MaquinaEntrevista.Encerrar(entrevista);
            if (!resultado.Aceito)
            {
                throw new EntrevistaSemRespostasException();
            }

            sessao = sessao with { Entrevista = resultado.Entrevista };
            await _repositorio.SalvarAsync(sessao, cancellationToken);
            semResposta = resultado.CategoriasSemResposta;
        }

        try
        {
            var recomendacoes = await _sender.Send(new RecomendacoesGerarCommand(request.SessaoId), cancellationToken);
            return new EntrevistaEncerradaVm(sessao.Entrevista.Estado, semResposta, recomendacoes, false);
        }
        catch (RecomendacoesIndisponiveisException)
        {
            // A entrevista já está encerrada; o Usuário pode tentar gerar as recomendações novamente.
            return new EntrevistaEncerradaVm(sessao.Entrevista.Estado, semResposta, null, true);
        }
    }

    private static IReadOnlyList<CategoriaEntrevista> CategoriasSemResposta(Entrevista entrevista) =>
        MaquinaEntrevista.TodasCategorias
            .Where(c => !entrevista.Categorias.TryGetValue(c, out var e) || e != EstadoCategoria.Respondida)
            .ToList();
}
