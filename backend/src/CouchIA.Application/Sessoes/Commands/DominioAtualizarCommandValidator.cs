using CouchIA.Application.Common.Localization;
using CouchIA.Application.Mentor.Services;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace CouchIA.Application.Sessoes.Commands;

public sealed class DominioAtualizarCommandValidator : AbstractValidator<DominioAtualizarCommand>
{
    public DominioAtualizarCommandValidator(IStringLocalizer<MentorLocalization> localizer, ICatalogoProvider catalogo)
    {
        RuleFor(x => x.HabilidadesDominadas)
            .NotNull()
            .WithMessage(_ => localizer["HabilidadeInexistente"]);

        RuleForEach(x => x.HabilidadesDominadas)
            .Must(h => !string.IsNullOrWhiteSpace(h))
            .WithMessage(_ => localizer["HabilidadeInexistente"])
            .Must(h => ExisteNoCatalogo(catalogo, h))
            .When(h => h.HabilidadesDominadas is not null)
            .WithMessage(_ => localizer["HabilidadeInexistente"]);
    }

    // Sem catálogo ativo não há como conferir; o mapa já falha de forma explícita nesse caso (Req. 4.6).
    private static bool ExisteNoCatalogo(ICatalogoProvider provider, string? id) =>
        string.IsNullOrWhiteSpace(id)
        || provider.Ativo is not { } catalogo
        || catalogo.Habilidades.Any(h => h.Id.Valor == id.Trim());
}
