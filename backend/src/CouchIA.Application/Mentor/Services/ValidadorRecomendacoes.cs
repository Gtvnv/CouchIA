using CouchIA.Domain;

namespace CouchIA.Application.Mentor.Services;

/// <summary>Item de recomendação como veio do LLM (entrada não confiável).</summary>
public sealed record CandidatoRecomendacao(string? Area, double? Pontuacao, string? Justificativa);

public enum FalhaRecomendacao
{
    QuantidadeInvalida,
    AreaAusente,
    AreaForaDoCatalogo,
    AreaRepetida,
    PontuacaoInvalida,
    JustificativaInvalida,
    JustificativaSemItemDoPerfil
}

public sealed record ErroRecomendacao(FalhaRecomendacao Falha, int? Indice);

public sealed record ResultadoValidacaoRecomendacoes(
    IReadOnlyList<RecomendacaoArea> Recomendacoes,
    IReadOnlyList<ErroRecomendacao> Erros)
{
    public bool Valido => Erros.Count == 0;
}

/// <summary>
/// Normaliza e valida a saída do LLM: 3 a 5 itens, sem área repetida, apenas áreas do catálogo,
/// pontuação inteira 0..100, justificativa de 1 a 500 caracteres citando item do perfil,
/// em ordem decrescente de pontuação. A lista é rejeitada por inteiro se qualquer regra falhar.
/// </summary>
public static class ValidadorRecomendacoes
{
    public const int QuantidadeMinima = 3;
    public const int QuantidadeMaxima = 5;
    public const int PontuacaoMinima = 0;
    public const int PontuacaoMaxima = 100;
    public const int JustificativaMaxima = 500;

    public static ResultadoValidacaoRecomendacoes Validar(
        IReadOnlyList<CandidatoRecomendacao>? candidatos,
        Catalogo catalogo,
        PerfilUsuario perfil)
    {
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(perfil);

        var erros = new List<ErroRecomendacao>();
        var itens = candidatos ?? Array.Empty<CandidatoRecomendacao>();

        if (itens.Count < QuantidadeMinima || itens.Count > QuantidadeMaxima)
            erros.Add(new ErroRecomendacao(FalhaRecomendacao.QuantidadeInvalida, null));

        var areasCatalogo = catalogo.Areas.ToDictionary(a => a.Id.Valor, a => a.Id, StringComparer.OrdinalIgnoreCase);
        var itensPerfil = ItensDoPerfil(perfil);
        var vistas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var validas = new List<RecomendacaoArea>();

        for (var i = 0; i < itens.Count; i++)
        {
            var c = itens[i];
            var errosAntes = erros.Count;

            AreaId? area = null;
            var idBruto = c?.Area?.Trim();
            if (string.IsNullOrEmpty(idBruto))
                erros.Add(new ErroRecomendacao(FalhaRecomendacao.AreaAusente, i));
            else if (!areasCatalogo.TryGetValue(idBruto, out var encontrada))
                erros.Add(new ErroRecomendacao(FalhaRecomendacao.AreaForaDoCatalogo, i));
            else if (!vistas.Add(encontrada.Valor))
                erros.Add(new ErroRecomendacao(FalhaRecomendacao.AreaRepetida, i));
            else
                area = encontrada;

            var pontuacao = 0;
            var p = c?.Pontuacao;
            if (p is null || double.IsNaN(p.Value) || p.Value != Math.Floor(p.Value)
                || p.Value < PontuacaoMinima || p.Value > PontuacaoMaxima)
                erros.Add(new ErroRecomendacao(FalhaRecomendacao.PontuacaoInvalida, i));
            else
                pontuacao = (int)p.Value;

            var justificativa = c?.Justificativa?.Trim() ?? string.Empty;
            if (justificativa.Length < 1 || justificativa.Length > JustificativaMaxima)
                erros.Add(new ErroRecomendacao(FalhaRecomendacao.JustificativaInvalida, i));
            else if (!CitaItemDoPerfil(justificativa, itensPerfil))
                erros.Add(new ErroRecomendacao(FalhaRecomendacao.JustificativaSemItemDoPerfil, i));

            if (erros.Count == errosAntes && area is not null)
                validas.Add(new RecomendacaoArea(area.Value, pontuacao, justificativa));
        }

        if (erros.Count > 0)
            return new ResultadoValidacaoRecomendacoes(Array.Empty<RecomendacaoArea>(), erros);

        // OrderByDescending é estável: empates mantêm a ordem recebida.
        var ordenadas = validas.OrderByDescending(r => r.Pontuacao).ToList();
        return new ResultadoValidacaoRecomendacoes(ordenadas, Array.Empty<ErroRecomendacao>());
    }

    private static List<string> ItensDoPerfil(PerfilUsuario perfil)
    {
        var itens = new List<string>();
        itens.AddRange(perfil.Interesses ?? Array.Empty<string>());
        itens.Add(perfil.ExperienciaPrevia ?? string.Empty);
        itens.Add(perfil.Formacao ?? string.Empty);
        itens.Add(perfil.EstiloDeTrabalho ?? string.Empty);
        itens.Add(perfil.Objetivos ?? string.Empty);
        return itens.Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
    }

    private static bool CitaItemDoPerfil(string justificativa, List<string> itensPerfil)
        => itensPerfil.Any(item => justificativa.Contains(item, StringComparison.OrdinalIgnoreCase));
}
