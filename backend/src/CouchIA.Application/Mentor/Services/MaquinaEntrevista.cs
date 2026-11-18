using CouchIA.Domain;

namespace CouchIA.Application.Mentor.Services;

/// <summary>
/// Extração estruturada de uma resposta do Usuário para uma categoria da entrevista.
/// Somente o campo correspondente à categoria é considerado.
/// </summary>
public sealed record ExtracaoEntrevista(
    CategoriaEntrevista Categoria,
    bool Utilizavel,
    IReadOnlyList<string>? Interesses = null,
    string? Texto = null,
    int? HorasEstudoPorSemana = null);

public enum AcaoEntrevista
{
    /// <summary>Resposta utilizável aplicada; categoria ficou Respondida.</summary>
    RespostaAplicada,
    /// <summary>Primeira resposta inutilizável: a pergunta deve ser reformulada.</summary>
    Reformular,
    /// <summary>Segunda resposta inutilizável: categoria NaoRespondida, segue para a próxima.</summary>
    CategoriaNaoRespondida,
    /// <summary>Entrevista não está em andamento ou categoria não está pendente; nada mudou.</summary>
    Ignorada
}

public sealed record ResultadoResposta(
    Entrevista Entrevista,
    PerfilUsuario Perfil,
    AcaoEntrevista Acao,
    bool Concluida);

public sealed record ResultadoEncerramento(
    bool Aceito,
    Entrevista Entrevista,
    IReadOnlyList<CategoriaEntrevista> CategoriasSemResposta);

/// <summary>
/// Regras determinísticas da entrevista: transições por categoria, uma reformulação por
/// categoria, conclusão e encerramento antecipado. Não tem estado nem efeitos colaterais.
/// </summary>
public static class MaquinaEntrevista
{
    public const int MinimoHoras = 1;
    public const int MaximoHoras = 168;
    public const int MaximoReformulacoes = 1;

    public static IReadOnlyList<CategoriaEntrevista> TodasCategorias { get; } =
        Enum.GetValues<CategoriaEntrevista>();

    public static bool DisponibilidadeValida(int horas)
        => horas >= MinimoHoras && horas <= MaximoHoras;

    /// <summary>Estado inicial: NaoIniciada, todas as categorias Pendente, sem reformulações.</summary>
    public static Entrevista Nova() => new(
        EstadoEntrevista.NaoIniciada,
        TodasCategorias.ToDictionary(c => c, _ => EstadoCategoria.Pendente),
        TodasCategorias.ToDictionary(c => c, _ => 0));

    /// <summary>NaoIniciada -> EmAndamento; nos demais estados devolve a entrevista inalterada.</summary>
    public static Entrevista Iniciar(Entrevista entrevista)
        => entrevista.Estado == EstadoEntrevista.NaoIniciada
            ? entrevista with { Estado = EstadoEntrevista.EmAndamento }
            : entrevista;

    /// <summary>Concluída se e somente se nenhuma categoria está Pendente.</summary>
    public static bool EstaConcluida(IReadOnlyDictionary<CategoriaEntrevista, EstadoCategoria> categorias)
        => TodasCategorias.All(c => EstadoDe(categorias, c) != EstadoCategoria.Pendente);

    public static int CategoriasRespondidas(Entrevista entrevista)
        => TodasCategorias.Count(c => EstadoDe(entrevista.Categorias, c) == EstadoCategoria.Respondida);

    /// <summary>
    /// Aplica a extração de uma resposta. Utilizável: marca Respondida e atualiza o perfil.
    /// Inutilizável: reformula uma vez; na segunda vez marca NaoRespondida e avança.
    /// </summary>
    public static ResultadoResposta AplicarExtracao(Entrevista entrevista, PerfilUsuario perfil, ExtracaoEntrevista extracao)
    {
        var categoria = extracao.Categoria;
        if (entrevista.Estado != EstadoEntrevista.EmAndamento
            || EstadoDe(entrevista.Categorias, categoria) != EstadoCategoria.Pendente)
        {
            return new ResultadoResposta(entrevista, perfil, AcaoEntrevista.Ignorada, entrevista.Estado == EstadoEntrevista.Concluida);
        }

        if (TentarAplicarPerfil(perfil, extracao, out var novoPerfil))
        {
            return Finalizar(entrevista, novoPerfil, categoria, EstadoCategoria.Respondida, entrevista.Reformulacoes, AcaoEntrevista.RespostaAplicada);
        }

        var reformulacoes = entrevista.Reformulacoes.TryGetValue(categoria, out var r) ? r : 0;
        if (reformulacoes < MaximoReformulacoes)
        {
            var novas = new Dictionary<CategoriaEntrevista, int>(entrevista.Reformulacoes) { [categoria] = reformulacoes + 1 };
            return new ResultadoResposta(entrevista with { Reformulacoes = novas }, perfil, AcaoEntrevista.Reformular, false);
        }

        return Finalizar(entrevista, perfil, categoria, EstadoCategoria.NaoRespondida, entrevista.Reformulacoes, AcaoEntrevista.CategoriaNaoRespondida);
    }

    /// <summary>
    /// Encerramento antecipado: aceito se e somente se há ao menos uma categoria Respondida.
    /// Recusado mantém a entrevista como está (EmAndamento).
    /// </summary>
    public static ResultadoEncerramento Encerrar(Entrevista entrevista)
    {
        if (CategoriasRespondidas(entrevista) == 0)
        {
            return new ResultadoEncerramento(false, entrevista, Array.Empty<CategoriaEntrevista>());
        }

        var semResposta = TodasCategorias
            .Where(c => EstadoDe(entrevista.Categorias, c) != EstadoCategoria.Respondida)
            .ToList();
        return new ResultadoEncerramento(true, entrevista with { Estado = EstadoEntrevista.Encerrada }, semResposta);
    }

    private static ResultadoResposta Finalizar(
        Entrevista entrevista,
        PerfilUsuario perfil,
        CategoriaEntrevista categoria,
        EstadoCategoria estado,
        IReadOnlyDictionary<CategoriaEntrevista, int> reformulacoes,
        AcaoEntrevista acao)
    {
        var categorias = new Dictionary<CategoriaEntrevista, EstadoCategoria>(entrevista.Categorias) { [categoria] = estado };
        var concluida = EstaConcluida(categorias);
        var nova = entrevista with
        {
            Estado = concluida ? EstadoEntrevista.Concluida : EstadoEntrevista.EmAndamento,
            Categorias = categorias,
            Reformulacoes = reformulacoes
        };
        return new ResultadoResposta(nova, perfil, acao, concluida);
    }

    private static bool TentarAplicarPerfil(PerfilUsuario perfil, ExtracaoEntrevista e, out PerfilUsuario novo)
    {
        novo = perfil;
        if (!e.Utilizavel)
        {
            return false;
        }

        switch (e.Categoria)
        {
            case CategoriaEntrevista.Interesses:
                var interesses = e.Interesses?.Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i.Trim()).ToList();
                if (interesses is not { Count: > 0 }) return false;
                novo = perfil with { Interesses = interesses };
                return true;
            case CategoriaEntrevista.DisponibilidadeSemanal:
                if (e.HorasEstudoPorSemana is not int horas || !DisponibilidadeValida(horas)) return false;
                novo = perfil with { HorasEstudoPorSemana = horas };
                return true;
            case CategoriaEntrevista.ExperienciaPrevia:
                if (string.IsNullOrWhiteSpace(e.Texto)) return false;
                novo = perfil with { ExperienciaPrevia = e.Texto.Trim() };
                return true;
            case CategoriaEntrevista.Formacao:
                if (string.IsNullOrWhiteSpace(e.Texto)) return false;
                novo = perfil with { Formacao = e.Texto.Trim() };
                return true;
            case CategoriaEntrevista.EstiloDeTrabalho:
                if (string.IsNullOrWhiteSpace(e.Texto)) return false;
                novo = perfil with { EstiloDeTrabalho = e.Texto.Trim() };
                return true;
            case CategoriaEntrevista.Objetivos:
                if (string.IsNullOrWhiteSpace(e.Texto)) return false;
                novo = perfil with { Objetivos = e.Texto.Trim() };
                return true;
            default:
                return false;
        }
    }

    private static EstadoCategoria EstadoDe(IReadOnlyDictionary<CategoriaEntrevista, EstadoCategoria> categorias, CategoriaEntrevista c)
        => categorias.TryGetValue(c, out var s) ? s : EstadoCategoria.Pendente;
}
