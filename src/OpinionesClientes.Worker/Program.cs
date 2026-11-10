using OpinionesClientes.Application;
using OpinionesClientes.Infrastructure;
using OpinionesClientes.Infrastructure.Logging;
using OpinionesClientes.Worker;

// Punto de arranque del Servicio ETL (Worker Service .NET 8).
// Aquí solo se "arman" las piezas; la lógica está en Application e Infrastructure.
var builder = Host.CreateApplicationBuilder(args);

// Logs en consola (por defecto) y también en archivo
var carpetaLogs = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath,
    builder.Configuration["Worker:CarpetaLogs"] ?? "logs"));
builder.Logging.AddProvider(new ArchivoLoggerProvider(carpetaLogs));

builder.Services.Configure<OpcionesWorker>(builder.Configuration.GetSection("Worker"));
builder.Services.AddAplicacion();
builder.Services.AddInfraestructura(builder.Configuration);
builder.Services.AddHostedService<Worker>();

builder.Build().Run();
