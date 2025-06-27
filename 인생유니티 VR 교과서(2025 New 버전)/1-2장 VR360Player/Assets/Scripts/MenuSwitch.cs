using UnityEngine;

public class MenuSwitch : MonoBehaviour
{
    public GameObject videoFrameMenu;
    public float minAngle = 65;
    public float maxAngle = 90;
    public float dot;

    void Start()
    {
        
    }

    void Update()
    {
        // 내적을 통한 방향 비교 
        dot = Vector3.Dot(transform.forward, Vector3.up);
        if (dot < -0.5)
        {
            videoFrameMenu.SetActive(true); // 메뉴 활성화 
        }
        else
        {
            videoFrameMenu.SetActive(false); // 메뉴 비활성화 
        }
    }
}
