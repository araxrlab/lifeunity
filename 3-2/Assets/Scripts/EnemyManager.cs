using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyManager : MonoBehaviour
{
    public GameObject enemyPrefab;

    public float creatTime = 3;
    float currentTime = 0;

    // Start is called before the first frame update
    void Start()
    {
        creatTime = Random.Range(1.0f, 5.0f);
    }

    // Update is called once per frame
    void Update()
    {
        currentTime += Time.deltaTime;

        if (currentTime > creatTime)
        {
            GameObject enemy = Instantiate(enemyPrefab);

            enemy.transform.position = transform.position;

            creatTime = Random.Range(1.0f, 5.0f);
            currentTime = 0;
        }
    }
}
