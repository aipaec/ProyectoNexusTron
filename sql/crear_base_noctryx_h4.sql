/* NOCTRYX H4: crea una base de práctica aislada y una tabla CRUD.
   Idempotente: no borra ni reemplaza una base o tabla existente. */
IF DB_ID(N'NOCTRYX_H4') IS NULL
    CREATE DATABASE NOCTRYX_H4;
GO

USE NOCTRYX_H4;
GO

IF OBJECT_ID(N'dbo.RegistrosPartida', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RegistrosPartida
    (
        RegistroId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RegistrosPartida PRIMARY KEY,
        Nombre NVARCHAR(60) NOT NULL,
        Mapa NVARCHAR(30) NOT NULL,
        Integridad INT NOT NULL,
        PosicionX REAL NULL,
        PosicionY REAL NULL,
        FechaGuardado DATETIME2(0) NOT NULL CONSTRAINT DF_RegistrosPartida_Fecha DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_RegistrosPartida_Integridad CHECK (Integridad BETWEEN 0 AND 100),
        CONSTRAINT CK_RegistrosPartida_Mapa CHECK (Mapa IN (N'CIUDAD EN COLAPSO', N'REALIDAD HOSTIL'))
    );
END;
GO

IF COL_LENGTH(N'dbo.RegistrosPartida', N'PosicionX') IS NULL
    ALTER TABLE dbo.RegistrosPartida ADD PosicionX REAL NULL;
IF COL_LENGTH(N'dbo.RegistrosPartida', N'PosicionY') IS NULL
    ALTER TABLE dbo.RegistrosPartida ADD PosicionY REAL NULL;
GO

SELECT RegistroId, Nombre, Mapa, Integridad, PosicionX, PosicionY, FechaGuardado
FROM dbo.RegistrosPartida
ORDER BY FechaGuardado DESC;
GO
