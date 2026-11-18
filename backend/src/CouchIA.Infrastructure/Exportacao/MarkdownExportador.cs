using System.Globalization;
using System.Text;
using CouchIA.Application.Mentor.Services;
using CouchIA.Domain;

namespace CouchIA.Infrastructure.Exportacao;

/// <summary>
/// Renderiza o plano em Markdown (UTF-8). É a fonte do PDF e usa um formato regular
/// para que um leitor de teste consiga reconstruir o plano.
/// </summary>
public sealed class MarkdownExportador : IExportadorPlano
{
    public Task<ArquivoExportado> ExportarAsync(PlanoDesenvolvimento plano, FormatoExportacao formato, CancellationToken ct)
    {
        if (formato != FormatoExportacao.Markdown)
            throw new ExportacaoFalhouException("Formato não suportado por este exportador.");

        try
        {
            ct.ThrowIfCancellationRequested();
            var bytes = new UTF8Encoding(false).GetBytes(Renderizar(plano));
            return Task.FromResult(new ArquivoExportado($"plano-{plano.Area.Valor}.md", "text/markdown; charset=utf-8", bytes));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ExportacaoFalhouException("Falha ao gerar o arquivo Markdown do plano.", ex);
        }
    }

    public static string Renderizar(PlanoDesenvolvimento plano)
    {
        ArgumentNullException.ThrowIfNull(plano);
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();

        sb.Append("# Plano de Desenvolvimento: ").Append(plano.Area.Valor).Append("\n\n");
        sb.Append("- Disponibilidade semanal: ").Append(plano.HorasPorSemana.ToString(inv)).Append(" horas\n");
        sb.Append("- Duração total: ").Append(plano.DuracaoTotalSemanas.ToString(inv)).Append(" semanas\n");

        foreach (var etapa in plano.Etapas)
        {
            sb.Append("\n## Etapa ").Append(etapa.Numero.ToString(inv)).Append('\n');
            sb.Append("\n- Duração: ").Append(etapa.DuracaoSemanas.ToString(inv)).Append(" semanas\n");

            sb.Append("\n### Habilidades\n\n");
            foreach (var h in etapa.Habilidades)
                sb.Append("- ").Append(h.Valor).Append('\n');

            sb.Append("\n### Recursos\n\n");
            foreach (var r in etapa.Recursos)
                sb.Append("- [").Append(EscaparTitulo(r.Titulo)).Append("](").Append(r.Url.OriginalString).Append(")\n");
        }

        return sb.ToString();
    }

    /// <summary>Escapa os caracteres que quebrariam o link Markdown.</summary>
    public static string EscaparTitulo(string titulo) =>
        titulo.Replace("\\", "\\\\").Replace("[", "\\[").Replace("]", "\\]")
              .Replace("\r", " ").Replace("\n", " ");
}
