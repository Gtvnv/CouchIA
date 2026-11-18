using CouchIA.Application.Mentor.Services;
using CouchIA.Domain;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace CouchIA.Application.Tests;

public class FeedbackServiceMotivoTests
{
    private static readonly AreaId[] Areas = 
        new[] { "backend", "frontend", "dados", "devops", "qa" }.Select(v => new AreaId(v)).ToArray();

    // Tamanhos de 0 a 700, com viés para a fronteira de 500.
    private static Gen<string?> GeradorMotivo()
    {
        var tamanho = Gen.Frequency(
            (3, Gen.Choose(0, 700)),
            (2, Gen.Choose(495, 505)));
        var texto = tamanho.Select(n => (string?)new string('x', n));
        return Gen.OneOf(Gen.Constant<string?>(null), texto);
    }

    private static Gen<List<FeedbackRecomendacao>> GeradorFeedbacks()
    {
        var item = from area in Gen.Elements(Areas)
                   from tipo in Gen.Elements(TipoFeedback.Util, TipoFeedback.NaoUtil)
                   select new FeedbackRecomendacao(area, tipo, "m", DateTimeOffset.UnixEpoch);
        return Gen.ListOf(item).Select(l => l.ToList());
    }

    // Feature: ia-mentor-carreira, Property 26: Validação do motivo do feedback
    // **Validates: Requirements 9.3, 9.6**
    [Property(MaxTest = 100)]
    public Property Motivo_aceito_se_e_somente_se_ate_500_caracteres_e_rejeitado_nao_altera_estado()
    {
        var gen = from motivo in GeradorMotivo()
                  from feedbacks in GeradorFeedbacks()
                  from area in Gen.Elements(Areas)
                  from tipo in Gen.Elements(TipoFeedback.Util, TipoFeedback.NaoUtil)
                  select (motivo, feedbacks, area, tipo);

        return Prop.ForAll(gen.ToArbitrary(), x =>
        {
            var copia = x.feedbacks.ToList();
            var esperadoValido = x.motivo is null || x.motivo.Length <= 500;

            var valido = FeedbackService.MotivoEhValido(x.motivo);
            var resultado = FeedbackService.Registrar(x.feedbacks, x.area, x.tipo, x.motivo, DateTimeOffset.UnixEpoch);

            if (valido != esperadoValido) return false;
            if (!x.feedbacks.SequenceEqual(copia)) return false; // entrada nunca é mutada

            return esperadoValido
                ? resultado is not null && resultado.Any(f => f.Area == x.area && f.Tipo == x.tipo)
                : resultado is null;
        });
    }

    [Fact]
    public void Motivo_vazio_ou_ausente_e_aceito_e_501_caracteres_e_rejeitado()
    {
        Assert.True(FeedbackService.MotivoEhValido(null));
        Assert.True(FeedbackService.MotivoEhValido(""));
        Assert.True(FeedbackService.MotivoEhValido(new string('a', 500)));
        Assert.False(FeedbackService.MotivoEhValido(new string('a', 501)));
        Assert.NotNull(FeedbackService.Registrar([], new AreaId("backend"), TipoFeedback.NaoUtil, null, DateTimeOffset.UnixEpoch));
    }
}
