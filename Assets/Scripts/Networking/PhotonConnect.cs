using CesiumForUnity;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class PhotonConnect : MonoBehaviourPunCallbacks
{
    public string playerName;
    private Transform geoRefTransform;

    private void Start()
    {
        geoRefTransform = FindObjectOfType<CesiumGeoreference>().transform;
        // Connect to Photon
        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    public override void OnConnectedToMaster()
    {
        // Automatically join a room upon connecting to the Photon Master server
        PhotonNetwork.JoinOrCreateRoom("MainRoom", new RoomOptions { MaxPlayers = 20 }, TypedLobby.Default);
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("Joined room: " + PhotonNetwork.CurrentRoom.Name);
        // Instantiate the player avatar from the Resources folder
        var playerObject = PhotonNetwork.Instantiate("Camera", Vector3.zero, Quaternion.identity);
        playerObject.transform.parent = geoRefTransform;
        playerObject.name = playerName;
        var photonView = playerObject.GetComponent<PhotonView>();
        PhotonNetwork.LocalPlayer.NickName = playerName;
        photonView.RPC("SetParentAndName", RpcTarget.AllBuffered, photonView.ViewID);
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError("Failed to join room: " + message);
        // Handle room join failure if necessary
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError("Failed to create room: " + message);
        // Handle room creation failure if necessary
    }
}
