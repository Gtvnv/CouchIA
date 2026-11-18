using CouchIA.Domain;

namespace CouchIA.Application.Mentor.Services;

public enum FormatoExportacao
{
    Markdown,
    Pdf
}

/// <summary>Arquivo gerado em memória. Nada é gravado em disco, então uma falha nunca deixa arquivo parcial.</summary>
public sealed record ArquivoExportado(string NomeArquivo, string ContentType, byte[] Conteudo);

/// <summary>Falha ao gerar o arquivo de exportação (Req. 5.8). Nenhum arquivo parcial é devolvido.</summary>
public sealed class ExportacaoFalhouException : Exception
{
    public ExportacaoFalhouException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

public interface IExportadorPlano
{
    /// <exception cref="ExportacaoFalhouException">Quando o arquivo não pôde ser gerado.</exception>
    Task<ArquivoExportado> ExportarAsync(PlanoDesenvolvimento plano, FormatoExportacao formato, CancellationToken ct);
}
