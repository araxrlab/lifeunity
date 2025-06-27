using System.Collections.Generic;
using UnityEngine;

public class VoxelMaker : MonoBehaviour
{
    // ���� ���� 
    public GameObject voxelFactory;
    // ������Ʈ Ǯ�� ũ�� 
    public int voxelPoolSize = 20;
    // ������Ʈ Ǯ 
    public static List<GameObject> voxelPool = new List<GameObject>();
    // ���� �ð� 
    public float createTime = 0.1f;
    // ��� �ð� 
    float currentTime = 0;
    // ũ�ν���� ���� 
    public Transform crosshair;

    void Start()
    {
        // ������Ʈ Ǯ�� ��Ȱ��ȭ�� ������ ��� �ʹ�. 
        for (int i = 0; i < voxelPoolSize; i++)
        {
            // 1. ���� ���忡�� ���� �����ϱ� 
            GameObject voxel = Instantiate(voxelFactory);
            // 2. ���� ��Ȱ��ȭ�ϱ� 
            voxel.SetActive(false);
            // 3. ������ ������Ʈ Ǯ�� ��� �ʹ�. 
            voxelPool.Add(voxel);
        }
    }

    void Update()
    {
        // ũ�ν���� �׸��� 
        ARAVRInput.DrawCrosshair(crosshair);

        // 1) VR ��Ʈ�ѷ��� �߻� ��ư�� ������ 
        if (ARAVRInput.Get(ARAVRInput.Button.One))
        {
            currentTime += Time.deltaTime;
            if (currentTime > createTime)
            {
                // Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition); 
                // 2) ��Ʈ�ѷ��� ���ϴ� �������� �ü� ����� 
                Ray ray = new Ray(ARAVRInput.RHandPosition, ARAVRInput.RHandDirection);
                RaycastHit hitInfo = new RaycastHit();

                if (Physics.Raycast(ray, out hitInfo))
                {
                    if (voxelPool.Count > 0)
                    {
                        // ������ �������� ���� ��� �ð��� �ʱ�ȭ���ش�. 
                        currentTime = 0;
                        // 2. ������Ʈ Ǯ���� ������ �ϳ� �����´�. 
                        GameObject voxel = voxelPool[0];
                        // 3. ������ Ȱ��ȭ�Ѵ�. 
                        voxel.SetActive(true);
                        // 4. ������ ��ġ�ϰ� �ʹ�. 
                        voxel.transform.position = hitInfo.point;
                        // 5. ������Ʈ Ǯ���� ������ �����Ѵ�. 
                        voxelPool.RemoveAt(0);
                    }
                }
            }
        }
    }
}
