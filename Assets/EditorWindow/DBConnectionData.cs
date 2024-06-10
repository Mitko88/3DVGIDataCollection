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

    public string GetConnectionString()
    {
        if (Host == "" || Username == "" || Password == "" ||
            Database == "")
        {
            throw new InvalidDataException("Database connection field are not set up correctly");
        }

        return $"Host={Host}; Username={Username}; Password={Password}; Database={Database}";
    }
}
