using CouchIA.Application.Common.Exceptions;
using CouchIA.Application.Common.Interfaces;
using CouchIA.Application.Common.Localization;
using CouchIA.Application.Mentor.Services;
using CouchIA.Application.Recomendacoes.ViewModels;
using CouchIA.Domain;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Localization;

namespace CouchIA.Application.Recomendacoes.Commands;

/// <summary>
/// Registra "útil" ou "não útil" para uma recomendação; o último feedback da área substitui o anterior
/// (Req. 9.1, 9.4). Devolve as recomendações já na nova ordem de exibição (Req. 9.5). Em falha de gravação
/// a exceção propaga e o feedback anterior permanece (Req. 9.7).
/// </summary>
public sealed record FeedbackRegistrarCommand(
    SessaoId SessaoId,
    string AreaId,
    TipoFeedback Tipo,
    string? Motivo) : IRequest<RecomendacoesVm>;

public sealed class FeedbackRegistrarCommandHandler : IRequestHandler<FeedbackRegistrarCommand, RecomendacoesVm>
{
    private readonly ISessaoRepository _repositorio;
    private readonly ICatalogoProvider _catalogo;
    private readonly TimeProvider _relogio;
    private readonly IStringLocalizer<MentorLocalization> _localizer;

    public FeedbackRegistrarCommandHandler(
        ISessaoRepository repositorio,
        ICatalogoProvider catalogo,
        TimeProvider relogio,
        IStringLocalizer<MentorLocalization> localizer)
    {
        _repositorio = repositorio;
        _catalogo = catalogo;
        _relogio = relogio;
        _localizer = localizer;
    }

    public async Task<RecomendacoesVm> Handle(FeedbackRegistrarCommand request, CancellationToken cancellationToken)
    {
        var sessao = await _repositorio.ObterAsync(request.SessaoId, cancellationToken)
            ?? throw new SessaoNaoEncontradaException();

        var area = new AreaId(request.AreaId);
        if (sessao.Recomendacoes.All(r => r.Area != area))
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure(nameof(request.AreaId), _localizer["FeedbackAreaNaoRecomendada"])
            });
        }

        var feedbacks = FeedbackService.Registrar(
            sessao.Feedbacks, area, request.Tipo, request.Motivo, _relogio.GetUtcNow())
            ?? throw new ValidationException(new[]
            {
                new ValidationFailure(nameof(request.Motivo), _localizer["FeedbackMotivoTamanhoInvalido"])
            });

        sessao = sessao with { Feedbacks = feedbacks };
        await _repositorio.SalvarAsync(sessao, cancellationToken);

        return RecomendacoesVm.De(sessao, _catalogo.Ativo);
    }
}
