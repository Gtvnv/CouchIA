using System.Text;
using CouchIA.Application.Mentor.Services;
using CouchIA.Domain;
using Microsoft.Extensions.Logging;

namespace CouchIA.Infrastructure.Catalogo;

/// <summary>
/// Mantém o último catálogo válido em memória. Carrega, valida e só então substitui o ativo.
/// </summary>
public sealed class CatalogoJsonProvider : ICatalogoProvider
{
    private readonly object _trava = new();
    private readonly ILogger<CatalogoJsonProvider>? _logger;
    private volatile Domain.Catalogo? _ativo;

    public CatalogoJsonProvider(ILogger<CatalogoJsonProvider>? logger = null) => _logger = logger;

    public Domain.Catalogo? Ativo => _ativo;

    public ResultadoCarga Carregar(string json)
    {
        var lido = CatalogoSerializer.Desserializar(json);
        if (!lido.Sucesso)
        {
            Registrar(lido.Erros);
            return new ResultadoCarga(null, lido.Erros);
        }

        var validacao = ValidadorCatalogo.Validar(lido.Catalogo);
        if (!validacao.EhValido)
        {
            var erros = validacao.Erros
                .Select(e => new ErroCarga(TipoFalhaCarga.ValidacaoDoCatalogo,
                    e.HabilidadeId is not null ? $"habilidade:{e.HabilidadeId}" : e.AreaId is not null ? $"area:{e.AreaId}" : null,
                    null, null, e.Detalhe, e))
                .ToList();
            Registrar(erros);
            return new ResultadoCarga(null, erros);
        }

        lock (_trava) _ativo = lido.Catalogo;
        return new ResultadoCarga(lido.Catalogo, Array.Empty<ErroCarga>());
    }

    /// <summary>Lê o arquivo JSON (UTF-8) e tenta carregá-lo. Falha de E/S mantém o ativo.</summary>
    public ResultadoCarga CarregarArquivo(string caminho)
    {
        string json;
        try
        {
            json = File.ReadAllText(caminho, new UTF8Encoding(false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            var erro = new ErroCarga(TipoFalhaCarga.JsonMalformado, caminho, null, null,
                $"Não foi possível ler o arquivo do catálogo: {ex.GetType().Name}");
            Registrar(new[] { erro });
            return new ResultadoCarga(null, new[] { erro });
        }
        return Carregar(json);
    }

    private void Registrar(IEnumerable<ErroCarga> erros)
    {
        if (_logger is null) return;
        foreach (var e in erros)
            _logger.LogWarning("Carga do catálogo recusada ({Tipo}) em {Campo} linha {Linha} posição {Posicao}: {Detalhe}",
                e.Tipo, e.Campo, e.Linha, e.Posicao, e.Detalhe);
    }
}
