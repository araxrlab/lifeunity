using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    // 플레이어가 이동할 속력
    public float speed = 5;

    void Update()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 dir = new Vector3(h, v, 0);

        // P = P0 + vt 공식으로 변경
        transform.position += dir * speed * Time.deltaTime;
    }
}

