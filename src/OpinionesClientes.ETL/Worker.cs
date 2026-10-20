namespace OpinionesClientes.ETL;

/// <summary>
/// Servicio en segundo plano: al iniciar la aplicación ejecuta el ETL
/// una vez de forma automática y luego cierra la aplicación.
/// </summary>
public class Worker : BackgroundService
{
    private readonly ProcesoEtl _proceso;
    private readonly IHostApplicationLifetime _cicloVida;
    private readonly ILogger<Worker> _logger;

    public Worker(ProcesoEtl proceso, IHostApplicationLifetime cicloVida, ILogger<Worker> logger)
    {
        _proceso = proceso;
        _cicloVida = cicloVida;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _proceso.EjecutarAsync(stoppingToken);
            Environment.ExitCode = 0;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("El proceso ETL fue cancelado.");
            Environment.ExitCode = 2;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "El proceso ETL terminó con un error grave: {Mensaje}", ex.Message);
            Environment.ExitCode = 1;
        }
        finally
        {
            _cicloVida.StopApplication();
        }
    }
}
