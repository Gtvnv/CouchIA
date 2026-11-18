using CouchIA.Application.Mentor.Services;
using CouchIA.Domain;

namespace CouchIA.Infrastructure.Exportacao;

/// <summary>Escolhe o exportador pelo formato pedido (Markdown ou PDF).</summary>
public sealed class ExportadorPlanoPorFormato : IExportadorPlano
{
    private readonly MarkdownExportador _markdown;
    private readonly PdfExportador _pdf;

    public ExportadorPlanoPorFormato(MarkdownExportador markdown, PdfExportador pdf)
    {
        _markdown = markdown;
        _pdf = pdf;
    }

    public Task<ArquivoExportado> ExportarAsync(PlanoDesenvolvimento plano, FormatoExportacao formato, CancellationToken ct) =>
        formato switch
        {
            FormatoExportacao.Markdown => _markdown.ExportarAsync(plano, formato, ct),
            FormatoExportacao.Pdf => _pdf.ExportarAsync(plano, formato, ct),
            _ => throw new ExportacaoFalhouException("Formato de exportação não suportado.")
        };
}
