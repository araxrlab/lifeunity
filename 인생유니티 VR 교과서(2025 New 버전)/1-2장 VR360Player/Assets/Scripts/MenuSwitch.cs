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
        // ������ ���� ���� �� 
        dot = Vector3.Dot(transform.forward, Vector3.up);
        if (dot < -0.5)
        {
            videoFrameMenu.SetActive(true); // �޴� Ȱ��ȭ 
        }
        else
        {
            videoFrameMenu.SetActive(false); // �޴� ��Ȱ��ȭ 
        }
    }
}
