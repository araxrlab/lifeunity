using UnityEngine;

public class CamRotate : MonoBehaviour
{
    // 현재 각도
    Vector3 angle;
    // 마우스 감도
    public float sensitivity = 200;

    void Start()
    {
        // 시작할 때 현재 카메라의 각도를 적용한다.
        angle = Camera.main.transform.eulerAngles;
        angle.x *= -1;
    }

    void Update()
    {
        // 마우스 입력에 따라 카메라를 회전시키고 싶다.
        // 1. 사용자의 마우스 입력을 얻어와야 한다.
        // 마우스의 좌우 입력을 받는다.
        float x = Input.GetAxis("Mouse Y");
        float y = Input.GetAxis("Mouse X");
        // 2. 방향이 필요하다.
        // 이동 공식에 대입해 각 속성별로 회전 값을 누적시킨다.
        angle.x += x * sensitivity * Time.deltaTime;
        angle.y += y * sensitivity * Time.deltaTime;

        angle.x = Mathf.Clamp(angle.x, -90, 90);
        // 3. 회전시키고 싶다.
        // 카메라의 회전 값에 새로 만들어진 회전 값을 할당한다.
        transform.eulerAngles = new Vector3(-angle.x, angle.y, transform.eulerAngles.z);
    }
}
