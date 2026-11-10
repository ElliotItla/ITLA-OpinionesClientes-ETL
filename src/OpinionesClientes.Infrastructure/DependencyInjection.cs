using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpinionesClientes.Application.Interfaces;
using OpinionesClientes.Infrastructure.Configuracion;
using OpinionesClientes.Infrastructure.Extractores;
using OpinionesClientes.Infrastructure.Staging;

namespace OpinionesClientes.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registra los extractores y los destinos de staging según appsettings.json.
    /// Activar o desactivar una fuente es cambiar "Habilitada" a true/false, sin tocar código.
    /// </summary>
    public static IServiceCollection AddInfraestructura(this IServiceCollection servicios, IConfiguration configuracion)
    {
        servicios.Configure<OpcionesFuentes>(configuracion.GetSection(OpcionesFuentes.Seccion));
        servicios.Configure<OpcionesStaging>(configuracion.GetSection(OpcionesStaging.Seccion));

        var fuentes = configuracion.GetSection(OpcionesFuentes.Seccion).Get<OpcionesFuentes>() ?? new OpcionesFuentes();
        var staging = configuracion.GetSection(OpcionesStaging.Seccion).Get<OpcionesStaging>() ?? new OpcionesStaging();

        // --- Fuentes de datos ---
        if (fuentes.Csv.Habilitada)
            servicios.AddSingleton<IExtractor, CsvExtractor>();

        if (fuentes.BaseDatos.Habilitada)
            servicios.AddSingleton<IExtractor, DatabaseExtractor>();

        if (fuentes.Api.Habilitada)
        {
            // IHttpClientFactory: reutiliza conexiones y centraliza URL, cabecera de seguridad y tiempo de espera.
            servicios.AddHttpClient(ApiExtractor.NombreCliente, (proveedor, cliente) =>
            {
                var api = proveedor.GetRequiredService<IOptions<OpcionesFuentes>>().Value.Api;
                if (string.IsNullOrWhiteSpace(api.ApiKey))
                    throw new InvalidOperationException(
                        "Falta la clave de la API. Ejecute: dotnet user-secrets set \"Fuentes:Api:ApiKey\" \"<su-clave>\"");

                cliente.BaseAddress = new Uri(api.UrlBase);
                cliente.Timeout = TimeSpan.FromSeconds(api.TiempoEsperaSegundos);
                cliente.DefaultRequestHeaders.Add("X-Api-Key", api.ApiKey);
            });
            servicios.AddSingleton<IExtractor, ApiExtractor>();
        }

        // --- Destinos de staging ---
        if (staging.GuardarEnArchivo)
            servicios.AddSingleton<IStagingRepository, ArchivoStagingRepository>();

        if (staging.GuardarEnBaseDatos)
            servicios.AddSingleton<IStagingRepository, SqlStagingRepository>();

        return servicios;
    }
}
