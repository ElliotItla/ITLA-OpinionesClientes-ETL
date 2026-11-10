using Microsoft.Extensions.Logging;

namespace OpinionesClientes.Infrastructure.Logging;

/// <summary>
/// Proveedor de ILogger que, además de la consola, escribe cada mensaje en
/// logs/etl-AAAAMMDD.log. Así queda historial de cada ejecución (trazabilidad).
/// </summary>
public sealed class ArchivoLoggerProvider : ILoggerProvider
{
    private readonly string _carpeta;
    private readonly object _candado = new();

    public ArchivoLoggerProvider(string carpeta)
    {
        _carpeta = carpeta;
        Directory.CreateDirectory(_carpeta);
    }

    public ILogger CreateLogger(string categoria) => new ArchivoLogger(this, categoria);

    public void Dispose() { }

    internal void Escribir(string linea)
    {
        var ruta = Path.Combine(_carpeta, $"etl-{DateTime.Now:yyyyMMdd}.log");
        lock (_candado)   // varios extractores escriben a la vez
            File.AppendAllText(ruta, linea + Environment.NewLine);
    }

    private sealed class ArchivoLogger : ILogger
    {
        private readonly ArchivoLoggerProvider _proveedor;
        private readonly string _categoria;

        public ArchivoLogger(ArchivoLoggerProvider proveedor, string categoria)
        {
            _proveedor = proveedor;
            _categoria = categoria.Split('.').Last();   // nombre corto de la clase
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel nivel) => nivel >= LogLevel.Information;

        public void Log<TState>(LogLevel nivel, EventId id, TState estado, Exception? ex, Func<TState, Exception?, string> formato)
        {
            if (!IsEnabled(nivel)) return;
            var linea = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{nivel}] {_categoria}: {formato(estado, ex)}";
            if (ex is not null) linea += $"{Environment.NewLine}    {ex.GetType().Name}: {ex.Message}";
            _proveedor.Escribir(linea);
        }
    }
}
