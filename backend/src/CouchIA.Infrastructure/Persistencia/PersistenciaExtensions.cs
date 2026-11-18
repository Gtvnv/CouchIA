using CouchIA.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CouchIA.Infrastructure.Persistencia;

public static class PersistenciaExtensions
{
    public const string ConnectionStringName = "Default";

    public static IServiceCollection AddPersistencia(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(SessaoOptions.DeConfiguracao(configuration));
        services.AddDbContext<ApplicationDbContext>(o =>
        {
            // Resolvida na primeira utilização, para não impedir a subida sem banco configurado.
            var conexao = configuration.GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException(
                    $"String de conexão ausente. Defina 'ConnectionStrings:{ConnectionStringName}' (variável ConnectionStrings__{ConnectionStringName}).");
            o.UseNpgsql(conexao);
        });
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<ISessaoRepository, SessaoRepository>();
        services.AddHostedService<LimpezaSessoesExpiradasService>();
        return services;
    }
}
