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

    // ��Ʈ��ũ�� ����ȭ�� �̵� �ӵ� ũ�� ���� 
    [Networked] float magnitude { get; set; }

    // ��Ʈ��ũ�� ����ȭ�� ����� �̸� 
    [Networked] string nickName { get; set; }
    // ȭ�鿡 ǥ���� TextUI 
    public TextMeshProUGUI nameText;
    // UI ������ �Է¹��� ����� �̸� 
    public string myInputName = "Player";

    //void Start() 
    public override void Spawned()
    {
        cc = GetComponent<NetworkCharacterController>();
        // �ٸ� ĳ������ ī�޶�� ��Ȱ��ȭ 
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
        // �ִϸ����� ���� Ʈ�� ������ ������ ũ�⸦ �����Ѵ�. 
        anim.SetFloat("Speed", magnitude);
    }

    public bool GetInput(out Vector3 direction)
    {
        // NetworkBehaviour�� GetInput �Լ��� �Էµ����� �������� 
        if (GetInput(out NetworkInputData data))
        {
            direction = data.direction;
            return true;
        }

        direction = Vector3.zero;
        return true;
    }

    // �̵� ��� 
    void Move()
    {
        Vector3 dir;
        if (GetInput(out dir) == false)
        {
            return;
        }
        dir.Normalize();

        // ĳ������ �̵� ���� ���͸� ī�޶� �ٶ󺸴� ������ �������� �ϵ��� �����Ѵ�. 
        dir = cameraRig.transform.TransformDirection(dir);
        cc.Move(dir * moveSpeed * Time.deltaTime);

        // ����, �޼� �潺ƽ�� ����̸� �� �������� ĳ���͸� ȸ����Ų��. 
        magnitude = dir.magnitude;

        if (magnitude > 0)
        {
            myCharacter.rotation = Quaternion.LookRotation(dir);
        }
    }

    // ȸ�� ���
    void Rotate()
    {
        if (GetInput(out NetworkInputData data))
        {
            // �������� ���� ������ �¿� ���⸦ ������Ų��.
            float rotH = data.rotation;
            // CameraRig ������Ʈ�� ȸ����Ų��.
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
