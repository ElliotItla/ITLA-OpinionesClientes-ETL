namespace OpinionesClientes.Domain.Enums;

/// <summary>Tipos de fuente de donde se extraen las opiniones.</summary>
public enum TipoFuente
{
    Csv,        // Encuestas internas (archivo CSV)
    BaseDatos,  // Reseñas web (base de datos relacional)
    Api         // Comentarios de redes sociales (API REST)
}
