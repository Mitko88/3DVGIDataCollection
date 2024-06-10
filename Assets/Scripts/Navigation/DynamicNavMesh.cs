using CesiumForUnity;
using GeoAPI.Geometries;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using TMPro;
using Unity.AI.Navigation;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class DynamicNavMesh : MonoBehaviour
{
    public NavMeshSurface navMeshSurface;
    public BoxCollider cesiumBoxExcluderCollider;
    private NavMeshPath path;
    private float extent = 100;
    public CesiumGeoreference cesiumGeoreference;
    [SerializeField] GameObject infoPanel;
    [SerializeField] TMP_Text targetVisibility;
    [SerializeField] TMP_Text pathLength;
    [SerializeField] TMP_Text pathSlope;
    [SerializeField] Toggle visualizeNavMesh;
    private GameObject navMeshObject;

    // used to delete LineObject if user does not approve data creation
    private GameObject lineObject;

    void Start()
    {
        path = new NavMeshPath();
        if (navMeshSurface == null)
        {
            navMeshSurface = GetComponent<NavMeshSurface>();
        }
    }

    public IEnumerator UpdateNavMesh(Vector3 pointA, Vector3 pointB)
    {
        // Calculate the center of the bounds
        Vector3 center = (pointA + pointB) / 2;

        // Calculate the size of the bounds
        Vector3 size = new Vector3(
            Mathf.Abs(pointA.x - pointB.x),
            Mathf.Abs(pointA.y - pointB.y),
            Mathf.Abs(pointA.z - pointB.z)
        );

        // Ensure the bounds are at least 10x10 in XZ plane and within limits
        size = new Vector3(Mathf.Max(size.x, extent), Mathf.Max(size.y, 1000), Mathf.Max(size.z, extent));

        // Define the bounds for the NavMesh update
        Bounds bounds = new Bounds(center, size);

        cesiumBoxExcluderCollider.size = size;
        yield return null;
        // Update the NavMeshSurface bounds and rebuild the NavMesh
        navMeshSurface.center = bounds.center;
        navMeshSurface.size = bounds.size;
        navMeshSurface.BuildNavMesh();

        CalculateAndDrawPath(pointA, pointB);
        cesiumBoxExcluderCollider.size = new Vector3(1000,10000,1000);
    }

    public void CalculateAndDrawPath(Vector3 startPoint, Vector3 endPoint)
    {
        if (startPoint != null && endPoint != null)
        {
            Vector3 startPointOnNavMesh = Vector3.zero;
            NavMeshHit navMeshHit;
            if (NavMesh.SamplePosition(startPoint, out navMeshHit, 5f, NavMesh.AllAreas))
            {
                startPointOnNavMesh = navMeshHit.position; // Return the closest point on the NavMesh
            }
            else
            {
                Debug.Log("StartPoint not detected");
            }

            Vector3 endPointOnNavMesh = Vector3.zero;
            if (NavMesh.SamplePosition(endPoint, out navMeshHit, 5f, NavMesh.AllAreas))
            {
                endPointOnNavMesh = navMeshHit.position; // Return the closest point on the NavMesh
            }
            else
            {
                Debug.Log("EndPoint not detected");
            }

            if (startPointOnNavMesh != Vector3.zero && endPointOnNavMesh != Vector3.zero)
            {
                // Calculate the path from startPoint to endPoint
                NavMesh.CalculatePath(startPointOnNavMesh, endPointOnNavMesh, NavMesh.AllAreas, path);

                if (path.corners.Length < 2)
                {
                    Debug.Log("Path not identified");
                    return;
                }

                var playerManagers = Object.FindObjectsOfType<PlayerManagment>();

                var playerManager = playerManagers[0];
                var photonView = playerManager.GetPhotonView();
                if (!photonView.IsMine)
                {
                    for (int i = 1; i < playerManagers.Length; i++)
                    {
                        playerManager = playerManagers[i];
                        photonView = playerManager.GetPhotonView();
                        if (photonView.IsMine)
                            break;
                    }
                }

                // Visualize the path
                DrawPathAsMesh(path, photonView);

                // Update panel info
                UpdateInfoPanel(path, playerManager);
            }
            else
            {
                Debug.Log("Path not identified");
            }
        }
    }

    void DrawPathAsMesh(NavMeshPath path, PhotonView photonView)
    {
        // Create a new GameObject for the line
        lineObject = PhotonNetwork.Instantiate("MeshLine", Vector3.zero, Quaternion.identity);

        // Add the necessary components
        MeshFilter meshFilter = lineObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = lineObject.AddComponent<MeshRenderer>();

        // Create a new mesh
        Mesh mesh = new Mesh();

        // Define the vertices and indices for the thick line
        int numSegments = path.corners.Length - 1;
        Vector3[] vertices = new Vector3[numSegments * 4];
        int[] indices = new int[numSegments * 6];

        for (int i = 0; i < numSegments; i++)
        {
            Vector3 start = path.corners[i];
            Vector3 end = path.corners[i + 1];
            Vector3 direction = (end - start).normalized;
            Vector3 offset = Vector3.Cross(direction, Vector3.up) * 0.2f * 0.5f;

            // Four vertices per segment (two quads)
            vertices[i * 4 + 0] = start - offset;
            vertices[i * 4 + 1] = start + offset;
            vertices[i * 4 + 2] = end - offset;
            vertices[i * 4 + 3] = end + offset;

            // Six indices per segment (two triangles)
            indices[i * 6 + 0] = i * 4 + 0;
            indices[i * 6 + 1] = i * 4 + 1;
            indices[i * 6 + 2] = i * 4 + 2;
            indices[i * 6 + 3] = i * 4 + 1;
            indices[i * 6 + 4] = i * 4 + 3;
            indices[i * 6 + 5] = i * 4 + 2;
        }

        // Assign the vertices and indices to the mesh
        mesh.vertices = vertices;
        mesh.triangles = indices;

        // Assign the mesh to the MeshFilter
        meshFilter.mesh = mesh;

        // Create and assign a material to the MeshRenderer
        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.color = UnityEngine.Color.yellow;
        meshRenderer.material = material;

        // Optional: Set the position and other properties of the line object
        lineObject.transform.position = Vector3.zero;
        lineObject.transform.parent = cesiumGeoreference.transform;
        var objectGlobalPosition = lineObject.AddComponent<CesiumGlobeAnchor>();

        //update object location on other players
        photonView.RPC("InstantiateGeoRefObject", RpcTarget.AllBuffered, new object[4]
            { objectGlobalPosition.latitude, objectGlobalPosition.longitude, objectGlobalPosition.height, lineObject.GetComponent<PhotonView>().ViewID });
    }

    void UpdateInfoPanel(NavMeshPath path, PlayerManagment playerManagment)
    {
        infoPanel.SetActive(true);

        //intervisibility
        var startPoint = path.corners[0];
        var endPoint = path.corners[path.corners.Length-1];
        var pointsDifference = startPoint - endPoint;
        var maxDistance = pointsDifference.magnitude - 0.1f;
        RaycastHit hit;
        if (Physics.Raycast(endPoint, pointsDifference.normalized, out hit, maxDistance))
        {
            targetVisibility.text = "Target visible: false";
            GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);

            // Set the position and rotation
            primitive.transform.position = hit.point;
            primitive.transform.rotation = Quaternion.identity;
        }
        else
        {
            targetVisibility.text = "Target visible: true";
        }

        //path length
        float length = 0.0f;
        for (int i = 1; i < path.corners.Length; i++)
        {
            length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        }
        pathLength.text = "Path length: " + length.ToString() + " m";

        //path slope
        var averageSlope = GetAverageSlope(path);
        pathSlope.text = "Average path slope: " + averageSlope.ToString() + " %";

        // Turn off camera controller and playerManagement script
        Object.FindObjectOfType<CesiumCameraController>().enabled = false;
        playerManagment.enabled = false;
    }

    float GetAverageSlope(NavMeshPath path)
    {
        float totalSlope = 0.0f;
        int segmentCount = path.corners.Length - 1;

        for (int i = 1; i < path.corners.Length; i++)
        {
            Vector3 previousCorner = path.corners[i - 1];
            Vector3 currentCorner = path.corners[i];

            float horizontalDistance = Vector3.Distance(new Vector3(previousCorner.x, 0, previousCorner.z), new Vector3(currentCorner.x, 0, currentCorner.z));
            float verticalDistance = currentCorner.y - previousCorner.y;

            // Avoid division by zero
            if (horizontalDistance != 0)
            {
                float slope = (verticalDistance / horizontalDistance) * 100; // Convert to percentage
                totalSlope += slope;
            }
        }

        return totalSlope / segmentCount;
    }

    public void VisualizeNavigationMesh()
    {
        if (visualizeNavMesh.isOn)
        {
            // Extract the triangulated mesh data from the NavMesh
            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();

            // Create a new Mesh
            Mesh mesh = new Mesh();
            mesh.vertices = triangulation.vertices;
            mesh.triangles = triangulation.indices;

            // Optional: Calculate normals for better visualization
            mesh.RecalculateNormals();

            // Create a GameObject to visualize the mesh
            navMeshObject = new GameObject("NavMeshVisualization", typeof(MeshFilter), typeof(MeshRenderer));
            navMeshObject.GetComponent<MeshFilter>().mesh = mesh;
            // Create and assign a material to the MeshRenderer
            Material material = Resources.Load<Material>("Materials/NavMesh");

            navMeshObject.GetComponent<MeshRenderer>().material = material;
            navMeshObject.AddComponent<CesiumGlobeAnchor>();
            navMeshObject.transform.parent = cesiumGeoreference.transform;
        }
        else
        {
            if (navMeshObject != null)
            {
                DestroyImmediate(navMeshObject);
            }
        }
    }

    public void DestroyLineObject()
    {
        if (lineObject != null)
        {
            PhotonNetwork.Destroy(lineObject);
            DestroyImmediate(navMeshObject);
        }
    }

    public List<double3> GetPathGeoreferencedPoints()
    {
        var georeferencesPathPoints = new List<double3>();
        for (int i = 0;i<path.corners.Length;i++)
        {
            var pointDouble = new double3(path.corners[i].x, path.corners[i].y, path.corners[i].z);
            var positionEcef = cesiumGeoreference.TransformUnityPositionToEarthCenteredEarthFixed(pointDouble);
            georeferencesPathPoints.Add(CesiumWgs84Ellipsoid.EarthCenteredEarthFixedToLongitudeLatitudeHeight(positionEcef));
        }
        return georeferencesPathPoints;
    }
}
