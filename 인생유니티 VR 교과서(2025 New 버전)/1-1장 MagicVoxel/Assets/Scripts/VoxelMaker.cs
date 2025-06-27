using System.Collections.Generic;
using UnityEngine;

public class VoxelMaker : MonoBehaviour
{
    // 복셀 공장 
    public GameObject voxelFactory;
    // 오브젝트 풀의 크기 
    public int voxelPoolSize = 20;
    // 오브젝트 풀 
    public static List<GameObject> voxelPool = new List<GameObject>();
    // 생성 시간 
    public float createTime = 0.1f;
    // 경과 시간 
    float currentTime = 0;
    // 크로스헤어 변수 
    public Transform crosshair;

    void Start()
    {
        // 오브젝트 풀에 비활성화된 복셀을 담고 싶다. 
        for (int i = 0; i < voxelPoolSize; i++)
        {
            // 1. 복셀 공장에서 복셀 생성하기 
            GameObject voxel = Instantiate(voxelFactory);
            // 2. 복셀 비활성화하기 
            voxel.SetActive(false);
            // 3. 복셀을 오브젝트 풀에 담고 싶다. 
            voxelPool.Add(voxel);
        }
    }

    void Update()
    {
        // 크로스헤어 그리기 
        ARAVRInput.DrawCrosshair(crosshair);

        // 1) VR 컨트롤러의 발사 버튼을 누르면 
        if (ARAVRInput.Get(ARAVRInput.Button.One))
        {
            currentTime += Time.deltaTime;
            if (currentTime > createTime)
            {
                // Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition); 
                // 2) 컨트롤러가 향하는 방향으로 시선 만들기 
                Ray ray = new Ray(ARAVRInput.RHandPosition, ARAVRInput.RHandDirection);
                RaycastHit hitInfo = new RaycastHit();

                if (Physics.Raycast(ray, out hitInfo))
                {
                    if (voxelPool.Count > 0)
                    {
                        // 복셀을 생성했을 때만 경과 시간을 초기화해준다. 
                        currentTime = 0;
                        // 2. 오브젝트 풀에서 복셀을 하나 가져온다. 
                        GameObject voxel = voxelPool[0];
                        // 3. 복셀을 활성화한다. 
                        voxel.SetActive(true);
                        // 4. 복셀을 배치하고 싶다. 
                        voxel.transform.position = hitInfo.point;
                        // 5. 오브젝트 풀에서 복셀을 제거한다. 
                        voxelPool.RemoveAt(0);
                    }
                }
            }
        }
    }
}
