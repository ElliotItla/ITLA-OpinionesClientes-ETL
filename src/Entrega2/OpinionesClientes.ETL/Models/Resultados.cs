namespace OpinionesClientes.ETL.Models;

public enum EstadoFila
{
    Insertado,
    Duplicado,
    Rechazado
}

/// <summary>Resultado de procesar una fila.</summary>
public record ResultadoFila(EstadoFila Estado, string? Motivo = null)
{
    public static ResultadoFila Ok() => new(EstadoFila.Insertado);
    public static ResultadoFila Duplicado(string motivo) => new(EstadoFila.Duplicado, motivo);
    public static ResultadoFila Rechazado(string motivo) => new(EstadoFila.Rechazado, motivo);
}

/// <summary>Contadores de un archivo procesado.</summary>
public class ResultadoArchivo
{
    public required string Archivo { get; init; }
    public int Procesados { get; set; }
    public int Insertados { get; set; }
    public int Rechazados { get; set; }   // incluye los duplicados
    public int Duplicados { get; set; }
    public string? Error { get; set; }    // error que impidió procesar el archivo completo
}

/// <summary>Registro rechazado con el motivo (se exporta a CSV).</summary>
public record Rechazo(string Archivo, int Linea, string Motivo, string Contenido);
