using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class ConnManager : MonoBehaviourPunCallbacks
{
    void Start()
    {
        // 어플리케이션의 버전을 설정한다.
        PhotonNetwork.GameVersion = "0.1";

        // 사용자의 이름을 설정한다.(Player0001 등)
        int num = Random.Range(0, 1000);
        PhotonNetwork.NickName = "Player" + num.ToString();

        // 마스터 클라이언트(방장)가 구성한 씬 환경을 방에 접속한 플레이어들과 자동 동기화한다.
        PhotonNetwork.AutomaticallySyncScene = true;

        // 마스터 서버에 접속한다.
        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("마스터 서버에 접속 완료!");

        // 로비에 진입한다.
        PhotonNetwork.JoinLobby(TypedLobby.Default);
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("로비에 접속 완료!");

        // 방에 대한 설정을 한다.
        RoomOptions ro = new RoomOptions()
        {
            IsVisible = true,
            IsOpen = true,
            MaxPlayers = 8
        };

        // 방에 들어가거나 방을 생성한다.
        PhotonNetwork.JoinOrCreateRoom("NetTest", ro, TypedLobby.Default);
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("룸에 입장!");

        // 반경 2미터 이내에 랜덤한 위치에 플레이어 캐릭터를 생성한다.
        Vector2 originPos = Random.insideUnitCircle * 2.0f;

        PhotonNetwork.Instantiate("Player", new Vector3(originPos.x, 0, originPos.y), Quaternion.identity);
    }

    //Vector2 stickPos = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

    //float rotH = Input.GetAxis("Mouse X");
}
