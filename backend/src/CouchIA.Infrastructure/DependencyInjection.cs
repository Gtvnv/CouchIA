using CouchIA.Application.Mentor.Services;
using CouchIA.Infrastructure.Catalogo;
using CouchIA.Infrastructure.Criptografia;
using CouchIA.Infrastructure.Exportacao;
using CouchIA.Infrastructure.Mentor;
using CouchIA.Infrastructure.Persistencia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CouchIA.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Redator único, registrado antes do cliente LLM (que usa TryAdd): cobre a chave de API e a conexão com o banco.
        services.TryAddSingleton(new RedatorDeSegredos(new[]
        {
            configuration[$"{AnthropicOptions.Secao}:ApiKey"],
            configuration.GetConnectionString(PersistenciaExtensions.ConnectionStringName)
        }));

        services.TryAddSingleton(TimeProvider.System);
        services.AddCriptografiaCampo(configuration);
        // Llm:Provedor = Mock troca o Anthropic por um LLM falso (demonstração local, sem chave).
        if (configuration.UsaMock())
            services.AddMockLlmClient(configuration);
        else
            services.AddAnthropicLlmClient(configuration);
        services.AddPersistencia(configuration);
        services.AddCatalogo(configuration);
        services.AddExportadores();

        return services;
    }

    private static IServiceCollection AddCatalogo(this IServiceCollection services, IConfiguration configuration)
    {
        var caminho = configuration[$"{CatalogoOptions.Secao}:Caminho"]
            ?? Path.Combine(AppContext.BaseDirectory, "Catalogo", "catalogo.json");

        services.AddSingleton(new CatalogoOptions(caminho));
        services.AddSingleton<CatalogoJsonProvider>();
        services.AddSingleton<ICatalogoProvider>(sp => sp.GetRequiredService<CatalogoJsonProvider>());
        services.AddHostedService<CatalogoCargaInicialService>();
        return services;
    }

    private static IServiceCollection AddExportadores(this IServiceCollection services)
    {
        services.AddSingleton<MarkdownExportador>();
        services.AddSingleton<PdfExportador>();
        services.AddSingleton<IExportadorPlano, ExportadorPlanoPorFormato>();
        return services;
    }
}
