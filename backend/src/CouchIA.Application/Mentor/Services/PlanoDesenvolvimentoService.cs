using CouchIA.Domain;

namespace CouchIA.Application.Mentor.Services;

public enum StatusGeracaoPlano
{
    Gerado,
    SemHabilidadesPendentes,
    /// <summary>Alguma Habilidade sozinha excede 12 semanas na disponibilidade informada.</summary>
    PlanoInviavel,
    /// <summary>O plano precisaria de mais de 20 etapas.</summary>
    ExcedeLimiteEtapas,
    /// <summary>O plano duraria mais de 104 semanas.</summary>
    ExcedeLimiteDuracao,
    DisponibilidadeInvalida,
    AreaNaoEncontrada,
    CatalogoInvalido
}

/// <summary>Aviso de pré-requisito não dominado que não entra no plano (fora da área).</summary>
public sealed record AvisoDependencia(HabilidadeId Habilidade, HabilidadeId PreRequisito);

public sealed record ResultadoPlano(
    StatusGeracaoPlano Status,
    PlanoDesenvolvimento? Plano,
    IReadOnlyList<AvisoDependencia> Avisos,
    int? DisponibilidadeMinimaHoras)
{
    public bool Sucesso => Status == StatusGeracaoPlano.Gerado;
}

/// <summary>
/// Geração determinística do plano: remove dominadas, ordena topologicamente (desempate por
/// prioridade e ordem do catálogo), agrupa em etapas (até 5 Habilidades e 12 semanas),
/// com no máximo 20 etapas e 104 semanas no total.
/// </summary>
public static class PlanoDesenvolvimentoService
{
    public const int HabilidadesPorEtapaMaximo = 5;
    public const int SemanasPorEtapaMaximo = 12;
    public const int EtapasMaximo = 20;
    public const int DuracaoTotalMaximaSemanas = 104;
    public const int DisponibilidadeMinima = 1;
    public const int DisponibilidadeMaxima = 168;
    public const int RecursosPorEtapaMaximo = 3;

    public static ResultadoPlano Gerar(
        Catalogo catalogo,
        AreaId area,
        IReadOnlySet<HabilidadeId> dominadas,
        int horasPorSemana,
        Guid? id = null)
    {
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(dominadas);

        if (horasPorSemana < DisponibilidadeMinima || horasPorSemana > DisponibilidadeMaxima)
            return Falha(StatusGeracaoPlano.DisponibilidadeInvalida);

        var areaCatalogo = catalogo.Areas.FirstOrDefault(a => a.Id == area);
        if (areaCatalogo is null)
            return Falha(StatusGeracaoPlano.AreaNaoEncontrada);

        var porId = new Dictionary<HabilidadeId, Habilidade>();
        foreach (var h in catalogo.Habilidades) porId[h.Id] = h;

        // Pendentes na ordem do catálogo (da área), sem duplicatas.
        var pendentes = new List<(Habilidade Habilidade, PrioridadeHabilidade Prioridade, int Indice)>();
        var vistos = new HashSet<HabilidadeId>();
        for (var i = 0; i < areaCatalogo.Habilidades.Count; i++)
        {
            var item = areaCatalogo.Habilidades[i];
            if (dominadas.Contains(item.Habilidade) || !vistos.Add(item.Habilidade)) continue;
            if (!porId.TryGetValue(item.Habilidade, out var hab))
                return Falha(StatusGeracaoPlano.CatalogoInvalido);
            pendentes.Add((hab, item.Prioridade, i));
        }

        if (pendentes.Count == 0)
            return Falha(StatusGeracaoPlano.SemHabilidadesPendentes);

        var avisos = CalcularAvisos(pendentes.Select(p => p.Habilidade), vistos, dominadas);

        var ordem = OrdenarTopologicamente(pendentes);
        if (ordem is null)
            return Falha(StatusGeracaoPlano.CatalogoInvalido);

        // Uma Habilidade sozinha acima de 12 semanas: nenhuma etapa válida existe.
        var maiorHoras = ordem.Max(h => h.HorasEstimadas);
        if (Semanas(maiorHoras, horasPorSemana) > SemanasPorEtapaMaximo)
            return new ResultadoPlano(StatusGeracaoPlano.PlanoInviavel, null, avisos,
                CeilDiv(maiorHoras, SemanasPorEtapaMaximo));

        var grupos = Agrupar(ordem, horasPorSemana);
        var totalSemanas = grupos.Sum(g => Semanas(g.Sum(h => h.HorasEstimadas), horasPorSemana));

        var status = grupos.Count > EtapasMaximo ? StatusGeracaoPlano.ExcedeLimiteEtapas
            : totalSemanas > DuracaoTotalMaximaSemanas ? StatusGeracaoPlano.ExcedeLimiteDuracao
            : StatusGeracaoPlano.Gerado;

        if (status != StatusGeracaoPlano.Gerado)
            return new ResultadoPlano(status, null, avisos, DisponibilidadeMinimaViavel(ordem));

        var etapas = new List<EtapaPlano>(grupos.Count);
        for (var i = 0; i < grupos.Count; i++)
        {
            var g = grupos[i];
            var recursos = g.SelectMany(h => h.Recursos)
                .DistinctBy(r => r.Url)
                .Take(RecursosPorEtapaMaximo)
                .ToList();
            etapas.Add(new EtapaPlano(
                i + 1,
                g.Select(h => h.Id).ToList(),
                Semanas(g.Sum(h => h.HorasEstimadas), horasPorSemana),
                recursos));
        }

        var plano = new PlanoDesenvolvimento(id ?? Guid.NewGuid(), area, horasPorSemana, etapas, totalSemanas);
        return new ResultadoPlano(StatusGeracaoPlano.Gerado, plano, avisos, null);
    }

    private static ResultadoPlano Falha(StatusGeracaoPlano status) =>
        new(status, null, Array.Empty<AvisoDependencia>(), null);

    private static int CeilDiv(int a, int b) => (a + b - 1) / b;

    private static int Semanas(int horas, int disponibilidade) => CeilDiv(horas, disponibilidade);

    private static List<AvisoDependencia> CalcularAvisos(
        IEnumerable<Habilidade> pendentes, HashSet<HabilidadeId> noPlano, IReadOnlySet<HabilidadeId> dominadas)
    {
        var avisos = new List<AvisoDependencia>();
        foreach (var h in pendentes)
            foreach (var pre in h.PreRequisitos)
                if (!dominadas.Contains(pre) && !noPlano.Contains(pre))
                    avisos.Add(new AvisoDependencia(h.Id, pre));
        return avisos;
    }

    private static List<Habilidade>? OrdenarTopologicamente(
        List<(Habilidade Habilidade, PrioridadeHabilidade Prioridade, int Indice)> pendentes)
    {
        var ids = pendentes.Select(p => p.Habilidade.Id).ToHashSet();
        var restantes = pendentes.ToList();
        var emitidas = new HashSet<HabilidadeId>();
        var resultado = new List<Habilidade>(pendentes.Count);

        while (restantes.Count > 0)
        {
            var prontas = restantes
                .Where(p => p.Habilidade.PreRequisitos.All(pr => !ids.Contains(pr) || emitidas.Contains(pr)))
                .OrderBy(p => (int)p.Prioridade)
                .ThenBy(p => p.Indice)
                .ToList();
            if (prontas.Count == 0) return null; // ciclo

            var escolhida = prontas[0];
            resultado.Add(escolhida.Habilidade);
            emitidas.Add(escolhida.Habilidade.Id);
            restantes.Remove(escolhida);
        }

        return resultado;
    }

    private static List<List<Habilidade>> Agrupar(List<Habilidade> ordem, int horasPorSemana)
    {
        var grupos = new List<List<Habilidade>>();
        var atual = new List<Habilidade>();
        var horasAtual = 0;
        foreach (var h in ordem)
        {
            var cheio = atual.Count >= HabilidadesPorEtapaMaximo
                || Semanas(horasAtual + h.HorasEstimadas, horasPorSemana) > SemanasPorEtapaMaximo;
            if (atual.Count > 0 && cheio)
            {
                grupos.Add(atual);
                atual = new List<Habilidade>();
                horasAtual = 0;
            }
            atual.Add(h);
            horasAtual += h.HorasEstimadas;
        }
        if (atual.Count > 0) grupos.Add(atual);
        return grupos;
    }

    /// <summary>Menor disponibilidade (1..168) que respeita todos os limites; null se nenhuma serve.</summary>
    private static int? DisponibilidadeMinimaViavel(List<Habilidade> ordem)
    {
        for (var d = DisponibilidadeMinima; d <= DisponibilidadeMaxima; d++)
        {
            if (Semanas(ordem.Max(h => h.HorasEstimadas), d) > SemanasPorEtapaMaximo) continue;
            var grupos = Agrupar(ordem, d);
            if (grupos.Count <= EtapasMaximo
                && grupos.Sum(g => Semanas(g.Sum(h => h.HorasEstimadas), d)) <= DuracaoTotalMaximaSemanas)
                return d;
        }
        return null;
    }
}
