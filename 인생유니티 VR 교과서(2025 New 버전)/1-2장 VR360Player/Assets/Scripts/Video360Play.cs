using System.Linq;
using UnityEngine;
// VideoPlayer ����� ����ϱ� ���� ���ӽ����̽� 
using UnityEngine.Video; 
// ���� �÷��̾ ���� 360 ���Ǿ ������ �������. 
// �� ���� ���� �ٸ� ������ ��ü�ϸ� ����Ѵ�. 
public class Video360Play : MonoBehaviour
{
    // ���� �÷��̾� ������Ʈ 
    VideoPlayer vp;
    // ����ؾ� �� VR 360 ������ ���� ���� 
    public VideoClip[] vcList; // �ټ��� ���� Ŭ���� �迭�� ����� �����Ѵ�. 
    int curVCidx; // ���� ��� ���� Ŭ�� ��ȣ�� �����Ѵ�. 

    void Start()
    {
        // ���� �÷��̾� ������Ʈ�� ������ �޾ƿ´�. 
        vp = GetComponent<VideoPlayer>(); 
        vp.clip = vcList[0]; 
        curVCidx = 0; 
    }

    void Update()
    {
        // ��ǻ�Ϳ��� ������ �����ϱ� ���� ��� 
        if (Input.GetKeyDown(KeyCode.LeftBracket)) // ���� ���ȣ �Է� �� ���� ���� 
        {
            SwapVideoClip(false);
        }
        else if (Input.GetKeyDown(KeyCode.RightBracket))// ������ ���ȣ �Է� �� ���� ���� 
        {
            SwapVideoClip(true);
        }
    }


    // ���ͷ����� ���� �Լ��� �ۺ����� �����Ѵ�. 
    // �迭�� �ε��� ��ȣ�� �������� ������ ��ü, ����ϱ� ���� �Լ� 
    // ���� ���� isNext�� true�̸� ���� ����, false�� ���� ���� ��� 
    public void SwapVideoClip(bool isNext)
    {
        // ���� ��� ���� ������ �ѹ��� �������� üũ�Ѵ�. 
        // ���� ���� ��ȣ�� ���� ���󺸴� �迭���� �ε��� ��ȣ�� 1�� �۴�. 
        // ���� ���� ��ȣ�� ���� ���󺸴� �迭���� �ε��� ��ȣ�� 1�� ũ��. 
        int setVCnum = curVCidx;    // ���� ��� ���� ������ �ѹ��� �Է��Ѵ�. 
        vp.Stop();                  // ���� ��� ���� ���� Ŭ���� �����Ѵ�. 

        // ����� ������ ���� ���� ���� 
        if (isNext)
        {
            // ����Ʈ ��ü ���̺��� ũ�� Ŭ���� ����Ʈ�� ù ��° �������� �����Ѵ�. 
            setVCnum = (setVCnum + 1) % vcList.Length;
        }
        else
        {
            setVCnum = ((setVCnum - 1) + vcList.Length) % vcList.Length;
        }
        vp.clip = vcList[setVCnum]; // Ŭ���� �����Ѵ�
        vp.Play();                  // �ٲ� Ŭ���� ����Ѵ�. 
        curVCidx = setVCnum;        // �ٲ� Ŭ���� ������ ��ȣ�� ������Ʈ�Ѵ�.
    }

    public void SetVideoPlay(int num)
    {
        // ���� ��� ���� ��ȣ�� ���޹��� ��ȣ�� �ٸ� ���� ���� 
        if (curVCidx != num)
        {
            vp.Stop();
            vp.clip = vcList[num];
            curVCidx = num;
            vp.Play();
        }
    }
} 
