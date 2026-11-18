using CouchIA.Domain;

namespace CouchIA.Application.Mentor.Services;

public sealed record HabilidadeDoMapa(
    HabilidadeId Id,
    string Nome,
    string Descricao,
    PrioridadeHabilidade Prioridade,
    bool Dominada);

public sealed record MapaHabilidades(AreaId AreaId, IReadOnlyList<HabilidadeDoMapa> Habilidades);

public enum TipoErroMapa
{
    CatalogoIndisponivel,
    AreaInexistente,
    AreaSemHabilidades,
    HabilidadeDaAreaInexistente
}

public sealed record ResultadoMapaHabilidades(MapaHabilidades? Mapa, TipoErroMapa? Erro)
{
    public bool Sucesso => Mapa is not null;
}

/// <summary>
/// Monta o Mapa de Habilidades de uma área: todas as Habilidades da área com a prioridade do
/// catálogo, ordenadas por essencial, importante e desejável (ordem do catálogo preservada
/// dentro do mesmo nível), marcando as dominadas sem alterar posições. Função pura, sem I/O.
/// Nunca devolve mapa parcial: qualquer falha resulta em erro.
/// </summary>
public static class MapaHabilidadesService
{
    public static ResultadoMapaHabilidades Montar(
        Catalogo? catalogo,
        AreaId areaId,
        IEnumerable<HabilidadeId>? dominadas)
    {
        if (catalogo is null)
            return Falha(TipoErroMapa.CatalogoIndisponivel);

        var area = catalogo.Areas.FirstOrDefault(a => a.Id.Equals(areaId));
        if (area is null)
            return Falha(TipoErroMapa.AreaInexistente);

        if (area.Habilidades.Count == 0)
            return Falha(TipoErroMapa.AreaSemHabilidades);

        var porId = new Dictionary<HabilidadeId, Habilidade>();
        foreach (var h in catalogo.Habilidades)
            porId.TryAdd(h.Id, h);

        var conjuntoDominadas = dominadas is null
            ? new HashSet<HabilidadeId>()
            : new HashSet<HabilidadeId>(dominadas);

        var itens = new List<HabilidadeDoMapa>(area.Habilidades.Count);
        foreach (var vinculo in area.Habilidades)
        {
            if (!porId.TryGetValue(vinculo.Habilidade, out var habilidade))
                return Falha(TipoErroMapa.HabilidadeDaAreaInexistente);

            itens.Add(new HabilidadeDoMapa(
                habilidade.Id,
                habilidade.Nome,
                habilidade.Descricao,
                vinculo.Prioridade,
                conjuntoDominadas.Contains(habilidade.Id)));
        }

        // OrderBy é estável: preserva a ordem do catálogo entre Habilidades do mesmo nível.
        var ordenadas = itens.OrderBy(i => (int)i.Prioridade).ToList();

        return new ResultadoMapaHabilidades(new MapaHabilidades(area.Id, ordenadas), null);
    }

    private static ResultadoMapaHabilidades Falha(TipoErroMapa erro) => new(null, erro);
}
