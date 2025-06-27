using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// 1. 복셀은 랜덤한 방향으로 날아가는 운동을 한다.
// 필요 속성: 날아갈 속도
// 2. 일정 시간이 지나면 복셀을 제거하고 싶다.
// 필요 속성: 복셀을 제거할 시간, 경과 시간
public class Voxel : MonoBehaviour
{
    // 1. 복셀이 날아갈 속도 구하기
    public float speed = 5;
    // 복셀을 제거할 시간
    public float destoryTime = 3.0f;
    // 경과 시간
    float currentTime;

    void OnEnable()
    {
        currentTime = 0;
        // 2. 랜덤한 방향을 찾는다.
        Vector3 direction = Random.insideUnitSphere;
        // 3. 랜덤한 방향으로 날아가는 속도를 준다.
        Rigidbody rb = gameObject.GetComponent<Rigidbody>();
        rb.velocity = direction * speed;
    }

    void Update()
    {
        // 일정 시간이 지나면 복셀을 제거하고 싶다.
        // 1. 시간이 흘러야 한다.
        currentTime += Time.deltaTime;
        // 2. 제거 시간이 됐으니까.
        // 만약 경과 시간이 제거 시간을 초과했다면
        if (currentTime > destoryTime)
        {
            // 3. Voxel을 비활성화시킨다.
            gameObject.SetActive(false);
            // 4. 오브젝트 풀에 다시 넣어준다.
            VoxelMaker.voxelPool.Add(gameObject);
        }
    }
}
