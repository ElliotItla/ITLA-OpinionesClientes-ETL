using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpinionesClientes.Application.Interfaces;
using OpinionesClientes.Domain.Entidades;
using OpinionesClientes.Domain.Enums;
using OpinionesClientes.Infrastructure.Configuracion;

namespace OpinionesClientes.Infrastructure.Extractores;

/// <summary>
/// Extrae las RESEÑAS WEB desde la base de datos relacional ResenasOrigenDB con ADO.NET.
/// Ejecuta la consulta definida en appsettings.json y lee los resultados de forma asíncrona.
/// </summary>
public class DatabaseExtractor : IExtractor
{
    private readonly OpcionesBaseDatos _opciones;
    private readonly string _cadenaConexion;
    private readonly ILogger<DatabaseExtractor> _logger;

    public DatabaseExtractor(IOptions<OpcionesFuentes> opciones, IConfiguration configuracion, ILogger<DatabaseExtractor> logger)
    {
        _opciones = opciones.Value.BaseDatos;
        _cadenaConexion = configuracion.GetConnectionString(_opciones.NombreConexion)
            ?? throw new InvalidOperationException($"Falta la cadena de conexión '{_opciones.NombreConexion}'.");
        _logger = logger;
    }

    public string Nombre => "DatabaseExtractor (reseñas web)";
    public TipoFuente TipoFuente => TipoFuente.BaseDatos;

    public async Task<IReadOnlyList<OpinionCruda>> ExtraerAsync(CancellationToken cancelacion)
    {
        var resultado = new List<OpinionCruda>();

        await using var conexion = new SqlConnection(_cadenaConexion);
        await conexion.OpenAsync(cancelacion);

        await using var comando = new SqlCommand(_opciones.Consulta, conexion)
        {
            CommandTimeout = _opciones.TiempoEsperaSegundos
        };

        await using var lector = await comando.ExecuteReaderAsync(cancelacion);
        while (await lector.ReadAsync(cancelacion))
        {
            resultado.Add(new OpinionCruda
            {
                TipoFuente = TipoFuente.BaseDatos,
                IdOrigen = Texto(lector, "IdReview"),
                IdCliente = Texto(lector, "IdCliente"),
                IdProducto = Texto(lector, "IdProducto"),
                Fecha = Texto(lector, "Fecha"),
                Comentario = Texto(lector, "Comentario"),
                Puntaje = Texto(lector, "Rating"),
                Canal = "Web"
            });
        }

        _logger.LogInformation("BD {Base}: {Total} reseñas leídas", conexion.Database, resultado.Count);
        return resultado;
    }

    private static string? Texto(SqlDataReader lector, string columna)
    {
        var valor = lector[columna];
        return valor is DBNull ? null : Convert.ToString(valor, System.Globalization.CultureInfo.InvariantCulture);
    }
}
