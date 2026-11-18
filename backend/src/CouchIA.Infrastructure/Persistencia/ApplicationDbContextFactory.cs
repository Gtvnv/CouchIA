using CouchIA.Infrastructure.Criptografia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CouchIA.Infrastructure.Persistencia;

/// <summary>Usada só pelas ferramentas `dotnet ef`. A chave abaixo é descartável e nunca grava dados.</summary>
public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var conexao = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Database=couchia;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(conexao).Options;
        return new ApplicationDbContext(options, new CriptografiaCampoAesGcm(new byte[32]));
    }
}
