/* =============================================================
   Script 04: Consultas de validación
   Ejecutar después del proceso ETL. Cada consulta indica qué
   resultado se espera si la carga fue correcta.
   ============================================================= */
USE OpinionesClientesDB;
GO

/* 1. Cantidad de registros cargados en cada tabla.
      Esperado: todas las tablas con datos. */
EXEC dbo.sp_ConteoTablas;

/* 2. Integridad referencial: registros "huérfanos" (que apuntan a un padre inexistente).
      Esperado: 0 en todas las filas. */
SELECT 'Producto sin categoría' AS Verificacion, COUNT(*) AS Cantidad
FROM dbo.Producto p LEFT JOIN dbo.Categoria c ON c.IdCategoria = p.IdCategoria
WHERE c.IdCategoria IS NULL
UNION ALL
SELECT 'Opinión con cliente inexistente', COUNT(*)
FROM dbo.Opinion o LEFT JOIN dbo.Cliente c ON c.IdCliente = o.IdCliente
WHERE o.IdCliente IS NOT NULL AND c.IdCliente IS NULL
UNION ALL
SELECT 'Opinión con producto inexistente', COUNT(*)
FROM dbo.Opinion o LEFT JOIN dbo.Producto p ON p.IdProducto = o.IdProducto
WHERE p.IdProducto IS NULL
UNION ALL
SELECT 'Opinión con fuente inexistente', COUNT(*)
FROM dbo.Opinion o LEFT JOIN dbo.FuenteDatos f ON f.IdFuente = o.IdFuente
WHERE f.IdFuente IS NULL
UNION ALL
SELECT 'Opinión con clasificación inexistente', COUNT(*)
FROM dbo.Opinion o LEFT JOIN dbo.Clasificacion cl ON cl.IdClasificacion = o.IdClasificacion
WHERE cl.IdClasificacion IS NULL;

/* 3. Cada opinión debe pertenecer a un solo tipo (encuesta, reseña web o red social).
      Esperado: ninguna fila. */
SELECT o.IdOpinion,
       CASE WHEN e.IdOpinion IS NOT NULL THEN 1 ELSE 0 END
     + CASE WHEN r.IdOpinion IS NOT NULL THEN 1 ELSE 0 END
     + CASE WHEN s.IdOpinion IS NOT NULL THEN 1 ELSE 0 END AS TiposAsignados
FROM dbo.Opinion o
LEFT JOIN dbo.Encuesta e         ON e.IdOpinion = o.IdOpinion
LEFT JOIN dbo.ResenaWeb r        ON r.IdOpinion = o.IdOpinion
LEFT JOIN dbo.ComentarioSocial s ON s.IdOpinion = o.IdOpinion
WHERE CASE WHEN e.IdOpinion IS NOT NULL THEN 1 ELSE 0 END
    + CASE WHEN r.IdOpinion IS NOT NULL THEN 1 ELSE 0 END
    + CASE WHEN s.IdOpinion IS NOT NULL THEN 1 ELSE 0 END <> 1;

/* 4. La suma de encuestas + reseñas + comentarios debe ser igual al total de opiniones.
      Esperado: Cuadra = 'Sí'. */
SELECT
    (SELECT COUNT(*) FROM dbo.Opinion)          AS TotalOpiniones,
    (SELECT COUNT(*) FROM dbo.Encuesta)         AS Encuestas,
    (SELECT COUNT(*) FROM dbo.ResenaWeb)        AS ResenasWeb,
    (SELECT COUNT(*) FROM dbo.ComentarioSocial) AS ComentariosSociales,
    CASE WHEN (SELECT COUNT(*) FROM dbo.Opinion) =
              (SELECT COUNT(*) FROM dbo.Encuesta)
            + (SELECT COUNT(*) FROM dbo.ResenaWeb)
            + (SELECT COUNT(*) FROM dbo.ComentarioSocial)
         THEN 'Sí' ELSE 'No' END AS Cuadra;

/* 5. Registros duplicados.
      Esperado: ninguna fila. */
SELECT 'Email de cliente repetido' AS Verificacion, Email AS Valor, COUNT(*) AS Veces
FROM dbo.Cliente GROUP BY Email HAVING COUNT(*) > 1
UNION ALL
SELECT 'Producto repetido', Nombre, COUNT(*)
FROM dbo.Producto GROUP BY Nombre HAVING COUNT(*) > 1
UNION ALL
SELECT 'Encuesta repetida', IdEncuestaOrigen, COUNT(*)
FROM dbo.Encuesta GROUP BY IdEncuestaOrigen HAVING COUNT(*) > 1
UNION ALL
SELECT 'Reseña web repetida', IdResenaOrigen, COUNT(*)
FROM dbo.ResenaWeb GROUP BY IdResenaOrigen HAVING COUNT(*) > 1
UNION ALL
SELECT 'Comentario social repetido', IdComentarioOrigen, COUNT(*)
FROM dbo.ComentarioSocial GROUP BY IdComentarioOrigen HAVING COUNT(*) > 1;

/* 6. Datos fuera de rango (puntajes fuera de 1-5 o fechas futuras).
      Esperado: 0 en todas las filas. */
SELECT 'Puntaje fuera de 1-5' AS Verificacion, COUNT(*) AS Cantidad
FROM dbo.Opinion WHERE PuntajeSatisfaccion NOT BETWEEN 1 AND 5
UNION ALL
SELECT 'Rating fuera de 1-5', COUNT(*)
FROM dbo.ResenaWeb WHERE Rating NOT BETWEEN 1 AND 5
UNION ALL
SELECT 'Fecha en el futuro', COUNT(*)
FROM dbo.Opinion WHERE Fecha > CAST(GETDATE() AS DATE);

/* 7. Prueba de integridad: intentar insertar una opinión con un producto que no existe.
      Esperado: error 547 (conflicto con la FOREIGN KEY) y no se guarda nada. */
BEGIN TRY
    BEGIN TRANSACTION;
    INSERT INTO dbo.Opinion (IdCliente, IdProducto, IdFuente, IdClasificacion, Fecha)
    VALUES (NULL, 999999, 1, 1, GETDATE());
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    SELECT ERROR_NUMBER() AS NumeroError, ERROR_MESSAGE() AS Mensaje;
END CATCH;

/* 8. Muestra de los datos cargados con todas sus relaciones (usa la vista). */
SELECT TOP 20 * FROM dbo.vw_OpinionesDetalle ORDER BY IdOpinion;

/* 9. Resúmenes analíticos (usan las vistas). */
SELECT * FROM dbo.vw_ResumenPorProducto ORDER BY TotalOpiniones DESC;
SELECT * FROM dbo.vw_ResumenPorClasificacion;
SELECT * FROM dbo.vw_ResumenPorFuente;
