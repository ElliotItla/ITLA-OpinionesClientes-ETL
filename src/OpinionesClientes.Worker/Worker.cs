using Microsoft.Extensions.Options;
using OpinionesClientes.Application.Servicios;

namespace OpinionesClientes.Worker;

public class OpcionesWorker
{
    /// <summary>Cada cuántos minutos repetir la extracción. 0 = ejecutar una sola vez y terminar.</summary>
    public int IntervaloMinutos { get; set; } = 0;
    public string CarpetaLogs { get; set; } = "logs";
}

/// <summary>
/// Servicio en segundo plano que lanza la extracción al iniciar
/// y, si se configura un intervalo, la repite periódicamente.
/// </summary>
public class Worker : BackgroundService
{
    private readonly ServicioExtraccion _servicio;
    private readonly OpcionesWorker _opciones;
    private readonly IHostApplicationLifetime _ciclo;
    private readonly ILogger<Worker> _logger;

    public Worker(ServicioExtraccion servicio, IOptions<OpcionesWorker> opciones,
                  IHostApplicationLifetime ciclo, ILogger<Worker> logger)
    {
        _servicio = servicio;
        _opciones = opciones.Value;
        _ciclo = ciclo;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        do
        {
            try
            {
                await _servicio.EjecutarAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "La extracción terminó con un error inesperado");
            }

            if (_opciones.IntervaloMinutos <= 0) break;

            _logger.LogInformation("Próxima extracción en {Minutos} minutos", _opciones.IntervaloMinutos);
            await Task.Delay(TimeSpan.FromMinutes(_opciones.IntervaloMinutos), stoppingToken);
        }
        while (!stoppingToken.IsCancellationRequested);

        if (_opciones.IntervaloMinutos <= 0)
            _ciclo.StopApplication();   // modo "una sola vez": cerrar al terminar
    }
}
