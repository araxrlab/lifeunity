using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARCore;
using Unity.Collections;
using UnityEngine.UI;

public class FindDetection : MonoBehaviour
{
    public ARFaceManager afm;
    public GameObject smallCube;
    public Text vertexText;

    List<GameObject> testCubes = new List<GameObject>();
    ARCoreFaceSubsystem subSys;
    NativeArray<ARCoreFaceRegionData> regionData;
    
    void Start()
    {
        // 얼굴에 위치시킬 큐브를 세 개 만들어서 리스트에 추가한다.
        // 큐브 오브젝트를 비활성화시킨다.
        for(int i = 0; i < 3; i++)
        {
            GameObject go = Instantiate(smallCube);
            testCubes.Add(go);
            go.SetActive(false);
        }

        // AR Face Manager가 얼굴을 인식하면 실행할 함수를 연결한다.
        //afm.facesChanged += OnDetectThreePoints;
        afm.facesChanged += OnDetectFaceAll;

        // AR Foundation용 Face subsystem 변수를 AR Core용 Face subsystem 변수로 캐스팅한다.
        subSys = (ARCoreFaceSubsystem)afm.subsystem;
    }

    void OnDetectThreePoints(ARFacesChangedEventArgs args)
    {
        // 얼굴 인식 정보가 갱신된 것이 있다면...
        if (args.updated.Count > 0)
        {
            // 인식된 얼굴로부터 세 군데 좌표를 가져온다.
            subSys.GetRegionPoses(args.updated[0].trackableId, Allocator.Persistent, ref regionData);

            // 인식한 얼굴의 세 군데 좌표에 큐브를 위치시킨다.
            for(int i = 0; i < regionData.Length; i++)
            {
                testCubes[i].transform.position = regionData[i].pose.position;
                testCubes[i].transform.rotation = regionData[i].pose.rotation;

                // 생성되어 있던 큐브를 활성화한다.
                testCubes[i].SetActive(true);
            }

        }
        // 얼굴 인식 정보를 잃었다면...
        else if (args.removed.Count > 0)
        {
            // 생성되어 있던 큐브를 비활성화한다.
            for(int i = 0; i < testCubes.Count; i++)
            {
                testCubes[i].SetActive(false);
            }
        }
    }

    void OnDetectFaceAll(ARFacesChangedEventArgs args)
    {
        // 얼굴 인식이 되고 있다면...
        if(args.updated.Count > 0)
        {
            // UI 텍스트의 문자열을 int형 숫자로 변환한다.
            int num = int.Parse(vertexText.text);

            // 얼굴 정점 배열에서 지정된 인덱스에 해당하는 위치값을 가져온다.
            Vector3 vertPosition = args.updated[0].vertices[num];

            // 정점 좌표(로컬)를 월드 좌표로 변경한다.
            vertPosition = args.updated[0].transform.TransformPoint(vertPosition);

            // 큐브를 활성화하고 정점 위치에 가져다 놓는다.
            testCubes[0].SetActive(true);
            testCubes[0].transform.position = vertPosition;
        }
        // 얼굴 인식을 놓쳤다면...
        else if(args.removed.Count > 0)
        {
            testCubes[0].SetActive(false);
        }
    }
}
