using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

public class VideoFrame : MonoBehaviour
{
    VideoPlayer vp;
    // Start is called before the first frame update
    void Start()
    {
        vp = GetComponent<VideoPlayer>();
        vp.Stop();
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown("space"))
        {
            if(vp.isPlaying)
            {
                vp.Pause();
            }else
            {
                vp.Play();
            }
        }
    }

    public void CheckVideoFrame(bool Checker)
    {
        if(Checker)
        {
            if(!vp.isPlaying)
            {
                vp.Play();
            }
        }
        else
        {
            vp.Stop();
        }
    }

    private void OnEnable()
    {
        if(vp != null)
        {
            vp.Stop();
        }
    }
}
