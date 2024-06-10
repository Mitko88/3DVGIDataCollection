using CesiumForUnity;
using UnityEngine;

public class TouchRaycaster : MonoBehaviour
{
    public GameObject cubePrefab;  // Reference to the cube prefab
    private LineRenderer lineRenderer;
    private Vector3 hitPoint;
    private bool hitDetected;
    public float lineHeight = 10;
    public float lineWidth = 0.5f;
    public GameObject GeoRefFolder;

    void Start()
    {
        // Initialize the LineRenderer component
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

        hitDetected = false;
    }

    void Update()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        HandleMouseInput();
#else
        HandleTouchInput();
#endif
    }

    void HandleMouseInput()
    {
        if (Input.GetMouseButton(0))
        {
            VisualizeRay(Input.mousePosition);
        }

        if (Input.GetMouseButtonUp(0) && hitDetected)
        {
            var gameObject = Instantiate(cubePrefab, hitPoint, Quaternion.identity);
            gameObject.AddComponent<CesiumGlobeAnchor>();
            gameObject.transform.parent = GeoRefFolder.transform;
        }
    }

    void HandleTouchInput()
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began || touch.phase == TouchPhase.Moved)
            {
                VisualizeRay(touch.position);
            }

            if (touch.phase == TouchPhase.Ended && hitDetected)
            {
                var gameobject = Instantiate(cubePrefab, hitPoint, Quaternion.identity);
                gameObject.AddComponent<CesiumGlobeAnchor>();
                gameObject.transform.parent = GeoRefFolder.transform;
            }
        }
        else
        {
            lineRenderer.positionCount = 0;
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
}
