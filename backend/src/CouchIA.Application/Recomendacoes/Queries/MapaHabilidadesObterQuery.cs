using CouchIA.Application.Common.Exceptions;
using CouchIA.Application.Common.Interfaces;
using CouchIA.Application.Mentor.Services;
using CouchIA.Application.Recomendacoes.ViewModels;
using CouchIA.Domain;
using MediatR;

namespace CouchIA.Application.Recomendacoes.Queries;

/// <summary>Mapa_de_Habilidades da área, marcando as dominadas do perfil (Req. 4.1 a 4.6).</summary>
public sealed record MapaHabilidadesObterQuery(SessaoId SessaoId, string AreaId) : IRequest<MapaHabilidadesVm>;

public sealed class MapaHabilidadesObterQueryHandler : IRequestHandler<MapaHabilidadesObterQuery, MapaHabilidadesVm>
{
    private readonly ISessaoRepository _repositorio;
    private readonly ICatalogoProvider _catalogo;

    public MapaHabilidadesObterQueryHandler(ISessaoRepository repositorio, ICatalogoProvider catalogo)
    {
        _repositorio = repositorio;
        _catalogo = catalogo;
    }

    public async Task<MapaHabilidadesVm> Handle(MapaHabilidadesObterQuery request, CancellationToken cancellationToken)
    {
        var sessao = await _repositorio.ObterAsync(request.SessaoId, cancellationToken)
            ?? throw new SessaoNaoEncontradaException();

        var resultado = MapaHabilidadesService.Montar(
            _catalogo.Ativo, new AreaId(request.AreaId), sessao.Perfil.HabilidadesDominadas);

        if (!resultado.Sucesso)
        {
            throw new MapaHabilidadesIndisponivelException(resultado.Erro!.Value);
        }

        var mapa = resultado.Mapa!;
        return new MapaHabilidadesVm(
            mapa.AreaId.Valor,
            mapa.Habilidades
                .Select(h => new HabilidadeMapaVm(h.Id.Valor, h.Nome, h.Descricao, h.Prioridade, h.Dominada))
                .ToList());
    }
}
