using UnityEngine;
using CesiumForUnity;
using Unity.Mathematics;
using System.Collections.Generic;
using System.Linq;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;
using GeoAPI.CoordinateSystems.Transformations;

public class DbPolyhedronWrite : MonoBehaviour
{
    public string TableName;
    public bool Truncate;

    private ICoordinateTransformation _wgs84ToUtm;

    public void WritePolyhedronData()
    {
        // Define the WGS84 and UTM coordinate systems
        var wgs84 = GeographicCoordinateSystem.WGS84;
        var utm = ProjectedCoordinateSystem.WGS84_UTM(33, true); // EPSG:25833 (UTM Zone 33N)

        // Create the coordinate transformation
        var transformationFactory = new CoordinateTransformationFactory();
        _wgs84ToUtm = transformationFactory.CreateFromCoordinateSystems(wgs84, utm);

        var cesiumGlobeAnchorComponents = new CesiumGlobeAnchor[] { GetComponent<CesiumGlobeAnchor>() };
        if (cesiumGlobeAnchorComponents[0] == null)
        {
            cesiumGlobeAnchorComponents = GetComponentsInChildren<CesiumGlobeAnchor>();
            if (cesiumGlobeAnchorComponents.Length == 0)
            {
                Debug.Log($"{gameObject.name} does not have CesiumGlobeAnchor component attached.");
                return;
            }
        }

        var centroids = new List<double3>();
        var meshFilters = new List<MeshFilter>();
        var georeference = FindObjectOfType<CesiumGeoreference>();

        if (georeference == null)
        {
            Debug.LogError("CesiumGeoreference component not found in the scene.");
            return;
        }

        foreach (var cesiumGlobeAnchor in cesiumGlobeAnchorComponents)
        {
            var positionUnity = cesiumGlobeAnchor.transform.position;
            var positionEcef = georeference.TransformUnityPositionToEarthCenteredEarthFixed(new double3(positionUnity.x,positionUnity.y, positionUnity.z));
            var positionLlh = CesiumWgs84Ellipsoid.EarthCenteredEarthFixedToLongitudeLatitudeHeight(positionEcef);

            // Reproject from WGS84 (EPSG:4326) to UTM (EPSG:25833)
            var reprojectedLocation = ReprojectToUtm(cesiumGlobeAnchor.longitudeLatitudeHeight);

            foreach (var meshFilter in cesiumGlobeAnchor.gameObject.GetComponents<MeshFilter>())
            {
                meshFilters.Add(meshFilter);
                var meshFilterTranslation = meshFilter.gameObject.transform.position;
                var centroid = new double3(
                    reprojectedLocation[0] - meshFilterTranslation.x,
                    reprojectedLocation[2] - meshFilterTranslation.y,
                    reprojectedLocation[1] - meshFilterTranslation.z);
                centroids.Add(centroid);
            }

            if (!meshFilters.Any())
            {
                foreach (var meshFilter in cesiumGlobeAnchor.gameObject.GetComponentsInChildren<MeshFilter>())
                {
                    meshFilters.Add(meshFilter);
                    var mainParentTranslation = cesiumGlobeAnchor.gameObject.transform.position;
                    var centroid = new double3(
                        reprojectedLocation[0] - mainParentTranslation.x,
                        reprojectedLocation[2] - mainParentTranslation.y,
                        reprojectedLocation[1] - mainParentTranslation.z);
                    centroids.Add(centroid);
                }
            }
        }

        if (!meshFilters.Any())
        {
            Debug.Log("No meshes detected.");
            return;
        }

        var connection = DbCommonFunctions.GetNpgsqlConnection();
        DBexport.ExportMeshesAsPolyhedrons2(meshFilters.ToArray(), connection, centroids.ToArray(), TableName, Truncate);
    }

    private double[] ReprojectToUtm(double3 positionLlh)
    {
        var llh = new double[] { positionLlh.x, positionLlh.y, positionLlh.z };
        return _wgs84ToUtm.MathTransform.Transform(llh);
    }
}
