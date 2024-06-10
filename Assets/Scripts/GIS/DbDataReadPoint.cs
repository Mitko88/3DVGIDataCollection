using UnityEngine;

#if UNITY_EDITOR
public class DbDataReadPoint : MonoBehaviour
{
    public string TableName;
    public GameObject Prefab;
    [Range(0.1f, 5f)]
    public float ScaleSize = 1f;
    public Material Material;

    public void LoadDataFromDb()
    {
        var connection = DbCommonFunctions.GetNpgsqlConnection();
        DBquery.LoadPointData(connection, TableName, this, Material, Prefab, ScaleSize);
    }
}
#endif
