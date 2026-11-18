using CouchIA.Application.Mentor.Services;
using CouchIA.Domain;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace CouchIA.Application.Tests;

public class MaquinaEntrevistaConclusaoTests
{
    private static Gen<Dictionary<CategoriaEntrevista, EstadoCategoria>> GeradorCategorias()
    {
        var estado = Gen.Elements(EstadoCategoria.Pendente, EstadoCategoria.Respondida, EstadoCategoria.NaoRespondida);
        return Gen.ArrayOf(estado, MaquinaEntrevista.TodasCategorias.Count).Select(estados =>
            MaquinaEntrevista.TodasCategorias
                .Zip(estados, (c, s) => (c, s))
                .ToDictionary(x => x.c, x => x.s));
    }

    private static Entrevista Montar(Dictionary<CategoriaEntrevista, EstadoCategoria> categorias)
        => MaquinaEntrevista.Nova() with { Estado = EstadoEntrevista.EmAndamento, Categorias = categorias };

    // Feature: ia-mentor-carreira, Property 4: Conclusão e encerramento da entrevista
    // **Validates: Requirements 2.5, 2.6, 2.7**
    [Property(MaxTest = 100)]
    public Property Concluida_se_e_somente_se_nenhuma_categoria_pendente()
    {
        return Prop.ForAll(GeradorCategorias().ToArbitrary(), categorias =>
            MaquinaEntrevista.EstaConcluida(categorias) == categorias.Values.All(s => s != EstadoCategoria.Pendente));
    }

    // Feature: ia-mentor-carreira, Property 4: Conclusão e encerramento da entrevista
    // **Validates: Requirements 2.5, 2.6, 2.7**
    [Property(MaxTest = 100)]
    public Property Encerramento_aceito_se_e_somente_se_ha_categoria_respondida()
    {
        return Prop.ForAll(GeradorCategorias().ToArbitrary(), categorias =>
        {
            var entrevista = Montar(categorias);
            var resultado = MaquinaEntrevista.Encerrar(entrevista);
            var haRespondida = categorias.Values.Any(s => s == EstadoCategoria.Respondida);

            if (resultado.Aceito != haRespondida) return false;

            if (resultado.Aceito)
            {
                var esperadas = categorias.Where(kv => kv.Value != EstadoCategoria.Respondida)
                    .Select(kv => kv.Key).OrderBy(c => c);
                return resultado.Entrevista.Estado == EstadoEntrevista.Encerrada
                    && resultado.CategoriasSemResposta.OrderBy(c => c).SequenceEqual(esperadas);
            }

            return resultado.Entrevista.Estado == EstadoEntrevista.EmAndamento
                && resultado.Entrevista == entrevista;
        });
    }

    [Fact]
    public void Encerramento_sem_respondida_e_recusado_e_com_respondida_e_aceito()
    {
        var inicial = MaquinaEntrevista.Iniciar(MaquinaEntrevista.Nova());
        var recusado = MaquinaEntrevista.Encerrar(inicial);
        Assert.False(recusado.Aceito);
        Assert.Equal(EstadoEntrevista.EmAndamento, recusado.Entrevista.Estado);

        var categorias = new Dictionary<CategoriaEntrevista, EstadoCategoria>(inicial.Categorias)
        {
            [CategoriaEntrevista.Interesses] = EstadoCategoria.Respondida
        };
        var aceito = MaquinaEntrevista.Encerrar(inicial with { Categorias = categorias });
        Assert.True(aceito.Aceito);
        Assert.Equal(5, aceito.CategoriasSemResposta.Count);
    }
}
