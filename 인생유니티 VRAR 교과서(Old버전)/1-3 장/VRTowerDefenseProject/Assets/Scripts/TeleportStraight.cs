using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

public class TeleportStraight : MonoBehaviour
{
    // �ڷ���Ʈ�� ǥ���� UI
    public Transform teleportCircleUI;
    // ���� �׸� ���� ������
    LineRenderer lr;
    // ���� �ڷ���Ʈ UI�� ũ��
    Vector3 originScale = Vector3.one * 0.02f;
    // ���� ��� ����
    public bool isWarp = false;
    // ������ �ɸ��� �ð�
    public float warpTime = 0.1f;
    // ����ϰ� �ִ� ����Ʈ ���μ��� ���� ������Ʈ
    public PostProcessVolume post;

    void Start()
    {
        // ������ �� ��Ȱ��ȭ�Ѵ�.
        teleportCircleUI.gameObject.SetActive(false);
        // ���� ������ ������Ʈ ������
        lr = GetComponent<LineRenderer>();
    }

    void Update()
    {
        // ���� ��Ʈ�ѷ��� One ��ư�� ������
        if (ARAVRInput.GetDown(ARAVRInput.Button.One, ARAVRInput.Controller.LTouch))
        {
            // ���� ������ ������Ʈ Ȱ��ȭ
            lr.enabled = true;
        }
        // ���� ��Ʈ�ѷ��� One ��ư���� ���� ����
        else if (ARAVRInput.GetUp(ARAVRInput.Button.One, ARAVRInput.Controller.LTouch))
        {
            // ���� ������ ��Ȱ��ȭ
            lr.enabled = false;
            if (teleportCircleUI.gameObject.activeSelf)
            {
                // ���� ��� ����� �ƴ� �� ���� �̵� ó��
                if (isWarp == false)
                {
                    GetComponent<CharacterController>().enabled = false;
                    // �ڷ���Ʈ UI ��ġ�� ���� �̵�
                    transform.position = teleportCircleUI.position + Vector3.up;
                    GetComponent<CharacterController>().enabled = true;
                }
                else
                {
                    // ���� ����� ����� ���� Warp() �ڷ�ƾ ȣ��
                    StartCoroutine(Warp());
                }
            }

            // �ڷ���Ʈ UI ��Ȱ��ȭ
            teleportCircleUI.gameObject.SetActive(false);
        }
        // ���� ��Ʈ�ѷ��� One ��ư�� ������ ���� ��
        else if (ARAVRInput.Get(ARAVRInput.Button.One, ARAVRInput.Controller.LTouch))
        {
            // 1. ���� ��Ʈ�ѷ��� �������� Ray�� �����.
            Ray ray = new Ray(ARAVRInput.LHandPosition, ARAVRInput.LHandDirection);
            RaycastHit hitInfo;

            int layer = 1 << LayerMask.NameToLayer("Terrain");
            // 2. Terrain�� Ray �浹 ����
            if (Physics.Raycast(ray, out hitInfo, 200, layer))
            {
                // 3. Ray�� �ε��� ������ ���� �׸���
                lr.SetPosition(0, ray.origin);
                lr.SetPosition(1, hitInfo.point);

                // 4. Ray�� �ε��� ������ �ڷ���Ʈ UI ǥ��
                teleportCircleUI.gameObject.SetActive(true);
                teleportCircleUI.position = hitInfo.point;
                // �ڷ���Ʈ UI�� ���� ���� �ֵ��� ���� ����
                teleportCircleUI.forward = hitInfo.normal;
                // �ڷ���Ʈ UI�� ũ�Ⱑ �Ÿ��� ���� �����ǵ��� ����
                teleportCircleUI.localScale = originScale * Mathf.Max(1, hitInfo.distance);
            }
            else
            {
                // Ray �浹�� �߻����� ������ ���� Ray �������� �׷������� ó��
                lr.SetPosition(0, ray.origin);
                lr.SetPosition(1, ray.origin + ARAVRInput.LHandDirection * 200);

                // �ڷ���Ʈ UI�� ȭ�鿡�� ��Ȱ��ȭ
                teleportCircleUI.gameObject.SetActive(false);
            }
        }
    }

    IEnumerator Warp()
    {
        // ���� ������ ǥ���� ��Ǻ�
        MotionBlur blur;
        // ���� ������ ���
        Vector3 pos = transform.position;
        // ������
        Vector3 targetPos = teleportCircleUI.position + Vector3.up;
        // ���� ��� �ð�
        float currentTime = 0;
        // ����Ʈ ���μ��̿��� ��� ���� �������Ͽ��� ��Ǻ� ������
        post.profile.TryGetSettings<MotionBlur>(out blur);
        // ���� ���� �� �� �ѱ�
        blur.active = true;
        GetComponent<CharacterController>().enabled = false;
        // ��� �ð��� �������� ª�� �ð� ���� �̵� ó��
        while (currentTime < warpTime)
        {
            // ��� �ð� �帣�� �ϱ�
            currentTime += Time.deltaTime;
            // ������ ���������� �������� �����ϱ� ���� ���� �ð� ���� �̵�
            transform.position = Vector3.Lerp(pos, targetPos, currentTime / warpTime);

            // �ڷ�ƾ ���
            yield return null;
        }

        // �ڷ���Ʈ UI ��ġ�� ���� �̵�
        transform.position = teleportCircleUI.position + Vector3.up;
        // ĳ���� ��Ʈ�ѷ� �ٽ� �ѱ�
        GetComponent<CharacterController>().enabled = true;
        // ����Ʈ ȿ�� ����
        blur.active = false;

    }
}
