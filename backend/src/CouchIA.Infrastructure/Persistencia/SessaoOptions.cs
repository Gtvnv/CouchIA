using Microsoft.Extensions.Configuration;

namespace CouchIA.Infrastructure.Persistencia;

/// <summary>Política de retenção das sessões. Config: <c>Sessoes:InatividadeDias</c> e <c>Sessoes:IntervaloLimpezaMinutos</c>.</summary>
public sealed record SessaoOptions(TimeSpan Inatividade, TimeSpan IntervaloLimpeza)
{
    public const int InatividadeDiasPadrao = 30;
    public const int IntervaloLimpezaMinutosPadrao = 60;

    public static SessaoOptions DeConfiguracao(IConfiguration configuration)
    {
        var dias = LerPositivo(configuration["Sessoes:InatividadeDias"], InatividadeDiasPadrao);
        var minutos = LerPositivo(configuration["Sessoes:IntervaloLimpezaMinutos"], IntervaloLimpezaMinutosPadrao);
        return new SessaoOptions(TimeSpan.FromDays(dias), TimeSpan.FromMinutes(minutos));
    }

    private static int LerPositivo(string? valor, int padrao) =>
        int.TryParse(valor, out var n) && n > 0 ? n : padrao;
}
