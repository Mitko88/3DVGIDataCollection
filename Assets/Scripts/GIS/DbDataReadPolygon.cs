using CesiumForUnity;
using System;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
public class DbDataReadPolygon : MonoBehaviour
{
    public string TableName;
    public Material Material;
    public string Extrusion;
    [SerializeField]
    private GISPlugin plugin;
    public static GISPlugin gisPlugin {get; private set;}
    public enum GISPlugin
    {
        CesiumForUnity,
        ArcGISForUnity
    }

    public void LoadDataFromDb()
    {
        if (plugin == GISPlugin.CesiumForUnity)
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetTypes().Any(t => t.Name == "CesiumRuntime"));

            if (assembly != null)
            {
                // Find the CesiumGlobeAnchor type in the assembly
                var cesiumGlobeAnchorType = assembly.GetType("CesiumForUnity.CesiumGlobeAnchor");

                if (cesiumGlobeAnchorType != null)
                {
                    Debug.Log("Cesium plugin might not be installed");
                    return;
                }
                else
                {
                    gisPlugin = GISPlugin.CesiumForUnity;
                }
            }
        }
        else
        {
            
        }

        var connection = DbCommonFunctions.GetNpgsqlConnection();
        DBquery.LoadTriangleData(connection, TableName, this, Extrusion, Material);
    }
}
#endif
