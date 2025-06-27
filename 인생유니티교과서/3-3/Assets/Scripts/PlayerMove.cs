using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    public float speed = 5;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 dir = new Vector3(h, v, 0);

        if (dir.magnitude > 1)
        {
            dir.Normalize();
        }

        transform.position += dir * speed * Time.deltaTime;
        
        Vector3 pos = Camera.main.WorldToViewportPoint(transform.position);
        
        if(pos.x < 0)
        {
            pos.x = 0;
        }
        if (pos.x > 1)
        {
            pos.x = 1;
        }
        if (pos.y < 0)
        {
            pos.y = 0;
        }
        if (pos.y > 1)
        {
            pos.y = 1;
        }

        transform.position = Camera.main.ViewportToWorldPoint(pos);
    }
}
