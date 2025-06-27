using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NoneSleepMode : MonoBehaviour
{
    void Start()
    {
        // 스마트폰의 자동 절전 기능을 작동되지 않도록 한다.
        //Screen.sleepTimeout = -1;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }
}
