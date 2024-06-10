using Npgsql;
using System;
using UnityEditor;
using UnityEngine;

public static class DbCommonFunctions
{

    public static string GetNpgsqlConnectionString()
    {
        var dbConnection = Resources.Load<DBConnectionData>("ConnectionData");

        return dbConnection.GetConnectionString();
    }

    public static NpgsqlConnection GetNpgsqlConnection()
    {
        var connectionString = GetNpgsqlConnectionString();
        var connection = new NpgsqlConnection(connectionString);

        return connection;
    }

    private static void CreateTable(string tableName, NpgsqlConnection connection, string fields)
    {
        var sql = $"CREATE TABLE {tableName} {fields};";
        var cmd = EstablishConnectionWithQuery(connection, sql);
        cmd.ExecuteNonQuery();
    }

    public static bool CheckIfTableExist(string tableName, NpgsqlConnection connection)
    {
        var sql = $"SELECT EXISTS ( SELECT FROM pg_tables WHERE schemaname = 'public' AND tablename = '{tableName}' );";
        var cmd = EstablishConnectionWithQuery(connection, sql);
        return (bool)cmd.ExecuteScalar();
    }

    private static void TruncateTable(string tableName, NpgsqlConnection connection)
    {
        var sql = $"TRUNCATE TABLE {tableName};";
        var cmd = EstablishConnectionWithQuery(connection, sql);
        cmd.ExecuteNonQuery();
    }

    public static void CreateTableIfNotExistOrTruncate(string tableName, NpgsqlConnection connection, string fields, bool truncate = false)
    {
        tableName = tableName.ToLower();

        if (!CheckIfTableExist(tableName, connection))
        {
            CreateTable(tableName, connection, fields);
        }
        else
        {
            if (truncate)
            {
                TruncateTable(tableName, connection);
            }
        }
    }

    public static NpgsqlCommand EstablishConnectionWithQuery(NpgsqlConnection connection, string sql)
    {
        var cmd = new NpgsqlCommand();
        cmd.Connection = connection;
        cmd.CommandText = sql;
        return cmd;
    }

    public static bool CheckConnection(string connectionString)
    {
        try
        {
            using (var connection = new NpgsqlConnection(connectionString))
            {
                connection.Open(); // Attempt to open the connection

                if (connection.State == System.Data.ConnectionState.Open)
                {
                    // Connection is successful
                    return true;
                }
            }
        }
        catch (NpgsqlException ex)
        {
            // Handle specific Npgsql exceptions if necessary
            Debug.Log("Connection failed: " + ex.Message);
        }
        catch (Exception ex)
        {
            // Handle other exceptions
            Debug.Log("An error occurred: " + ex.Message);
        }

        // Connection failed
        return false;
    }

    public static void InstallExtensions(String connectionString, bool[] extensions)
    {
        var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        var cmd = new NpgsqlCommand();
        cmd.Connection = connection;
        var sql = "";
        if (extensions[0]) 
        {
            sql += "CREATE EXTENSION postgis;";
        }
        if (extensions[1])
        {
            sql += "CREATE EXTENSION postgis_raster;";
        }
        if (extensions[2])
        {
            sql += "CREATE EXTENSION postgis_sfcgal;";
        }
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
        connection.Close();
        Debug.Log("Extensions instelled");
    }

}

