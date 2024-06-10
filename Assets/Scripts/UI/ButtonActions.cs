using CesiumForUnity;
using NetTopologySuite.Geometries;
using Npgsql;
using Photon.Pun.Demo.PunBasics;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ButtonActions : MonoBehaviour
{
    [SerializeField] GameObject panel;
    public void OnConfirm()
    {
        var playerManagers = UnityEngine.Object.FindObjectsOfType<PlayerManagment>();
        var playerManagement = playerManagers[0];
        var photonView = playerManagement.GetPhotonView();
        if (!photonView.IsMine)
        {
            for (int i = 1; i < playerManagers.Length; i++)
            {
                playerManagement = playerManagers[i];
                photonView = playerManagement.GetPhotonView();
                if (photonView.IsMine)
                    break;
            }
        }

        var pickedPoint = playerManagement.GetPickedPoint();
        var entranceType = GameObject.FindGameObjectWithTag("entrance").GetComponent<TMP_Dropdown>();
        var comment = GameObject.FindGameObjectWithTag("comment").GetComponent<TMP_InputField>();
        var pathGeoreferencedPoints = UnityEngine.Object.FindObjectOfType<DynamicNavMesh>().GetPathGeoreferencedPoints();

        // create tables PostgreSQL
        var connection = DbCommonFunctions.GetNpgsqlConnection();
        string fields = "(id int, geom GEOMETRY(POINTZ), type text, comment text)";
        connection.Open();
        DbCommonFunctions.CreateTableIfNotExistOrTruncate("entrances_new", connection, fields, false);
        fields = "(id int, geom GEOMETRY(LINESTRINGZ))";
        DbCommonFunctions.CreateTableIfNotExistOrTruncate("paths_new", connection, fields, false);

        // Insert into point table
        var query = $"INSERT INTO entrances_new (geom, type, comment) VALUES( ST_GeomFromText('POINTZ({pickedPoint.longitude} {pickedPoint.latitude} {pickedPoint.height})', 4326)," +
            $" '{entranceType.options[entranceType.value].text}', '{comment.text}')";
        var cmd = new NpgsqlCommand(query, connection);
        cmd.ExecuteNonQuery();

        // Insert into linestring table
        var firstPoint = pathGeoreferencedPoints[0];
        query = $"INSERT INTO paths_new (geom) VALUES( ST_MakeLine(ARRAY[ST_MakePoint({firstPoint.x},{firstPoint.y},{firstPoint.z})";
        using (var conn = connection)
        {
            cmd = new NpgsqlCommand();
            cmd.Connection = conn;
            var sql = new System.Text.StringBuilder(query);
            for (var i = 1; i < pathGeoreferencedPoints.Count; i++)
            {
                var point = pathGeoreferencedPoints[i];
                sql.Append($", ST_MakePoint({point.x},{point.y},{point.z})");
            }
            sql.Append("]))");

            cmd.CommandText = sql.ToString();
            cmd.ExecuteNonQuery();
        }
        connection.Close();

        // Turn on camera controller and playerManagement script
        UnityEngine.Object.FindObjectOfType<CesiumCameraController>().enabled = true;
        playerManagement.pointIsSuccesfullyAdded = true;
        playerManagement.enabled = true;
        panel.SetActive(false);
    }

    public void OnClose()
    {
        // Turn on camera controller and playerManagement script
        UnityEngine.Object.FindObjectOfType<CesiumCameraController>().enabled = true;
        var playerManagers = UnityEngine.Object.FindObjectsOfType<PlayerManagment>();
        var playerManagement = playerManagers[0];
        var photonView = playerManagement.GetPhotonView();
        if (!photonView.IsMine)
        {
            for (int i = 1; i < playerManagers.Length; i++)
            {
                playerManagement = playerManagers[i];
                photonView = playerManagement.GetPhotonView();
                if (photonView.IsMine)
                    break;
            }
        }
        playerManagement.pointIsSuccesfullyAdded = false;
        playerManagement.enabled = true;
        panel.SetActive(false);
        
        // Remove lineObject if existant;
        var dynamicNavMesh = UnityEngine.Object.FindObjectOfType<DynamicNavMesh>();
        dynamicNavMesh.DestroyLineObject();
    }
}
