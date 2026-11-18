using CouchIA.Application.Common.Exceptions;
using CouchIA.Application.Common.Interfaces;
using CouchIA.Application.Mentor.Services;
using CouchIA.Application.Planos.Exceptions;
using CouchIA.Application.Planos.ViewModels;
using CouchIA.Domain;
using MediatR;

namespace CouchIA.Application.Planos.Commands;

/// <summary>
/// Gera e persiste um Plano_de_Desenvolvimento (Req. 5.1 a 5.8). A disponibilidade vem do Perfil_do_Usuario;
/// <see cref="HorasPorSemana"/> permite informá-la neste pedido (1 a 168) sem alterar o perfil.
/// </summary>
public sealed record PlanoGerarCommand(SessaoId SessaoId, AreaId AreaId, int? HorasPorSemana = null)
    : IRequest<PlanoGeradoVm>;

public sealed class PlanoGerarCommandHandler : IRequestHandler<PlanoGerarCommand, PlanoGeradoVm>
{
    private readonly ISessaoRepository _repositorio;
    private readonly ICatalogoProvider _catalogo;

    public PlanoGerarCommandHandler(ISessaoRepository repositorio, ICatalogoProvider catalogo)
    {
        _repositorio = repositorio;
        _catalogo = catalogo;
    }

    public async Task<PlanoGeradoVm> Handle(PlanoGerarCommand request, CancellationToken cancellationToken)
    {
        var sessao = await _repositorio.ObterAsync(request.SessaoId, cancellationToken)
            ?? throw new SessaoNaoEncontradaException();

        var catalogo = _catalogo.Ativo ?? throw new CatalogoIndisponivelException();

        var horas = request.HorasPorSemana ?? sessao.Perfil.HorasEstudoPorSemana ?? 0;

        var resultado = PlanoDesenvolvimentoService.Gerar(
            catalogo, request.AreaId, sessao.Perfil.HabilidadesDominadas, horas);

        if (!resultado.Sucesso || resultado.Plano is null)
        {
            // Nada é persistido: perfil e planos anteriores ficam intactos (Req. 5.7, 5.8).
            return new PlanoGeradoVm(resultado.Status, null, resultado.Avisos, resultado.DisponibilidadeMinimaHoras);
        }

        // Só os planos são acrescentados; o perfil não é tocado. Se a gravação falhar, o estado persistido não muda.
        var atualizada = sessao with { Planos = sessao.Planos.Append(resultado.Plano).ToList() };
        await _repositorio.SalvarAsync(atualizada, cancellationToken);

        return new PlanoGeradoVm(resultado.Status, resultado.Plano, resultado.Avisos, null);
    }
}
