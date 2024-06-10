using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
public class DbDataWrite : MonoBehaviour
{
    public string TableName;
    public bool Truncate;
    public void WritePolyhedronData()
    {
        var meshFilters = Object.FindObjectsOfType<MeshFilter>();

        if (meshFilters.Length == 0)
        {
            Debug.Log("No meshes detected.");
            return;
        }

        var connection = DbCommonFunctions.GetNpgsqlConnection();
        DBexport.ExportMeshesAsPolyhedrons(meshFilters.ToArray(), connection, TableName, Truncate);
    }
}
#endif
