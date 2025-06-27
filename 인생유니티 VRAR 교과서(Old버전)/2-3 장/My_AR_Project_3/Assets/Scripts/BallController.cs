using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BallController : MonoBehaviour
{
    public float resetTime = 3.0f;
    public Text result;
    public float captureRate = 0.5f;
    public GameObject effect;

    Rigidbody rb;
    bool isReady = true;
    Vector2 startPos;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;

        // UI 텍스트를 공백으로 초기화한다.
        result.text = "";
    }

    void Update()
    {
        // 공이 날아가고 있는 중에는 업데이트를 종료한다.
        if(!isReady)
        {
            return;
        }

        SetBallPosition(Camera.main.transform);

        // 만일, 사용자가 화면을 터치하고 있다면...
        if(Input.touchCount > 0 && isReady)
        {
            Touch touch = Input.GetTouch(0);

            if(touch.phase == TouchPhase.Began)
            {
                // 터치 시작 위치를 저장한다.
                startPos = touch.position;
            }

            else if (touch.phase == TouchPhase.Ended)
            {
                // 터치가 시작된 지점에서 끝나는 지점까지의 거리를 구한다.
                float dragDistance = touch.position.y - startPos.y;

                // 공이 날아갈 방향을 구한다.
                Vector3 throwAngle = (Camera.main.transform.forward + Camera.main.transform.up).normalized;

                // 물리 능력을 활성화하고 준비 상태를 false로 변경한다.
                rb.isKinematic = false;
                isReady = false;

                // 공을 날아갈 방향과 힘을 이용하여 물리적으로 발사한다.
                rb.AddForce(throwAngle * dragDistance * 0.005f, ForceMode.VelocityChange);

                // 지정된 시간 뒤에 ResetBall() 함수가 실행되도록 예약을 한다.
                Invoke("ResetBall", resetTime);
            }
        }

    }

    void SetBallPosition(Transform anchor)
    {
        // 카메라로부터 앞쪽으로 0.5미터, 아래쪽으로 0.2미터 위치를 잡는다.
        Vector3 offset = anchor.forward * 0.5f + anchor.up * -0.2f;

        // 공의 위치를 카메라로부터 특정 위치에 놓는다(위치 보정).
        transform.position = anchor.position + offset;
    }

    void ResetBall()
    {
        // 물리 능력을 비활성화하고 속도도 초기화한다.
        rb.isKinematic = true;
        rb.velocity = Vector3.zero;

        // 준비 상태로 돌려놓는다.
        isReady = true;

        // 공을 활성화한다.
        gameObject.SetActive(true);
    }

    private void OnCollisionEnter(Collision collision)
    {
        // 준비 상태라면 충돌 처리 이벤트 함수를 그냥 종료시킨다.
        if(isReady)
        {
            return;
        }

        // 포획 확률을 추첨한다.
        float draw = Random.Range(0, 1);

        if(draw <= captureRate)
        {
            result.text = "포획 성공!";

            // DB 데이터의 포획 여부를 변경한다.
            DB_Manager.instance.UpdateCaptured();
        }
        else
        {
            result.text = "포획에 실패하여 도망쳤습니다...";
        }

        // 고양이 캐릭터를 제거한다.
        Destroy(collision.gameObject);

        // 이펙트를 생성한다.
        Instantiate(effect, collision.transform.position, Camera.main.transform.rotation);

        // 공을 비활성화한다.
        gameObject.SetActive(false);
    }
}
