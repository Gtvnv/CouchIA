namespace CouchIA.Domain;

public sealed record Catalogo(IReadOnlyList<AreaAtuacao> Areas, IReadOnlyList<Habilidade> Habilidades);

public sealed record AreaAtuacao(
    AreaId Id,
    string Nome,                                    // 1..100, único
    string Descricao,                               // 1..1000
    IReadOnlyList<HabilidadeNaArea> Habilidades);   // 1..100, ordem preservada

public sealed record HabilidadeNaArea(HabilidadeId Habilidade, PrioridadeHabilidade Prioridade);

public sealed record Habilidade(
    HabilidadeId Id,
    string Nome,                                    // 1..100
    string Descricao,                               // 1..300, 1 a 3 frases
    int HorasEstimadas,                             // 1..500
    IReadOnlyList<RecursoEstudo> Recursos,          // 1..3
    IReadOnlyList<HabilidadeId> PreRequisitos);     // 0..10, sem repetição, sem a própria

public sealed record RecursoEstudo(string Titulo, Uri Url);
