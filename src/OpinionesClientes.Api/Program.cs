using OpinionesClientes.Api.Servicios;

// ---------------------------------------------------------------------
// API REST simulada de comentarios de redes sociales.
// Hace el papel de un servicio EXTERNO: el ETL (ApiExtractor) la consume
// por HTTP igual que consumiría la API real de una red social.
// ---------------------------------------------------------------------

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<RepositorioComentarios>();

var app = builder.Build();

// Seguridad: toda petición a /api debe traer la cabecera X-Api-Key correcta.
// La clave NO está en el código ni en appsettings.json: se guarda en User Secrets.
var claveEsperada = app.Configuration["ApiComentarios:ApiKey"];
if (string.IsNullOrWhiteSpace(claveEsperada))
    throw new InvalidOperationException(
        "No hay clave de API configurada. Ejecute: dotnet user-secrets set \"ApiComentarios:ApiKey\" \"<su-clave>\"");

app.Use(async (contexto, siguiente) =>
{
    if (contexto.Request.Path.StartsWithSegments("/api"))
    {
        var claveRecibida = contexto.Request.Headers["X-Api-Key"].ToString();
        if (claveRecibida != claveEsperada)
        {
            app.Logger.LogWarning("Petición rechazada a {Ruta}: clave de API inválida o ausente", contexto.Request.Path);
            contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await contexto.Response.WriteAsJsonAsync(new { error = "Clave de API inválida o ausente." });
            return;
        }
    }
    await siguiente();
});

// GET /api/comentarios?pagina=1&tamanoPagina=50
app.MapGet("/api/comentarios", (RepositorioComentarios repo, int pagina = 1, int tamanoPagina = 50) =>
{
    if (pagina < 1 || tamanoPagina < 1 || tamanoPagina > 100)
        return Results.BadRequest(new { error = "pagina debe ser >= 1 y tamanoPagina entre 1 y 100." });

    return Results.Ok(repo.ObtenerPagina(pagina, tamanoPagina));
});

// Ruta simple para comprobar que la API está encendida (no pide clave)
app.MapGet("/", () => "API de comentarios en ejecución. Use GET /api/comentarios con la cabecera X-Api-Key.");

app.Run();
