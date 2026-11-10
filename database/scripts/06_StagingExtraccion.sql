/* =====================================================================
   Actividad 1 - Área de staging de la extracción
   Crea (si no existe) la base analítica OpinionesClientesDW, el esquema
   "stg" y la tabla stg.Opiniones, donde el Worker guarda los datos tal
   como vienen de las 3 fuentes (CSV, BD y API), sin transformarlos.
   La transformación y la carga al modelo estrella se harán después.
   ===================================================================== */

IF DB_ID(N'OpinionesClientesDW') IS NULL
    CREATE DATABASE OpinionesClientesDW;
GO

USE OpinionesClientesDW;
GO

IF SCHEMA_ID(N'stg') IS NULL
    EXEC (N'CREATE SCHEMA stg');
GO

IF OBJECT_ID(N'stg.Opiniones', N'U') IS NOT NULL
    DROP TABLE stg.Opiniones;
GO

CREATE TABLE stg.Opiniones
(
    IdStaging        BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    IdLote           UNIQUEIDENTIFIER NOT NULL,      -- identifica cada ejecución del Worker
    TipoFuente       NVARCHAR(20)     NOT NULL,      -- Csv / BaseDatos / Api
    IdOrigen         NVARCHAR(50)     NULL,          -- id del registro en su fuente
    IdCliente        NVARCHAR(50)     NULL,
    IdProducto       NVARCHAR(50)     NULL,
    Fecha            NVARCHAR(50)     NULL,          -- texto: se convierte en la Transformación
    Comentario       NVARCHAR(MAX)    NULL,
    Clasificacion    NVARCHAR(50)     NULL,
    Puntaje          NVARCHAR(20)     NULL,          -- PuntajeSatisfacción o Rating
    Canal            NVARCHAR(50)     NULL,          -- EncuestaInterna, Instagram, Twitter...
    FechaExtraccion  DATETIME2(0)     NOT NULL DEFAULT SYSDATETIME()
);
GO

-- Consulta para revisar lo extraído en la última ejecución
-- SELECT TipoFuente, COUNT(*) AS Registros
-- FROM stg.Opiniones
-- WHERE IdLote = (SELECT TOP 1 IdLote FROM stg.Opiniones ORDER BY IdStaging DESC)
-- GROUP BY TipoFuente;
