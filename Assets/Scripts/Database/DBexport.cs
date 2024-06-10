using System;
using UnityEngine;
using Npgsql;
using System.Text;
using System.Globalization;
using Unity.Mathematics;
using Unity.Burst.CompilerServices;


public static class DBexport
{
    public static void ExportMeshesAsPolyhedrons(MeshFilter[] meshFilters, NpgsqlConnection connection, string tableName, bool truncate = false, string[] excludeShaders = null)
    {
        const string fields = "(id int, name text, geom GEOMETRY(POLYHEDRALSURFACEZ))";
        connection.Open();
        DbCommonFunctions.CreateTableIfNotExistOrTruncate(tableName, connection, fields, truncate);

        var initialSqlPart = $"INSERT INTO {tableName} (id, name, geom) Values(";

        using (var conn = connection)
        {
            var cmd = new NpgsqlCommand();
            cmd.Connection = conn;

            for (var index = 0; index < meshFilters.Length; index++)
            {
                var meshFilter = meshFilters[index];
                if (!IsMeshReadable(meshFilter) || ExcludeBasedOnShader(meshFilter, excludeShaders))
                {
                    continue;
                }

                var mesh = GetTransformedMesh(meshFilter);
                var vertices = mesh.vertices;
                var triangles = mesh.triangles;

                var sql = new System.Text.StringBuilder(initialSqlPart);

                var p = vertices[triangles[0]];
                var q = vertices[triangles[1]];
                var r = vertices[triangles[2]];

                sql.Append(
                    $"{index}, '{meshFilter.gameObject.name}','SRID=7856;POLYHEDRALSURFACE Z((({p.x} {p.z} {p.y}, {q.x} {q.z} {q.y},{r.x} {r.z} {r.y}," +
                    $"{p.x} {p.z} {p.y}))");

                for (var i = 3; i < meshFilter.sharedMesh.triangles.Length; i += 3)
                {
                    p = vertices[triangles[i]];
                    q = vertices[triangles[i + 1]];
                    r = vertices[triangles[i + 2]];

                    var polygon = $",(({p.x} {p.z} {p.y}, {q.x} {q.z} {q.y},{r.x} {r.z} {r.y},{p.x} {p.z} {p.y}))";

                    sql.Append(polygon);
                }

                sql.Append(")')");

                cmd.CommandText = sql.ToString();
                cmd.ExecuteNonQuery();
            }

            Debug.Log("Data export is done.");
        }
        //connection.Close();
    }

    public static void ExportMeshesAsPolyhedrons2(MeshFilter[] meshFilters, NpgsqlConnection connection, double3[] centroids, string tableName, bool truncate = false)
    {
        tableName = tableName.ToLower();
        const string fields = "(id int, geom GEOMETRY(POLYHEDRALSURFACEZ))";
        connection.Open();
        DbCommonFunctions.CreateTableIfNotExistOrTruncate(tableName, connection, fields, truncate);

        var initialSqlPart = $"INSERT INTO {tableName} (id, geom) Values(";

        using (var conn = connection)
        {
            var cmd = new NpgsqlCommand();
            cmd.Connection = conn;

            for (var index = 0; index < meshFilters.Length; index++)
            {
                var meshFilter = meshFilters[index];
                var centroid = centroids[index];
                var vertices = GetShiftedVertices(meshFilter.sharedMesh.vertices, centroid, meshFilter);
                var triangles = meshFilter.sharedMesh.triangles;

                var sql = new StringBuilder(initialSqlPart);

                var p = vertices[triangles[0]];
                var q = vertices[triangles[1]];
                var r = vertices[triangles[2]];

                sql.Append(
                    $"{index},'SRID=25833;POLYHEDRALSURFACE Z((({p.x} {p.y} {p.z}, {q.x} {q.y} {q.z}, {r.x} {r.y} {r.z}, {p.x} {p.y} {p.z}))");

                for (var i = 3; i < meshFilter.sharedMesh.triangles.Length; i += 3)
                {
                    p = vertices[triangles[i]];
                    q = vertices[triangles[i + 1]];
                    r = vertices[triangles[i + 2]];

                    var polygon = $",(({p.x} {p.y} {p.z}, {q.x} {q.y} {q.z}, {r.x} {r.y} {r.z}, {p.x} {p.y} {p.z}))";

                    sql.Append(polygon);
                }

                sql.Append(")')");

                cmd.CommandText = sql.ToString();
                cmd.ExecuteNonQuery();
            }

            Debug.Log("Data export is done.");
        }
        connection.Close();
    }

    private static double3[] GetShiftedVertices(Vector3[] vertices, double3 centroid, MeshFilter meshFilter)
    {
        var shiftedVertices = new double3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {   
            var vertex = meshFilter.transform.TransformPoint(vertices[i]);
            shiftedVertices[i] = new double3(vertex.x + centroid.x, vertex.z + centroid.z, vertex.y + centroid.y);
        }
        return shiftedVertices;
    }

    private static Mesh GetTransformedMesh(MeshFilter meshFilter)
    {
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            return null;
        }

        var originalMesh = meshFilter.sharedMesh;
        var transformedMesh = new Mesh();

        // Clone the original mesh
        transformedMesh.vertices = originalMesh.vertices;
        transformedMesh.triangles = originalMesh.triangles;
        transformedMesh.uv = originalMesh.uv;
        transformedMesh.normals = originalMesh.normals;
        transformedMesh.colors = originalMesh.colors;
        transformedMesh.tangents = originalMesh.tangents;

        // Apply transformation
        var transformedVertices = new Vector3[transformedMesh.vertexCount];
        for (var i = 0; i < transformedVertices.Length; i++)
        {
            // Apply scale and rotation
            transformedVertices[i] = meshFilter.transform.TransformPoint(transformedMesh.vertices[i]);
        }

        // Update the mesh with transformed vertices
        transformedMesh.vertices = transformedVertices;
        transformedMesh.RecalculateBounds();

        return transformedMesh;
    }

    private static bool IsMeshReadable(MeshFilter meshFilter)
    {
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            return false;
        }

        try
        {
            // Try accessing vertex data
            var vertices = meshFilter.sharedMesh.vertices;
            return true;
        }
        catch
        {
            // An exception indicates the mesh is not readable
            return false;
        }
    }

    private static bool ExcludeBasedOnShader(MeshFilter meshFilter, string[] excludeShaders)
    {
        var meshRenderer = meshFilter.GetComponent<MeshRenderer>();

        if (meshRenderer != null)
        {
            var material = meshRenderer.sharedMaterial;

            if (material != null)
            {
                if (excludeShaders != null)
                {
                    var shader = material.shader;
                    return Array.Exists(excludeShaders, element => shader.name.ToLower().Contains(element.ToLower()));
                }
                return false;
            }

            Debug.Log("No material found.");
            return true;
        }

        Debug.Log("No MeshRenderer found.");
        return true;
    }

    public static string CreateRasterInsertQuery(int width, int height, double xOrigin, double yOrigin, double pixelSizeX, 
        double pixelSizeY, int srid, float[,] layer)
    {
        var queryBuilder = new StringBuilder();

        // Start constructing the query
        queryBuilder.Append("SELECT ST_SetValues(ST_AddBand(ST_MakeEmptyRaster(");
        queryBuilder.Append($"{width}, {height}, {xOrigin.ToString(CultureInfo.InvariantCulture)}, ");
        queryBuilder.Append($"{yOrigin.ToString(CultureInfo.InvariantCulture)}, {pixelSizeX.ToString(CultureInfo.InvariantCulture)}, ");
        queryBuilder.Append($"{pixelSizeY.ToString(CultureInfo.InvariantCulture)}, 0, 0, {srid}), '32BF'::text, 100, 0),");
        queryBuilder.Append("1, 1, 1, ARRAY[");

        // Add alphamap data
        for (var y = 0; y < height; y++)
        {
            if (y > 0)
                queryBuilder.Append(", ");

            queryBuilder.Append("[");

            for (var x = 0; x < width; x++)
            {
                if (x > 0)
                    queryBuilder.Append(", ");

                queryBuilder.Append(layer[x, y].ToString(CultureInfo.InvariantCulture));
            }

            queryBuilder.Append("]");
        }

        queryBuilder.Append("]) AS new_raster");

        return queryBuilder.ToString();
    }


    //public static void ExportPolyhedralsSurface(Polyhedron3[] polyhedrons, string tableName, string[] names, bool truncate = false)
    //{
    //    var connectionString = GetConnectionString();
    //    tableName = tableName.ToLower();

    //    if (!CheckIfTableExist(tableName, connectionString))
    //    {
    //        CreateTable(tableName, connectionString, "(id text, geom GEOMETRY(POLYHEDRALSURFACEZ))");
    //    }
    //    else
    //    {
    //        if (truncate)
    //        {
    //            TruncateTable(tableName, connectionString);
    //        }
    //    }

    //    var sql = $"INSERT INTO {tableName} (id, geom) Values(";

    //    using (var conn = new NpgsqlConnection(connectionString))
    //    {
    //        conn.Open();
    //        var cmd = new NpgsqlCommand();
    //        cmd.Connection = conn;

    //        var k = 0;
    //        foreach (var polyhedron in polyhedrons)
    //        {
    //            var triangle = polyhedron.GetTriangle(0);

    //            var polygon = $"'{names[k]}'ST_GeomFromEWKT('SRID=7856;POLYHEDRALSURFACE Z((({triangle.A.x} {triangle.A.y} {triangle.A.z}, {triangle.B.x} {triangle.B.y} {triangle.B.z},{triangle.C.x} {triangle.C.y} {triangle.C.z},{triangle.A.x} {triangle.A.y} {triangle.A.z}))";

    //            var polygons = new System.Text.StringBuilder(polygon);

    //            for (var i = 1; i < polyhedron.FaceCount; i++)
    //            {
    //                triangle = polyhedron.GetTriangle(i);

    //                polygon = $", (({triangle.A.x} {triangle.A.y} {triangle.A.z}, {triangle.B.x} {triangle.B.y} {triangle.B.z},{triangle.C.x} {triangle.C.y} {triangle.C.z},{triangle.A.x} {triangle.A.y} {triangle.A.z}))";

    //                polygons.Append(polygon);
    //            }
    //            polygons.Append(")'))");

    //            cmd.CommandText = $"{sql}{polygons}";
    //            cmd.ExecuteNonQuery();
    //            k++;
    //        }
    //    }
    //}

    //public static void ExportPolyhedralSurface(NpgsqlCommand command, Polyhedron3 polyhedron, string tableName, string name)
    //{
    //    var triangle = polyhedron.GetTriangle(0);

    //    var initSql = $"INSERT INTO {tableName} (id, geom) Values('{name}',ST_GeomFromEWKT('SRID=7856;POLYHEDRALSURFACE Z((({triangle.A.x} {triangle.A.y} {triangle.A.z}, {triangle.B.x} {triangle.B.y} {triangle.B.z},{triangle.C.x} {triangle.C.y} {triangle.C.z},{triangle.A.x} {triangle.A.y} {triangle.A.z}))";

    //    var polygons = new System.Text.StringBuilder(initSql);

    //    for (var i = 1; i < polyhedron.FaceCount; i++)
    //    {
    //        triangle = polyhedron.GetTriangle(i);

    //        var polygon = $", (({triangle.A.x} {triangle.A.y} {triangle.A.z}, {triangle.B.x} {triangle.B.y} {triangle.B.z},{triangle.C.x} {triangle.C.y} {triangle.C.z},{triangle.A.x} {triangle.A.y} {triangle.A.z}))";

    //        polygons.Append(polygon);
    //    }
    //    polygons.Append(")'))");

    //    command.CommandText = polygons.ToString();
    //    command.ExecuteNonQuery();
    //}

    //public static void ExportPolyhedronsToPolygons(List<Polyhedron3<EIK>> polyhedrons, string tableName, bool truncate = false)
    //{
    //    var connectionString = GetConnectionString();
    //    tableName = tableName.ToLower();

    //    if (!CheckIfTableExist(tableName, connectionString))
    //    {
    //        CreateTable(tableName, connectionString, "(id integer, flipped integer, normal GEOMETRY(POINTZ), geom GEOMETRY(POLYGONZ))");
    //    }
    //    else
    //    {
    //        if (truncate)
    //        {
    //            TruncateTable(tableName, connectionString);
    //        }
    //    }

    //    var rotateAroundX = Math.Sqrt(0.5d);
    //    var sql = $"INSERT INTO {tableName} (id, flipped, normal, geom) Values(";

    //    using (var conn = new NpgsqlConnection(connectionString))
    //    {
    //        conn.Open();
    //        var cmd = new NpgsqlCommand();
    //        cmd.Connection = conn;

    //        var id = 0;
    //        foreach (var polyhedron in polyhedrons)
    //        {
    //            var polygon = new System.Text.StringBuilder(sql);
    //            var triangle = polyhedron.GetTriangle(0);
    //            var normal = Vector3.Cross(GetVector3FromVector3d(triangle.B - triangle.A), GetVector3FromVector3d(triangle.C - triangle.A));
    //            if (normal.x != 0)
    //            {
    //                polygon.Append(
    //                    $"{id}, 1, ST_MakePoint({Mathf.Abs(normal.normalized.x)}, {Mathf.Abs(normal.normalized.y)}, {Mathf.Abs(normal.normalized.z)}), ST_Union(ARRAY[ST_MakePolygon('LINESTRING Z({triangle.A.z} {triangle.A.y} {triangle.A.x}, {triangle.B.z} {triangle.B.y} {triangle.B.x},{triangle.C.z} {triangle.C.y} {triangle.C.x},{triangle.A.z} {triangle.A.y} {triangle.A.x})')");

    //                for (var i = 1; i < polyhedron.FaceCount; i++)
    //                {
    //                    triangle = polyhedron.GetTriangle(i);

    //                    polygon.Append(
    //                        $",ST_MakePolygon('LINESTRING Z({triangle.A.z} {triangle.A.y} {triangle.A.x}, {triangle.B.z} {triangle.B.y} {triangle.B.x},{triangle.C.z} {triangle.C.y} {triangle.C.x},{triangle.A.z} {triangle.A.y} {triangle.A.x})')");
    //                }

    //            }
    //            else if(normal.y != 0)
    //            {
    //                polygon.Append(
    //                    $"{id}, 2, ST_MakePoint({Mathf.Abs(normal.normalized.x)}, {Mathf.Abs(normal.normalized.y)}, {Mathf.Abs(normal.normalized.z)}), ST_Union(ARRAY[ST_MakePolygon('LINESTRING Z({triangle.A.x} {triangle.A.z} {triangle.A.y}, {triangle.B.x} {triangle.B.z} {triangle.B.y},{triangle.C.x} {triangle.C.z} {triangle.C.y},{triangle.A.x} {triangle.A.z} {triangle.A.y})')");

    //                for (var i = 1; i < polyhedron.FaceCount; i++)
    //                {
    //                    triangle = polyhedron.GetTriangle(i);

    //                    polygon.Append(
    //                        $",ST_MakePolygon('LINESTRING Z({triangle.A.x} {triangle.A.z} {triangle.A.y}, {triangle.B.x} {triangle.B.z} {triangle.B.y},{triangle.C.x} {triangle.C.z} {triangle.C.y},{triangle.A.x} {triangle.A.z} {triangle.A.y})')");
    //                }
    //            }
    //            else
    //            {
    //               polygon.Append(
    //                    $"{id}, 0, ST_MakePoint({Mathf.Abs(normal.normalized.x)}, {Mathf.Abs(normal.normalized.y)}, {Mathf.Abs(normal.normalized.z)}), ST_Union(ARRAY[ST_MakePolygon('LINESTRING Z({triangle.A.x} {triangle.A.y} {triangle.A.z}, {triangle.B.x} {triangle.B.y} {triangle.B.z},{triangle.C.x} {triangle.C.y} {triangle.C.z},{triangle.A.x} {triangle.A.y} {triangle.A.z})')");

    //                for (var i = 1; i < polyhedron.FaceCount; i++)
    //                {
    //                    triangle = polyhedron.GetTriangle(i);

    //                    polygon.Append(
    //                        $",ST_MakePolygon('LINESTRING Z({triangle.A.x} {triangle.A.y} {triangle.A.z}, {triangle.B.x} {triangle.B.y} {triangle.B.z},{triangle.C.x} {triangle.C.y} {triangle.C.z},{triangle.A.x} {triangle.A.y} {triangle.A.z})')");
    //                }
    //            }
    //            polygon.Append("]))");

    //            cmd.CommandText = polygon.ToString();
    //            cmd.ExecuteNonQuery();
    //            id++;
    //        }
    //    }
    //}

    //private static Vector3 GetVector3FromVector3d(Vector3d vector3d)
    //{
    //    return new Vector3((float)vector3d.x, (float)vector3d.y, (float)vector3d.z);
    //}

    //public static void ExportPolyhedronTriangles(Polyhedron3<EIK> polyhedron, string tableName, bool truncate = false)
    //{
    //    var connectionString = GetConnectionString();
    //    tableName = tableName.ToLower();

    //    if (!CheckIfTableExist(tableName, connectionString))
    //    {
    //        CreateTable(tableName, connectionString, "(id int, geom GEOMETRY(TINZ))");
    //    }
    //    else
    //    {
    //        if (truncate)
    //        {
    //            TruncateTable(tableName, connectionString);
    //        }
    //    }

    //    var sql = $"INSERT INTO {tableName} (id, geom) Values(";

    //    using (var conn = new NpgsqlConnection(connectionString))
    //    {
    //        conn.Open();
    //        var cmd = new NpgsqlCommand();
    //        cmd.Connection = conn;

    //        var index = 0;
    //        for (var i = 0; i < polyhedron.FaceCount; i++)
    //        {
    //            var triangle = polyhedron.GetTriangle(i);

    //            cmd.CommandText = $"{sql}{index},'TIN Z((({triangle.A.x} {triangle.A.y} {triangle.A.z}, {triangle.B.x} {triangle.B.y} {triangle.B.z},{triangle.C.x} {triangle.C.y} {triangle.C.z},{triangle.A.x} {triangle.A.y} {triangle.A.z})))')";
    //            cmd.ExecuteNonQuery();
    //            index++;
    //        }
    //    }
    //}

    //public static void ExportPolyhedronsToTins(List<Polyhedron3<EIK>> polyhedrons, string tableName, bool truncate = false)
    //{
    //    var connectionString = GetConnectionString();
    //    tableName = tableName.ToLower();

    //    if (!CheckIfTableExist(tableName, connectionString))
    //    {
    //        CreateTable(tableName, connectionString, "(id int, geom GEOMETRY(TINZ))");
    //    }
    //    else
    //    {
    //        if (truncate)
    //        {
    //            TruncateTable(tableName, connectionString);
    //        }
    //    }

    //    using (var conn = new NpgsqlConnection(connectionString))
    //    {
    //        conn.Open();
    //        var cmd = new NpgsqlCommand();
    //        cmd.Connection = conn;

    //        foreach (var polyhedron in polyhedrons)
    //        {
    //            var triangle = polyhedron.GetTriangle(0);
    //            var tin = new System.Text.StringBuilder($"INSERT INTO {tableName} (id, geom) Values(0,'TIN Z((({triangle.A.x} {triangle.A.y} {triangle.A.z}, {triangle.B.x} {triangle.B.y} {triangle.B.z},{triangle.C.x} {triangle.C.y} {triangle.C.z},{triangle.A.x} {triangle.A.y} {triangle.A.z}))");
    //            for (var i = 1; i < polyhedron.FaceCount; i++)
    //            {
    //                triangle = polyhedron.GetTriangle(i);
    //                tin.Append(
    //                    $",(({triangle.A.x} {triangle.A.y} {triangle.A.z}, {triangle.B.x} {triangle.B.y} {triangle.B.z},{triangle.C.x} {triangle.C.y} {triangle.C.z},{triangle.A.x} {triangle.A.y} {triangle.A.z}))");

    //            }

    //            tin.Append(")')");
    //            cmd.CommandText = tin.ToString();
    //            cmd.ExecuteNonQuery();
    //        }
    //    }
    //}

    //public static void ExportPolyhedronTriangles(Mesh mesh, string tableName, bool truncate = false)
    //{
    //    var connectionString = GetConnectionString();
    //    tableName = tableName.ToLower();

    //    if (!CheckIfTableExist(tableName, connectionString))
    //    {
    //        CreateTable(tableName, connectionString, "(id int, geom GEOMETRY(TINZ))");
    //    }
    //    else
    //    {
    //        if (truncate)
    //        {
    //            TruncateTable(tableName, connectionString);
    //        }
    //    }

    //    var sql = $"INSERT INTO {tableName} (id, geom) Values(";

    //    using (var conn = new NpgsqlConnection(connectionString))
    //    {
    //        conn.Open();
    //        var cmd = new NpgsqlCommand();
    //        cmd.Connection = conn;

    //        var index = 0;

    //        for (var i = 0; i < mesh.triangles.Length; i += 3)
    //        {
    //            var p = mesh.vertices[mesh.triangles[i]];
    //            var q = mesh.vertices[mesh.triangles[i + 1]];
    //            var r = mesh.vertices[mesh.triangles[i + 2]];

    //            cmd.CommandText = $"{sql}{index},'TIN Z((({p.x} {p.y} {p.z}, {q.x} {q.y} {q.z},{r.x} {r.y} {r.z},{p.x} {p.y} {p.z})))')";
    //            cmd.ExecuteNonQuery();
    //            index++;
    //        }
    //    }
    //}

    private static void CreateTable(string tableName, string connectionString, string fields)
    {
        var sql = $"CREATE TABLE {tableName} {fields};";
        var connection = new NpgsqlConnection(connectionString);
        var cmd = EstablishConnectionWithQuery(connection, sql);
        cmd.ExecuteNonQuery();
    }

    private static bool CheckIfTableExist(string tableName, string connectionString)
    {
        var sql = $"SELECT EXISTS ( SELECT FROM pg_tables WHERE schemaname = 'public' AND tablename = '{tableName}' );";
        var connection = new NpgsqlConnection(connectionString);
        var cmd = EstablishConnectionWithQuery(connection, sql);
        return (bool)cmd.ExecuteScalar();
    }

    private static void TruncateTable(string tableName, string connectionString)
    {
        var sql = $"TRUNCATE TABLE {tableName};";
        var connection = new NpgsqlConnection(connectionString);
        var cmd = EstablishConnectionWithQuery(connection, sql);
        cmd.ExecuteNonQuery();
    }

    private static NpgsqlCommand EstablishConnectionWithQuery(NpgsqlConnection connection, string sql)
    {
        connection.Open();
        var cmd = new NpgsqlCommand();
        cmd.Connection = connection;
        cmd.CommandText = sql;
        return cmd;
    }
}
