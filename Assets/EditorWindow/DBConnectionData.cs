using Npgsql;
using System;
using System.IO;
using UnityEngine;

[Serializable]
public class DBConnectionData : ScriptableObject
{
    public string Host;
    public string Username;
    public string Password;
    public string Database;
    public int Port = 5432; // Default port for PostgreSQL
    public int SSLmode;
    private string[] sslModes = new string[] { "Disable", "Allow", "Prefer", "Require", "Verify-ca", "Verify-full" };

    public string GetConnectionString()
    {
        if (Host == "" || Username == "" || Password == "" ||
            Database == "")
        {
            throw new InvalidDataException("Database connection field are not set up correctly");
        }

        return $"Host={Host};Username={Username};Password={Password};Database={Database};SSL mode={sslModes[SSLmode]}";
    }
}
