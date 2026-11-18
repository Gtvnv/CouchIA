using CouchIA.Application.Common.Interfaces;
using CouchIA.Application.Sessoes.ViewModels;
using CouchIA.Application.Mentor.Services;
using CouchIA.Domain;
using MediatR;

namespace CouchIA.Application.Sessoes.Commands;

/// <summary>
/// Cria uma Sessao com identificador de 256 bits, histórico vazio e data de criação (Req. 1.2).
/// Por padrão a entrevista já nasce EmAndamento (o botão de início aciona este comando).
/// </summary>
public sealed record SessaoCriarCommand(bool IniciarEntrevista = true) : IRequest<SessaoCriadaVm>;

public sealed class SessaoCriarCommandHandler : IRequestHandler<SessaoCriarCommand, SessaoCriadaVm>
{
    private readonly ISessaoRepository _repositorio;
    private readonly TimeProvider _relogio;

    public SessaoCriarCommandHandler(ISessaoRepository repositorio, TimeProvider relogio)
    {
        _repositorio = repositorio;
        _relogio = relogio;
    }

    public async Task<SessaoCriadaVm> Handle(SessaoCriarCommand request, CancellationToken cancellationToken)
    {
        var entrevista = MaquinaEntrevista.Nova();
        if (request.IniciarEntrevista)
        {
            entrevista = MaquinaEntrevista.Iniciar(entrevista);
        }

        var perfil = new PerfilUsuario(
            Array.Empty<string>(), null, null, null, null, null, new HashSet<HabilidadeId>());

        var sessao = new Sessao(
            SessaoId.Gerar(),
            _relogio.GetUtcNow(),
            Array.Empty<MensagemSessao>(),
            perfil,
            entrevista,
            Array.Empty<RecomendacaoArea>(),
            Array.Empty<PlanoDesenvolvimento>(),
            Array.Empty<FeedbackRecomendacao>());

        await _repositorio.SalvarAsync(sessao, cancellationToken);

        return new SessaoCriadaVm(sessao.Id.Valor, sessao.CriadaEm, sessao.Entrevista.Estado);
    }
}
