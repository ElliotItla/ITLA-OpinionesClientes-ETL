/* =============================================================
   Script 02: Procedimientos almacenados usados por el ETL
   Cada procedimiento valida duplicados e integridad antes de
   insertar. @Insertado = 1 si se insertó, 0 si ya existía.
   ============================================================= */
USE OpinionesClientesDB;
GO

/* ---------- Categoría: devuelve la existente o crea una nueva ---------- */
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

/* ---------- Producto (duplicado = mismo nombre) ---------- */
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

/* ---------- Cliente (duplicado = mismo email) ---------- */
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

/* ---------- Fuente de datos desde fuente_datos.csv (duplicado = mismo tipo y fecha) ---------- */
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

/* ---------- Fuente de la ejecución actual: un registro por cada archivo de opiniones cargado ---------- */
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

/* ---------- Opinión base (lo usan los tres procedimientos de abajo) ---------- */
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

/* ---------- Encuesta ---------- */
CREATE OR ALTER PROCEDURE dbo.sp_InsertarEncuesta
    @IdOrigen      NVARCHAR(20),
    @IdCliente     INT,
    @IdProducto    INT,
    @IdFuente      INT OUTPUT,   -- NULL la primera vez: se registra la fuente al insertar la primera opinión nueva
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

/* ---------- Reseña web ---------- */
CREATE OR ALTER PROCEDURE dbo.sp_InsertarResenaWeb
    @IdOrigen      NVARCHAR(20),
    @IdCliente     INT,
    @IdProducto    INT,
    @IdFuente      INT OUTPUT,   -- NULL la primera vez: se registra la fuente al insertar la primera opinión nueva
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

/* ---------- Comentario de red social ---------- */
CREATE OR ALTER PROCEDURE dbo.sp_InsertarComentarioSocial
    @IdOrigen      NVARCHAR(20),
    @IdCliente     INT = NULL,
    @IdProducto    INT,
    @IdFuente      INT OUTPUT,   -- NULL la primera vez: se registra la fuente al insertar la primera opinión nueva
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

/* ---------- Conteo de registros por tabla (resumen final del ETL) ---------- */
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
