using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyManager : MonoBehaviour
{
    // 최소시간
    float minTime = 1;
    // 최대시간
    float maxTime = 5;

    // 일정시간
    public float createTime;
    // 현재시간
    float currentTime;
    // 적 공장
    public GameObject enemyFactory;

    // Start is called before the first frame update
    void Start()
    {
        // 태어날 때 적 생성시간을 설정하고
        createTime = UnityEngine.Random.Range(minTime, maxTime);
    }

    // Update is called once per frame
    void Update()
    {
        // 1. 시간이 흐르다가
        currentTime += Time.deltaTime;
        // 2. 만약 현재시간이 일정시간이 되면
        if (currentTime > createTime)
        {
            // 3. 적 공장에서 적을 생성해서
            GameObject enemy = Instantiate(enemyFactory);
            // 4. 생성된 적을 내 위치에 갖다 놓고 싶다.
            enemy.transform.position = transform.position;
            // 5. 현재시간을 0으로 초기화
            currentTime = 0;

            // 적을 생성한 후 적 생성시간을 다시 설정하고 싶다.
            createTime = UnityEngine.Random.Range(minTime, maxTime);
        }
    }
}
