using CouchIA.Application.Common.Localization;
using CouchIA.Application.Mentor.Services;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace CouchIA.Application.Recomendacoes.Commands;

public sealed class FeedbackRegistrarCommandValidator : AbstractValidator<FeedbackRegistrarCommand>
{
    public FeedbackRegistrarCommandValidator(IStringLocalizer<MentorLocalization> localizer)
    {
        RuleFor(x => x.AreaId)
            .Must(a => !string.IsNullOrWhiteSpace(a))
            .WithMessage(_ => localizer["AreaObrigatoria"]);

        RuleFor(x => x.Tipo)
            .IsInEnum()
            .WithMessage(_ => localizer["FeedbackTipoInvalido"]);

        RuleFor(x => x.Motivo)
            .Must(FeedbackService.MotivoEhValido)
            .WithMessage(_ => localizer["FeedbackMotivoTamanhoInvalido"]);
    }
}
