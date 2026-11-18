using CouchIA.Application.Common.Localization;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace CouchIA.Application.Recomendacoes.Queries;

public sealed class MapaHabilidadesObterQueryValidator : AbstractValidator<MapaHabilidadesObterQuery>
{
    public MapaHabilidadesObterQueryValidator(IStringLocalizer<MentorLocalization> localizer)
    {
        RuleFor(x => x.AreaId)
            .Must(a => !string.IsNullOrWhiteSpace(a))
            .WithMessage(_ => localizer["AreaObrigatoria"]);
    }
}
