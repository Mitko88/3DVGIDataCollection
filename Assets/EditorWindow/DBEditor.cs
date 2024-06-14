using System;
using System.IO;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
[Serializable]
public class DBEditor : EditorWindow
{
    private DBConnectionData _dbEditor;
    private bool _postgis;
    private bool _postgisRaster;
    private bool _postgisSfcgal;
    private string[] sslModes = new string[] { "Disable", "Allow", "Prefer", "Require", "Verify-ca", "Verify-full" };

    [MenuItem("Tools/DB Connection")]
    public static void ShowDBEditor()
    {
        // This method is called when the user selects the menu item in the Editor
        var wnd = GetWindow<DBEditor>();
        wnd.titleContent = new GUIContent("DB Connection");
    }

    private void OnEnable()
    {
        hideFlags = HideFlags.HideAndDontSave;
        if (AssetDatabase.LoadAssetAtPath("Assets/Resources/ConnectionData.asset", typeof(DBConnectionData)) == null)
        {
            _dbEditor = CreateInstance<DBConnectionData>();
        }
        else
        {
            _dbEditor = (DBConnectionData)AssetDatabase.LoadAssetAtPath("Assets/Resources/ConnectionData.asset", typeof(DBConnectionData));
        }
    }

    public string GetConnectionString()
    {
        if (string.IsNullOrEmpty(_dbEditor.Host) || string.IsNullOrEmpty(_dbEditor.Username) || string.IsNullOrEmpty(_dbEditor.Password) ||
            string.IsNullOrEmpty(_dbEditor.Database))
        {
            throw new InvalidDataException("Database connection fields are not set up");
        }

        return $"Host={_dbEditor.Host};Username={_dbEditor.Username};Password={_dbEditor.Password};Database={_dbEditor.Database};SSL Mode={sslModes[_dbEditor.SSLmode]}";
    }

    private void OnGUI()
    {
        GUILayout.Label("Database connection");
        _dbEditor.Host = EditorGUILayout.TextField("Host", _dbEditor.Host);
        _dbEditor.Port = EditorGUILayout.IntField("Port", _dbEditor.Port); // Add port field
        _dbEditor.Username = EditorGUILayout.TextField("Username", _dbEditor.Username);
        _dbEditor.Password = EditorGUILayout.PasswordField("Password", _dbEditor.Password);
        _dbEditor.Database = EditorGUILayout.TextField("Database", _dbEditor.Database);
        _dbEditor.SSLmode = EditorGUILayout.Popup("SSL mode", _dbEditor.SSLmode, sslModes);

        var connectionString = GetConnectionString();

        GUILayout.Space(10);

        // Create a horizontal layout group
        GUILayout.BeginHorizontal();

        // Add flexible space before the button to center it
        GUILayout.FlexibleSpace();

        // Create the button
        if (GUILayout.Button("Test Connection", GUILayout.ExpandWidth(false))) // Prevent button from expanding
        {
            var isConnected = DbCommonFunctions.CheckConnection(connectionString);
            if (isConnected)
            {
                Debug.Log("Connection successful");
            }
        }

        // Add flexible space after the button to center it
        GUILayout.FlexibleSpace();

        // End the horizontal layout group
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
        GUILayout.Label("Extensions");

        _postgis = EditorGUILayout.Toggle("PostGIS", _postgis);
        _postgisRaster = EditorGUILayout.Toggle("PostGIS Raster", _postgisRaster);
        _postgisSfcgal = EditorGUILayout.Toggle("PostGIS Sfcgal", _postgisSfcgal);

        GUILayout.Space(10);

        // Create a horizontal layout group
        GUILayout.BeginHorizontal();

        // Add flexible space before the button to center it
        GUILayout.FlexibleSpace();

        // Create the button
        if (GUILayout.Button("Install Extensions", GUILayout.ExpandWidth(false))) // Prevent button from expanding
        {
            // Code to execute when the button is clicked
            var extensions = new bool[] { _postgis, _postgisRaster, _postgisSfcgal };
            DbCommonFunctions.InstallExtensions(connectionString, extensions);
        }

        // Add flexible space after the button to center it
        GUILayout.FlexibleSpace();

        // End the horizontal layout group
        GUILayout.EndHorizontal();
    }

    void OnDestroy()
    {
        if (AssetDatabase.LoadAssetAtPath("Assets/Resources/ConnectionData.asset", typeof(DBConnectionData)) == null)
        {
            AssetDatabase.CreateAsset(_dbEditor, "Assets/Resources/ConnectionData.asset");
        }
        else
        {
            EditorUtility.SetDirty(_dbEditor);
            AssetDatabase.SaveAssets();
        }
    }
}
#endif