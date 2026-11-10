using System.Data;
using Microsoft.Data.SqlClient;
using OpinionesClientes.ETL.Models;

namespace OpinionesClientes.ETL.Load;

/// <summary>
/// LOAD: inserta los datos en SQL Server llamando a los procedimientos almacenados.
/// Los procedimientos validan duplicados e integridad referencial antes de insertar.
/// </summary>
public class RepositorioOpiniones
{
    private readonly string _cadenaConexion;

    public RepositorioOpiniones(string cadenaConexion) => _cadenaConexion = cadenaConexion;

    public async Task<SqlConnection> AbrirConexionAsync(CancellationToken ct)
    {
        var conexion = new SqlConnection(_cadenaConexion);
        await conexion.OpenAsync(ct);
        return conexion;
    }

    public async Task<int> ObtenerOInsertarCategoriaAsync(SqlConnection cn, string nombre, CancellationToken ct)
    {
        using var cmd = Procedimiento(cn, "dbo.sp_ObtenerOInsertarCategoria");
        cmd.Parameters.Add("@Nombre", SqlDbType.NVarChar, 100).Value = nombre;
        var id = Salida(cmd, "@IdCategoria", SqlDbType.Int);
        await cmd.ExecuteNonQueryAsync(ct);
        return (int)id.Value;
    }

    public async Task<(int Id, bool Insertado)> InsertarProductoAsync(SqlConnection cn, ProductoDto p, int idCategoria, CancellationToken ct)
    {
        using var cmd = Procedimiento(cn, "dbo.sp_InsertarProducto");
        cmd.Parameters.Add("@Nombre", SqlDbType.NVarChar, 150).Value = p.Nombre;
        cmd.Parameters.Add("@IdCategoria", SqlDbType.Int).Value = idCategoria;
        var id = Salida(cmd, "@IdProducto", SqlDbType.Int);
        var insertado = Salida(cmd, "@Insertado", SqlDbType.Bit);
        await cmd.ExecuteNonQueryAsync(ct);
        return ((int)id.Value, (bool)insertado.Value);
    }

    public async Task<(int Id, bool Insertado)> InsertarClienteAsync(SqlConnection cn, ClienteDto c, CancellationToken ct)
    {
        using var cmd = Procedimiento(cn, "dbo.sp_InsertarCliente");
        cmd.Parameters.Add("@Nombre", SqlDbType.NVarChar, 150).Value = c.Nombre;
        cmd.Parameters.Add("@Email", SqlDbType.NVarChar, 150).Value = c.Email;
        var id = Salida(cmd, "@IdCliente", SqlDbType.Int);
        var insertado = Salida(cmd, "@Insertado", SqlDbType.Bit);
        await cmd.ExecuteNonQueryAsync(ct);
        return ((int)id.Value, (bool)insertado.Value);
    }

    public async Task<bool> InsertarFuenteDatosAsync(SqlConnection cn, FuenteDto f, CancellationToken ct)
    {
        using var cmd = Procedimiento(cn, "dbo.sp_InsertarFuenteDatos");
        cmd.Parameters.Add("@TipoFuente", SqlDbType.NVarChar, 50).Value = f.TipoFuente;
        cmd.Parameters.Add("@FechaCarga", SqlDbType.DateTime2).Value = f.FechaCarga;
        Salida(cmd, "@IdFuente", SqlDbType.Int);
        var insertado = Salida(cmd, "@Insertado", SqlDbType.Bit);
        await cmd.ExecuteNonQueryAsync(ct);
        return (bool)insertado.Value;
    }

    public async Task<int> RegistrarFuenteAsync(SqlConnection cn, string tipoFuente, CancellationToken ct)
    {
        using var cmd = Procedimiento(cn, "dbo.sp_RegistrarFuente");
        cmd.Parameters.Add("@TipoFuente", SqlDbType.NVarChar, 50).Value = tipoFuente;
        var id = Salida(cmd, "@IdFuente", SqlDbType.Int);
        await cmd.ExecuteNonQueryAsync(ct);
        return (int)id.Value;
    }

    /// <summary>Inserta la opinión y su subtipo (encuesta, reseña o comentario) en una sola transacción.</summary>
    /// <remarks>
    /// idFuente llega en null la primera vez: el procedimiento registra la fuente solo cuando
    /// inserta la primera opinión nueva del archivo y devuelve su Id para las siguientes.
    /// Así una carga sin opiniones nuevas no deja fuentes vacías.
    /// </remarks>
    public async Task<(bool Insertado, int? IdFuente)> InsertarOpinionAsync(SqlConnection cn, OpinionDto o, int? idCliente,
                                                                       int idProducto, int? idFuente, string tipoFuente, CancellationToken ct)
    {
        var procedimiento = o.Tipo switch
        {
            TipoFuente.Encuesta => "dbo.sp_InsertarEncuesta",
            TipoFuente.ResenaWeb => "dbo.sp_InsertarResenaWeb",
            _ => "dbo.sp_InsertarComentarioSocial"
        };

        using var cmd = Procedimiento(cn, procedimiento);
        cmd.Parameters.Add("@IdOrigen", SqlDbType.NVarChar, 20).Value = o.IdOrigen;
        cmd.Parameters.Add("@IdCliente", SqlDbType.Int).Value = (object?)idCliente ?? DBNull.Value;
        cmd.Parameters.Add("@IdProducto", SqlDbType.Int).Value = idProducto;
        var pFuente = cmd.Parameters.Add("@IdFuente", SqlDbType.Int);
        pFuente.Direction = ParameterDirection.InputOutput;
        pFuente.Value = (object?)idFuente ?? DBNull.Value;
        cmd.Parameters.Add("@TipoFuente", SqlDbType.NVarChar, 50).Value = tipoFuente;
        cmd.Parameters.Add("@Clasificacion", SqlDbType.NVarChar, 20).Value = o.Clasificacion;
        cmd.Parameters.Add("@Fecha", SqlDbType.Date).Value = o.Fecha;
        cmd.Parameters.Add("@Comentario", SqlDbType.NVarChar, 1000).Value = (object?)o.Comentario ?? DBNull.Value;

        switch (o.Tipo)
        {
            case TipoFuente.Encuesta:
                cmd.Parameters.Add("@Puntaje", SqlDbType.Int).Value = o.Puntaje!.Value;
                break;
            case TipoFuente.ResenaWeb:
                cmd.Parameters.Add("@Rating", SqlDbType.Int).Value = o.Rating!.Value;
                break;
            default:
                cmd.Parameters.Add("@RedSocial", SqlDbType.NVarChar, 50).Value = o.RedSocial!;
                break;
        }

        var insertado = Salida(cmd, "@Insertado", SqlDbType.Bit);
        await cmd.ExecuteNonQueryAsync(ct);
        int? fuente = pFuente.Value is DBNull or null ? null : (int)pFuente.Value;
        return ((bool)insertado.Value, fuente);
    }

    /// <summary>Cantidad de registros en cada tabla al terminar la carga.</summary>
    public async Task<List<(string Tabla, int Registros)>> ConteoTablasAsync(SqlConnection cn, CancellationToken ct)
    {
        var lista = new List<(string, int)>();
        using var cmd = Procedimiento(cn, "dbo.sp_ConteoTablas");
        using var lector = await cmd.ExecuteReaderAsync(ct);
        while (await lector.ReadAsync(ct))
            lista.Add((lector.GetString(0), lector.GetInt32(1)));
        return lista;
    }

    private static SqlCommand Procedimiento(SqlConnection cn, string nombre) =>
        new(nombre, cn) { CommandType = CommandType.StoredProcedure };

    private static SqlParameter Salida(SqlCommand cmd, string nombre, SqlDbType tipo)
    {
        var p = cmd.Parameters.Add(nombre, tipo);
        p.Direction = ParameterDirection.Output;
        return p;
    }
}
