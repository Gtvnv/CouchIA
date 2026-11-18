using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CouchIA.Domain;

namespace CouchIA.Infrastructure.Persistencia;

/// <summary>Converte entre o agregado Sessao e as entidades de persistência.</summary>
internal static class SessaoMapeador
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.General);

    public static string CalcularHash(SessaoId id) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id.Valor))).ToLowerInvariant();

    private sealed record PerfilDto(List<string> Interesses, string? ExperienciaPrevia, string? Formacao,
        string? EstiloDeTrabalho, int? HorasEstudoPorSemana, string? Objetivos, List<string> HabilidadesDominadas);

    private sealed record EntrevistaDto(EstadoEntrevista Estado, Dictionary<string, EstadoCategoria> Categorias,
        Dictionary<string, int> Reformulacoes);

    private sealed record RecursoDto(string Titulo, string Url);

    private sealed record EtapaDto(int Numero, List<string> Habilidades, int DuracaoSemanas, List<RecursoDto> Recursos);

    private sealed record PlanoDto(Guid Id, string Area, int HorasPorSemana, List<EtapaDto> Etapas, int DuracaoTotalSemanas);

    public static string SerializarPerfil(PerfilUsuario p) => JsonSerializer.Serialize(new PerfilDto(
        p.Interesses.ToList(), p.ExperienciaPrevia, p.Formacao, p.EstiloDeTrabalho, p.HorasEstudoPorSemana,
        p.Objetivos, p.HabilidadesDominadas.Select(h => h.Valor).ToList()), Json);

    public static PerfilUsuario DesserializarPerfil(string json)
    {
        var d = JsonSerializer.Deserialize<PerfilDto>(json, Json)!;
        return new PerfilUsuario(d.Interesses, d.ExperienciaPrevia, d.Formacao, d.EstiloDeTrabalho,
            d.HorasEstudoPorSemana, d.Objetivos, d.HabilidadesDominadas.Select(h => new HabilidadeId(h)).ToHashSet());
    }

    public static string SerializarEntrevista(Entrevista e) => JsonSerializer.Serialize(new EntrevistaDto(
        e.Estado,
        e.Categorias.ToDictionary(k => k.Key.ToString(), k => k.Value),
        e.Reformulacoes.ToDictionary(k => k.Key.ToString(), k => k.Value)), Json);

    public static Entrevista DesserializarEntrevista(string json)
    {
        var d = JsonSerializer.Deserialize<EntrevistaDto>(json, Json)!;
        return new Entrevista(d.Estado,
            d.Categorias.ToDictionary(k => Enum.Parse<CategoriaEntrevista>(k.Key), k => k.Value),
            d.Reformulacoes.ToDictionary(k => Enum.Parse<CategoriaEntrevista>(k.Key), k => k.Value));
    }

    public static string SerializarPlano(PlanoDesenvolvimento p) => JsonSerializer.Serialize(new PlanoDto(
        p.Id, p.Area.Valor, p.HorasPorSemana,
        p.Etapas.Select(e => new EtapaDto(e.Numero, e.Habilidades.Select(h => h.Valor).ToList(), e.DuracaoSemanas,
            e.Recursos.Select(r => new RecursoDto(r.Titulo, r.Url.OriginalString)).ToList())).ToList(),
        p.DuracaoTotalSemanas), Json);

    public static PlanoDesenvolvimento DesserializarPlano(string json)
    {
        var d = JsonSerializer.Deserialize<PlanoDto>(json, Json)!;
        return new PlanoDesenvolvimento(d.Id, new AreaId(d.Area), d.HorasPorSemana,
            d.Etapas.Select(e => new EtapaPlano(e.Numero, e.Habilidades.Select(h => new HabilidadeId(h)).ToList(),
                e.DuracaoSemanas, e.Recursos.Select(r => new RecursoEstudo(r.Titulo, new Uri(r.Url))).ToList())).ToList(),
            d.DuracaoTotalSemanas);
    }

    public static Sessao ParaDominio(SessaoId id, SessaoEntidade e) => new(
        id,
        e.CriadaEm,
        e.Mensagens.OrderBy(m => m.Ordem)
            .Select(m => new MensagemSessao(m.ClientMessageId, m.Ordem, (PapelMensagem)m.Papel, m.Texto, m.CriadaEm)).ToList(),
        DesserializarPerfil(e.Perfil),
        DesserializarEntrevista(e.Entrevista),
        e.Recomendacoes.OrderBy(r => r.OrdemOriginal)
            .Select(r => new RecomendacaoArea(new AreaId(r.AreaId), r.Pontuacao, r.Justificativa)).ToList(),
        e.Planos.OrderBy(p => p.Ordem).Select(p => DesserializarPlano(p.Conteudo)).ToList(),
        e.Feedbacks.OrderBy(f => f.Em)
            .Select(f => new FeedbackRecomendacao(new AreaId(f.AreaId), (TipoFeedback)f.Tipo, f.Motivo, f.Em)).ToList());
}
