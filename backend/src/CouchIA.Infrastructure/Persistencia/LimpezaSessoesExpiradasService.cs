using CouchIA.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CouchIA.Infrastructure.Persistencia;

/// <summary>Remove periodicamente as sessões sem atividade além do prazo de inatividade (Req. 7.4).</summary>
public sealed class LimpezaSessoesExpiradasService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SessaoOptions _options;
    private readonly TimeProvider _relogio;
    private readonly ILogger<LimpezaSessoesExpiradasService> _logger;

    public LimpezaSessoesExpiradasService(IServiceScopeFactory scopeFactory, SessaoOptions options,
        TimeProvider relogio, ILogger<LimpezaSessoesExpiradasService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _relogio = relogio;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.IntervaloLimpeza, _relogio);
        do
        {
            await LimparAsync(stoppingToken);
        }
        while (await AguardarProximoAsync(timer, stoppingToken));
    }

    public async Task<int> LimparAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var removidas = await db.RemoverSessoesInativasAsync(_relogio.GetUtcNow() - _options.Inatividade, ct);
            if (removidas > 0)
                _logger.LogInformation("Sessões expiradas removidas: {Quantidade}", removidas);
            return removidas;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Falha ao limpar sessões expiradas.");
            return 0;
        }
    }

    private static async Task<bool> AguardarProximoAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try { return await timer.WaitForNextTickAsync(ct); }
        catch (OperationCanceledException) { return false; }
    }
}
