using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using UnityEngine.UI;

public class PlayerMove : MonoBehaviourPun, IPunObservable
{
    public GameObject cameraRig;
    public Transform myCharacter;
    public Animator anim;
    public float moveSpeed = 3.0f;
    public float rotSpeed = 200.0f;
    public Text nameText;

    Vector3 setPos;
    Quaternion setRot;
    float dir_Speed = 0;

    void Start()
    {
        // 내 앱의 내 캐릭터일 경우에만 카메라 장치를 활성화한다.
        cameraRig.SetActive(photonView.IsMine);

        // nameText에 사용자 자신의 이름을 출력한다.
        nameText.text = photonView.Owner.NickName;

        // 내 앱의 내 캐릭터일 경우에는 이름의 색상을 빨강색으로 하고,
        // 상대방 캐릭터일 경우에는 이름의 색상을 녹색으로 한다.
        if (photonView.IsMine)
        {
            nameText.color = Color.green;
        }
        else
        {
            nameText.color = Color.red;
        }
    }

    void Update()
    {
        Move();
        Rotate();
    }

    // 이동 기능
    void Move()
    {
        // 내 앱의 내 캐릭터일 경우...
        if (photonView.IsMine)
        {
            // 왼손 썸스틱의 기울기 만큼 방향 값을 이용해서 캐릭터의 이동 방향을 결정한다.
            //Vector2 stickPos = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LHand);
            Vector2 stickPos = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

            Vector3 dir = new Vector3(stickPos.x, 0, stickPos.y);
            dir.Normalize();

            // 카메라의 방향을 기준으로 이동 방향 벡터의 정면 방향을 변경한다.
            dir = cameraRig.transform.TransformDirection(dir);
            transform.position += dir * moveSpeed * Time.deltaTime;

            // 캐릭터의 정면 방향을 이동 방향에 맞게 회전시킨다.
            float magnitude = dir.magnitude;

            if (magnitude > 0)
            {
                myCharacter.rotation = Quaternion.LookRotation(dir);
            }

            // 애니메이션 블랜드 트리에 이동 벡터의 크기를 전달한다.
            anim.SetFloat("Speed", magnitude);
        }
        // 다른 사람의 앱의 나의 캐릭터일 경우(동기화)
        else
        {
            // 서버로부터 읽어온 값으로 이동 또는 회전을 한다.
            transform.position = Vector3.Lerp(transform.position, setPos, Time.deltaTime * 20.0f);
            myCharacter.transform.rotation = Quaternion.Lerp(myCharacter.transform.rotation, setRot, Time.deltaTime * 20.0f);
            anim.SetFloat("Speed", dir_Speed);
        }
    }

    // 회전 기능
    void Rotate()
    {
        if (photonView.IsMine)
        {
            // 오른쪽 썸스틱의 기울기 값을 구한다.
            //float rotH = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch).x;
            float rotH = Input.GetAxis("Mouse X");

            // 좌우 스틱 값에 비례해서 카메라를 회전시킨다.
            cameraRig.transform.eulerAngles += new Vector3(0, rotH, 0) * rotSpeed * Time.deltaTime;
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        // 만일, 서버에 전송을 하는 상황이라면...
        if(stream.IsWriting)
        {
            stream.SendNext(transform.position);
            stream.SendNext(myCharacter.rotation);
            stream.SendNext(anim.GetFloat("Speed"));
        }
        // 그렇지 않고 만일 서버로부터 전송을 받는 상황이라면...
        else if(stream.IsReading)
        {
            setPos = (Vector3)stream.ReceiveNext();
            setRot = (Quaternion)stream.ReceiveNext();
            dir_Speed = (float)stream.ReceiveNext();
        }
    }
}
