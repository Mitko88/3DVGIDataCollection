using CesiumForUnity;
using GeoAPI.Geometries;
using NetTopologySuite.Geometries;
using Npgsql;
using Photon.Pun;
using Photon.Realtime;
using System.Net;
using Unity.AI.Navigation;
using UnityEngine;

public class PlayerManagment : MonoBehaviour
{
    [SerializeField] PhotonView photonView;

    // geo location
    private CesiumGlobeAnchor cameraGeoPosition;
    private Transform cesiumGeorefTransform;

    // navmesh 
    private DynamicNavMesh dynamicNavMesh;

    // hit point helper
    private LineRenderer lineRenderer;
    private Vector3 hitPoint;
    private bool hitDetected;
    public float lineHeight = 100;
    public float lineWidth = 0.5f;

    // onEnable update
    public bool pointIsSuccesfullyAdded;
    private GameObject pickedPoint;
    private GameObject closestPointOnLine;
    private void Start()
    {
        if (photonView.IsMine)
        {
            photonView.gameObject.AddComponent<AudioListener>();
            photonView.gameObject.GetComponent<Camera>().tag = "MainCamera";
            photonView.gameObject.AddComponent<CesiumCameraController>();
            photonView.gameObject.GetComponent<Camera>().enabled = true;
            cameraGeoPosition = photonView.gameObject.GetComponent<CesiumGlobeAnchor>();
        }
        else
        {
            DestroyImmediate(photonView.gameObject.GetComponent<CesiumOriginShift>());
            DestroyImmediate(photonView.gameObject.GetComponent<CesiumFlyToController>());
        }

        //raycasting
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            Debug.LogError("LineRenderer component missing from this GameObject. Please add a LineRenderer component.");
            return;
        }

        // Set some default properties for the line renderer
        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;

        //georef
        cesiumGeorefTransform = FindObjectOfType<CesiumGeoreference>().transform;

        //nav mesh
        dynamicNavMesh = FindObjectOfType<DynamicNavMesh>();
    }

    void Update()
    {
        if (photonView.IsMine)
        {
            // Call the RPC to update the value on remote clients
            photonView.RPC("SyncCameraPosition", RpcTarget.AllBuffered, new object[4]
            { cameraGeoPosition.latitude, cameraGeoPosition.longitude, cameraGeoPosition.height, photonView.ViewID });

            if (Input.GetMouseButton(0))
            {
                VisualizeRay(Input.mousePosition);
            }

            if (Input.GetMouseButtonUp(0) && hitDetected)
            {
                InstatiateAndSyncObject();
                //reset lineRenderer
                lineRenderer.positionCount = 0;
            }
        }
    }

    void OnEnable()
    {
        if (!pointIsSuccesfullyAdded)
        {
            if(pickedPoint != null) {
                PhotonNetwork.Destroy(pickedPoint);
            }
            if (closestPointOnLine != null)
            {
                PhotonNetwork.Destroy(closestPointOnLine);
            }
        }
    }

    [PunRPC]
    void SyncCameraPosition(double latitude, double longitude, double height, int userId)
    {
        var targetPhotonView = PhotonView.Find(userId);

        if (targetPhotonView != null && !targetPhotonView.IsMine)
        {
            var globalPosition = targetPhotonView.GetComponent<CesiumGlobeAnchor>();
            globalPosition.latitude = latitude;
            globalPosition.longitude = longitude;
            globalPosition.height = height;
        }
    }

    [PunRPC]
    void SetParentAndName(int viewID)
    {
        // Find the instantiated GameObject by its PhotonView ID
        GameObject playerObject = PhotonView.Find(viewID).gameObject;
        // Set its parent to geoRefGO.transform
        playerObject.transform.parent = FindObjectOfType<CesiumGeoreference>().transform;
    }

    private void InstatiateAndSyncObject()
    {
        // Instantiate the cube across the network
        pickedPoint = PhotonNetwork.Instantiate("Cube", hitPoint, Quaternion.identity);

        // Set the parent to geoRefGO
        pickedPoint.transform.parent = cesiumGeorefTransform;
        var objectGlobalPosition = pickedPoint.GetComponent<CesiumGlobeAnchor>();

        //update object location on other players
        photonView.RPC("InstantiateGeoRefObject", RpcTarget.AllBuffered, new object[4]
            { objectGlobalPosition.latitude, objectGlobalPosition.longitude, objectGlobalPosition.height, pickedPoint.GetComponent<PhotonView>().ViewID });

        //get the closest point to road segments
        var closestLinePoint = ClosestPointOnLineSegment(objectGlobalPosition.latitude,objectGlobalPosition.longitude);
        if (closestLinePoint != null)
        {
            closestPointOnLine = PhotonNetwork.Instantiate("Cube", Vector3.zero, Quaternion.identity);

            closestPointOnLine.transform.parent = cesiumGeorefTransform;
            var closestPointGlobalPosition = closestPointOnLine.GetComponent<CesiumGlobeAnchor>();
            closestPointGlobalPosition.latitude = closestLinePoint.Y;
            closestPointGlobalPosition.longitude = closestLinePoint.X;
            closestPointGlobalPosition.height = objectGlobalPosition.height;

            //update object location on other players
            photonView.RPC("InstantiateGeoRefObject", RpcTarget.AllBuffered, new object[4]
                { closestPointGlobalPosition.latitude, closestPointGlobalPosition.longitude, closestPointGlobalPosition.height, closestPointOnLine.GetComponent<PhotonView>().ViewID });

            if (dynamicNavMesh != null)
            {
                StartCoroutine(dynamicNavMesh.UpdateNavMesh(pickedPoint.transform.position, closestPointOnLine.transform.position));
            }
        }
    }

    [PunRPC]
    public void InstantiateGeoRefObject(double latitude, double longitude, double height, int userId)
    {
        GameObject playerObject = PhotonView.Find(userId).gameObject;

        if (playerObject != null)
        {
            playerObject.transform.parent = cesiumGeorefTransform;
            var globalPosition = playerObject.GetComponent<CesiumGlobeAnchor>();
            globalPosition.latitude = latitude;
            globalPosition.longitude = longitude;
            globalPosition.height = height;
        }
    }

    void VisualizeRay(Vector3 screenPosition)
    {
        Ray ray = Camera.main.ScreenPointToRay(screenPosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            hitPoint = hit.point;
            hitDetected = true;

            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, new Vector3(hit.point.x, hit.point.y + lineHeight, hit.point.z));
            lineRenderer.SetPosition(1, hit.point);
        }
        else
        {
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, new Vector3(hit.point.x, hit.point.y + lineHeight, hit.point.z));
            lineRenderer.SetPosition(1, ray.origin + ray.direction * 100);
            hitDetected = false;
        }
    }

    private Point ClosestPointOnLineSegment(double latitude, double longitude)
    {
        var sql = $"Select ST_ClosestPoint(geom,ST_GeomFromText('POINT({longitude} {latitude})',4326))," +
            $"ST_Distance(roads_muenster.geom, ST_GeomFromText('POINT({longitude} {latitude})',4326)) AS distance from roads_muenster " +
            $"ORDER BY roads_muenster.geom <-> ST_GeomFromText('POINT({longitude} {latitude})',4326) LIMIT 1;";
        var connection = DbCommonFunctions.GetNpgsqlConnection();
        connection.Open();
        connection.TypeMapper.UseNetTopologySuite();
        var cmd = new NpgsqlCommand(sql, connection);
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                // Read geom
                return (Point)reader[0];
            }
        }
        connection.Close();

        return null;
    }

    public CesiumGlobeAnchor GetPickedPoint()
    { 
        return pickedPoint.GetComponent<CesiumGlobeAnchor>();
    }

    public PhotonView GetPhotonView()
    {
        return photonView;
    }
}

