﻿namespace Arisoul.T212.App.Storage;

public static class Constants
{
    public const string DatabaseFilename = "database.db3";

    public const SQLite.SQLiteOpenFlags Flags =
        // open the database in read/write mode
        SQLite.SQLiteOpenFlags.ReadWrite |
        // create the database if it doesn't exist
        SQLite.SQLiteOpenFlags.Create |
        // enable multi-threaded database access
        SQLite.SQLiteOpenFlags.SharedCache;

    public static string DatabasePath =>
        Path.Combine(FileSystem.AppDataDirectory, DatabaseFilename);

    public struct ParameterKeys
    {
        public const string LastDividendCursor = nameof(LastDividendCursor);
        public const string LastOrderCursor = nameof(LastOrderCursor);
    }
}