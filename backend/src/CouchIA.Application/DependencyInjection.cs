using System.Reflection;
using AutoMapper;
using AutoMapper.Internal;
using CouchIA.Application.Common.Behaviours;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CouchIA.Application;

public static class DependencyInjection
{
    /// <summary>Limite global de profundidade do AutoMapper (mitiga GHSA-rvv3-g6hj-g44x).</summary>
    public const int AutoMapperMaxDepth = 5;

    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddAutoMapper(cfg => cfg.Internal().ForAllMaps((_, map) => map.MaxDepth(AutoMapperMaxDepth)), Assembly.GetExecutingAssembly());

        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());

            // Ordem padrão. Não há autenticação no MVP, então o AuthorizationBehaviour é omitido.
            cfg.AddOpenBehavior(typeof(UnhandledExceptionBehaviour<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehaviour<,>));
            cfg.AddOpenBehavior(typeof(PerformanceBehaviour<,>));
            cfg.AddOpenRequestPreProcessor(typeof(LoggingBehaviour<>));
        });
        return services;
    }
}




