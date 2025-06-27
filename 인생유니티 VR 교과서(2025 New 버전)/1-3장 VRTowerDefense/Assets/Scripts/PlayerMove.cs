using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    // 이동 속도 
    public float speed = 5;
    // CharacterController 컴포넌트 
    CharacterController cc;

    // 중력 가속도의 크기 
    public float gravity = -20;
    // 수직 속도 
    float yVelocity = 0;
    // 점프 크기 
    public float jumpPower = 5;

    void Start()
    {
        cc = GetComponent<CharacterController>();
    }

    void Update()
    {
        // 사용자의 입력에 따라 전후좌우로 이동하고 싶다. 
        // 1. 사용자의 입력을 받는다. 
        float h = ARAVRInput.GetAxis("Horizontal");
        float v = ARAVRInput.GetAxis("Vertical");
        // 2. 방향을 만든다. 
        Vector3 dir = new Vector3(h, 0, v);

        // 2.0 사용자가 바라보는 방향으로 입력 값 변화시키기 
        dir = Camera.main.transform.TransformDirection(dir);

        // 2.1 중력을 적용한 수직 방향 추가 v=v0+at 
        yVelocity += gravity * Time.deltaTime;

        // 2.2 바닥에 있을 경우, 수직 항력을 처리하기 위해 속도를 0으로 한다. 
        if (cc.isGrounded)
        {
            yVelocity = 0;
        }
        // 2.3 사용자가 점프 버튼을 누르면 속도에 점프 크기를 할당한다. 
        if (ARAVRInput.GetDown(ARAVRInput.Button.Two, ARAVRInput.Controller.RTouch))
        {
            yVelocity = jumpPower;
        }

        dir.y = yVelocity;

        // 3. 이동한다. 
        if (cc.enabled)
            cc.Move(dir * speed * Time.deltaTime);
    }
}
