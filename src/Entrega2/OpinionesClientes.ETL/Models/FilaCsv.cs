namespace OpinionesClientes.ETL.Models;

/// <summary>Una fila leída del CSV tal como venía, antes de validarla.</summary>
public class FilaCsv
{
    public int NumeroLinea { get; init; }

    /// <summary>Valores de la fila indexados por el nombre normalizado de la columna.</summary>
    public Dictionary<string, string> Campos { get; init; } = new();

    public int CantidadCampos { get; init; }
    public int CantidadEsperada { get; init; }

    /// <summary>Texto original de la fila (se guarda en el archivo de rechazos).</summary>
    public string Original { get; init; } = string.Empty;
}

/// <summary>Contenido completo de un archivo CSV.</summary>
public class ArchivoCsv
{
    public required string Nombre { get; init; }
    public required List<string> Encabezados { get; init; }
    public required List<FilaCsv> Filas { get; init; }
}
