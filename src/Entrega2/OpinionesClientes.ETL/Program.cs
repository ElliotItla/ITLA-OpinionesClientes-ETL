using OpinionesClientes.ETL;
using OpinionesClientes.ETL.Extract;
using OpinionesClientes.ETL.Load;
using OpinionesClientes.ETL.Models;
using OpinionesClientes.ETL.Transform;

var builder = Host.CreateApplicationBuilder(args);

// Configuración (appsettings.json -> sección "Etl")
builder.Services.Configure<EtlOptions>(builder.Configuration.GetSection("Etl"));

// Extract - Transform - Load
builder.Services.AddSingleton<LectorCsv>();
builder.Services.AddSingleton<ValidadorRegistros>();
builder.Services.AddSingleton(_ =>
{
    var cadena = builder.Configuration.GetConnectionString("OpinionesDB")
        ?? throw new InvalidOperationException("Falta la cadena de conexión 'OpinionesDB' en appsettings.json.");
    return new RepositorioOpiniones(cadena);
});

// Orquestador del proceso y Worker que lo ejecuta
builder.Services.AddSingleton<ProcesoEtl>();
builder.Services.AddHostedService<Worker>();

builder.Build().Run();
