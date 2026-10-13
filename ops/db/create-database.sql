-- Creates the T212Ext database and its own login on traceon's SQL Server.
-- Run through ops/db/create-database.sh, which supplies $(T212EXT_DB_PASSWORD) from .env.
-- Safe to run again: it creates what is missing and sets the login's password to the one in .env.
SET NOCOUNT ON;

IF DB_ID(N'T212Ext') IS NULL
    CREATE DATABASE [T212Ext];
GO

-- Full backups run nightly and there are no log backups: with the FULL model the log would grow forever.
ALTER DATABASE [T212Ext] SET RECOVERY SIMPLE;
GO

IF SUSER_ID(N't212ext') IS NULL
    CREATE LOGIN [t212ext] WITH PASSWORD = N'$(T212EXT_DB_PASSWORD)', DEFAULT_DATABASE = [T212Ext], CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
ELSE
    ALTER LOGIN [t212ext] WITH PASSWORD = N'$(T212EXT_DB_PASSWORD)';
GO

-- Owner of its own database only (the app applies its migrations), nothing on TraceonDb or the server.
USE [T212Ext];
IF USER_ID(N't212ext') IS NULL
    CREATE USER [t212ext] FOR LOGIN [t212ext];
ELSE
    -- After a restore on another server the user keeps the old login's id: map it to this login.
    ALTER USER [t212ext] WITH LOGIN = [t212ext];
ALTER ROLE db_owner ADD MEMBER [t212ext];
GO

SELECT name, recovery_model_desc, state_desc FROM sys.databases WHERE name = N'T212Ext';
GO
