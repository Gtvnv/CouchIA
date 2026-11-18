using CouchIA.Application.Mentor.Services;
using CouchIA.Domain;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace CouchIA.Application.Tests;

public class MaquinaEntrevistaReformulacaoTests
{
    private static PerfilUsuario PerfilVazio() =>
        new(new List<string>(), null, null, null, null, null, new HashSet<HabilidadeId>());

    // Extrações inutilizáveis: flag falso ou conteúdo vazio/inválido mesmo com flag verdadeiro.
    private static Gen<ExtracaoEntrevista> GeradorInutilizavel(CategoriaEntrevista c) =>
        Gen.Elements(
            new ExtracaoEntrevista(c, false),
            new ExtracaoEntrevista(c, true),
            new ExtracaoEntrevista(c, true, Interesses: new List<string>()),
            new ExtracaoEntrevista(c, true, Interesses: new List<string> { "  " }),
            new ExtracaoEntrevista(c, true, Texto: "   "),
            new ExtracaoEntrevista(c, true, HorasEstudoPorSemana: 0),
            new ExtracaoEntrevista(c, true, HorasEstudoPorSemana: 169));

    // Sequências de 1 a 30 respostas inutilizáveis, intercaladas entre categorias.
    private static Gen<ExtracaoEntrevista[]> GeradorSequencia() =>
        Gen.Choose(1, 30).SelectMany(n =>
            Gen.ArrayOf(
                Gen.Elements(MaquinaEntrevista.TodasCategorias.ToArray())
                    .SelectMany(GeradorInutilizavel), n));

    // Feature: ia-mentor-carreira, Property 5: Reformulação ocorre no máximo uma vez por categoria
    // **Validates: Requirements 2.2, 2.4**
    [Property(MaxTest = 100)]
    public Property No_maximo_uma_reformulacao_por_categoria_e_segunda_marca_NaoRespondida()
    {
        return Prop.ForAll(GeradorSequencia().ToArbitrary(), sequencia =>
        {
            var entrevista = MaquinaEntrevista.Iniciar(MaquinaEntrevista.Nova());
            var perfil = PerfilVazio();
            var vistas = MaquinaEntrevista.TodasCategorias.ToDictionary(c => c, _ => 0);

            foreach (var extracao in sequencia)
            {
                var c = extracao.Categoria;
                var antes = vistas[c];
                var r = MaquinaEntrevista.AplicarExtracao(entrevista, perfil, extracao);
                // Só conta respostas aplicadas a entrevista em andamento e categoria pendente.
                var esperada = entrevista.Estado != EstadoEntrevista.EmAndamento || antes >= 2
                    ? AcaoEntrevista.Ignorada
                    : antes == 0 ? AcaoEntrevista.Reformular : AcaoEntrevista.CategoriaNaoRespondida;
                if (esperada != AcaoEntrevista.Ignorada) vistas[c] = antes + 1;

                entrevista = r.Entrevista;

                var contadorOk = MaquinaEntrevista.TodasCategorias.All(k =>
                    !entrevista.Reformulacoes.TryGetValue(k, out var n) || n <= 1);
                var estadoEsperado = antes >= 2 || esperada == AcaoEntrevista.CategoriaNaoRespondida
                    ? EstadoCategoria.NaoRespondida
                    : EstadoCategoria.Pendente;

                if (r.Acao != esperada || !contadorOk || entrevista.Categorias[c] != estadoEsperado)
                    return false;
            }

            return true;
        });
    }

    [Fact]
    public void Primeira_inutilizavel_reformula_segunda_marca_NaoRespondida_e_terceira_e_ignorada()
    {
        var c = CategoriaEntrevista.Formacao;
        var e = MaquinaEntrevista.Iniciar(MaquinaEntrevista.Nova());
        var ruim = new ExtracaoEntrevista(c, false);

        var r1 = MaquinaEntrevista.AplicarExtracao(e, PerfilVazio(), ruim);
        Assert.Equal(AcaoEntrevista.Reformular, r1.Acao);
        Assert.Equal(EstadoCategoria.Pendente, r1.Entrevista.Categorias[c]);
        Assert.Equal(1, r1.Entrevista.Reformulacoes[c]);

        var r2 = MaquinaEntrevista.AplicarExtracao(r1.Entrevista, r1.Perfil, ruim);
        Assert.Equal(AcaoEntrevista.CategoriaNaoRespondida, r2.Acao);
        Assert.Equal(EstadoCategoria.NaoRespondida, r2.Entrevista.Categorias[c]);
        Assert.Equal(1, r2.Entrevista.Reformulacoes[c]);

        var r3 = MaquinaEntrevista.AplicarExtracao(r2.Entrevista, r2.Perfil, ruim);
        Assert.Equal(AcaoEntrevista.Ignorada, r3.Acao);
        Assert.Equal(1, r3.Entrevista.Reformulacoes[c]);
    }
}
