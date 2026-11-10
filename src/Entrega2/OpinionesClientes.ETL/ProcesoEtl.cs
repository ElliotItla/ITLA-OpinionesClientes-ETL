using System.Diagnostics;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using OpinionesClientes.ETL.Extract;
using OpinionesClientes.ETL.Load;
using OpinionesClientes.ETL.Models;
using OpinionesClientes.ETL.Transform;

namespace OpinionesClientes.ETL;

/// <summary>
/// Orquesta el proceso completo: Extract → Transform → Load para cada archivo,
/// respetando el orden de las relaciones (primero los padres, luego las opiniones).
/// </summary>
public class ProcesoEtl
{
    private readonly LectorCsv _lector;
    private readonly ValidadorRegistros _validador;
    private readonly RepositorioOpiniones _repositorio;
    private readonly EtlOptions _opciones;
    private readonly ILogger<ProcesoEtl> _logger;

    // Relación entre el Id que trae el CSV y el Id que generó la base de datos
    private readonly Dictionary<string, int> _clientes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _productos = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _categorias = new(StringComparer.OrdinalIgnoreCase);

    private readonly List<ResultadoArchivo> _resultados = new();
    private readonly List<Rechazo> _rechazos = new();

    public ProcesoEtl(LectorCsv lector, ValidadorRegistros validador, RepositorioOpiniones repositorio,
                      IOptions<EtlOptions> opciones, ILogger<ProcesoEtl> logger)
    {
        _lector = lector;
        _validador = validador;
        _repositorio = repositorio;
        _opciones = opciones.Value;
        _logger = logger;
    }

    public async Task EjecutarAsync(CancellationToken ct)
    {
        var cronometro = Stopwatch.StartNew();
        _logger.LogInformation("Iniciando proceso ETL. Carpeta de archivos: {Ruta}", Path.GetFullPath(_opciones.RutaCsv));

        await using var cn = await _repositorio.AbrirConexionAsync(ct);
        _logger.LogInformation("Conexión con la base de datos establecida.");

        // 1. Tablas padre
        await CargarFuentesAsync(cn, ct);
        await CargarClientesAsync(cn, ct);
        await CargarProductosAsync(cn, ct);

        // 2. Opiniones (dependen de clientes y productos)
        await CargarOpinionesAsync(cn, TipoFuente.Encuesta, _opciones.ArchivoEncuestas, ct);
        await CargarOpinionesAsync(cn, TipoFuente.ResenaWeb, _opciones.ArchivoResenasWeb, ct);
        await CargarOpinionesAsync(cn, TipoFuente.ComentarioSocial, _opciones.ArchivoComentariosSociales, ct);

        cronometro.Stop();
        var rutaRechazos = GuardarRechazos();
        var conteo = await _repositorio.ConteoTablasAsync(cn, ct);
        MostrarResumen(cronometro.Elapsed, rutaRechazos, conteo);
    }

    // ------------------------------------------------------------------ fuentes de datos

    private Task CargarFuentesAsync(SqlConnection cn, CancellationToken ct)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tipoYFecha = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return ProcesarArchivoAsync(_opciones.ArchivoFuentes, ValidadorRegistros.ColumnasFuentes, async fila =>
        {
            if (!_validador.TryValidarFuente(fila, out var fuente, out var error))
                return ResultadoFila.Rechazado(error);
            if (!ids.Add(fuente.IdOrigen))
                return ResultadoFila.Duplicado($"IdFuente '{fuente.IdOrigen}' repetido en el archivo");
            if (!tipoYFecha.Add($"{fuente.TipoFuente}|{fuente.FechaCarga:yyyy-MM-dd}"))
                return ResultadoFila.Duplicado($"Ya hay una fuente '{fuente.TipoFuente}' con fecha {fuente.FechaCarga:yyyy-MM-dd} en el archivo");

            return await _repositorio.InsertarFuenteDatosAsync(cn, fuente, ct)
                ? ResultadoFila.Ok()
                : ResultadoFila.Duplicado($"La fuente '{fuente.TipoFuente}' del {fuente.FechaCarga:yyyy-MM-dd} ya existe en la base de datos");
        }, ct);
    }

    // ------------------------------------------------------------------ clientes

    private Task CargarClientesAsync(SqlConnection cn, CancellationToken ct)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var emails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return ProcesarArchivoAsync(_opciones.ArchivoClientes, ValidadorRegistros.ColumnasClientes, async fila =>
        {
            if (!_validador.TryValidarCliente(fila, out var cliente, out var error))
                return ResultadoFila.Rechazado(error);
            if (!ids.Add(cliente.IdOrigen))
                return ResultadoFila.Duplicado($"IdCliente '{cliente.IdOrigen}' repetido en el archivo");
            if (!emails.Add(cliente.Email))
                return ResultadoFila.Duplicado($"Email '{cliente.Email}' repetido en el archivo");

            var (id, insertado) = await _repositorio.InsertarClienteAsync(cn, cliente, ct);
            _clientes[cliente.IdOrigen] = id;   // se guarda aunque ya existiera, para enlazar sus opiniones
            return insertado
                ? ResultadoFila.Ok()
                : ResultadoFila.Duplicado($"El cliente con email '{cliente.Email}' ya existe en la base de datos");
        }, ct);
    }

    // ------------------------------------------------------------------ productos

    private Task CargarProductosAsync(SqlConnection cn, CancellationToken ct)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var nombres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return ProcesarArchivoAsync(_opciones.ArchivoProductos, ValidadorRegistros.ColumnasProductos, async fila =>
        {
            if (!_validador.TryValidarProducto(fila, out var producto, out var error))
                return ResultadoFila.Rechazado(error);
            if (!ids.Add(producto.IdOrigen))
                return ResultadoFila.Duplicado($"IdProducto '{producto.IdOrigen}' repetido en el archivo");
            if (!nombres.Add(producto.Nombre))
                return ResultadoFila.Duplicado($"Producto '{producto.Nombre}' repetido en el archivo");

            if (!_categorias.TryGetValue(producto.Categoria, out var idCategoria))
            {
                idCategoria = await _repositorio.ObtenerOInsertarCategoriaAsync(cn, producto.Categoria, ct);
                _categorias[producto.Categoria] = idCategoria;
            }

            var (id, insertado) = await _repositorio.InsertarProductoAsync(cn, producto, idCategoria, ct);
            _productos[producto.IdOrigen] = id;
            return insertado
                ? ResultadoFila.Ok()
                : ResultadoFila.Duplicado($"El producto '{producto.Nombre}' ya existe en la base de datos");
        }, ct);
    }

    // ------------------------------------------------------------------ opiniones

    private Task CargarOpinionesAsync(SqlConnection cn, TipoFuente tipo, string archivo, CancellationToken ct)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int? idFuente = null;

        return ProcesarArchivoAsync(archivo, ValidadorRegistros.ColumnasOpinion(tipo), async fila =>
        {
            if (!_validador.TryValidarOpinion(tipo, fila, out var opinion, out var error))
                return ResultadoFila.Rechazado(error);
            if (!ids.Add(opinion.IdOrigen))
                return ResultadoFila.Duplicado($"Id '{opinion.IdOrigen}' repetido en el archivo");

            // Integridad referencial: el cliente y el producto deben haberse cargado antes
            int? idCliente = null;
            if (opinion.IdClienteOrigen is not null)
            {
                if (!_clientes.TryGetValue(opinion.IdClienteOrigen, out var idC))
                    return ResultadoFila.Rechazado($"Integridad referencial: el cliente '{opinion.IdClienteOrigen}' no existe");
                idCliente = idC;
            }
            if (!_productos.TryGetValue(opinion.IdProductoOrigen, out var idProducto))
                return ResultadoFila.Rechazado($"Integridad referencial: el producto '{opinion.IdProductoOrigen}' no existe");

            // La fuente (una por archivo) se registra junto con la primera opinión nueva
            var (insertado, fuente) = await _repositorio.InsertarOpinionAsync(
                cn, opinion, idCliente, idProducto, idFuente, NombreFuente(tipo), ct);
            idFuente = fuente;
            return insertado
                ? ResultadoFila.Ok()
                : ResultadoFila.Duplicado($"La opinión '{opinion.IdOrigen}' ya existe en la base de datos");
        }, ct);
    }

    private static string NombreFuente(TipoFuente tipo) => tipo switch
    {
        TipoFuente.Encuesta => "Encuesta Interna",
        TipoFuente.ResenaWeb => "Web",
        _ => "Red Social"
    };

    // ------------------------------------------------------------------ procesamiento común

    /// <summary>
    /// Lee el archivo, valida su estructura y procesa fila por fila llevando los contadores.
    /// Los errores de una fila no detienen el proceso: la fila se rechaza y se continúa.
    /// </summary>
    private async Task ProcesarArchivoAsync(string nombreArchivo, IEnumerable<string> columnas,
                                            Func<FilaCsv, Task<ResultadoFila>> procesarFila, CancellationToken ct)
    {
        var resultado = new ResultadoArchivo { Archivo = nombreArchivo };
        _resultados.Add(resultado);
        _logger.LogInformation("Procesando {Archivo}...", nombreArchivo);

        ArchivoCsv archivo;
        try
        {
            archivo = _lector.Leer(Path.Combine(_opciones.RutaCsv, nombreArchivo));   // EXTRACT
            _validador.ValidarEstructura(archivo, columnas);                           // estructura
        }
        catch (Exception ex) when (ex is FileNotFoundException or ExcepcionEstructuraCsv or IOException)
        {
            resultado.Error = ex.Message;
            _logger.LogError("No se pudo procesar {Archivo}: {Mensaje}", nombreArchivo, ex.Message);
            return;
        }

        foreach (var fila in archivo.Filas)
        {
            ct.ThrowIfCancellationRequested();
            resultado.Procesados++;

            ResultadoFila r;
            try
            {
                r = await procesarFila(fila);                                          // TRANSFORM + LOAD
            }
            catch (SqlException ex)
            {
                r = ResultadoFila.Rechazado($"Error de base de datos ({ex.Number}): {ex.Message}");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                r = ResultadoFila.Rechazado($"Error inesperado: {ex.Message}");
            }

            switch (r.Estado)
            {
                case EstadoFila.Insertado:
                    resultado.Insertados++;
                    break;
                case EstadoFila.Duplicado:
                    resultado.Rechazados++;
                    resultado.Duplicados++;
                    _rechazos.Add(new Rechazo(nombreArchivo, fila.NumeroLinea, "Duplicado: " + r.Motivo, fila.Original));
                    break;
                default:
                    resultado.Rechazados++;
                    _rechazos.Add(new Rechazo(nombreArchivo, fila.NumeroLinea, r.Motivo ?? "Sin motivo", fila.Original));
                    break;
            }
        }

        _logger.LogInformation("{Archivo}: {P} procesados, {I} insertados, {R} rechazados.",
            nombreArchivo, resultado.Procesados, resultado.Insertados, resultado.Rechazados);
    }

    // ------------------------------------------------------------------ salida

    private string? GuardarRechazos()
    {
        if (_rechazos.Count == 0) return null;
        try
        {
            Directory.CreateDirectory(_opciones.RutaRechazos);
            var ruta = Path.Combine(_opciones.RutaRechazos, $"rechazos_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            var sb = new StringBuilder("Archivo,Linea,Motivo,Contenido\n");
            foreach (var r in _rechazos)
                sb.AppendLine($"{Csv(r.Archivo)},{r.Linea},{Csv(r.Motivo)},{Csv(r.Contenido)}");
            File.WriteAllText(ruta, sb.ToString(), new UTF8Encoding(true));   // con BOM para que Excel lea las tildes
            return Path.GetFullPath(ruta);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("No se pudo guardar el archivo de rechazos: {Mensaje}", ex.Message);
            return null;
        }
    }

    private static string Csv(string valor) => "\"" + valor.Replace("\"", "\"\"") + "\"";

    private void MostrarResumen(TimeSpan duracion, string? rutaRechazos, List<(string Tabla, int Registros)> conteo)
    {
        const string linea = "+---------------------------+------------+------------+------------+------------+";
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("=================== RESUMEN DEL PROCESO ETL ===================");
        sb.AppendLine($"Fecha de ejecución : {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
        sb.AppendLine($"Duración           : {duracion.TotalSeconds:N2} segundos");
        sb.AppendLine(linea);
        sb.AppendLine($"| {"Archivo",-25} | {"Procesados",10} | {"Insertados",10} | {"Rechazados",10} | {"Duplicados",10} |");
        sb.AppendLine(linea);
        foreach (var r in _resultados)
        {
            sb.AppendLine($"| {r.Archivo,-25} | {r.Procesados,10} | {r.Insertados,10} | {r.Rechazados,10} | {r.Duplicados,10} |");
            if (r.Error is not null) sb.AppendLine($"|   ERROR: {r.Error}");
        }
        sb.AppendLine(linea);
        sb.AppendLine($"| {"TOTAL",-25} | {_resultados.Sum(r => r.Procesados),10} | {_resultados.Sum(r => r.Insertados),10} | " +
                      $"{_resultados.Sum(r => r.Rechazados),10} | {_resultados.Sum(r => r.Duplicados),10} |");
        sb.AppendLine(linea);

        if (_rechazos.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Detalle de registros rechazados:");
            foreach (var r in _rechazos.Take(30))
                sb.AppendLine($"  - {r.Archivo} (línea {r.Linea}): {r.Motivo}");
            if (_rechazos.Count > 30) sb.AppendLine($"  ... y {_rechazos.Count - 30} más.");
            if (rutaRechazos is not null) sb.AppendLine($"Listado completo en: {rutaRechazos}");
        }

        sb.AppendLine();
        sb.AppendLine("Registros en la base de datos:");
        foreach (var (tabla, registros) in conteo)
            sb.AppendLine($"  {tabla,-18}: {registros}");

        var errores = _resultados.Count(r => r.Error is not null);
        sb.AppendLine();
        sb.AppendLine(errores == 0
            ? "Proceso finalizado correctamente."
            : $"Proceso finalizado con {errores} archivo(s) que no se pudieron procesar.");
        sb.AppendLine("===============================================================");

        Console.WriteLine(sb.ToString());
    }
}
