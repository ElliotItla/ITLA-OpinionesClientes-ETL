using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpinionesClientes.Application.Interfaces;
using OpinionesClientes.Domain.Entidades;
using OpinionesClientes.Domain.Enums;
using OpinionesClientes.Infrastructure.Configuracion;

namespace OpinionesClientes.Infrastructure.Extractores;

/// <summary>
/// Extrae las ENCUESTAS INTERNAS desde archivos CSV usando CsvHelper.
/// Lee fila por fila (streaming), así no carga el archivo entero en memoria.
/// </summary>
public class CsvExtractor : IExtractor
{
    // Columnas obligatorias de surveys_part*.csv
    private static readonly string[] ColumnasRequeridas =
        { "IdOpinion", "IdCliente", "IdProducto", "Fecha", "Comentario" };

    private readonly OpcionesCsv _opciones;
    private readonly IHostEnvironment _entorno;
    private readonly ILogger<CsvExtractor> _logger;

    public CsvExtractor(IOptions<OpcionesFuentes> opciones, IHostEnvironment entorno, ILogger<CsvExtractor> logger)
    {
        _opciones = opciones.Value.Csv;
        _entorno = entorno;
        _logger = logger;
    }

    public string Nombre => "CsvExtractor (encuestas)";
    public TipoFuente TipoFuente => TipoFuente.Csv;

    public async Task<IReadOnlyList<OpinionCruda>> ExtraerAsync(CancellationToken cancelacion)
    {
        var carpeta = Path.GetFullPath(Path.Combine(_entorno.ContentRootPath, _opciones.Carpeta));
        var resultado = new List<OpinionCruda>();

        foreach (var archivo in _opciones.Archivos)
        {
            var ruta = Path.Combine(carpeta, archivo);
            if (!File.Exists(ruta))
            {
                _logger.LogWarning("CSV no encontrado, se omite: {Ruta}", ruta);
                continue;
            }

            var antes = resultado.Count;
            var descartadas = 0;

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                TrimOptions = TrimOptions.Trim,
                MissingFieldFound = null,   // una columna vacía no detiene la lectura
                BadDataFound = args => _logger.LogWarning("Dato mal formado en {Archivo}: {Dato}", archivo, args.RawRecord)
            };

            using var lector = new StreamReader(ruta);
            using var csv = new CsvReader(lector, config);

            // Validación de estructura: el archivo debe tener las columnas esperadas.
            await csv.ReadAsync();
            csv.ReadHeader();
            var encabezados = csv.HeaderRecord ?? Array.Empty<string>();
            var faltantes = ColumnasRequeridas.Where(c => !encabezados.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();
            if (faltantes.Count > 0)
                throw new InvalidDataException($"El archivo {archivo} no tiene las columnas: {string.Join(", ", faltantes)}");

            while (await csv.ReadAsync())
            {
                cancelacion.ThrowIfCancellationRequested();

                var id = csv.GetField("IdOpinion");
                if (string.IsNullOrWhiteSpace(id))
                {
                    descartadas++;   // sin identificador no se puede rastrear
                    continue;
                }

                resultado.Add(new OpinionCruda
                {
                    TipoFuente = TipoFuente.Csv,
                    IdOrigen = id,
                    IdCliente = Campo(csv, "IdCliente"),
                    IdProducto = Campo(csv, "IdProducto"),
                    Fecha = Campo(csv, "Fecha"),
                    Comentario = Campo(csv, "Comentario"),
                    Clasificacion = Campo(csv, "Clasificación", "Clasificacion"),
                    Puntaje = Campo(csv, "PuntajeSatisfacción", "PuntajeSatisfaccion"),
                    Canal = Campo(csv, "Fuente") ?? "EncuestaInterna"
                });
            }

            _logger.LogInformation("CSV {Archivo}: {Leidas} filas leídas, {Descartadas} descartadas",
                archivo, resultado.Count - antes, descartadas);
        }

        return resultado;
    }

    // Devuelve el valor de la primera columna que exista (acepta nombres con y sin tilde).
    private static string? Campo(CsvReader csv, params string[] nombres)
    {
        foreach (var nombre in nombres)
            if (csv.TryGetField<string>(nombre, out var valor))
                return string.IsNullOrWhiteSpace(valor) ? null : valor;
        return null;
    }
}
