using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OpinionesClientes.Application.Interfaces;
using OpinionesClientes.Domain.Entidades;
using OpinionesClientes.Infrastructure.Configuracion;

namespace OpinionesClientes.Infrastructure.Staging;

/// <summary>
/// Guarda lo extraído en la tabla stg.Opiniones de OpinionesClientesDW.
/// Usa SqlBulkCopy: inserta miles de filas de una vez en lugar de un INSERT por fila.
/// </summary>
public class SqlStagingRepository : IStagingRepository
{
    private readonly OpcionesStaging _opciones;
    private readonly string _cadenaConexion;

    public SqlStagingRepository(IOptions<OpcionesStaging> opciones, IConfiguration configuracion)
    {
        _opciones = opciones.Value;
        _cadenaConexion = configuracion.GetConnectionString(_opciones.NombreConexion)
            ?? throw new InvalidOperationException($"Falta la cadena de conexión '{_opciones.NombreConexion}'.");
    }

    public string Nombre => $"SQL Server ({_opciones.Tabla})";

    public async Task GuardarAsync(Guid idLote, IReadOnlyList<OpinionCruda> registros, CancellationToken cancelacion)
    {
        if (registros.Count == 0) return;

        var tabla = new DataTable();
        tabla.Columns.Add("IdLote", typeof(Guid));
        tabla.Columns.Add("TipoFuente", typeof(string));
        tabla.Columns.Add("IdOrigen", typeof(string));
        tabla.Columns.Add("IdCliente", typeof(string));
        tabla.Columns.Add("IdProducto", typeof(string));
        tabla.Columns.Add("Fecha", typeof(string));
        tabla.Columns.Add("Comentario", typeof(string));
        tabla.Columns.Add("Clasificacion", typeof(string));
        tabla.Columns.Add("Puntaje", typeof(string));
        tabla.Columns.Add("Canal", typeof(string));

        foreach (var r in registros)
        {
            tabla.Rows.Add(idLote, r.TipoFuente.ToString(),
                (object?)r.IdOrigen ?? DBNull.Value, (object?)r.IdCliente ?? DBNull.Value,
                (object?)r.IdProducto ?? DBNull.Value, (object?)r.Fecha ?? DBNull.Value,
                (object?)r.Comentario ?? DBNull.Value, (object?)r.Clasificacion ?? DBNull.Value,
                (object?)r.Puntaje ?? DBNull.Value, (object?)r.Canal ?? DBNull.Value);
        }

        await using var conexion = new SqlConnection(_cadenaConexion);
        await conexion.OpenAsync(cancelacion);

        using var copia = new SqlBulkCopy(conexion)
        {
            DestinationTableName = _opciones.Tabla,
            BatchSize = _opciones.TamanoLote
        };
        foreach (DataColumn columna in tabla.Columns)
            copia.ColumnMappings.Add(columna.ColumnName, columna.ColumnName);

        await copia.WriteToServerAsync(tabla, cancelacion);
    }
}
