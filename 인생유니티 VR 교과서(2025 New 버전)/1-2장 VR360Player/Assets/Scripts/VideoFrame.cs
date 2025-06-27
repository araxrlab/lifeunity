using UnityEngine;
using UnityEngine.Video;

// VideoPlayer ����� ����ϱ� ���� ���ӽ����̽� 
// Video Player ������Ʈ�� ��������! 
public class VideoFrame : MonoBehaviour
{
    // Video Player ������Ʈ 
    VideoPlayer vp;

    private void OnEnable()
    {
        if (vp != null)
        {
            vp.Stop();
        }
    }

    void Start()
    {
        vp = GetComponent<VideoPlayer>();
        // �ڵ� ����Ǵ� ���� ���´�. 
        vp.Stop();
    }

    void Update()
    {
        // S�� ������ �����϶�. 
        if (Input.GetKeyDown(KeyCode.S))
        {
            vp.Stop();
        }

        // �����̽� �ٸ� ������ �� ��� �Ǵ� �Ͻ� ������ �϶�. 
        if (Input.GetKeyDown("space"))
        {
            // ���� ���� �÷��̾ �÷��� �������� Ȯ���϶�. 
            if (vp.isPlaying)
            {
                // �÷���(���) ���̶�� �Ͻ� �����϶�. 
                vp.Pause();
            }
            else
            {
                // �׷��� �ʴٸ�(�Ͻ� ���� �� �Ǵ� ����) �÷���(���)�϶�. 
                vp.Play();
            }
        }
    }

    // GazePointerCtrl���� ���� ����� ��Ʈ���ϱ� ���� �Լ� 
    public void CheckVideoFrame(bool Checker)
    {
        if (Checker)
        {
            if (!vp.isPlaying)
            {
                vp.Play();
            }
        }
        else
        {
            vp.Stop();
        }
    }
}
