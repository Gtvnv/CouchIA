using CouchIA.Application.Common.Exceptions;
using CouchIA.Domain;
using CouchIA.WebUI.Sessao;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CouchIA.WebUI.Controllers;

/// <summary>Base dos controllers: expõe o <see cref="Mediator"/> e o contexto de Sessao. Controllers não contêm regra de negócio.</summary>
[ApiController]
[Route("api/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    private ISender? _mediator;
    private SessaoCookie? _cookie;

    protected ISender Mediator => _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();

    private SessaoCookie Cookie => _cookie ??= HttpContext.RequestServices.GetRequiredService<SessaoCookie>();

    /// <summary>Sessao do cookie, ou null se ausente/inválida.</summary>
    protected SessaoId? SessaoIdAtual => HttpContext.ObterSessaoId();

    /// <summary>Sessao do cookie; sem cookie válido lança <see cref="SessaoNaoEncontradaException"/> (404 localizado).</summary>
    protected SessaoId ExigirSessaoId() => SessaoIdAtual ?? throw new SessaoNaoEncontradaException();

    protected void DefinirCookieSessao(SessaoId id) => Cookie.Definir(Response, id);

    protected void RemoverCookieSessao() => Cookie.Remover(Response);
}
