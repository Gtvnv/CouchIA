using System.Text.Json;
using System.Text.Json.Serialization;
using CouchIA.Application.Mentor.Services;
using CouchIA.Application.Sessoes.ViewModels;
using CouchIA.Domain;
using PapelDominio = CouchIA.Domain.PapelMensagem;

namespace CouchIA.WebUI.Contratos;

/// <summary>Opções JSON do contrato com a SPA: camelCase e enums como strings camelCase.</summary>
public static class JsonContrato
{
    public static readonly JsonSerializerOptions Opcoes = Criar();

    public static void Configurar(JsonSerializerOptions o)
    {
        o.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        o.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        o.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    }

    private static JsonSerializerOptions Criar()
    {
        var o = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Configurar(o);
        return o;
    }
}

// Requisições.
public sealed record MensagemEnviarRequisicao(Guid ClientMessageId, string Texto);

public sealed record FeedbackRegistrarRequisicao(
    [System.ComponentModel.DataAnnotations.Required] TipoFeedback? Tipo,
    string? Motivo);

public sealed record PlanoGerarRequisicao(
    [System.ComponentModel.DataAnnotations.Required] string AreaId,
    int? HorasPorSemana = null);

public sealed record SessaoExcluirRequisicao(bool Confirmar);

/// <summary>Formato do parâmetro <c>formato</c> da exportação: <c>pdf</c> ou <c>md</c>.</summary>
public enum FormatoArquivoApi { Pdf, Md }

// Respostas.
public sealed record MensagemDto(Guid ClientMessageId, int Ordem, string Papel, string Texto, DateTimeOffset CriadaEm);

public sealed record PerfilDto(
    IReadOnlyList<string> Interesses,
    string? ExperienciaPrevia,
    string? Formacao,
    string? EstiloDeTrabalho,
    int? HorasEstudoPorSemana,
    string? Objetivos,
    IReadOnlyList<string> HabilidadesDominadas);

public sealed record EntrevistaDto(
    EstadoEntrevista Estado,
    IReadOnlyDictionary<string, EstadoCategoria> Categorias,
    IReadOnlyDictionary<string, int> Reformulacoes);

public sealed record RecomendacaoDto(
    string AreaId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Nome,
    int Pontuacao,
    string Justificativa);

public sealed record FeedbackDto(string AreaId, TipoFeedback Tipo, string? Motivo, DateTimeOffset Em);

public sealed record RecursoDto(string Titulo, string Url);

public sealed record EtapaDto(int Numero, IReadOnlyList<string> Habilidades, int DuracaoSemanas, IReadOnlyList<RecursoDto> Recursos);

public sealed record PlanoDto(Guid Id, string AreaId, int HorasPorSemana, IReadOnlyList<EtapaDto> Etapas, int DuracaoTotalSemanas);

/// <summary>Sessão para a SPA. O identificador da Sessao nunca é exposto: ele vive apenas no cookie HttpOnly.</summary>
public sealed record SessaoDto(
    DateTimeOffset CriadaEm,
    IReadOnlyList<MensagemDto> Historico,
    PerfilDto Perfil,
    EntrevistaDto Entrevista,
    IReadOnlyList<RecomendacaoDto> Recomendacoes,
    IReadOnlyList<PlanoDto> Planos,
    IReadOnlyList<FeedbackDto> Feedbacks,
    bool BaixaConfianca);

public sealed record EntrevistaEncerrarDto(IReadOnlyList<CategoriaEntrevista> CategoriasSemResposta);

// Eventos SSE da resposta do assistente.
public sealed record EventoTrecho(string Texto)
{
    public string Tipo => "trecho";
}

public sealed record EventoFinal(MensagemDto Mensagem, EntrevistaDto Entrevista, PerfilDto Perfil)
{
    public string Tipo => "final";
}

public sealed record EventoErro(string Mensagem)
{
    public string Tipo => "erro";
}

/// <summary>Converte os modelos de Application/Domain para o contrato JSON esperado pela SPA.</summary>
public static class MapeadorContratos
{
    public static string Papel(PapelDominio papel) => papel == PapelDominio.Usuario ? "user" : "assistant";

    public static MensagemDto Mensagem(MensagemVm m) =>
        new(m.ClientMessageId, m.Ordem, Papel(m.Papel), m.Texto, m.CriadaEm);

    public static PerfilDto Perfil(PerfilUsuario p) => new(
        p.Interesses,
        p.ExperienciaPrevia,
        p.Formacao,
        p.EstiloDeTrabalho,
        p.HorasEstudoPorSemana,
        p.Objetivos,
        p.HabilidadesDominadas.Select(h => h.Valor).OrderBy(v => v, StringComparer.Ordinal).ToList());

    public static EntrevistaDto Entrevista(Entrevista e) => new(
        e.Estado,
        e.Categorias.ToDictionary(kv => Chave(kv.Key), kv => kv.Value),
        e.Reformulacoes.ToDictionary(kv => Chave(kv.Key), kv => kv.Value));

    public static PlanoDto Plano(PlanoDesenvolvimento p) => new(
        p.Id,
        p.Area.Valor,
        p.HorasPorSemana,
        p.Etapas.Select(e => new EtapaDto(
            e.Numero,
            e.Habilidades.Select(h => h.Valor).ToList(),
            e.DuracaoSemanas,
            e.Recursos.Select(r => new RecursoDto(r.Titulo, r.Url.ToString())).ToList())).ToList(),
        p.DuracaoTotalSemanas);

    public static FeedbackDto Feedback(FeedbackRecomendacao f) => new(f.Area.Valor, f.Tipo, f.Motivo, f.Em);

    public static SessaoDto Sessao(SessaoVm s) => new(
        s.CriadaEm,
        s.Historico.Select(Mensagem).ToList(),
        Perfil(s.Perfil),
        Entrevista(s.Entrevista),
        s.Recomendacoes.Select(r => new RecomendacaoDto(r.Area.Valor, null, r.Pontuacao, r.Justificativa)).ToList(),
        s.Planos.Select(Plano).ToList(),
        s.Feedbacks.Select(Feedback).ToList(),
        ConfiancaRecomendacao.BaixaConfianca(MaquinaEntrevista.CategoriasRespondidas(s.Entrevista)));

    private static string Chave(CategoriaEntrevista c) => JsonNamingPolicy.CamelCase.ConvertName(c.ToString());
}
