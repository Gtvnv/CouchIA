using CouchIA.Application.Common.Interfaces;
using CouchIA.Application.Common.Localization;
using CouchIA.Application.Mentor.Services;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace CouchIA.Application.Planos.Commands;

public sealed class PlanoGerarCommandValidator : AbstractValidator<PlanoGerarCommand>
{
    public PlanoGerarCommandValidator(
        IStringLocalizer<MentorLocalization> localizer,
        ISessaoRepository repositorio,
        ICatalogoProvider catalogo)
    {
        // Req. 5.6: valor informado deve estar entre 1 e 168.
        RuleFor(x => x.HorasPorSemana)
            .InclusiveBetween(PlanoDesenvolvimentoService.DisponibilidadeMinima, PlanoDesenvolvimentoService.DisponibilidadeMaxima)
            .When(x => x.HorasPorSemana.HasValue)
            .WithMessage(_ => localizer["DisponibilidadeSemanalInvalida"]);

        // Req. 5.6: sem valor no pedido, o perfil precisa ter uma disponibilidade válida. O perfil não é alterado.
        RuleFor(x => x)
            .MustAsync(async (x, ct) =>
            {
                var sessao = await repositorio.ObterAsync(x.SessaoId, ct);
                // Sessao inexistente é tratada pelo handler (SessaoNaoEncontradaException).
                if (sessao is null) return true;
                var horas = sessao.Perfil.HorasEstudoPorSemana;
                return horas is >= PlanoDesenvolvimentoService.DisponibilidadeMinima
                    and <= PlanoDesenvolvimentoService.DisponibilidadeMaxima;
            })
            .When(x => !x.HorasPorSemana.HasValue)
            .WithName(nameof(PlanoGerarCommand.HorasPorSemana))
            .WithMessage(_ => localizer["DisponibilidadeSemanalObrigatoria"]);

        // Req. 5.1: a área precisa existir no catálogo. Catálogo indisponível é tratado no handler.
        RuleFor(x => x.AreaId)
            .Must(area => area.Valor is not null)
            .WithMessage(_ => localizer["AreaNaoEncontrada"])
            .Must(area => catalogo.Ativo is null || catalogo.Ativo.Areas.Any(a => a.Id == area))
            .WithMessage(_ => localizer["AreaNaoEncontrada"]);
    }
}
