using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.UI;

public class UI_Manager : MonoBehaviour
{
    public ARFaceManager faceManager;
    public Material[] faceMats;
    public Text indexText;

    int vertNum = 0;
    int vertCount = 468;

    void Start()
    {
        // 초기 인덱스 값을 0으로 초기화해준다.
        indexText.text = vertNum.ToString();
    }


    // 버튼을 눌렀을 때 실행될 함수
    public void ToggleMaskImage()
    {
        // faceManager가 검출한 얼굴 정보를 순회한다.
        foreach(ARFace face in faceManager.trackables)
        {
            // 만일, 인식 상태가 얼굴을 인식 중이라면...
            if(face.trackingState == TrackingState.Tracking)
            {
                // AR Face 오브젝트의 활성화 상태를 반대로 변경한다.
                face.gameObject.SetActive(!face.gameObject.activeInHierarchy);
            }
        }
    }

    // 얼굴 매터리얼을 교체하는 함수
    public void SwitchFaceMaterial(int num)
    {
        foreach(ARFace face in faceManager.trackables)
        {
            // 만일, 인식 상태가 얼굴을 인식하고 있는 중이라면...
            if(face.trackingState == TrackingState.Tracking)
            {
                // face 오브젝트의 MeshRenderer의 Material을 버튼에 할당된 번호에 해당하는 매터리얼로 교체한다.
                face.gameObject.GetComponent<MeshRenderer>().material = faceMats[num];
            }
        }
    }

    public void IndexIncrease()
    {
        // vertNum의 숫자를 1 증가시키되 정점 갯수 최대치를 넘지 않도록 한다.
        int number = Mathf.Min(++vertNum, vertCount);
        indexText.text = number.ToString();
    }

    public void IndexDecrease()
    {
        // vertNum의 숫자를 1 감소시키되 0를 넘지 않도록 한다.
        int number = Mathf.Min(--vertNum, 0);
        indexText.text = number.ToString();
    }
}
