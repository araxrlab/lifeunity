using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MouseDist : MonoBehaviour
{
    public bool clickChecker, ballChecker;
    public float dist;

    public Vector3 start;
    public Vector3 curPos;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        //클릭을 시작했을때 마우스의 위치
        if (Input.GetButtonDown("Fire1"))
        {
            start = Input.mousePosition;
        }
        //누르고 있는 동안 눌렸다는 신호를 감지
        if(Input.GetButton("Fire1"))
        {
            clickChecker = true;
        }
        
        if(clickChecker && ballChecker)
        {
            curPos = Input.mousePosition;
            dist = Vector3.Distance(start, curPos);
        }

        //버튼에서 떼었을때 는 모든것을 초기화
        if(Input.GetButtonUp("Fire1"))
        {
            Shot();
            clickChecker = false;
            start = Vector3.zero;
            curPos = Vector3.zero;
            dist = 0;
        }
    }

    void Shot()
    {
        //볼을 
    }
}
