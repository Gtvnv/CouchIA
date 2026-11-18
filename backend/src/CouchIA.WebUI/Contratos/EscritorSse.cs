using System.Text;
using System.Text.Json;

namespace CouchIA.WebUI.Contratos;

/// <summary>
/// Escreve eventos Server-Sent Events (<c>data: {json}</c>). O cabeçalho só é enviado no primeiro evento,
/// de modo que falhas anteriores (validação, Sessao inexistente) ainda viram um ProblemDetails com o status correto.
/// </summary>
public sealed class EscritorSse
{
    private readonly HttpResponse _resposta;

    public EscritorSse(HttpResponse resposta) => _resposta = resposta;

    /// <summary>Indica se a resposta já foi iniciada (status e cabeçalhos enviados).</summary>
    public bool Iniciado { get; private set; }

    public async Task EscreverAsync(object evento, CancellationToken ct)
    {
        if (!Iniciado)
        {
            _resposta.StatusCode = StatusCodes.Status200OK;
            _resposta.ContentType = "text/event-stream; charset=utf-8";
            _resposta.Headers.CacheControl = "no-cache";
            _resposta.Headers["X-Accel-Buffering"] = "no";
            Iniciado = true;
            await _resposta.StartAsync(ct);
        }

        var json = JsonSerializer.Serialize(evento, evento.GetType(), JsonContrato.Opcoes);
        await _resposta.Body.WriteAsync(Encoding.UTF8.GetBytes($"data: {json}\n\n"), ct);
        await _resposta.Body.FlushAsync(ct);
    }
}
