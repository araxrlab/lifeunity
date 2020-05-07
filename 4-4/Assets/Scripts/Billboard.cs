using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Billboard : MonoBehaviour
{
    public Transform target;
    
    void Update()
    {
        // 자기 자신의 방향을 카메라의 방향과 일치시킨다.
        transform.forward = target.forward;
    }
}
