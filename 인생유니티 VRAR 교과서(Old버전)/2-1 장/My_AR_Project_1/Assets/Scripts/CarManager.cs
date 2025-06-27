using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.EventSystems;

public class CarManager : MonoBehaviour
{
    public GameObject indicator;
    public GameObject myCar;
    public float relocateDistance = 1.0f;

    ARRaycastManager arManager;
    GameObject placedObject;

    void Start()
    {
        // AR Raycast Manager 컴포넌트를 가져온다.
        arManager = GetComponent<ARRaycastManager>();

        // 인디케이터를 비활성화한다.
        indicator.SetActive(false);
    }

    void Update()
    {
        // 바닥 감지 및 이미지 출력 함수
        DetectGround();

        // 만일, 버튼을 터치했다면, 업데이트를 종료한다.
        if(EventSystem.current.currentSelectedGameObject)
        {
            return;
        }

        // 만일, 인디케이터가 활성화된 상태에서 화면을 터치하면, 자동차 모델링을 생성한다.
        if (indicator.activeInHierarchy && Input.touchCount > 0)
        {
            // 터치 정보를 가져온다
            Touch touch = Input.GetTouch(0);

            // 터치 상태가 시작 상태라면 자동차 모델링을 생성한다.
            if (touch.phase == TouchPhase.Began)
            {
                // 만일, 자동차가 생성된 적이 없다면, 차를 인디케이터 위치에 생성한다.
                if (placedObject == null)
                {
                    placedObject = Instantiate(myCar, indicator.transform.position, indicator.transform.rotation);
                }
                // 그렇지 않다면, 차의 위치를 인디케이터의 위치로 이동한다.
                else
                {
                    // 만일, 표식과 자동차와의 거리가 일정 범위 이상 차이가 날 경우에만 자동차의 위치를 변경한다.
                    if (Vector3.Distance(placedObject.transform.position, indicator.transform.position) > relocateDistance)
                    {
                        //placedObject.transform.SetPositionAndRotation(indicator.transform.position, indicator.transform.rotation);
                        placedObject.transform.position = indicator.transform.position;
                        placedObject.transform.rotation = indicator.transform.rotation;
                    }
                }
            }
        }
    }

    void DetectGround()
    {
        // 스크린의 정 중앙의 위치를 찾는다.
        Vector2 screenSize = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        // 레이에 부딪힌 대상의 정보를 저장할 리스트 변수를 만든다.
        List<ARRaycastHit> hitInfos = new List<ARRaycastHit>();

        // 레이를 발사한다.
        // 만일, 스크린 중앙에서 레이를 발사해서 그 지점이 Plane 타입이라면...
        if (arManager.Raycast(screenSize, hitInfos, TrackableType.Planes))
        {
            // 표식 오브젝트를 활성화한다.
            indicator.SetActive(true);

            // 표식 오브젝트의 위치 & 회전 값을 레이의 위치(화면 정 중앙)에 위치시킨다.
            indicator.transform.position = hitInfos[0].pose.position;
            indicator.transform.rotation = hitInfos[0].pose.rotation;

            // 표식 오브젝트의 위치를 위쪽 방향으로 1센티미터 올려준다.
            indicator.transform.position += indicator.transform.up * 0.01f;
        }
        // 그렇지 않다면...
        else
        {
            // 표식 오브젝트를 비활성화한다.
            indicator.SetActive(false);
        }
    }

}
