/* =============================================================
   Script 03: Vistas
   ============================================================= */
USE OpinionesClientesDB;
GO

/* Detalle completo de cada opinión con todos sus datos relacionados */
CREATE OR ALTER VIEW dbo.vw_OpinionesDetalle
AS
SELECT
    o.IdOpinion,
    o.Fecha,
    CASE
        WHEN e.IdOpinion IS NOT NULL THEN N'Encuesta'
        WHEN r.IdOpinion IS NOT NULL THEN N'Reseña Web'
        WHEN s.IdOpinion IS NOT NULL THEN N'Red Social'
    END                                                         AS TipoOpinion,
    COALESCE(e.IdEncuestaOrigen, r.IdResenaOrigen, s.IdComentarioOrigen) AS IdOrigen,
    c.Nombre                                                    AS Cliente,
    c.Email,
    p.Nombre                                                    AS Producto,
    cat.Nombre                                                  AS Categoria,
    cl.Nombre                                                   AS Clasificacion,
    o.PuntajeSatisfaccion,
    r.Rating,
    s.RedSocial,
    o.Comentario,
    f.TipoFuente,
    f.FechaCarga
FROM dbo.Opinion o
LEFT  JOIN dbo.Cliente          c   ON c.IdCliente        = o.IdCliente
INNER JOIN dbo.Producto         p   ON p.IdProducto       = o.IdProducto
INNER JOIN dbo.Categoria        cat ON cat.IdCategoria    = p.IdCategoria
INNER JOIN dbo.Clasificacion    cl  ON cl.IdClasificacion = o.IdClasificacion
INNER JOIN dbo.FuenteDatos      f   ON f.IdFuente         = o.IdFuente
LEFT  JOIN dbo.Encuesta         e   ON e.IdOpinion        = o.IdOpinion
LEFT  JOIN dbo.ResenaWeb        r   ON r.IdOpinion        = o.IdOpinion
LEFT  JOIN dbo.ComentarioSocial s   ON s.IdOpinion        = o.IdOpinion;
GO

/* Resumen de opiniones por producto */
CREATE OR ALTER VIEW dbo.vw_ResumenPorProducto
AS
SELECT
    p.IdProducto,
    p.Nombre                                                           AS Producto,
    cat.Nombre                                                         AS Categoria,
    COUNT(o.IdOpinion)                                                 AS TotalOpiniones,
    SUM(CASE WHEN cl.Nombre = N'Positiva' THEN 1 ELSE 0 END)           AS Positivas,
    SUM(CASE WHEN cl.Nombre = N'Neutral'  THEN 1 ELSE 0 END)           AS Neutrales,
    SUM(CASE WHEN cl.Nombre = N'Negativa' THEN 1 ELSE 0 END)           AS Negativas,
    CAST(AVG(CAST(o.PuntajeSatisfaccion AS DECIMAL(5,2))) AS DECIMAL(5,2)) AS PromedioSatisfaccion
FROM dbo.Producto p
INNER JOIN dbo.Categoria     cat ON cat.IdCategoria    = p.IdCategoria
LEFT  JOIN dbo.Opinion       o   ON o.IdProducto       = p.IdProducto
LEFT  JOIN dbo.Clasificacion cl  ON cl.IdClasificacion = o.IdClasificacion
GROUP BY p.IdProducto, p.Nombre, cat.Nombre;
GO

/* Distribución de opiniones por clasificación */
CREATE OR ALTER VIEW dbo.vw_ResumenPorClasificacion
AS
SELECT
    cl.Nombre                                                        AS Clasificacion,
    COUNT(o.IdOpinion)                                               AS TotalOpiniones,
    CAST(100.0 * COUNT(o.IdOpinion) / NULLIF(SUM(COUNT(o.IdOpinion)) OVER (), 0) AS DECIMAL(5,2)) AS Porcentaje
FROM dbo.Clasificacion cl
LEFT JOIN dbo.Opinion o ON o.IdClasificacion = cl.IdClasificacion
GROUP BY cl.Nombre;
GO

/* Opiniones cargadas por cada tipo de fuente */
CREATE OR ALTER VIEW dbo.vw_ResumenPorFuente
AS
SELECT
    f.TipoFuente,
    COUNT(DISTINCT f.IdFuente) AS CantidadCargas,
    COUNT(o.IdOpinion)         AS TotalOpiniones,
    MAX(f.FechaCarga)          AS UltimaCarga
FROM dbo.FuenteDatos f
LEFT JOIN dbo.Opinion o ON o.IdFuente = f.IdFuente
GROUP BY f.TipoFuente;
GO
