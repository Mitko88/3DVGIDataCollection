using System;
using System.Collections;
using System.Collections.Generic;
using CesiumForUnity;
using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;
using Npgsql;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
public static class DBquery
{
   public static void LoadTriangleData(NpgsqlConnection connection, string tableName, MonoBehaviour handle, string extrusion, Material material)
   {
        connection.Open();
        DbCommonFunctions.CheckIfTableExist(tableName, connection);

        connection.TypeMapper.UseNetTopologySuite();

        var metadata = LoadMetadata(connection, tableName);

        var geomType = metadata.Item2[0];
        if (geomType == PostgisGeometryType.ST_Polygon.ToString() 
            || geomType == PostgisGeometryType.ST_MultiPolygon.ToString()            )
        {
            var centroids = GetCentroids(connection, tableName);
            CreateOrCheckLayer("gis");
            if (extrusion == "")
            {
                DrawPolygons(connection, tableName, handle, material, metadata.Item1, centroids);
            }
            else
            {
                DrawExtrudedPolygon(connection, tableName, handle, material, metadata.Item1, extrusion, centroids);
            }
        }
        else if(geomType == PostgisGeometryType.ST_PolyhedralSurface.ToString())
        {
            CreateOrCheckLayer("gis");
            var centroids = GetCentroids(connection, tableName);
            DrawPolyhedron(connection, tableName, handle, material,metadata.Item1, centroids);
        }
        else
        {
            Debug.Log("Not supported geometry.");
        }

        connection.Close();
    }

   public static void LoadPointData(NpgsqlConnection connection, string tableName, MonoBehaviour handle, Material material, GameObject prefab, float scaleSize)
   {
       connection.Open();
       DbCommonFunctions.CheckIfTableExist(tableName, connection);

       connection.TypeMapper.UseNetTopologySuite();

       var metadata = LoadMetadata(connection, tableName);

       var geomType = metadata.Item2[0];
       if (geomType == PostgisGeometryType.ST_Point.ToString())
       {
            CreateOrCheckLayer("gis");
            DrawPoints(connection, tableName, handle, prefab, metadata.Item1, material, scaleSize);
       }
       else
       {
           Debug.Log("Not supported geometry.");
       }

       connection.Close();
   }

    private static (List<string[]>, string[]) LoadMetadata(NpgsqlConnection connection, string tableName)
   {
       var sqlTest = $"SELECT column_name, data_type FROM information_schema.columns WHERE table_name = '{tableName}'";
       var cmd = new NpgsqlCommand(sqlTest, connection);
       var columnList = new List<string>();
       using (var reader = cmd.ExecuteReader())
       {
           while (reader.Read())
           {
               columnList.Add(reader[0].ToString());
           }
       }

       var fieldCount = columnList.Count;
       var sqlTest1 = $"SELECT * FROM \"{tableName}\"";
       cmd = new NpgsqlCommand(sqlTest1, connection);
       cmd.AllResultTypesAreUnknown = true;
       var metadata = new List<string[]>();
       using (var reader = cmd.ExecuteReader())
       {
           while (reader.Read())
           {
               var data = new string[fieldCount];
               for (var j = 0; j < reader.FieldCount; j++)
               {
                   data[j] = $"{columnList[j]}: {reader[j]}";
               }

               metadata.Add(data);
           }
       }

       //update geom field
       var geometriesType = new string[metadata.Count];
       for (var j = 0; j < columnList.Count; j++)
       {
           var value = columnList[j];
           var valueLowerCase = value.ToLower();
           if (valueLowerCase == "geom" || valueLowerCase == "geometry")
           {
               var sqlGeometryType = $"SELECT ST_GeometryType(st_geometryN({value},1)) from \"{tableName}\"";
               var cmd1 = new NpgsqlCommand(sqlGeometryType, connection);

               using (var reader = cmd1.ExecuteReader())
               {
                   var k = 0;
                   while (reader.Read())
                   {
                       var geomType = reader[0].ToString();
                       metadata[k][j] = $"{value}: {geomType}";
                       geometriesType[k] = geomType;
                       k++;
                   }
               }

               break;
           }
       }

       return (metadata, geometriesType);
   }


   private static void DrawPoints(NpgsqlConnection connection, string tableName, MonoBehaviour handle, GameObject prefab, List<string[]> metadata, Material material, float pointSize)
   {
       var sqlCentroids = $"select ST_Transform(geom,4326) from \"{tableName}\"";

       var cmd = new NpgsqlCommand(sqlCentroids, connection);
       var i = 0;
       using (var reader = cmd.ExecuteReader())
       {
           while (reader.Read())
           {
               var point = (Point)reader[0];
               handle.StartCoroutine(InstantiatePoint(prefab, handle.gameObject, point, metadata[i], material, pointSize));
               i++;
           }
       }
   }

   private static IEnumerator InstantiatePoint(GameObject prefab, GameObject parent, Point point, string[] metadata, Material material, float pointSize)
   {
       GameObject pointGO;
       var verticalOffset = 0f;
       if (prefab != null)
       {
           pointGO = Object.Instantiate(prefab);
           verticalOffset = pointGO.GetComponent<Renderer>().bounds.size.y / 2;
       }
       else
       {
           pointGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
           pointGO.GetComponent<Renderer>().material = material;
           Object.DestroyImmediate(pointGO.GetComponent<SphereCollider>());
       }

       pointGO.transform.localScale = new Vector3(pointSize, pointSize, pointSize);

       pointGO.name = metadata[0];
       pointGO.transform.parent = parent.transform;
       pointGO.layer = LayerMask.NameToLayer("gis");
        pointGO.AddComponent<DataPropeties>().Propeties = metadata;
       var location = pointGO.AddComponent<CesiumGlobeAnchor>();
       location.longitudeLatitudeHeight = new double3(point.X, point.Y, 5000);
        var groundHeight = GetCesiumTerrainHitForLocation(parent.transform.parent.gameObject, location.transform.position);
        location.height = groundHeight;
        yield return null;
       yield return null;
       pointGO.AddComponent<MeshCollider>();
   }

   private static void DrawPolygons(NpgsqlConnection connection, string tableName, MonoBehaviour handle, Material material, 
       List<string[]> metaData, List<(Point,Point)> centroids)
   {
        var sqlPoints = $"select a.id, (a.geom_pnt).geom from(SELECT id, ST_DumpPoints(st_tesselate(ST_ForcePolygonCW(geom))) As geom_pnt FROM \"{tableName}\") as a";

        var cmd = new NpgsqlCommand(sqlPoints, connection);
        cmd.CommandTimeout = 600;

        var indices = new List<int>();
        var vertices = new List<Vector3>();
        var i = 0;
        var i1 = 0;
        var previousSurfaceId = 1;
        var polyhedronId = 0;
        var polyhedronCentroid = centroids[polyhedronId];
        var polyhedronsCount = centroids.Count;
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                switch (i)
                {
                    case 0:
                        var surfaceId = Convert.ToInt32(reader[0]);
                        if (surfaceId > previousSurfaceId)
                        {
                            handle.StartCoroutine(InstantiateMesh(handle.gameObject, material, $"Polygon {polyhedronId}", vertices, indices, metaData[polyhedronId], polyhedronCentroid));
                            indices = new List<int>();
                            vertices = new List<Vector3>();
                            i1 = 0;
                            polyhedronId++;
                            if (polyhedronsCount != polyhedronId + 1)
                            {
                                polyhedronCentroid = centroids[polyhedronId];
                            }
                        }
                        previousSurfaceId = surfaceId;

                        indices.Add(i1);
                        var vertex = GetVector3((Point)reader[1]);
                        vertices.Add(vertex);
                        i++;
                        i1++;
                        break;
                    case 1:
                        indices.Add(i1);
                        vertex = GetVector3((Point)reader[1]);
                        vertices.Add(vertex);
                        i++;
                        i1++;
                        break;
                    case 2:
                        indices.Add(i1);
                        vertex = GetVector3((Point)reader[1]);
                        vertices.Add(vertex);
                        i++;
                        i1++;
                        break;
                    default:
                        i = 0;
                        break;
                }
            }
            handle.StartCoroutine(InstantiateMesh(handle.gameObject, material, $"Polygon {polyhedronId}", vertices, indices, metaData[polyhedronId], polyhedronCentroid));
        }
    }

   private static void DrawExtrudedPolygon(NpgsqlConnection connection, string tableName, MonoBehaviour handle, Material material, List<string[]> metaData, 
       string extrusion, List<(Point, Point)> centroids)
    {
        //var sqlPoints = $"select b.surface[2], (b.geom_pnt).geom from(SELECT(a.p_geom).path as surface, " +
        //                    $"ST_DumpPoints(st_tesselate((a.p_geom).geom)) As geom_pnt FROM (select ST_Dump(st_extrude(geom,0,0,{extrusion})) as p_geom from \"{tableName}\") as a) as b";

        var polyhedronsPoints = $"select b.surface[2] as surface_id, (b.geom_pnt).geom as pnt from(SELECT(a.p_geom).path as surface, " +
            $"ST_DumpPoints(st_tesselate((a.p_geom).geom)) As geom_pnt FROM(select ST_Dump(st_extrude(geom, 0, 0, {extrusion})) as p_geom " +
            $" from \"{tableName}\") as a) as b";

        ConstractMeshFromPolyhedronPolygons(connection, handle, material, metaData, polyhedronsPoints, centroids);
    }

    private static void DrawPolyhedron(NpgsqlConnection connection, string tableName, MonoBehaviour handle, Material material, List<string[]> metaData
        , List<(Point, Point)> centroids)
    {
        var sqlPoints = $"select b.surface[2], (b.geom_pnt).geom from(SELECT(a.p_geom).path as surface, " +
                            $"ST_DumpPoints(st_tesselate((a.p_geom).geom)) As geom_pnt FROM (select ST_Dump(geom) as p_geom from \"{tableName}\") as a) as b";

        ConstractMeshFromPolyhedronPolygons(connection, handle, material, metaData, sqlPoints, centroids);
    }

    private static void ConstractMeshFromPolyhedronPolygons(NpgsqlConnection connection, MonoBehaviour handle, Material material, List<string[]> metaData, 
        string sqlPoints, List<(Point, Point)> centroids)
    {
        var cmd = new NpgsqlCommand(sqlPoints, connection);

        var indices = new List<int>();
        var vertices = new List<Vector3>();
        var i = 0;
        var i1 = 0;
        var previousSurfaceId = 1;
        var polyhedronId = 0;
        var polyhedronCentroid = centroids[polyhedronId];
        var polyhedronsCount = centroids.Count;
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                switch (i)
                {
                    case 0:
                        var surfaceId = (int)reader[0];
                        if (surfaceId < previousSurfaceId)
                        {
                            handle.StartCoroutine(InstantiateMesh(handle.gameObject, material, $"Polyhedron {polyhedronId}", vertices, indices, 
                                metaData[polyhedronId], polyhedronCentroid));
                            indices = new List<int>();
                            vertices = new List<Vector3>();
                            i1 = 0;
                            polyhedronId++;
                            previousSurfaceId = 1;
                            if (polyhedronId + 1 != polyhedronsCount)
                            {
                                polyhedronCentroid = centroids[polyhedronId];
                            }
                        }
                        else
                        {
                            previousSurfaceId = surfaceId;
                        }

                        indices.Add(i1);
                        var vertex = GetShiftedVector3((Point)reader[1], polyhedronCentroid.Item1);
                        vertices.Add(vertex);
                        i++;
                        i1++;
                        break;
                    case 1:
                        indices.Add(i1);
                        vertex = GetShiftedVector3((Point)reader[1], polyhedronCentroid.Item1);
                        vertices.Add(vertex);
                        i++;
                        i1++;
                        break;
                    case 2:
                        indices.Add(i1);
                        vertex = GetShiftedVector3((Point)reader[1], polyhedronCentroid.Item1);
                        vertices.Add(vertex);
                        i++;
                        i1++;
                        break;
                    default:
                        i = 0;
                        break;
                }
            }

            handle.StartCoroutine(InstantiateMesh(handle.gameObject, material, $"Polyhedron {polyhedronId}", vertices, indices, metaData[polyhedronId],
                polyhedronCentroid));
        }
    }

    private static Vector3 GetShiftedVector3(Point point, Point shift)
    {
        if (double.IsNaN(point.Z))
        {
            return new Vector3((float)(point.X - shift.X), (float)(point.Y - shift.Y), 0);
        }

        return new Vector3((float)(point.X - shift.X), (float)(point.Y - shift.Y), (float)point.Z);
    }

    private static IEnumerator InstantiateMesh(GameObject parent, Material material, 
       string objectName, List<Vector3> vertices, List<int> indices, string[] properties, (Point, Point) centroid)
    {
        var gameObject = new GameObject(objectName, typeof(MeshFilter), typeof(MeshRenderer), typeof(DataPropeties));
        var mesh = new Mesh
        {
            vertices = vertices.ToArray(),
            triangles = indices.ToArray()
        };

        mesh.RecalculateNormals();
        gameObject.GetComponent<MeshFilter>().mesh = mesh;
        gameObject.GetComponent<Renderer>().material = material;
        gameObject.GetComponent<DataPropeties>().Propeties = properties;
        gameObject.transform.parent = parent.transform;
        gameObject.transform.localRotation = Quaternion.Euler(90, 0, 0);
        gameObject.layer = LayerMask.NameToLayer("gis");

        if (DbDataReadPolygon.gisPlugin == DbDataReadPolygon.GISPlugin.CesiumForUnity)
        { 
            var location = gameObject.AddComponent<CesiumGlobeAnchor>();
            location.longitudeLatitudeHeight = new Unity.Mathematics.double3(centroid.Item2.X, centroid.Item2.Y, 5000);
            var groundHeight = GetCesiumTerrainHitForLocation(parent.transform.parent.gameObject, location.transform.position);            

            var extrusion = 0f;
            for (var i = 0; i < mesh.vertices.Length; i++)
            {
                var vertex = vertices[i];
                if (vertex.z != 0)
                {
                    extrusion = vertex.z;
                    break;
                }
            }

            location.height = groundHeight + extrusion;
        }
        // need a frame for location component updates to occur
        yield return gameObject;
        gameObject.AddComponent<MeshCollider>();
    }

   public static Vector3 GetVector3(Point point)
   {
       if (point.Z is Double.NaN)
       {
           return new Vector3((float)point.X, (float)point.Y, 0);
       }
       else
       {
           return new Vector3((float)point.X, (float)point.Y, (float)point.Z);
       }
   }

   public static void RunQuery(NpgsqlConnection connection, string query, string tableName = "temp", int timeout = 30)
   {
       connection.Open();
       var fullQuery = $@"DO 
                    $$ 
                    BEGIN 
                      -- Check if the table exists 
                      IF EXISTS (SELECT FROM information_schema.tables 
                      WHERE table_schema = 'public' AND table_name = '{tableName}') THEN 
                        -- Truncate the table if it exists 
                        TRUNCATE TABLE {tableName};
                        -- Insert into the table
                        INSERT INTO {tableName} {query};
                      ELSE 
                        -- Create the table if it does not exist  
                        CREATE TABLE {tableName} as {query}; 
                      END IF; 
                    END 
                    $$;";

       var cmd = new NpgsqlCommand(fullQuery, connection);
       cmd.CommandTimeout = timeout;
       cmd.ExecuteNonQuery();
       connection.Close();
    }

    private static List<(Point, Point)> GetCentroids(NpgsqlConnection connection, string tableName)
    {
        var sqlCentroids = $"select st_centroid(ST_Points(geom)), st_transform(st_setsrid(st_centroid(ST_Points(geom)),25833),4326) from \"{tableName}\"";

        var cmd = new NpgsqlCommand(sqlCentroids, connection);
        var objectsCentroids = new List<(Point, Point)>();

        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                objectsCentroids.Add(((Point)reader[0], (Point)reader[1]));
            }
        }

        return objectsCentroids;
    }

    private static void CreateOrCheckLayer(string layerName)
    {
        string newLayerName = layerName; // Change this to the name you want for your new layer

        // Check if the layer already exists
        if (LayerExists(newLayerName))
        {
            Debug.Log("Layer already exists: " + newLayerName);
        }
        else
        {
            // Create the new layer
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layersProp = tagManager.FindProperty("layers");

            for (int i = 8; i < layersProp.arraySize; i++)
            {
                SerializedProperty layerSP = layersProp.GetArrayElementAtIndex(i);
                if (layerSP.stringValue == "")
                {
                    layerSP.stringValue = newLayerName;
                    tagManager.ApplyModifiedProperties();
                    Debug.Log("Layer created: " + newLayerName);
                    return;
                }
            }
            Debug.LogWarning("All layer slots are full. Could not create a new layer.");
        }
    }

    private static bool LayerExists(string layerName)
    {
        for (int i = 0; i < 32; i++)
        {
            string existingLayerName = LayerMask.LayerToName(i);
            if (existingLayerName == layerName)
            {
                return true;
            }
        }
        return false;
    }

    private static double GetCesiumTerrainHitForLocation(GameObject cesiumTileset, Vector3 position)
    {
        Ray ray = new Ray(position, Vector3.down);
        RaycastHit hit;
        int gisLayer = LayerMask.NameToLayer("gis");
        int layerMask = ~(1 << gisLayer);

        // Perform the raycast using Unity's physics system
        if (Physics.Raycast(ray, out hit, Mathf.Infinity, layerMask))
        {
            CesiumGeoreference georeference = cesiumTileset.GetComponent<CesiumGeoreference>();

            // Convert the hit point to longitude, latitude, and height
            double3 positionEcef = georeference.TransformUnityPositionToEarthCenteredEarthFixed(new Unity.Mathematics.double3(hit.point.x,hit.point.y,hit.point.z));
            double3 positionLlh = CesiumWgs84Ellipsoid.EarthCenteredEarthFixedToLongitudeLatitudeHeight(positionEcef);

            double longitude = positionLlh.x;
            double latitude = positionLlh.y;
            double height = positionLlh.z;

            return height;
        }
        return 0;
    }
}
#endif