using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class MultipleImageTracker : MonoBehaviour
{
    ARTrackedImageManager imageManager;

    void Start()
    {
        imageManager = GetComponent<ARTrackedImageManager>();

        StartCoroutine(TurnOnImageTracker());
    }

    IEnumerator TurnOnImageTracker()
    {
        imageManager.enabled = false;

        // GPS Manager의 첫 번째 위치 정보 데이터를 수신할 때까지 대기한다.
        while(!GPS_Manager.instance.receiveGPS)
        {
            yield return null;
        }

        imageManager.enabled = true;

        // 이미지를 인식할 때 실행할 함수를 연결한다.
        imageManager.trackedImagesChanged += OnTrackedImage;
    }

    void OnTrackedImage(ARTrackedImagesChangedEventArgs args)
    {
        // 새로 인식된 이미지들을 순회한다.
        foreach(ARTrackedImage trackedImage in args.added)
        {
            // 현재 나의 위치를 Vector2 형태로 저장한다.
            Vector2 myPos = new Vector2(GPS_Manager.instance.latitude,
                                        GPS_Manager.instance.longitude);

            // DB에서 나의 위치에 대응하는 오브젝트를 찾고 생성하는 코루틴을 실행한다.
            StartCoroutine(DB_Manager.instance.LoadData(myPos,trackedImage.transform));
        }
        
        // 위치나 회전 데이터가 갱신된 이미지 리스트를 순회한다.
        foreach(ARTrackedImage trackedImage in args.updated)
        {
            // 만일, 자식 오브젝트가 있다면...
            if(trackedImage.transform.childCount > 0)
            {
                // 오브젝트의 Transform 데이터를 이미지의 Transform 데이터와 동기화한다.
                trackedImage.transform.GetChild(0).position = trackedImage.transform.position;
                trackedImage.transform.GetChild(0).rotation = trackedImage.transform.rotation;
                trackedImage.transform.GetChild(0).localScale = trackedImage.transform.localScale;
            }
        }
    }
}
