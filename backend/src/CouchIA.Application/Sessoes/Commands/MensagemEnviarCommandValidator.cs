using CouchIA.Application.Common.Localization;
using CouchIA.Application.Mentor.Services;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace CouchIA.Application.Sessoes.Commands;

public sealed class MensagemEnviarCommandValidator : AbstractValidator<MensagemEnviarCommand>
{
    public MensagemEnviarCommandValidator(IStringLocalizer<MentorLocalization> localizer)
    {
        RuleFor(x => x.Texto)
            .Must(ValidadorMensagem.EhValida)
            .WithMessage(_ => localizer["MensagemTamanhoInvalido"]);

        RuleFor(x => x.ClientMessageId)
            .NotEqual(Guid.Empty)
            .WithMessage(_ => localizer["ClientMessageIdObrigatorio"]);
    }
}
