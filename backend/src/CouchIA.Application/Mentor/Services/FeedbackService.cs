using CouchIA.Domain;

namespace CouchIA.Application.Mentor.Services;

/// <summary>
/// Regras puras de feedback: um feedback ativo por área (o último substitui),
/// validação do motivo (0 a 500) e ordem de exibição que rebaixa áreas "não útil".
/// </summary>
public static class FeedbackService
{
    public const int MotivoTamanhoMaximo = 500;

    /// <summary>Motivo ausente ou vazio é aceito; caso contrário exige até 500 caracteres.</summary>
    public static bool MotivoEhValido(string? motivo) =>
        motivo is null || motivo.Length <= MotivoTamanhoMaximo;

    /// <summary>
    /// Registra o feedback. Substitui o feedback anterior da mesma área e preserva os demais.
    /// Retorna null quando o motivo é inválido (a lista original não é alterada).
    /// </summary>
    public static IReadOnlyList<FeedbackRecomendacao>? Registrar(
        IReadOnlyList<FeedbackRecomendacao> feedbacks,
        AreaId area,
        TipoFeedback tipo,
        string? motivo,
        DateTimeOffset em)
    {
        ArgumentNullException.ThrowIfNull(feedbacks);

        if (!MotivoEhValido(motivo))
            return null;

        var resultado = feedbacks.Where(f => f.Area != area).ToList();
        resultado.Add(new FeedbackRecomendacao(area, tipo, string.IsNullOrEmpty(motivo) ? null : motivo, em));
        return resultado;
    }

    /// <summary>
    /// Ordem de exibição: áreas sem feedback "não útil" primeiro, depois as "não útil".
    /// Cada grupo segue pontuação decrescente (estável). Pontuações e itens permanecem intactos.
    /// </summary>
    public static IReadOnlyList<RecomendacaoArea> OrdenarParaExibicao(
        IReadOnlyList<RecomendacaoArea> recomendacoes,
        IReadOnlyList<FeedbackRecomendacao> feedbacks)
    {
        ArgumentNullException.ThrowIfNull(recomendacoes);
        ArgumentNullException.ThrowIfNull(feedbacks);

        // O último feedback registrado por área é o ativo.
        var naoUteis = new HashSet<AreaId>();
        foreach (var f in feedbacks)
        {
            if (f.Tipo == TipoFeedback.NaoUtil) naoUteis.Add(f.Area);
            else naoUteis.Remove(f.Area);
        }

        return recomendacoes
            .OrderBy(r => naoUteis.Contains(r.Area) ? 1 : 0)
            .ThenByDescending(r => r.Pontuacao)
            .ToList();
    }
}
