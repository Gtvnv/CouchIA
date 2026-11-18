using CouchIA.Domain;

namespace CouchIA.WebUI.Sessao;

/// <summary>Configuração do cookie de sessão (seção <c>Sessao</c>).</summary>
public sealed class SessaoCookieOptions
{
    public const string Secao = "Sessao";

    public string NomeCookie { get; set; } = "couchia_sessao";

    /// <summary>Duração do cookie em dias. A expiração por inatividade é aplicada pelo repositório (Req. 7.5).</summary>
    public int DuracaoDias { get; set; } = 30;
}

/// <summary>Emite e remove o cookie de sessão: HttpOnly, Secure e SameSite=Strict (Req. 8.6).</summary>
public sealed class SessaoCookie
{
    private readonly SessaoCookieOptions _options;

    public SessaoCookie(Microsoft.Extensions.Options.IOptions<SessaoCookieOptions> options) => _options = options.Value;

    public string Nome => _options.NomeCookie;

    public void Definir(HttpResponse resposta, SessaoId id) =>
        resposta.Cookies.Append(Nome, id.Valor, Opcoes(TimeSpan.FromDays(Math.Max(1, _options.DuracaoDias))));

    public void Remover(HttpResponse resposta) => resposta.Cookies.Delete(Nome, Opcoes(null));

    private static CookieOptions Opcoes(TimeSpan? duracao) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        IsEssential = true,
        MaxAge = duracao
    };
}

public static class SessaoHttpContextExtensions
{
    private const string ChaveItem = "CouchIA.SessaoId";

    /// <summary>Sessão identificada pelo cookie, ou null quando ausente ou inválida (tratada como Sessao vazia).</summary>
    public static SessaoId? ObterSessaoId(this HttpContext contexto) =>
        contexto.Items.TryGetValue(ChaveItem, out var valor) && valor is SessaoId id ? id : null;

    internal static void DefinirSessaoId(this HttpContext contexto, SessaoId id) => contexto.Items[ChaveItem] = id;
}

/// <summary>
/// Lê o cookie de sessão. Cookie ausente ou malformado equivale a Sessao vazia (Req. 7.5);
/// um cookie malformado é removido para não ser reenviado.
/// </summary>
public sealed class SessaoMiddleware
{
    private readonly RequestDelegate _next;

    public SessaoMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext contexto, SessaoCookie cookie)
    {
        if (contexto.Request.Cookies.TryGetValue(cookie.Nome, out var valor))
        {
            if (SessaoId.TryParse(valor, out var id))
                contexto.DefinirSessaoId(id);
            else
                cookie.Remover(contexto.Response);
        }

        return _next(contexto);
    }
}
