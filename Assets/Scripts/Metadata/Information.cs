using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Information : MonoBehaviour
{
    public Transform Container;
    public Transform Template;
    public RectTransform Background;
    private readonly float _templateHeight = 120f;
    private GameObject _templateObj;
    private List<GameObject> _templateRows;
    private Renderer _selectedObjRenderer;
    private Material _selectionMaterial;
    private Material _initialObjectMaterial;

    void Start()
    {
        Template.gameObject.SetActive(false);
        _templateObj = this.transform.GetChild(0).gameObject;
        _templateRows = new List<GameObject>();
        _selectionMaterial =  new Material(Shader.Find("Universal Render Pipeline/Lit"));
        _selectionMaterial.color = Color.yellow;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            RaycastHit hit;
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out hit))
            {
                _templateObj.SetActive(true);
                UpdateInfo(hit);
            }
            else
            {
                _templateObj.SetActive(false);
                if (_selectedObjRenderer != null)
                {
                    _selectedObjRenderer.material = _initialObjectMaterial;
                }
            }
        }
    }

    private void UpdateInfo(RaycastHit hit)
    {
        var selectedObj = hit.transform.gameObject;

        var data = selectedObj.GetComponent<DataPropeties>();

        if (data == null)
        {
            _templateObj.SetActive(false);
        }
        else
        {
            var properties = data.Propeties;

            var metadata = _templateObj.GetComponent<RectTransform>();
            var height = _templateHeight * properties.Length / 10 + 20;
            Background.sizeDelta = new Vector2(250, height);
            metadata.sizeDelta = Background.sizeDelta;
            metadata.anchoredPosition3D = new Vector3(metadata.anchoredPosition3D.x, -height / 2 - 3, 0);

            foreach (var templateGameObject in _templateRows)
            {
                Destroy(templateGameObject);
            }

            _templateRows.Clear();

            for (var i = 1; i < properties.Length; i++)
            {
                var entityTransform = Instantiate(Template, Container);
                var entityRectTransform = entityTransform.GetComponent<RectTransform>();
                entityRectTransform.anchoredPosition = new Vector2(0, -_templateHeight * i);
                entityTransform.gameObject.SetActive(true);

                var property = properties[i].Split(":");
                entityTransform.GetChild(0).GetComponent<Text>().text = property[0];
                entityTransform.GetChild(1).GetComponent<Text>().text = property[1];
                _templateRows.Add(entityTransform.gameObject);
            }

            //color update
            var objRenderer = selectedObj.GetComponent<Renderer>();
            if (_selectedObjRenderer != null)
            {
                _selectedObjRenderer.material = _initialObjectMaterial;
            }

            _selectedObjRenderer = objRenderer;

            if (_initialObjectMaterial == null)
            {
                _initialObjectMaterial = Instantiate(selectedObj.GetComponent<Renderer>().material);
            }
            else
            {
                if (_initialObjectMaterial != _selectedObjRenderer.material)
                {
                    _initialObjectMaterial = Instantiate(selectedObj.GetComponent<Renderer>().material);
                }
            }

            _selectedObjRenderer.material = _selectionMaterial;
        }
    }
}
