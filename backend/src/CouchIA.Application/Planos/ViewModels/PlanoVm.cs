using CouchIA.Application.Mentor.Services;
using CouchIA.Domain;

namespace CouchIA.Application.Planos.ViewModels;

/// <summary>
/// Resultado da geração. <see cref="Plano"/> só existe quando <see cref="Status"/> é <see cref="StatusGeracaoPlano.Gerado"/>;
/// nos demais casos nada é persistido (ex.: sem Habilidades pendentes, Req. 5.7, ou plano inviável com a
/// disponibilidade mínima em <see cref="DisponibilidadeMinimaHoras"/>).
/// </summary>
public sealed record PlanoGeradoVm(
    StatusGeracaoPlano Status,
    PlanoDesenvolvimento? Plano,
    IReadOnlyList<AvisoDependencia> Avisos,
    int? DisponibilidadeMinimaHoras);
