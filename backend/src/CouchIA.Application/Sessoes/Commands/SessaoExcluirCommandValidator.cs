using CouchIA.Application.Common.Localization;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace CouchIA.Application.Sessoes.Commands;

public sealed class SessaoExcluirCommandValidator : AbstractValidator<SessaoExcluirCommand>
{
    public SessaoExcluirCommandValidator(IStringLocalizer<MentorLocalization> localizer)
    {
        RuleFor(x => x.Confirmado)
            .Equal(true)
            .WithMessage(_ => localizer["ConfirmacaoExclusaoObrigatoria"]);
    }
}
