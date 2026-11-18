using CouchIA.Application.Mentor.Services;
using CouchIA.Domain;

namespace CouchIA.Application.Recomendacoes.ViewModels;

public sealed record RecomendacaoVm(
    string AreaId,
    string Nome,
    int Pontuacao,
    string Justificativa,
    TipoFeedback? Feedback,
    string? MotivoFeedback);

/// <summary>
/// Recomendações na ordem de exibição (já aplicando o feedback). <see cref="BaixaConfianca"/> indica o aviso
/// de baixa confiança, acompanhado da sugestão de continuar a entrevista (Req. 3.6).
/// </summary>
public sealed record RecomendacoesVm(
    IReadOnlyList<RecomendacaoVm> Itens,
    bool BaixaConfianca,
    int CategoriasRespondidas)
{
    public static RecomendacoesVm De(Sessao sessao, Catalogo? catalogo)
    {
        var nomes = catalogo?.Areas.ToDictionary(a => a.Id, a => a.Nome) ?? new Dictionary<AreaId, string>();
        var ordenadas = FeedbackService.OrdenarParaExibicao(sessao.Recomendacoes, sessao.Feedbacks);

        var itens = ordenadas.Select(r =>
        {
            var feedback = sessao.Feedbacks.LastOrDefault(f => f.Area == r.Area);
            return new RecomendacaoVm(
                r.Area.Valor,
                nomes.TryGetValue(r.Area, out var nome) ? nome : r.Area.Valor,
                r.Pontuacao,
                r.Justificativa,
                feedback?.Tipo,
                feedback?.Motivo);
        }).ToList();

        var respondidas = MaquinaEntrevista.CategoriasRespondidas(sessao.Entrevista);
        return new RecomendacoesVm(itens, ConfiancaRecomendacao.BaixaConfianca(respondidas), respondidas);
    }
}

public sealed record HabilidadeMapaVm(
    string Id,
    string Nome,
    string Descricao,
    PrioridadeHabilidade Prioridade,
    bool Dominada);

public sealed record MapaHabilidadesVm(string AreaId, IReadOnlyList<HabilidadeMapaVm> Habilidades);
