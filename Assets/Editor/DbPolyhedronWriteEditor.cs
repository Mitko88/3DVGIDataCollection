using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DbPolyhedronWrite))]
public class DbPolyhedronWriteEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var dbManager = (DbPolyhedronWrite)target;

        if (GUILayout.Button("Write Polyhedron Data"))
        {
            dbManager.WritePolyhedronData();;
        }
    }
}
