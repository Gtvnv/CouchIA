using CouchIA.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CouchIA.Infrastructure.Criptografia;

public static class CriptografiaCampoExtensions
{
    public static IServiceCollection AddCriptografiaCampo(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ICriptografiaCampo>(_ => CriptografiaCampoAesGcm.DeConfiguracao(configuration));
        return services;
    }

    /// <summary>Conversor de valor do EF Core que cifra a string ao gravar e decifra ao ler.</summary>
    public static ValueConverter<string, string> CriarConversor(ICriptografiaCampo criptografia) =>
        new(v => criptografia.Criptografar(v), v => criptografia.Descriptografar(v));

    /// <summary>Aplica a criptografia de campo à propriedade string.</summary>
    public static PropertyBuilder<string> HasCriptografiaCampo(this PropertyBuilder<string> property, ICriptografiaCampo criptografia) =>
        property.HasConversion(CriarConversor(criptografia));
}
