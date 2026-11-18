using CouchIA.Application;
using CouchIA.Infrastructure;
using CouchIA.WebUI;
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

app.UseApi();

app.MapControllers();

app.Run();

/// <summary>Torna o ponto de entrada acessível ao <c>WebApplicationFactory&lt;Program&gt;</c> dos testes de integração.</summary>
public partial class Program { }
