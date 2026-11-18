using CouchIA.Application.Mentor.Services;

namespace CouchIA.Application.Tests;

public class CatalogoInicialTests
{
    private static string Caminho()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidato = Path.Combine(dir.FullName, "src", "CouchIA.WebUI", "Catalogo", "catalogo.json");
            if (File.Exists(candidato)) return candidato;
            dir = dir.Parent;
        }
        throw new FileNotFoundException("catalogo.json não encontrado.");
    }

    [Fact]
    public void CatalogoInicial_E_Desserializado_E_Aceito_Pelo_Validador()
    {
        var lido = CatalogoSerializer.Desserializar(File.ReadAllText(Caminho()));

        Assert.True(lido.Sucesso, string.Join("; ", lido.Erros.Select(e => $"{e.Campo}: {e.Detalhe}")));
        Assert.True(lido.Catalogo!.Areas.Count >= 3);

        var r = ValidadorCatalogo.Validar(lido.Catalogo);
        Assert.True(r.EhValido, string.Join("; ", r.Erros.Select(e => $"{e.Tipo} {e.HabilidadeId} {e.AreaId}: {e.Detalhe}")));
    }
}
