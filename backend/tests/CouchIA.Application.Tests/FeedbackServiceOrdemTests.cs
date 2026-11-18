using CouchIA.Application.Mentor.Services;
using CouchIA.Domain;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace CouchIA.Application.Tests;

public class FeedbackServiceOrdemTests
{
    private static readonly AreaId[] Areas = new[] { "a", "b", "c", "d", "e" }.Select(v => new AreaId(v)).ToArray();

    private static Gen<List<RecomendacaoArea>> GeradorRecomendacoes() =>
        Gen.Choose(0, Areas.Length).SelectMany(n =>
            Gen.ArrayOf(Gen.Choose(0, 100), n).Select(pontos =>
                Areas.Take(n).Zip(pontos, (a, p) => new RecomendacaoArea(a, p, "j")).ToList()));

    private static Gen<List<(AreaId Area, TipoFeedback Tipo)>> GeradorSequencia() =>
        Gen.ListOf(Gen.Elements(Areas).SelectMany(a =>
            Gen.Elements(TipoFeedback.Util, TipoFeedback.NaoUtil).Select(t => (a, t))))
            .Select(l => l.ToList());

    private static IReadOnlyList<FeedbackRecomendacao> Aplicar(List<(AreaId Area, TipoFeedback Tipo)> sequencia)
    {
        IReadOnlyList<FeedbackRecomendacao> feedbacks = new List<FeedbackRecomendacao>();
        var i = 0;
        foreach (var (area, tipo) in sequencia)
            feedbacks = FeedbackService.Registrar(feedbacks, area, tipo, null, DateTimeOffset.UnixEpoch.AddSeconds(i++))!;
        return feedbacks;
    }

    // Feature: ia-mentor-carreira, Property 25: Ordem de exibição depende só do feedback atual
    // **Validates: Requirements 9.4, 9.5**
    [Property(MaxTest = 100)]
    public Property Areas_nao_uteis_vem_depois_sem_remover_nem_alterar_pontuacoes()
    {
        var gen = GeradorRecomendacoes().SelectMany(r => GeradorSequencia().Select(s => (r, s)));
        return Prop.ForAll(gen.ToArbitrary(), entrada =>
        {
            var (recomendacoes, sequencia) = entrada;
            var feedbacks = Aplicar(sequencia);
            var naoUteis = feedbacks.Where(f => f.Tipo == TipoFeedback.NaoUtil).Select(f => f.Area).ToHashSet();

            var ordem = FeedbackService.OrdenarParaExibicao(recomendacoes, feedbacks);

            // Nenhuma área removida e pontuações intactas.
            var mesmosItens = ordem.Count == recomendacoes.Count
                && ordem.OrderBy(r => r.Area.Valor).SequenceEqual(recomendacoes.OrderBy(r => r.Area.Valor));

            // Todas as áreas sem "não útil" antes das com "não útil".
            var primeiroNaoUtil = ordem.ToList().FindIndex(r => naoUteis.Contains(r.Area));
            var particionado = primeiroNaoUtil < 0
                || ordem.Skip(primeiroNaoUtil).All(r => naoUteis.Contains(r.Area));

            // Cada grupo em pontuação decrescente.
            var decrescente = ordem.Zip(ordem.Skip(1), (x, y) =>
                naoUteis.Contains(x.Area) != naoUteis.Contains(y.Area) || x.Pontuacao >= y.Pontuacao).All(b => b);

            // Igual à ordem obtida só com os feedbacks ativos (o último por área).
            var ativos = sequencia.GroupBy(s => s.Area).Select(g => g.Last())
                .Select(s => new FeedbackRecomendacao(s.Area, s.Tipo, null, DateTimeOffset.UnixEpoch)).ToList();
            var igualAtivos = FeedbackService.OrdenarParaExibicao(recomendacoes, ativos).SequenceEqual(ordem);

            return mesmosItens && particionado && decrescente && igualAtivos;
        });
    }

    [Fact]
    public void Nao_util_substituido_por_util_deixa_de_rebaixar()
    {
        var a = Areas[0];
        var b = Areas[1];
        var recomendacoes = new List<RecomendacaoArea> { new(a, 90, "j"), new(b, 50, "j") };
        var feedbacks = Aplicar(new() { (a, TipoFeedback.NaoUtil) });
        Assert.Equal(new[] { b, a }, FeedbackService.OrdenarParaExibicao(recomendacoes, feedbacks).Select(r => r.Area));

        feedbacks = FeedbackService.Registrar(feedbacks, a, TipoFeedback.Util, null, DateTimeOffset.UnixEpoch.AddDays(1))!;
        Assert.Equal(new[] { a, b }, FeedbackService.OrdenarParaExibicao(recomendacoes, feedbacks).Select(r => r.Area));
    }
}
