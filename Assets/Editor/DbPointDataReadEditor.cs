using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DbDataReadPoint))]
public class DbPointDataReadEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var dbManager = (DbDataReadPoint)target;

        if (GUILayout.Button("Load Data"))
        {
            dbManager.LoadDataFromDb();;
        }
    }
}
