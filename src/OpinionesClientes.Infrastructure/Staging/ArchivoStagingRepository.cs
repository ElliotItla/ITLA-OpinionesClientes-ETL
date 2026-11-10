using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpinionesClientes.Application.Interfaces;
using OpinionesClientes.Domain.Entidades;
using OpinionesClientes.Infrastructure.Configuracion;

namespace OpinionesClientes.Infrastructure.Staging;

/// <summary>
/// Guarda lo extraído en un archivo JSON temporal (uno por ejecución).
/// Sirve de respaldo y permite revisar los datos sin abrir SQL Server.
/// </summary>
public class ArchivoStagingRepository : IStagingRepository
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,   // conserva tildes y ñ legibles
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _carpeta;

    public ArchivoStagingRepository(IOptions<OpcionesStaging> opciones, IHostEnvironment entorno)
    {
        _carpeta = Path.GetFullPath(Path.Combine(entorno.ContentRootPath, opciones.Value.CarpetaArchivos));
    }

    public string Nombre => "Archivo JSON";

    public async Task GuardarAsync(Guid idLote, IReadOnlyList<OpinionCruda> registros, CancellationToken cancelacion)
    {
        Directory.CreateDirectory(_carpeta);
        var ruta = Path.Combine(_carpeta, $"extraccion_{DateTime.Now:yyyyMMdd_HHmmss}_{idLote.ToString()[..8]}.json");

        await using var archivo = File.Create(ruta);
        await JsonSerializer.SerializeAsync(archivo, new { IdLote = idLote, Total = registros.Count, Registros = registros }, Json, cancelacion);
    }
}
