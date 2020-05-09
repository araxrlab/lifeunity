using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyManager : MonoBehaviour
{
    //필요 속성 : 오브젝트풀 크기, 오브젝트풀 배열, SpawnPoint 들
    //오브젝트풀 크기
    public int poolSize = 10;
    //오브젝트풀 배열
    public List<GameObject> enemyObjectPool;
    //SpawnPoint 들
    public Transform[] spawnPoints;

    public GameObject enemyFactory;

    // 생성할 최소시간
    public float minTime = 0.5f;
    // 생성할 최대시간
    public float maxTime = 1.5f;
    // 생성시간
    float creatTime;
    float currentTime = 0;

    //1. 태어 날 때
    void Start()
    {
        creatTime = Random.Range(minTime, maxTime);
        //2. 오브젝트풀을 에너미들을 담을 수 있는 크기로 만들어 준다.
        enemyObjectPool = new List<GameObject>();
        //3. 오브젝트풀에 넣을 에너미 개수 만큼 반복하여
        for (int i = 0; i < poolSize; i++)
        {
            //4. 에너미공장에서 에너미를 생성한다.
            GameObject enemy = Instantiate(enemyFactory);
            //5. 에너미를 오브젝트풀에 넣고싶다.
            enemyObjectPool.Add(enemy);
            // 비활성화 시키자.
            enemy.SetActive(false);
        }
    }

    // Update is called once per frame
    void Update()
    {
        currentTime += Time.deltaTime;
        //1.생성 시간이 되었으니까
        if (currentTime > creatTime)
        {
            //2.오브젝트풀에 에너미가 있다면
            GameObject enemy = enemyObjectPool[0];
            if (enemyObjectPool.Count > 0)
            {
                //3.에너미를 활성화 하고 싶다.
                enemy.SetActive(true);
                //4.오브젝트풀에서 총알제거
                enemyObjectPool.Remove(enemy);
                // 랜덤으로 인덱스 선택
                int index = Random.Range(0, spawnPoints.Length);
                // 5.에너미 위치 시키기
                enemy.transform.position = spawnPoints[index].position;
            }

            creatTime = Random.Range(minTime, maxTime);
            currentTime = 0;
        }
    }
}
