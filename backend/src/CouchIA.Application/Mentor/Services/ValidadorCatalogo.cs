using CouchIA.Domain;

namespace CouchIA.Application.Mentor.Services;

public enum TipoErroCatalogo
{
    NomeAreaInvalido,
    NomeAreaDuplicado,
    DescricaoAreaInvalida,
    QuantidadeHabilidadesAreaInvalida,
    HabilidadeDaAreaInexistente,
    IdentificadorHabilidadeInvalido,
    IdentificadorHabilidadeDuplicado,
    NomeHabilidadeInvalido,
    DescricaoHabilidadeInvalida,
    HorasEstimadasInvalidas,
    QuantidadeRecursosInvalida,
    QuantidadePreRequisitosInvalida,
    PreRequisitoDuplicado,
    PreRequisitoAutoReferencia,
    PreRequisitoInexistente,
    CicloDeDependencia
}

/// <summary>
/// Erro de validação do catálogo. Identifica a Habilidade afetada (quando aplicável),
/// a área afetada (quando aplicável) e o tipo da falha.
/// </summary>
public sealed record ErroCatalogo(
    TipoErroCatalogo Tipo,
    string? HabilidadeId,
    string? AreaId,
    string Detalhe);

public sealed record ResultadoValidacaoCatalogo(IReadOnlyList<ErroCatalogo> Erros)
{
    public bool EhValido => Erros.Count == 0;
}

/// <summary>
/// Valida um catálogo candidato: limites, unicidade de nomes e identificadores,
/// pré-requisitos existentes, sem auto-referência e sem ciclos. Função pura, sem I/O.
/// </summary>
public static class ValidadorCatalogo
{
    public const int NomeAreaMaximo = 100;
    public const int DescricaoAreaMaximo = 1000;
    public const int HabilidadesPorAreaMinimo = 1;
    public const int HabilidadesPorAreaMaximo = 100;
    public const int NomeHabilidadeMaximo = 100;
    public const int DescricaoHabilidadeMaximo = 300;
    public const int FrasesMinimo = 1;
    public const int FrasesMaximo = 3;
    public const int HorasMinimo = 1;
    public const int HorasMaximo = 500;
    public const int RecursosMinimo = 1;
    public const int RecursosMaximo = 3;
    public const int PreRequisitosMaximo = 10;

    public static ResultadoValidacaoCatalogo Validar(Catalogo? catalogo)
    {
        var erros = new List<ErroCatalogo>();
        if (catalogo is null)
            return new ResultadoValidacaoCatalogo(erros);

        var areas = catalogo.Areas ?? Array.Empty<AreaAtuacao>();
        var habilidades = catalogo.Habilidades ?? Array.Empty<Habilidade>();

        var idsHabilidade = new HashSet<string>(StringComparer.Ordinal);
        var idsDuplicados = new HashSet<string>(StringComparer.Ordinal);
        foreach (var h in habilidades)
        {
            var id = h.Id.Valor;
            if (string.IsNullOrWhiteSpace(id))
            {
                erros.Add(new(TipoErroCatalogo.IdentificadorHabilidadeInvalido, id, null,
                    "O identificador da habilidade é obrigatório."));
                continue;
            }
            if (!idsHabilidade.Add(id) && idsDuplicados.Add(id))
                erros.Add(new(TipoErroCatalogo.IdentificadorHabilidadeDuplicado, id, null,
                    "O identificador da habilidade aparece mais de uma vez."));
        }

        ValidarAreas(areas, idsHabilidade, erros);

        foreach (var h in habilidades)
            ValidarHabilidade(h, idsHabilidade, erros);

        DetectarCiclos(habilidades, idsHabilidade, erros);

        return new ResultadoValidacaoCatalogo(erros);
    }

    private static void ValidarAreas(
        IReadOnlyList<AreaAtuacao> areas, HashSet<string> idsHabilidade, List<ErroCatalogo> erros)
    {
        var nomes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var nomesDuplicados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var area in areas)
        {
            var areaId = area.Id.Valor;
            var nome = area.Nome?.Trim() ?? string.Empty;

            if (nome.Length < 1 || nome.Length > NomeAreaMaximo)
                erros.Add(new(TipoErroCatalogo.NomeAreaInvalido, null, areaId,
                    $"O nome da área deve ter de 1 a {NomeAreaMaximo} caracteres."));
            else if (!nomes.Add(nome) && nomesDuplicados.Add(nome))
                erros.Add(new(TipoErroCatalogo.NomeAreaDuplicado, null, areaId,
                    $"O nome da área '{nome}' aparece mais de uma vez."));

            var descricao = area.Descricao?.Trim() ?? string.Empty;
            if (descricao.Length < 1 || descricao.Length > DescricaoAreaMaximo)
                erros.Add(new(TipoErroCatalogo.DescricaoAreaInvalida, null, areaId,
                    $"A descrição da área deve ter de 1 a {DescricaoAreaMaximo} caracteres."));

            var itens = area.Habilidades ?? Array.Empty<HabilidadeNaArea>();
            if (itens.Count < HabilidadesPorAreaMinimo || itens.Count > HabilidadesPorAreaMaximo)
                erros.Add(new(TipoErroCatalogo.QuantidadeHabilidadesAreaInvalida, null, areaId,
                    $"A área deve ter de {HabilidadesPorAreaMinimo} a {HabilidadesPorAreaMaximo} habilidades."));

            foreach (var item in itens)
            {
                var hid = item.Habilidade.Valor;
                if (string.IsNullOrWhiteSpace(hid) || !idsHabilidade.Contains(hid))
                    erros.Add(new(TipoErroCatalogo.HabilidadeDaAreaInexistente, hid, areaId,
                        "A habilidade referenciada pela área não existe no catálogo."));
            }
        }
    }

    private static void ValidarHabilidade(
        Habilidade h, HashSet<string> idsHabilidade, List<ErroCatalogo> erros)
    {
        var id = h.Id.Valor;
        if (string.IsNullOrWhiteSpace(id))
            return; // já reportada como identificador inválido

        var nome = h.Nome?.Trim() ?? string.Empty;
        if (nome.Length < 1 || nome.Length > NomeHabilidadeMaximo)
            erros.Add(new(TipoErroCatalogo.NomeHabilidadeInvalido, id, null,
                $"O nome da habilidade deve ter de 1 a {NomeHabilidadeMaximo} caracteres."));

        var descricao = h.Descricao?.Trim() ?? string.Empty;
        if (descricao.Length < 1 || descricao.Length > DescricaoHabilidadeMaximo
            || ContarFrases(descricao) is < FrasesMinimo or > FrasesMaximo)
            erros.Add(new(TipoErroCatalogo.DescricaoHabilidadeInvalida, id, null,
                $"A descrição da habilidade deve ter de 1 a {DescricaoHabilidadeMaximo} caracteres e de {FrasesMinimo} a {FrasesMaximo} frases."));

        if (h.HorasEstimadas < HorasMinimo || h.HorasEstimadas > HorasMaximo)
            erros.Add(new(TipoErroCatalogo.HorasEstimadasInvalidas, id, null,
                $"As horas estimadas devem estar entre {HorasMinimo} e {HorasMaximo}."));

        var recursos = h.Recursos?.Count ?? 0;
        if (recursos < RecursosMinimo || recursos > RecursosMaximo)
            erros.Add(new(TipoErroCatalogo.QuantidadeRecursosInvalida, id, null,
                $"A habilidade deve ter de {RecursosMinimo} a {RecursosMaximo} recursos de estudo."));

        var pre = h.PreRequisitos ?? Array.Empty<HabilidadeId>();
        if (pre.Count > PreRequisitosMaximo)
            erros.Add(new(TipoErroCatalogo.QuantidadePreRequisitosInvalida, id, null,
                $"A habilidade pode ter no máximo {PreRequisitosMaximo} pré-requisitos."));

        var vistos = new HashSet<string>(StringComparer.Ordinal);
        var repetidos = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in pre)
        {
            var pid = p.Valor;
            if (string.IsNullOrWhiteSpace(pid) || !idsHabilidade.Contains(pid))
            {
                erros.Add(new(TipoErroCatalogo.PreRequisitoInexistente, id, null,
                    $"O pré-requisito '{pid}' não existe no catálogo."));
                continue;
            }
            if (pid == id)
            {
                erros.Add(new(TipoErroCatalogo.PreRequisitoAutoReferencia, id, null,
                    "A habilidade não pode ser pré-requisito de si mesma."));
                continue;
            }
            if (!vistos.Add(pid) && repetidos.Add(pid))
                erros.Add(new(TipoErroCatalogo.PreRequisitoDuplicado, id, null,
                    $"O pré-requisito '{pid}' aparece mais de uma vez."));
        }
    }

    // Conta frases terminadas por '.', '!' ou '?' (sequências contam como um terminador).
    // Texto sem terminador final conta como uma frase.
    private static int ContarFrases(string texto)
    {
        var frases = 0;
        var temConteudo = false;
        for (var i = 0; i < texto.Length; i++)
        {
            var c = texto[i];
            if (c is '.' or '!' or '?')
            {
                var fim = i + 1 >= texto.Length || char.IsWhiteSpace(texto[i + 1]);
                var proximoTambemTerminador = i + 1 < texto.Length && texto[i + 1] is '.' or '!' or '?';
                if (fim && temConteudo && !proximoTambemTerminador)
                {
                    frases++;
                    temConteudo = false;
                }
            }
            else if (!char.IsWhiteSpace(c))
            {
                temConteudo = true;
            }
        }
        if (temConteudo) frases++;
        return frases;
    }

    // DFS iterativa. Reporta a habilidade cujo pré-requisito fecha o ciclo (aresta de retorno).
    private static void DetectarCiclos(
        IReadOnlyList<Habilidade> habilidades, HashSet<string> ids, List<ErroCatalogo> erros)
    {
        var arestas = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var h in habilidades)
        {
            var id = h.Id.Valor;
            if (string.IsNullOrWhiteSpace(id) || arestas.ContainsKey(id)) continue;
            arestas[id] = (h.PreRequisitos ?? Array.Empty<HabilidadeId>())
                .Select(p => p.Valor)
                .Where(p => !string.IsNullOrWhiteSpace(p) && p != id && ids.Contains(p))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        const int emAndamento = 1, concluido = 2;
        var estado = new Dictionary<string, int>(StringComparer.Ordinal);
        var reportadas = new HashSet<string>(StringComparer.Ordinal);

        foreach (var raiz in arestas.Keys)
        {
            if (estado.ContainsKey(raiz)) continue;

            var pilha = new Stack<(string No, int Indice)>();
            pilha.Push((raiz, 0));
            estado[raiz] = emAndamento;

            while (pilha.Count > 0)
            {
                var (no, indice) = pilha.Pop();
                var vizinhos = arestas[no];
                if (indice < vizinhos.Count)
                {
                    pilha.Push((no, indice + 1));
                    var v = vizinhos[indice];
                    if (!estado.TryGetValue(v, out var s))
                    {
                        estado[v] = emAndamento;
                        pilha.Push((v, 0));
                    }
                    else if (s == emAndamento && reportadas.Add(no))
                    {
                        erros.Add(new(TipoErroCatalogo.CicloDeDependencia, no, null,
                            $"A habilidade participa de um ciclo de dependência (pré-requisito '{v}')."));
                    }
                }
                else
                {
                    estado[no] = concluido;
                }
            }
        }
    }
}
