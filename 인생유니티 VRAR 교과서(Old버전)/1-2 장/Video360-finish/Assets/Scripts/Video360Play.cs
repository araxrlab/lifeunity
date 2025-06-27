using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

public class Video360Play : MonoBehaviour
{
    VideoPlayer vp;

    public VideoClip[] vcList;
    int curVCidx;
    // Start is called before the first frame update
    void Start()
    {
        vp = GetComponent<VideoPlayer>();
        vp.clip = vcList[0];
        curVCidx = 0;
        vp.Stop();
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.LeftBracket))
        {
            SwpVideoClip(false);
        }
        else if (Input.GetKeyDown(KeyCode.RightBracket))
        {
            SwpVideoClip(true);
        }
    }

    public void SwpVideoClip(bool isNext)
    {
        int setVCnum = curVCidx;
        vp.Stop();
        if(isNext)
        {
            setVCnum = (setVCnum + 1) % vcList.Length;
        }else
        {
            setVCnum = ((setVCnum - 1)+ vcList.Length) % vcList.Length;
        }
        vp.clip = vcList[setVCnum];
        vp.Play();
        curVCidx = setVCnum;
    }
    public void SetVideoPlay(int num)
    {
        if(curVCidx != num)
        {
            vp.Stop();
            vp.clip = vcList[num];
            curVCidx = num;
            vp.Play();
        }
    }
}
