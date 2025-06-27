using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MenuSwitch : MonoBehaviour
{
    public GameObject videoFrameMenu;
    public float dot;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        dot = Vector3.Dot(transform.forward, Vector3.up);
        if(dot < -0.5)
        {
            videoFrameMenu.SetActive(true);
        }else
        {
            videoFrameMenu.SetActive(false);
        }
    }
}
