using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DbDataReadPolygon))]
public class DbPolygonDataReadEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var dbManager = (DbDataReadPolygon)target;

        if (GUILayout.Button("Load Data"))
        {
            dbManager.LoadDataFromDb();;
        }
    }
}
