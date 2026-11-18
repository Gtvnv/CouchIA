using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CouchIA.Infrastructure.Catalogo;

/// <summary>
/// Carrega o catálogo na subida. Se o arquivo for inválido, a aplicação sobe em modo degradado
/// (sem catálogo ativo) e as operações que dependem dele respondem com erro de indisponibilidade.
/// </summary>
public sealed class CatalogoCargaInicialService : IHostedService
{
    private readonly CatalogoJsonProvider _provider;
    private readonly CatalogoOptions _opcoes;
    private readonly ILogger<CatalogoCargaInicialService> _logger;

    public CatalogoCargaInicialService(CatalogoJsonProvider provider, CatalogoOptions opcoes, ILogger<CatalogoCargaInicialService> logger)
    {
        _provider = provider;
        _opcoes = opcoes;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var resultado = _provider.CarregarArquivo(_opcoes.Caminho);
        if (resultado.Erros.Count == 0)
            _logger.LogInformation("Catálogo carregado de {Caminho}.", _opcoes.Caminho);
        else
            _logger.LogError("Catálogo inválido em {Caminho}: iniciando em modo degradado, sem catálogo ativo.", _opcoes.Caminho);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed record CatalogoOptions(string Caminho)
{
    public const string Secao = "Catalogo";
}
