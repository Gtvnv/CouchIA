namespace CouchIA.Domain;

public sealed record Sessao(
    SessaoId Id,
    DateTimeOffset CriadaEm,
    IReadOnlyList<MensagemSessao> Historico,
    PerfilUsuario Perfil,
    Entrevista Entrevista,
    IReadOnlyList<RecomendacaoArea> Recomendacoes,
    IReadOnlyList<PlanoDesenvolvimento> Planos,
    IReadOnlyList<FeedbackRecomendacao> Feedbacks);

public sealed record MensagemSessao(Guid ClientMessageId, int Ordem, PapelMensagem Papel, string Texto, DateTimeOffset CriadaEm);

public sealed record PerfilUsuario(
    IReadOnlyList<string> Interesses,
    string? ExperienciaPrevia,
    string? Formacao,
    string? EstiloDeTrabalho,
    int? HorasEstudoPorSemana,                      // 1..168
    string? Objetivos,
    IReadOnlySet<HabilidadeId> HabilidadesDominadas);

public sealed record Entrevista(
    EstadoEntrevista Estado,
    IReadOnlyDictionary<CategoriaEntrevista, EstadoCategoria> Categorias,
    IReadOnlyDictionary<CategoriaEntrevista, int> Reformulacoes);

public sealed record RecomendacaoArea(AreaId Area, int Pontuacao /*0..100*/, string Justificativa /*1..500*/);

public sealed record FeedbackRecomendacao(AreaId Area, TipoFeedback Tipo, string? Motivo /*0..500*/, DateTimeOffset Em);
