using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using CouchIA.Application.Mentor.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CouchIA.Infrastructure.Mentor;

/// <summary>
/// LLM falso e determinístico para demonstração local (sem rede e sem chave da Anthropic).
/// Segue os mesmos contratos do LLM real: no chat devolve o texto visível, depois <c>&lt;&lt;EXTRACAO&gt;&gt;</c>
/// e o JSON de extração; nas recomendações devolve <c>{"recomendacoes":[...]}</c>.
/// Habilitado com <c>Llm:Provedor = Mock</c>. Não use em produção.
/// </summary>
public sealed partial class MockLlmClient : ILlmClient
{
    public const string Marcador = "<<EXTRACAO>>";

    /// <summary>Atraso padrão entre trechos, para o streaming ficar visível na interface.</summary>
    public static readonly TimeSpan AtrasoPadrao = TimeSpan.FromMilliseconds(30);

    private static readonly JsonSerializerOptions Json = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private const int TamanhoMaximoTexto = 120;

    private readonly TimeSpan _atraso;

    public MockLlmClient(TimeSpan? atrasoPorTrecho = null) => _atraso = atrasoPorTrecho ?? AtrasoPadrao;

    public Task<string> CompletarAsync(RequisicaoLlm requisicao, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var (texto, extracao) = Responder(requisicao);
        return Task.FromResult(extracao is null ? texto : $"{texto}\n{Marcador}{extracao}");
    }

    public async IAsyncEnumerable<string> CompletarEmFluxoAsync(
        RequisicaoLlm requisicao, [EnumeratorCancellation] CancellationToken ct)
    {
        var (texto, extracao) = Responder(requisicao);

        if (EhPedidoDeRecomendacoes(requisicao))
        {
            // Saída estruturada: sem atraso, em trechos pequenos.
            foreach (var parte in Fatiar(texto, 40)) yield return parte;
            yield break;
        }

        foreach (var palavra in SepararPalavras(texto))
        {
            ct.ThrowIfCancellationRequested();
            if (_atraso > TimeSpan.Zero) await Task.Delay(_atraso, ct);
            yield return palavra;
        }

        if (extracao is null) yield break;

        // O marcador é emitido partido ao meio, como um LLM real poderia fazer.
        yield return "\n<<EXTRA";
        yield return "CAO>>";
        foreach (var parte in Fatiar(extracao, 24)) yield return parte;
    }

    // ---------------------------------------------------------------------------------------------
    // Roteamento

    private static bool EhPedidoDeRecomendacoes(RequisicaoLlm r) =>
        r.Mensagens.Any(m => m.Conteudo.Contains("\"recomendacoes\"", StringComparison.Ordinal)
                             && m.Conteudo.Contains("Catálogo de áreas:", StringComparison.Ordinal));

    private static (string Texto, string? ExtracaoJson) Responder(RequisicaoLlm r) =>
        EhPedidoDeRecomendacoes(r) ? (Recomendar(r), null) : ConduzirEntrevista(r);

    // ---------------------------------------------------------------------------------------------
    // Entrevista

    private static (string Texto, string? ExtracaoJson) ConduzirEntrevista(RequisicaoLlm r)
    {
        var contexto = r.Mensagens.FirstOrDefault()?.Conteudo ?? string.Empty;
        var textoUsuario = DesdelimitarUltimaMensagem(r);
        var turno = r.Mensagens.Count(m => m.Papel == PapelMensagem.Assistant);

        if (contexto.Contains("não está em andamento", StringComparison.Ordinal))
        {
            return ("A entrevista já terminou, e eu já tenho o que preciso do seu perfil. " +
                    "Veja as recomendações de área e o plano de estudos nas outras telas. " +
                    "Quer que eu explique como funcionam as recomendações?", null);
        }

        var emFoco = Capturar(EmFocoRegex(), contexto);
        var proxima = emFoco is null
            ? Capturar(PrimeiraRegex(), contexto)
            : Capturar(ProximaRegex(), contexto);
        var jaReformulada = contexto.Contains("já foi reformulada", StringComparison.Ordinal);

        // Primeiro turno: apresentação e primeira pergunta; não há resposta a extrair.
        if (emFoco is null)
        {
            var abertura = "Olá! Eu sou o CouchIA, seu mentor de carreira em tecnologia. " +
                           "Vou te fazer algumas perguntas, uma de cada vez, para entender o seu perfil. ";
            return (abertura + Pergunta(proxima ?? "interesses", reformulada: false), null);
        }

        var (utilizavel, extracao, comentario) = Extrair(emFoco, textoUsuario);

        if (!utilizavel && !jaReformulada)
        {
            return ("Não consegui aproveitar bem essa resposta, então vou perguntar de outro jeito. " +
                    Pergunta(emFoco, reformulada: true),
                    JsonSerializer.Serialize(new { utilizavel = false }, Json));
        }

        var reconhecimento = utilizavel ? Reconhecimento(turno, comentario) : "Tudo bem, vamos seguir adiante. ";
        var json = utilizavel ? extracao : JsonSerializer.Serialize(new { utilizavel = false }, Json);

        if (proxima is null)
        {
            return (reconhecimento +
                    "Isso conclui a entrevista. Obrigado por compartilhar tudo isso! " +
                    "Vou preparar as suas recomendações de áreas agora.", json);
        }

        return (reconhecimento + Pergunta(proxima, reformulada: false), json);
    }

    private static string Reconhecimento(int turno, string? comentario)
    {
        var abertura = (turno % 4) switch
        {
            0 => "Ótimo, obrigado por contar! ",
            1 => "Entendi, isso ajuda bastante. ",
            2 => "Legal, anotei aqui. ",
            _ => "Perfeito, faz sentido. "
        };
        return comentario is null ? abertura : abertura + comentario + " ";
    }

    private static string Pergunta(string categoria, bool reformulada) => (Normalizar(categoria), reformulada) switch
    {
        ("interesses", false) => "Para começar: quais assuntos de tecnologia mais despertam a sua curiosidade? Pode citar mais de um, como APIs, interfaces, dados ou inteligência artificial.",
        ("interesses", true) => "Pensando em tecnologia, o que você gosta de fazer ou de aprender? Por exemplo, criar sites, trabalhar com dados, automatizar tarefas?",
        ("experiencia previa", false) => "Você já tem alguma experiência prévia com programação ou tecnologia? Pode ser estudo, projetos pessoais ou trabalho.",
        ("experiencia previa", true) => "Conta um pouco do que você já fez na prática com tecnologia, mesmo que seja pouco ou só em cursos. O que foi?",
        ("formacao", false) => "Qual é a sua formação? Pode ser curso superior, técnico, bootcamp ou aprendizado por conta própria.",
        ("formacao", true) => "Qual foi o seu caminho de estudos até aqui? Escola, faculdade, cursos online, alguma coisa assim?",
        ("estilo de trabalho", false) => "Como você prefere trabalhar? Sozinho ou em equipe, com tarefas bem definidas ou com liberdade para explorar?",
        ("estilo de trabalho", true) => "Em um projeto ideal, como seria o seu dia? Você gostaria de colaborar muito com outras pessoas ou de ter tempo focado?",
        ("disponibilidade semanal de estudo", false) => "Quantas horas por semana você consegue reservar para estudar? Um número aproximado já ajuda.",
        ("disponibilidade semanal de estudo", true) => "Pensando na sua rotina, quantas horas por semana sobram para estudar? Pode responder só com um número, como 6 horas.",
        ("objetivos profissionais", false) => "Para finalizar: qual é o seu objetivo profissional? Onde você gostaria de estar daqui a um ou dois anos?",
        ("objetivos profissionais", true) => "O que você quer alcançar com a tecnologia na sua carreira? Uma vaga, uma transição, um projeto próprio?",
        (_, _) => "Quais assuntos de tecnologia mais despertam a sua curiosidade?"
    };

    private static (bool Utilizavel, string Json, string? Comentario) Extrair(string categoria, string texto)
    {
        var limpo = Regex.Replace(texto.Trim(), @"\s+", " ");
        var inutil = EhRespostaVazia(limpo);

        switch (Normalizar(categoria))
        {
            case "interesses":
            {
                var itens = inutil ? new List<string>() : SepararInteresses(limpo);
                if (itens.Count == 0) return (false, "{\"utilizavel\":false}", null);
                return (true,
                    JsonSerializer.Serialize(new { utilizavel = true, interesses = itens }, Json),
                    itens.Count > 1 ? "Você tem mais de um interesse, e isso abre várias possibilidades." : null);
            }
            case "disponibilidade semanal de estudo":
            {
                var horas = inutil ? null : ExtrairHoras(limpo);
                if (horas is null) return (false, "{\"utilizavel\":false}", null);
                return (true,
                    JsonSerializer.Serialize(new { utilizavel = true, horasEstudoPorSemana = horas.Value }, Json),
                    $"Anotei cerca de {horas.Value} horas por semana.");
            }
            default:
            {
                if (inutil) return (false, "{\"utilizavel\":false}", null);
                return (true, JsonSerializer.Serialize(new { utilizavel = true, texto = Encurtar(limpo) }, Json), null);
            }
        }
    }

    private static bool EhRespostaVazia(string texto)
    {
        var letras = texto.Count(char.IsLetterOrDigit);
        if (letras < 3) return true;
        return RespostaVaziaRegex().IsMatch(Normalizar(texto));
    }

    private static List<string> SepararInteresses(string texto)
    {
        var semPrefixo = PrefixoInteresseRegex().Replace(texto, string.Empty);
        return SeparadorRegex().Split(semPrefixo)
            .Select(p => PrefixoInteresseRegex().Replace(p.Trim().TrimEnd('.', '!', '?').Trim(), string.Empty))
            .Select(p => PreposicaoInicialRegex().Replace(p, string.Empty).Trim())
            .Where(p => p.Count(char.IsLetterOrDigit) >= 2)
            .Select(Encurtar)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();
    }

    /// <summary>Extrai horas por semana do texto livre. Retorna null (inutilizável) se não houver número de 1 a 168.</summary>
    internal static int? ExtrairHoras(string texto)
    {
        var norm = Normalizar(texto);
        int? valor = null;

        var m = HorasRegex().Match(norm);
        if (m.Success) valor = int.Parse(m.Groups["n"].Value, CultureInfo.InvariantCulture);
        else
        {
            var n = NumeroRegex().Match(norm);
            if (n.Success) valor = int.Parse(n.Groups["n"].Value, CultureInfo.InvariantCulture);
            else
            {
                foreach (var (palavra, v) in NumerosPorExtenso)
                {
                    if (Regex.IsMatch(norm, $@"\b{palavra}\b")) { valor = v; break; }
                }
            }
        }

        if (valor is null) return null;
        if (PorDiaRegex().IsMatch(norm)) valor *= 7;
        return MaquinaEntrevista.DisponibilidadeValida(valor.Value) ? valor : null;
    }

    private static readonly (string, int)[] NumerosPorExtenso =
    {
        ("vinte", 20), ("quinze", 15), ("dez", 10), ("nove", 9), ("oito", 8), ("sete", 7), ("seis", 6),
        ("cinco", 5), ("quatro", 4), ("tres", 3), ("duas", 2), ("dois", 2), ("uma", 1), ("um", 1)
    };

    // ---------------------------------------------------------------------------------------------
    // Recomendações

    private sealed record AreaCatalogo(string Id, string Nome, string Descricao);

    private static readonly Dictionary<string, string[]> PalavrasChave = new()
    {
        // Prefixo "=" exige palavra inteira; sem prefixo basta conter o trecho (texto já sem acentos).
        ["backend"] = new[] { "=api", "apis", "servidor", "backend", "back-end", "back end", "banco de dados", "=sql", "=c#", "csharp", ".net",
                              "=java", "=node", "microservic", "docker", "nuvem", "cloud", "logica", "integracao", "automatiz", "arquitetura" },
        ["frontend"] = new[] { "front", "interface", "=ux", "=ui", "design", "visual", "site", "=web", "react", "javascript",
                               "=css", "=html", "tela", "usuario", "criativ", "acessibilidade" },
        ["dados"] = new[] { "dado", "python", "estatistic", "machine", "inteligencia artificial", "=ia", "analise", "analis",
                            "planilha", "excel", "modelo", "predi", "pesquisa", "matematic", "ciencia", "grafico", "=bi" }
    };

    private static string Recomendar(RequisicaoLlm r)
    {
        var instrucoes = string.Join("\n", r.Mensagens.Select(m => m.Conteudo));
        var areas = AreaRegex().Matches(instrucoes)
            .Select(m => new AreaCatalogo(m.Groups["id"].Value.Trim(), m.Groups["nome"].Value.Trim(), m.Groups["desc"].Value.Trim()))
            .DistinctBy(a => a.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var perfilTexto = r.Mensagens.LastOrDefault(m => m.Conteudo.Contains("Perfil da pessoa:", StringComparison.Ordinal))?.Conteudo
                          ?? string.Empty;
        var itens = ItensDoPerfil(perfilTexto);

        var pontuadas = areas
            .Select((area, indice) =>
            {
                var palavras = PalavrasChave.TryGetValue(area.Id.ToLowerInvariant(), out var p)
                    ? p
                    : Normalizar(area.Nome + " " + area.Descricao).Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Where(w => w.Length > 5).Distinct().ToArray();

                var citados = itens.Where(i => Combina(i, palavras)).ToList();
                var acertos = citados.Count;
                // Itens citados valem pela quantidade; desempate pela ordem do catálogo, para variar as notas.
                var nota = Math.Clamp(42 + (acertos * 17) - (indice * 3), 12, 96);
                return (Area: area, Nota: nota, Citados: citados, Acertos: acertos);
            })
            .OrderByDescending(x => x.Nota)
            .Take(ValidadorRecomendacoes.QuantidadeMaxima)
            .ToList();

        var saida = pontuadas.Select(x => new
        {
            area = x.Area.Id,
            pontuacao = x.Nota,
            justificativa = Justificar(x.Area, x.Citados, itens)
        });

        return JsonSerializer.Serialize(new { recomendacoes = saida }, Json);
    }

    private static string Justificar(AreaCatalogo area, List<string> citados, List<string> itens)
    {
        static string Lista(IEnumerable<string> xs)
        {
            var l = xs.Select(x => $"\"{x}\"").ToList();
            return l.Count <= 1 ? string.Join("", l) : string.Join(", ", l.Take(l.Count - 1)) + " e " + l[^1];
        }

        var curtos = citados.Where(c => c.Length <= 140).Take(2).ToList();
        string texto;
        if (curtos.Count > 0)
        {
            texto = $"Você mencionou {Lista(curtos)}, o que conversa bem com {area.Nome}. " +
                    "Começar por essa área aproveita o que já chama a sua atenção.";
        }
        else
        {
            var base_ = itens.Where(i => i.Length <= 140).Take(1).ToList();
            texto = base_.Count > 0
                ? $"Considerando {Lista(base_)}, {area.Nome} pode ser uma opção a explorar, ainda que o seu perfil não aponte para ela de forma direta. Vale testar com um projeto pequeno."
                : $"{area.Nome} pode ser uma opção a explorar.";
        }

        return texto.Length <= ValidadorRecomendacoes.JustificativaMaxima ? texto : texto[..ValidadorRecomendacoes.JustificativaMaxima];
    }

    private static List<string> ItensDoPerfil(string perfilTexto)
    {
        var itens = new List<string>();
        foreach (var linha in perfilTexto.Split('\n'))
        {
            var l = linha.Trim();
            var i = l.IndexOf(':');
            if (i < 0) continue;
            var rotulo = Normalizar(l[..i]);
            var valor = l[(i + 1)..].Trim();
            if (valor.Length == 0) continue;

            switch (rotulo)
            {
                case "interesses":
                    itens.AddRange(valor.Split("; ", StringSplitOptions.RemoveEmptyEntries).Select(v => v.Trim()));
                    break;
                case "experiencia previa":
                case "formacao":
                case "estilo de trabalho":
                case "objetivos":
                    itens.Add(valor);
                    break;
            }
        }

        return itens.Where(i => i.Length > 0).ToList();
    }

    private static bool Combina(string item, string[] palavras)
    {
        var norm = " " + Normalizar(item) + " ";
        return palavras.Any(p => p.StartsWith('=')
            ? Regex.IsMatch(norm, $@"(?<![a-z0-9]){Regex.Escape(p[1..])}(?![a-z0-9])")
            : norm.Contains(p, StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------------------------------------
    // Utilidades

    private static string DesdelimitarUltimaMensagem(RequisicaoLlm r)
    {
        var ultima = r.Mensagens.LastOrDefault(m => m.Papel == PapelMensagem.User)?.Conteudo ?? string.Empty;
        var ini = ultima.IndexOf(PromptBuilder.DelimitadorInicio, StringComparison.Ordinal);
        var fim = ultima.LastIndexOf(PromptBuilder.DelimitadorFim, StringComparison.Ordinal);
        return ini >= 0 && fim > ini
            ? ultima[(ini + PromptBuilder.DelimitadorInicio.Length)..fim].Trim()
            : ultima.Trim();
    }

    private static string? Capturar(Regex regex, string texto)
    {
        var m = regex.Match(texto);
        return m.Success ? m.Groups["c"].Value.Trim() : null;
    }

    private static string Encurtar(string texto)
    {
        if (texto.Length <= TamanhoMaximoTexto) return texto;
        var corte = texto.LastIndexOf(' ', TamanhoMaximoTexto);
        return texto[..(corte > 40 ? corte : TamanhoMaximoTexto)].TrimEnd(',', ';', ' ');
    }

    internal static string Normalizar(string texto)
    {
        var d = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static IEnumerable<string> SepararPalavras(string texto)
    {
        var inicio = 0;
        for (var i = 0; i < texto.Length; i++)
        {
            if (texto[i] != ' ') continue;
            yield return texto[inicio..(i + 1)];
            inicio = i + 1;
        }
        if (inicio < texto.Length) yield return texto[inicio..];
    }

    private static IEnumerable<string> Fatiar(string texto, int tamanho)
    {
        for (var i = 0; i < texto.Length; i += tamanho)
            yield return texto.Substring(i, Math.Min(tamanho, texto.Length - i));
    }

    [GeneratedRegex(@"A última pergunta tratava da categoria: (?<c>[^.\n]+)\.")]
    private static partial Regex EmFocoRegex();

    [GeneratedRegex(@"Nenhuma pergunta foi feita ainda:.*?categoria: (?<c>[^.\n]+)\.")]
    private static partial Regex PrimeiraRegex();

    [GeneratedRegex(@"a próxima pergunta trata da categoria: (?<c>[^.\n]+)\.")]
    private static partial Regex ProximaRegex();

    [GeneratedRegex(@"^(nao sei|nada|nenhum[a]?|sei la|nao tenho|nao quero|talvez|hum+|\?+|-+|\.+)[\s.!?]*$")]
    private static partial Regex RespostaVaziaRegex();

    [GeneratedRegex(@"^(eu )?(gosto d[eoa]s?|me interesso por|tenho interesse (em|por)|curto|adoro|gosto muito d[eoa]s?)\s+", RegexOptions.IgnoreCase)]
    private static partial Regex PrefixoInteresseRegex();

    [GeneratedRegex(@"^(?:d[eoa]s?|em|por)\s+", RegexOptions.IgnoreCase)]
    private static partial Regex PreposicaoInicialRegex();

    [GeneratedRegex(@"\s*(?:,|;|/|\be\b|\bou\b)\s*", RegexOptions.IgnoreCase)]
    private static partial Regex SeparadorRegex();

    [GeneratedRegex(@"(?<n>\d{1,3})\s*(?:h\b|hr|hrs|hora)")]
    private static partial Regex HorasRegex();

    [GeneratedRegex(@"(?<n>\d{1,3})")]
    private static partial Regex NumeroRegex();

    [GeneratedRegex(@"\b(por dia|ao dia|diari)")]
    private static partial Regex PorDiaRegex();

    [GeneratedRegex(@"^- id: (?<id>[^;\n]+); nome: (?<nome>[^;\n]*); descrição: (?<desc>.*)$", RegexOptions.Multiline)]
    private static partial Regex AreaRegex();
}

public static class MockLlmClientExtensions
{
    public const string ChaveProvedor = "Llm:Provedor";

    public static bool UsaMock(this IConfiguration configuration) =>
        string.Equals(configuration[ChaveProvedor], "Mock", StringComparison.OrdinalIgnoreCase);

    public static IServiceCollection AddMockLlmClient(this IServiceCollection services, IConfiguration configuration)
    {
        TimeSpan? atraso = int.TryParse(configuration["Llm:Mock:AtrasoMs"], NumberStyles.None, CultureInfo.InvariantCulture, out var ms)
            ? TimeSpan.FromMilliseconds(ms)
            : null;
        services.AddSingleton<ILlmClient>(new MockLlmClient(atraso));
        return services;
    }
}
