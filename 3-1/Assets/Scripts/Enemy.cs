using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    // 필요속성 : 속력
    public float speed = 5;

    // 방향을 전역변수로 만들어서 Start에서 담고 Update에서 사용
    Vector3 dir;
    private void Start()
    {
        // Vector3 dir; -> 삭제

        // 0부터 9(10-1) 까지 값중에 하나를 랜덤으로 가져와서
        int randValue = UnityEngine.Random.Range(0, 10);
        // 만약 3보다 작으면 플레이어방향
        if (randValue < 3)
        {
            // 플레이어를 찾아서 target으로 하고싶다.
            GameObject target = GameObject.Find("Player");
            // 방향을 구하고싶다. target - me
            dir = target.transform.position - transform.position;
            // 방향의 크기를 1로 하고 싶다.
            dir.Normalize();
        }
        // 그렇지 않으면 아래방향으로 정하고 싶다.
        else
        {
            dir = Vector3.down;
        }
    }
    void Update()
    {
        // 1. 방향을 구한다.
        // Vector3 dir = Vector3.down; -> 삭제
        // 2. 이동하고 싶다. 공식 P = P0 + vt
        transform.position += dir * speed * Time.deltaTime;
    }

    // 충돌 시작
    private void OnCollisionEnter(Collision other)
    {
        // 너죽고
        Destroy(other.gameObject);
        // 나죽자
        Destroy(gameObject);
    }

}
