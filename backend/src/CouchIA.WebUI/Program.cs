using CouchIA.Application;
using CouchIA.Infrastructure;
using CouchIA.Infrastructure.Persistencia;
using CouchIA.WebUI;
using Microsoft.EntityFrameworkCore;
using CouchIA.WebUI.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel((contexto, kestrel) =>
{
    kestrel.AddServerHeader = false;
    kestrel.Limits.MaxRequestBodySize =
        contexto.Configuration.GetValue<long?>($"{LimiteCorpoOptions.Secao}:{nameof(LimiteCorpoOptions.TamanhoMaximoCorpoBytes)}")
        ?? new LimiteCorpoOptions().TamanhoMaximoCorpoBytes;
});

builder.Services.AddLocalization();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApi(builder.Configuration);

builder.Services.AddControllers()
    .AddJsonOptions(o => CouchIA.WebUI.Contratos.JsonContrato.Configurar(o.JsonSerializerOptions));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Somente demonstração/ambientes descartáveis: aplica as migrações do EF na subida (Banco:AplicarMigracoes=true).
if (app.Configuration.GetValue<bool>("Banco:AplicarMigracoes"))
{
    using var escopo = app.Services.CreateScope();
    var db = escopo.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
}

app.UseApi();

app.MapControllers();

app.Run();

/// <summary>Torna o ponto de entrada acessível ao <c>WebApplicationFactory&lt;Program&gt;</c> dos testes de integração.</summary>
public partial class Program { }
