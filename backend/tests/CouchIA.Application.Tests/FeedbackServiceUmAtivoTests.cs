using CouchIA.Application.Mentor.Services;
using CouchIA.Domain;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace CouchIA.Application.Tests;

public class FeedbackServiceUmAtivoTests
{
    private static readonly AreaId[] Areas = new[] { "a", "b", "c", "d" }.Select(v => new AreaId(v)).ToArray();

    private static Gen<List<(AreaId Area, TipoFeedback Tipo, string? Motivo)>> GeradorSequencia()
    {
        var item = Gen.Elements(Areas).SelectMany(a =>
            Gen.Elements(TipoFeedback.Util, TipoFeedback.NaoUtil).SelectMany(t =>
                Gen.Elements<string?>(null, "", "motivo", new string('x', 500)).Select(m => (a, t, m))));
        return Gen.ListOf(item).Select(l => l.ToList());
    }

    // Feature: ia-mentor-carreira, Property 24: Feedback: um ativo por área, o último vence
    // **Validates: Requirements 9.1, 9.4**
    [Property(MaxTest = 100)]
    public Property Cada_area_tem_no_maximo_um_feedback_ativo_o_ultimo_registrado()
    {
        return Prop.ForAll(GeradorSequencia().ToArbitrary(), sequencia =>
        {
            IReadOnlyList<FeedbackRecomendacao> feedbacks = new List<FeedbackRecomendacao>();
            var inicio = DateTimeOffset.UnixEpoch;
            var i = 0;
            foreach (var (area, tipo, motivo) in sequencia)
            {
                var r = FeedbackService.Registrar(feedbacks, area, tipo, motivo, inicio.AddMinutes(i++));
                if (r is null) return false;
                feedbacks = r;
            }

            var ultimoPorArea = sequencia
                .Select((s, idx) => (s, idx))
                .GroupBy(x => x.s.Area)
                .ToDictionary(g => g.Key, g => g.Last());

            if (feedbacks.GroupBy(f => f.Area).Any(g => g.Count() > 1)) return false;
            if (feedbacks.Count != ultimoPorArea.Count) return false;

            return feedbacks.All(f =>
            {
                var esperado = ultimoPorArea[f.Area];
                return f.Tipo == esperado.s.Tipo
                    && f.Em == inicio.AddMinutes(esperado.idx)
                    && f.Motivo == (string.IsNullOrEmpty(esperado.s.Motivo) ? null : esperado.s.Motivo);
            });
        });
    }
}
