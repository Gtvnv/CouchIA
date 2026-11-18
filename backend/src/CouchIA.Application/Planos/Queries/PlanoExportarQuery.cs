using CouchIA.Application.Common.Exceptions;
using CouchIA.Application.Common.Interfaces;
using CouchIA.Application.Mentor.Services;
using CouchIA.Application.Planos.Exceptions;
using CouchIA.Domain;
using MediatR;

namespace CouchIA.Application.Planos.Queries;

/// <summary>Exporta um plano da Sessao em PDF ou Markdown (Req. 5.5).</summary>
public sealed record PlanoExportarQuery(SessaoId SessaoId, Guid PlanoId, FormatoExportacao Formato)
    : IRequest<ArquivoExportado>;

public sealed class PlanoExportarQueryHandler : IRequestHandler<PlanoExportarQuery, ArquivoExportado>
{
    private readonly ISessaoRepository _repositorio;
    private readonly IExportadorPlano _exportador;

    public PlanoExportarQueryHandler(ISessaoRepository repositorio, IExportadorPlano exportador)
    {
        _repositorio = repositorio;
        _exportador = exportador;
    }

    public async Task<ArquivoExportado> Handle(PlanoExportarQuery request, CancellationToken cancellationToken)
    {
        var sessao = await _repositorio.ObterAsync(request.SessaoId, cancellationToken)
            ?? throw new SessaoNaoEncontradaException();

        var plano = sessao.Planos.FirstOrDefault(p => p.Id == request.PlanoId)
            ?? throw new PlanoNaoEncontradoException();

        // O arquivo é gerado em memória; qualquer falha vira erro e nenhum arquivo parcial é devolvido (Req. 5.8).
        try
        {
            return await _exportador.ExportarAsync(plano, request.Formato, cancellationToken);
        }
        catch (ExportacaoFalhouException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ExportacaoFalhouException("Falha ao exportar o plano.", ex);
        }
    }
}
