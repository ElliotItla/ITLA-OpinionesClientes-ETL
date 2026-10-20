IF DB_ID('OpinionesClientesDB') IS NULL
    CREATE DATABASE OpinionesClientesDB;
GO

USE OpinionesClientesDB;
GO

DROP TABLE IF EXISTS dbo.ComentarioSocial;
DROP TABLE IF EXISTS dbo.ResenaWeb;
DROP TABLE IF EXISTS dbo.Encuesta;
DROP TABLE IF EXISTS dbo.Opinion;
DROP TABLE IF EXISTS dbo.Clasificacion;
DROP TABLE IF EXISTS dbo.FuenteDatos;
DROP TABLE IF EXISTS dbo.Cliente;
DROP TABLE IF EXISTS dbo.Producto;
DROP TABLE IF EXISTS dbo.Categoria;
GO

CREATE TABLE dbo.Categoria (
    IdCategoria INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Categoria PRIMARY KEY,
    Nombre      NVARCHAR(100)     NOT NULL
);

CREATE TABLE dbo.Producto (
    IdProducto  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Producto PRIMARY KEY,
    Nombre      NVARCHAR(150)     NOT NULL,
    IdCategoria INT               NOT NULL
        CONSTRAINT FK_Producto_Categoria REFERENCES dbo.Categoria (IdCategoria)
);

CREATE TABLE dbo.Cliente (
    IdCliente INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Cliente PRIMARY KEY,
    Nombre    NVARCHAR(150)     NOT NULL,
    Email     NVARCHAR(150)     NOT NULL
);

CREATE TABLE dbo.FuenteDatos (
    IdFuente   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FuenteDatos PRIMARY KEY,
    TipoFuente NVARCHAR(50)      NOT NULL,
    FechaCarga DATETIME2(0)      NOT NULL CONSTRAINT DF_FuenteDatos_FechaCarga DEFAULT SYSDATETIME()
);

CREATE TABLE dbo.Clasificacion (
    IdClasificacion INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Clasificacion PRIMARY KEY,
    Nombre          NVARCHAR(20)      NOT NULL
);

CREATE TABLE dbo.Opinion (
    IdOpinion           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Opinion PRIMARY KEY,
    IdCliente           INT               NULL
        CONSTRAINT FK_Opinion_Cliente REFERENCES dbo.Cliente (IdCliente),
    IdProducto          INT               NOT NULL
        CONSTRAINT FK_Opinion_Producto REFERENCES dbo.Producto (IdProducto),
    IdFuente            INT               NOT NULL
        CONSTRAINT FK_Opinion_FuenteDatos REFERENCES dbo.FuenteDatos (IdFuente),
    IdClasificacion     INT               NOT NULL
        CONSTRAINT FK_Opinion_Clasificacion REFERENCES dbo.Clasificacion (IdClasificacion),
    Fecha               DATE              NOT NULL,
    Comentario          NVARCHAR(1000)    NULL,
    PuntajeSatisfaccion INT               NULL
);

CREATE TABLE dbo.Encuesta (
    IdOpinion        INT          NOT NULL CONSTRAINT PK_Encuesta PRIMARY KEY
        CONSTRAINT FK_Encuesta_Opinion REFERENCES dbo.Opinion (IdOpinion),
    IdEncuestaOrigen NVARCHAR(20) NOT NULL
);

CREATE TABLE dbo.ResenaWeb (
    IdOpinion      INT          NOT NULL CONSTRAINT PK_ResenaWeb PRIMARY KEY
        CONSTRAINT FK_ResenaWeb_Opinion REFERENCES dbo.Opinion (IdOpinion),
    IdResenaOrigen NVARCHAR(20) NOT NULL,
    Rating         INT          NOT NULL
);

CREATE TABLE dbo.ComentarioSocial (
    IdOpinion          INT          NOT NULL CONSTRAINT PK_ComentarioSocial PRIMARY KEY
        CONSTRAINT FK_ComentarioSocial_Opinion REFERENCES dbo.Opinion (IdOpinion),
    IdComentarioOrigen NVARCHAR(20) NOT NULL,
    RedSocial          NVARCHAR(50) NOT NULL
);
GO

INSERT INTO dbo.Clasificacion (Nombre) VALUES (N'Positiva'), (N'Neutral'), (N'Negativa');
GO

USE OpinionesClientesDB;
GO

CREATE OR ALTER PROCEDURE dbo.sp_ObtenerOInsertarCategoria
    @Nombre      NVARCHAR(100),
    @IdCategoria INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    IF @Nombre IS NULL OR LTRIM(RTRIM(@Nombre)) = ''
        THROW 50001, 'El nombre de la categoría es obligatorio.', 1;

    SET @IdCategoria = NULL;
    SELECT @IdCategoria = IdCategoria FROM dbo.Categoria WHERE Nombre = @Nombre;

    IF @IdCategoria IS NULL
    BEGIN
        INSERT INTO dbo.Categoria (Nombre) VALUES (@Nombre);
        SET @IdCategoria = SCOPE_IDENTITY();
    END
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_InsertarProducto
    @Nombre      NVARCHAR(150),
    @IdCategoria INT,
    @IdProducto  INT OUTPUT,
    @Insertado   BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    IF @Nombre IS NULL OR LTRIM(RTRIM(@Nombre)) = ''
        THROW 50002, 'El nombre del producto es obligatorio.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.Categoria WHERE IdCategoria = @IdCategoria)
        THROW 50003, 'La categoría indicada no existe.', 1;

    SET @IdProducto = NULL;
    SELECT @IdProducto = IdProducto FROM dbo.Producto WHERE Nombre = @Nombre;

    IF @IdProducto IS NOT NULL
    BEGIN
        SET @Insertado = 0;
        RETURN;
    END

    INSERT INTO dbo.Producto (Nombre, IdCategoria) VALUES (@Nombre, @IdCategoria);
    SET @IdProducto = SCOPE_IDENTITY();
    SET @Insertado = 1;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_InsertarCliente
    @Nombre    NVARCHAR(150),
    @Email     NVARCHAR(150),
    @IdCliente INT OUTPUT,
    @Insertado BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    IF @Nombre IS NULL OR LTRIM(RTRIM(@Nombre)) = ''
       OR @Email IS NULL OR LTRIM(RTRIM(@Email)) = ''
        THROW 50004, 'El nombre y el email del cliente son obligatorios.', 1;

    SET @IdCliente = NULL;
    SELECT @IdCliente = IdCliente FROM dbo.Cliente WHERE Email = @Email;

    IF @IdCliente IS NOT NULL
    BEGIN
        SET @Insertado = 0;
        RETURN;
    END

    INSERT INTO dbo.Cliente (Nombre, Email) VALUES (@Nombre, @Email);
    SET @IdCliente = SCOPE_IDENTITY();
    SET @Insertado = 1;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_InsertarFuenteDatos
    @TipoFuente NVARCHAR(50),
    @FechaCarga DATETIME2(0),
    @IdFuente   INT OUTPUT,
    @Insertado  BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    IF @TipoFuente IS NULL OR LTRIM(RTRIM(@TipoFuente)) = '' OR @FechaCarga IS NULL
        THROW 50005, 'El tipo y la fecha de carga de la fuente son obligatorios.', 1;

    SET @IdFuente = NULL;
    SELECT @IdFuente = IdFuente FROM dbo.FuenteDatos
    WHERE TipoFuente = @TipoFuente AND FechaCarga = @FechaCarga;

    IF @IdFuente IS NOT NULL
    BEGIN
        SET @Insertado = 0;
        RETURN;
    END

    INSERT INTO dbo.FuenteDatos (TipoFuente, FechaCarga) VALUES (@TipoFuente, @FechaCarga);
    SET @IdFuente = SCOPE_IDENTITY();
    SET @Insertado = 1;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_RegistrarFuente
    @TipoFuente NVARCHAR(50),
    @IdFuente   INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.FuenteDatos (TipoFuente, FechaCarga) VALUES (@TipoFuente, SYSDATETIME());
    SET @IdFuente = SCOPE_IDENTITY();
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_InsertarOpinion
    @IdCliente     INT,
    @IdProducto    INT,
    @IdFuente      INT,
    @Clasificacion NVARCHAR(20),
    @Fecha         DATE,
    @Comentario    NVARCHAR(1000),
    @Puntaje       INT,
    @IdOpinion     INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @IdClasificacion INT =
        (SELECT IdClasificacion FROM dbo.Clasificacion WHERE Nombre = @Clasificacion);

    IF @IdClasificacion IS NULL
        THROW 50010, 'La clasificación indicada no existe.', 1;
    IF @IdCliente IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.Cliente WHERE IdCliente = @IdCliente)
        THROW 50011, 'El cliente indicado no existe.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.Producto WHERE IdProducto = @IdProducto)
        THROW 50012, 'El producto indicado no existe.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.FuenteDatos WHERE IdFuente = @IdFuente)
        THROW 50013, 'La fuente de datos indicada no existe.', 1;

    INSERT INTO dbo.Opinion (IdCliente, IdProducto, IdFuente, IdClasificacion, Fecha, Comentario, PuntajeSatisfaccion)
    VALUES (@IdCliente, @IdProducto, @IdFuente, @IdClasificacion, @Fecha, @Comentario, @Puntaje);

    SET @IdOpinion = SCOPE_IDENTITY();
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_InsertarEncuesta
    @IdOrigen      NVARCHAR(20),
    @IdCliente     INT,
    @IdProducto    INT,
    @IdFuente      INT OUTPUT,
    @TipoFuente    NVARCHAR(50),
    @Clasificacion NVARCHAR(20),
    @Fecha         DATE,
    @Comentario    NVARCHAR(1000) = NULL,
    @Puntaje       INT,
    @Insertado     BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF EXISTS (SELECT 1 FROM dbo.Encuesta WHERE IdEncuestaOrigen = @IdOrigen)
    BEGIN
        SET @Insertado = 0;
        RETURN;
    END

    BEGIN TRY
        BEGIN TRANSACTION;
            IF @IdFuente IS NULL
                EXEC dbo.sp_RegistrarFuente @TipoFuente, @IdFuente OUTPUT;
            DECLARE @IdOpinion INT;
            EXEC dbo.sp_InsertarOpinion @IdCliente, @IdProducto, @IdFuente, @Clasificacion,
                                        @Fecha, @Comentario, @Puntaje, @IdOpinion OUTPUT;
            INSERT INTO dbo.Encuesta (IdOpinion, IdEncuestaOrigen) VALUES (@IdOpinion, @IdOrigen);
        COMMIT TRANSACTION;
        SET @Insertado = 1;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_InsertarResenaWeb
    @IdOrigen      NVARCHAR(20),
    @IdCliente     INT,
    @IdProducto    INT,
    @IdFuente      INT OUTPUT,
    @TipoFuente    NVARCHAR(50),
    @Clasificacion NVARCHAR(20),
    @Fecha         DATE,
    @Comentario    NVARCHAR(1000) = NULL,
    @Rating        INT,
    @Insertado     BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF EXISTS (SELECT 1 FROM dbo.ResenaWeb WHERE IdResenaOrigen = @IdOrigen)
    BEGIN
        SET @Insertado = 0;
        RETURN;
    END

    BEGIN TRY
        BEGIN TRANSACTION;
            IF @IdFuente IS NULL
                EXEC dbo.sp_RegistrarFuente @TipoFuente, @IdFuente OUTPUT;
            DECLARE @IdOpinion INT;
            EXEC dbo.sp_InsertarOpinion @IdCliente, @IdProducto, @IdFuente, @Clasificacion,
                                        @Fecha, @Comentario, @Rating, @IdOpinion OUTPUT;
            INSERT INTO dbo.ResenaWeb (IdOpinion, IdResenaOrigen, Rating) VALUES (@IdOpinion, @IdOrigen, @Rating);
        COMMIT TRANSACTION;
        SET @Insertado = 1;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_InsertarComentarioSocial
    @IdOrigen      NVARCHAR(20),
    @IdCliente     INT = NULL,
    @IdProducto    INT,
    @IdFuente      INT OUTPUT,
    @TipoFuente    NVARCHAR(50),
    @Clasificacion NVARCHAR(20),
    @Fecha         DATE,
    @Comentario    NVARCHAR(1000),
    @RedSocial     NVARCHAR(50),
    @Insertado     BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF EXISTS (SELECT 1 FROM dbo.ComentarioSocial WHERE IdComentarioOrigen = @IdOrigen)
    BEGIN
        SET @Insertado = 0;
        RETURN;
    END

    BEGIN TRY
        BEGIN TRANSACTION;
            IF @IdFuente IS NULL
                EXEC dbo.sp_RegistrarFuente @TipoFuente, @IdFuente OUTPUT;
            DECLARE @IdOpinion INT;
            EXEC dbo.sp_InsertarOpinion @IdCliente, @IdProducto, @IdFuente, @Clasificacion,
                                        @Fecha, @Comentario, NULL, @IdOpinion OUTPUT;
            INSERT INTO dbo.ComentarioSocial (IdOpinion, IdComentarioOrigen, RedSocial)
            VALUES (@IdOpinion, @IdOrigen, @RedSocial);
        COMMIT TRANSACTION;
        SET @Insertado = 1;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ConteoTablas
AS
BEGIN
    SET NOCOUNT ON;
    SELECT 'Categoria'        AS Tabla, COUNT(*) AS Registros FROM dbo.Categoria
    UNION ALL SELECT 'Producto',         COUNT(*) FROM dbo.Producto
    UNION ALL SELECT 'Cliente',          COUNT(*) FROM dbo.Cliente
    UNION ALL SELECT 'FuenteDatos',      COUNT(*) FROM dbo.FuenteDatos
    UNION ALL SELECT 'Clasificacion',    COUNT(*) FROM dbo.Clasificacion
    UNION ALL SELECT 'Opinion',          COUNT(*) FROM dbo.Opinion
    UNION ALL SELECT 'Encuesta',         COUNT(*) FROM dbo.Encuesta
    UNION ALL SELECT 'ResenaWeb',        COUNT(*) FROM dbo.ResenaWeb
    UNION ALL SELECT 'ComentarioSocial', COUNT(*) FROM dbo.ComentarioSocial;
END
GO

USE OpinionesClientesDB;
GO

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
