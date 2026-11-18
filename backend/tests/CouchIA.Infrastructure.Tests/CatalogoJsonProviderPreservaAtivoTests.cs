using CouchIA.Application.Mentor.Services;
using CouchIA.Domain;
using CouchIA.Infrastructure.Catalogo;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging;

using CatalogoDominio = CouchIA.Domain.Catalogo;

namespace CouchIA.Infrastructure.Tests;

public class CatalogoJsonProviderPreservaAtivoTests
{
    private sealed class LoggerCapturador : ILogger<CatalogoJsonProvider>
    {
        public int Avisos;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Warning) Interlocked.Increment(ref Avisos);
        }
    }

    private static Habilidade Hab(string id, params string[] pre) =>
        new(new HabilidadeId(id), $"Habilidade {id}", "Descrição curta.", 10,
            new[] { new RecursoEstudo("Recurso", new Uri("https://exemplo.dev/" + id)) },
            pre.Select(p => new HabilidadeId(p)).ToList());

    private static CatalogoDominio Base()
    {
        var habs = new List<Habilidade> { Hab("a"), Hab("b", "a"), Hab("c", "b") };
        var area = new AreaAtuacao(new AreaId("area"), "Backend", "Área de backend.",
            habs.Select(h => new HabilidadeNaArea(h.Id, PrioridadeHabilidade.Essencial)).ToList());
        return new CatalogoDominio(new[] { area }, habs);
    }

    private static CatalogoDominio ComHabilidadeA(CatalogoDominio c, Func<Habilidade, Habilidade> f) =>
        c with { Habilidades = c.Habilidades.Select(h => h.Id.Valor == "a" ? f(h) : h).ToList() };

    // Catálogos candidatos que parseiam, mas violam alguma regra do ValidadorCatalogo.
    private static readonly Func<CatalogoDominio, int, CatalogoDominio>[] Mutacoes =
    {
        (c, n) => ComHabilidadeA(c, h => h with { HorasEstimadas = n % 2 == 0 ? 0 : 501 + n }),
        (c, n) => ComHabilidadeA(c, h => h with { Nome = new string('x', 101 + n) }),
        (c, n) => ComHabilidadeA(c, h => h with { PreRequisitos = new[] { new HabilidadeId("c") } }),
        (c, n) => ComHabilidadeA(c, h => h with { PreRequisitos = new[] { new HabilidadeId("nao-existe-" + n) } }),
        (c, n) => c with { Habilidades = c.Habilidades.Concat(new[] { c.Habilidades[0] }).ToList() },
        (c, n) => ComHabilidadeA(c, h => h with { Recursos = Array.Empty<RecursoEstudo>() }),
        (c, n) => c with { Areas = new[] { c.Areas[0] with { Habilidades = new[]
            { new HabilidadeNaArea(new HabilidadeId("fantasma-" + n), PrioridadeHabilidade.Importante) } } } },
        (c, n) => c with { Areas = new[] { c.Areas[0] with { Habilidades = Array.Empty<HabilidadeNaArea>() } } },
        (c, n) => c with { Areas = new[] { c.Areas[0] with { Nome = "   " } } },
        (c, n) => ComHabilidadeA(c, h => h with { PreRequisitos = new[] { h.Id } }),
    };

    private static Gen<string> JsonCandidatoInvalido()
    {
        var valido = CatalogoSerializer.Serializar(Base());

        var semantico = Gen.Choose(0, Mutacoes.Length - 1)
            .SelectMany(i => Gen.Choose(0, 50).Select(n => CatalogoSerializer.Serializar(Mutacoes[i](Base(), n))));

        var truncado = Gen.Choose(0, valido.TrimEnd().Length - 1)
            .Select(len => valido[..len]);

        var lixo = Gen.Elements("", "   ", "{", "[]", "{}", "null", "42", "\"texto\"", "{\"areas\":[]}",
            "{\"areas\":{},\"habilidades\":[]}", "{\"areas\":[1],\"habilidades\":[2]}", "not json", "{,}");

        // Conteúdo extra antes ou depois da raiz sempre invalida o documento.
        var corrompido = Gen.Elements("}", "]", ",", "@@", "{\"areas\":[]}")
            .SelectMany(lixoExtra => Gen.Elements(lixoExtra + valido, valido + lixoExtra));

        return Gen.OneOf(semantico, truncado, lixo, corrompido);
    }

    // Feature: ia-mentor-carreira, Property 27: Catálogo inválido preserva o ativo
    // **Validates: Requirements 6.5, 6.7**
    [Property(MaxTest = 100)]
    public Property Catalogo_invalido_ou_malformado_preserva_o_ativo_e_registra_a_falha()
    {
        return Prop.ForAll(JsonCandidatoInvalido().ToArbitrary(), json =>
        {
            var logger = new LoggerCapturador();
            var provider = new CatalogoJsonProvider(logger);

            var inicial = provider.Carregar(CatalogoSerializer.Serializar(Base()));
            if (inicial.Catalogo is null) return false;
            var ativoAntes = provider.Ativo;

            var resultado = provider.Carregar(json);

            return resultado.Catalogo is null
                && resultado.Erros.Count > 0
                && ReferenceEquals(provider.Ativo, ativoAntes)
                && logger.Avisos == resultado.Erros.Count;
        });
    }
}

