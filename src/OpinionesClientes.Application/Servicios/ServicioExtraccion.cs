using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpinionesClientes.Application.Interfaces;
using OpinionesClientes.Domain.Entidades;

namespace OpinionesClientes.Application.Servicios;

/// <summary>
/// Orquesta la fase de EXTRACCIÓN:
/// 1) ejecuta todos los extractores registrados AL MISMO TIEMPO (Task.WhenAll),
/// 2) mide cuánto tarda cada uno con Stopwatch,
/// 3) guarda todo lo extraído en staging.
/// No conoce las clases concretas (CSV, BD, API): solo la interfaz IExtractor.
/// </summary>
public class ServicioExtraccion
{
    private readonly IEnumerable<IExtractor> _extractores;
    private readonly IEnumerable<IStagingRepository> _stagings;
    private readonly ILogger<ServicioExtraccion> _logger;

    public ServicioExtraccion(
        IEnumerable<IExtractor> extractores,
        IEnumerable<IStagingRepository> stagings,
        ILogger<ServicioExtraccion> logger)
    {
        _extractores = extractores;
        _stagings = stagings;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ResultadoExtraccion>> EjecutarAsync(CancellationToken cancelacion)
    {
        var idLote = Guid.NewGuid();
        var cronometroTotal = Stopwatch.StartNew();
        _logger.LogInformation("=== Inicio de extracción. Lote {IdLote} ===", idLote);

        // Paralelismo: las 3 fuentes se leen a la vez en lugar de una detrás de otra.
        var tareas = _extractores.Select(e => EjecutarExtractorAsync(e, cancelacion));
        var resultados = await Task.WhenAll(tareas);

        var registros = resultados.Where(r => r.Exitoso).SelectMany(r => r.Registros).ToList();

        foreach (var staging in _stagings)
        {
            try
            {
                var cronometro = Stopwatch.StartNew();
                await staging.GuardarAsync(idLote, registros, cancelacion);
                _logger.LogInformation("Staging {Staging}: {Total} registros guardados en {Ms} ms",
                    staging.Nombre, registros.Count, cronometro.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar en staging {Staging}", staging.Nombre);
            }
        }

        cronometroTotal.Stop();
        foreach (var r in resultados)
        {
            if (r.Exitoso)
                _logger.LogInformation("Resumen {Extractor}: {Total} registros en {Ms} ms",
                    r.Extractor, r.Registros.Count, (long)r.Duracion.TotalMilliseconds);
            else
                _logger.LogWarning("Resumen {Extractor}: FALLÓ ({Error})", r.Extractor, r.Error);
        }
        _logger.LogInformation("=== Fin de extracción. {Total} registros en {Ms} ms ===",
            registros.Count, cronometroTotal.ElapsedMilliseconds);

        return resultados;
    }

    // Cada extractor se protege por separado: si una fuente falla, las demás siguen.
    private async Task<ResultadoExtraccion> EjecutarExtractorAsync(IExtractor extractor, CancellationToken cancelacion)
    {
        var cronometro = Stopwatch.StartNew();
        try
        {
            _logger.LogInformation("Extrayendo de {Extractor}...", extractor.Nombre);
            var datos = await extractor.ExtraerAsync(cancelacion);
            cronometro.Stop();
            return new ResultadoExtraccion(extractor.Nombre, extractor.TipoFuente, datos, cronometro.Elapsed);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            cronometro.Stop();
            _logger.LogError(ex, "Error en {Extractor}", extractor.Nombre);
            return new ResultadoExtraccion(extractor.Nombre, extractor.TipoFuente, Array.Empty<OpinionCruda>(), cronometro.Elapsed, ex.Message);
        }
    }
}
