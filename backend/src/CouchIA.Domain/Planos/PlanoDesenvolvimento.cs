namespace CouchIA.Domain;

public sealed record PlanoDesenvolvimento(
    Guid Id,
    AreaId Area,
    int HorasPorSemana,
    IReadOnlyList<EtapaPlano> Etapas,
    int DuracaoTotalSemanas);

public sealed record EtapaPlano(
    int Numero,                                     // 1..n
    IReadOnlyList<HabilidadeId> Habilidades,        // 1..5, em ordem topológica
    int DuracaoSemanas,                             // 1..12
    IReadOnlyList<RecursoEstudo> Recursos);         // 1..3
