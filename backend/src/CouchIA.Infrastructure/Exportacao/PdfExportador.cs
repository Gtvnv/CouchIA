using System.Globalization;
using CouchIA.Application.Mentor.Services;
using CouchIA.Domain;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CouchIA.Infrastructure.Exportacao;

/// <summary>Gera o PDF do plano com QuestPDF, a partir do mesmo modelo usado pelo Markdown.</summary>
public sealed class PdfExportador : IExportadorPlano
{
    static PdfExportador()
    {
        // Projeto pessoal, sem fins comerciais corporativos: licença Community definida explicitamente.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task<ArquivoExportado> ExportarAsync(PlanoDesenvolvimento plano, FormatoExportacao formato, CancellationToken ct)
    {
        if (formato != FormatoExportacao.Pdf)
            throw new ExportacaoFalhouException("Formato não suportado por este exportador.");

        try
        {
            ct.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(plano);
            var bytes = Gerar(plano);
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(new ArquivoExportado($"plano-{plano.Area.Valor}.pdf", "application/pdf", bytes));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ExportacaoFalhouException("Falha ao gerar o arquivo PDF do plano.", ex);
        }
    }

    private static byte[] Gerar(PlanoDesenvolvimento plano)
    {
        var inv = CultureInfo.InvariantCulture;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(t => t.FontSize(11));

                page.Content().Column(col =>
                {
                    col.Spacing(6);
                    col.Item().Text($"Plano de Desenvolvimento: {plano.Area.Valor}").FontSize(20).Bold();
                    col.Item().Text($"Disponibilidade semanal: {plano.HorasPorSemana.ToString(inv)} horas");
                    col.Item().Text($"Duração total: {plano.DuracaoTotalSemanas.ToString(inv)} semanas");

                    foreach (var etapa in plano.Etapas)
                    {
                        col.Item().PaddingTop(10).Text($"Etapa {etapa.Numero.ToString(inv)}").FontSize(15).Bold();
                        col.Item().Text($"Duração: {etapa.DuracaoSemanas.ToString(inv)} semanas");

                        col.Item().Text("Habilidades").Bold();
                        foreach (var h in etapa.Habilidades)
                            col.Item().Text($"• {h.Valor}");

                        col.Item().Text("Recursos").Bold();
                        foreach (var r in etapa.Recursos)
                            col.Item().Text($"• {r.Titulo} ({r.Url.OriginalString})");
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        }).GeneratePdf();
    }
}
