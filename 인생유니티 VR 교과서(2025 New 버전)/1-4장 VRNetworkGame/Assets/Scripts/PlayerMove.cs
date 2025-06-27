using Fusion;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(NetworkCharacterController))]
public class PlayerMove : NetworkBehaviour
{
    public float moveSpeed = 3.0f;
    public float rotSpeed = 200.0f;
    public GameObject cameraRig;
    public Transform myCharacter;
    public Animator anim;
    NetworkCharacterController cc;

    // 네트워크로 동기화될 이동 속도 크기 변수 
    [Networked] float magnitude { get; set; }

    // 네트워크로 동기화될 사용자 이름 
    [Networked] string nickName { get; set; }
    // 화면에 표시할 TextUI 
    public TextMeshProUGUI nameText;
    // UI 등으로 입력받은 사용자 이름 
    public string myInputName = "Player";

    //void Start() 
    public override void Spawned()
    {
        cc = GetComponent<NetworkCharacterController>();
        // 다른 캐릭터의 카메라는 비활성화 
        if (HasInputAuthority == false)
        {
            cameraRig.transform.GetChild(0).gameObject.SetActive(false);
            nameText.text = nickName;
        }
        else
        {
            myInputName += UnityEngine.Random.Range(0, 1000);
            RPC_UpdateNickName(myInputName);
            nameText.color = Color.red;
        }
    }

    //void Update() 
    public override void FixedUpdateNetwork()
    {
        Move();
        Rotate();
    }

    public override void Render()
    {
        // 애니메이터 블랜드 트리 변수에 벡터의 크기를 전달한다. 
        anim.SetFloat("Speed", magnitude);
    }

    public bool GetInput(out Vector3 direction)
    {
        // NetworkBehaviour의 GetInput 함수로 입력데이터 가져오기 
        if (GetInput(out NetworkInputData data))
        {
            direction = data.direction;
            return true;
        }

        direction = Vector3.zero;
        return true;
    }

    // 이동 기능 
    void Move()
    {
        Vector3 dir;
        if (GetInput(out dir) == false)
        {
            return;
        }
        dir.Normalize();

        // 캐릭터의 이동 방향 벡터를 카메라가 바라보는 방향을 정면으로 하도록 변경한다. 
        dir = cameraRig.transform.TransformDirection(dir);
        cc.Move(dir * moveSpeed * Time.deltaTime);

        // 만일, 왼손 썸스틱을 기울이면 그 방향으로 캐릭터를 회전시킨다. 
        magnitude = dir.magnitude;

        if (magnitude > 0)
        {
            myCharacter.rotation = Quaternion.LookRotation(dir);
        }
    }

    // 회전 기능
    void Rotate()
    {
        if (GetInput(out NetworkInputData data))
        {
            // 오른손의 방향 값에서 좌우 기울기를 누적시킨다.
            float rotH = data.rotation;
            // CameraRig 오브젝트를 회전시킨다.
            cameraRig.transform.eulerAngles += new Vector3(0, rotH, 0) * rotSpeed * Runner.DeltaTime;
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_UpdateNickName(string strName)
    {
        nickName = strName;
        RPC_SendAllName(strName);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_SendAllName(string strName)
    {
        nameText.text = strName;
    }
}
