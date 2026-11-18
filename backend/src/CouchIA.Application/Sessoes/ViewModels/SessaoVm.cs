using CouchIA.Domain;

namespace CouchIA.Application.Sessoes.ViewModels;

public sealed record SessaoCriadaVm(string Id, DateTimeOffset CriadaEm, EstadoEntrevista EstadoEntrevista);

public sealed record MensagemVm(Guid ClientMessageId, int Ordem, PapelMensagem Papel, string Texto, DateTimeOffset CriadaEm);

public sealed record SessaoVm(
    string Id,
    DateTimeOffset CriadaEm,
    IReadOnlyList<MensagemVm> Historico,
    PerfilUsuario Perfil,
    Entrevista Entrevista,
    IReadOnlyList<RecomendacaoArea> Recomendacoes,
    IReadOnlyList<PlanoDesenvolvimento> Planos,
    IReadOnlyList<FeedbackRecomendacao> Feedbacks)
{
    public static SessaoVm De(Sessao s) => new(
        s.Id.Valor,
        s.CriadaEm,
        s.Historico
            .OrderBy(m => m.Ordem)
            .Select(m => new MensagemVm(m.ClientMessageId, m.Ordem, m.Papel, m.Texto, m.CriadaEm))
            .ToList(),
        s.Perfil,
        s.Entrevista,
        s.Recomendacoes,
        s.Planos,
        s.Feedbacks);
}

/// <summary>Resultado do envio de uma mensagem. <see cref="Reenvio"/> indica que a resposta já existia (idempotência).</summary>
public sealed record MensagemRespostaVm(
    Guid ClientMessageId,
    string Resposta,
    EstadoEntrevista EstadoEntrevista,
    int CategoriasRespondidas,
    bool EntrevistaConcluida,
    bool Reenvio);
