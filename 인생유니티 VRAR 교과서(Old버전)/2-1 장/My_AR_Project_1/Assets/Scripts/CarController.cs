using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CarController : MonoBehaviour
{
    public GameObject[] bodyObjects;
    public Color32[] colors;
    public float rotSpeed = 0.1f;

    Material[] carMats;

    void Start()
    {
        // carMats 배열을 초기화한다.
        carMats = new Material[bodyObjects.Length];
        
        // 색상 담당 오브젝트에서 매터리얼을 carMats 배열에 등록한다.
        for(int i = 0; i < carMats.Length; i++)
        {
            carMats[i] = bodyObjects[i].GetComponent<MeshRenderer>().material;
        }

        // 색상 배열 0번에 자동차의 기본 색상을 저장한다.
        colors[0] = carMats[0].color;
    }

    public void ChangeColor(int num)
    {
        // 버튼에 할당된 매개변수(num)의 숫자에 해당하는 컬러를 자동차에 색상으로 바꾼다.
        for(int i = 0; i < carMats.Length; i++)
        {
            carMats[i].color = colors[num];
        }
    }

    void Update()
    {
        // 만일, 화면을 터치하고 있다면...
        if(Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            // 만일, 터치 상태가 움직이는 상태라면...
            if(touch.phase == TouchPhase.Moved)
            {
                // 만일, 자동차를 터치하고 있는 상태라면...
                Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);

                RaycastHit hitInfo;

                if(Physics.Raycast(ray, out hitInfo, Mathf.Infinity, 1 << 8))
                {
                    // 자동차를 손가락의 좌우 움직임에 맞춰서 회전시킨다.
                    Vector2 deltaPos = touch.deltaPosition;

                    transform.Rotate(transform.up, deltaPos.x * -1.0f * rotSpeed);
                }
            }
        }
    }
}
