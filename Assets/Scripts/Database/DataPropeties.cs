using UnityEngine;

public class DataPropeties : MonoBehaviour
{
    public string[] Propeties;

    public DataPropeties(int propertiesCount)
    {
        Propeties = new string[propertiesCount];
    }
}
