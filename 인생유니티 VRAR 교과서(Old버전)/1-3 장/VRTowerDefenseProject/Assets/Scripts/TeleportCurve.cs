using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TeleportCurve : MonoBehaviour
{
    // �ڷ���Ʈ�� ǥ���� UI
    public Transform teleportCircleUI;
    // ���� �׸� ���� ������
    LineRenderer lr;
    // ���� �ڷ���Ʈ UI ũ��
    Vector3 originScale = Vector3.one * 0.02f;
    // Ŀ���� �ε巯�� ����
    public int lineSmooth = 40;
    // Ŀ���� ����
    public float curveLength = 50;
    // Ŀ���� �߷�
    public float gravity = -60;
    // � �ùķ��̼��� ���� �� �ð�
    public float simulateTime = 0.02f;
    // ��� �̷�� ������ ����� ����Ʈ
    List<Vector3> lines = new List<Vector3>();

    void Start()
    {
        // ������ �� ��Ȱ��ȭ�Ѵ�.
        teleportCircleUI.gameObject.SetActive(false);
        // ���� ������ ������Ʈ ������
        lr = GetComponent<LineRenderer>();
        // ���� �������� �� �ʺ� ����
        lr.startWidth = 0.0f;
        lr.endWidth = 0.2f;
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

            // �ڷ���Ʈ UI�� Ȱ��ȭ�� ���� ��
            if (teleportCircleUI.gameObject.activeSelf)
            {
                GetComponent<CharacterController>().enabled = false;
                // �ڷ���Ʈ UI ��ġ�� ���� �̵�
                transform.position = teleportCircleUI.position + Vector3.up;
                GetComponent<CharacterController>().enabled = true;
            }

            // �ڷ���Ʈ UI ��Ȱ��ȭ
            teleportCircleUI.gameObject.SetActive(false);
        }
        // ���� ��Ʈ�ѷ��� One ��ư�� ������ ���� ��
        else if (ARAVRInput.Get(ARAVRInput.Button.One, ARAVRInput.Controller.LTouch))
        {
            // �־��� ���� ũ���� Ŀ�긦 ����� �ʹ�.
            MakeLines();
        }
    }

    // ���� �������� �̿��� ���� ����� ���� �׸���.
    void MakeLines()
    {
        // ����Ʈ�� ��� ��ġ �������� ����ش�.
        lines.RemoveRange(0, lines.Count);
        // ���� ����� ������ ���Ѵ�.
        Vector3 dir = ARAVRInput.LHandDirection * curveLength;
        // ���� �׷��� ��ġ�� �ʱ갪�� �����Ѵ�.
        Vector3 pos = ARAVRInput.LHandPosition;
        // ���� ��ġ�� ����Ʈ�� ��´�.
        lines.Add(pos);
        // lineSmooth ������ŭ �ݺ��Ѵ�.
        for (int i = 0; i < lineSmooth; i++)
        {
            // ���� ��ġ ���
            Vector3 lastPos = pos;
            // �߷��� ������ �ӵ� ���
            // v = v0 + at
            dir.y += gravity * simulateTime;
            // ��� ����� ���� ��ġ ���
            //P = P0 + vt
            pos += dir * simulateTime;
            // Ray �浹 üũ�� �Ͼ���� 
            if (CheckHitRay(lastPos, ref pos))
            {
                // �浹 ������ ����ϰ� ����
                lines.Add(pos);
                break;
            }
            else
            {
                // �ڷ���Ʈ UI ��Ȱ��ȭ
                teleportCircleUI.gameObject.SetActive(false);
            }
            // ���� ��ġ�� ���
            lines.Add(pos);
        }

        // ���� �������� ǥ���� ���� ������ ��ϵ� ������ ũ��� �Ҵ�
        lr.positionCount = lines.Count;
        // ���� �������� ������ ���� ������ ����
        lr.SetPositions(lines.ToArray());
    }

    // �� ���� ��ġ�� ���� ���� ��ġ�� �޾� ������ �浹�� üũ
    private bool CheckHitRay(Vector3 lastPos, ref Vector3 pos)
    {
        // �� �� lastPos���� ���� �� pos�� ���ϴ� ���� ���
        Vector3 rayDir = pos - lastPos;
        Ray ray = new Ray(lastPos, rayDir);
        RaycastHit hitInfo;
        // Raycast�� �� ������ ũ�⸦ �� ���� ���� �� ������ �Ÿ��� �����Ѵ�.
        if (Physics.Raycast(ray, out hitInfo, rayDir.magnitude))
        {
            // ���� ���� ��ġ�� �浹�� �������� ����
            pos = hitInfo.point;
            int layer = LayerMask.NameToLayer("Terrain");

            // Terrain ���̾�� �浹���� ��쿡�� �ڷ���Ʈ UI�� ǥ�õǵ��� �Ѵ�.
            if (hitInfo.transform.gameObject.layer == layer)
            {
                // �ڷ���Ʈ UI Ȱ��ȭ
                teleportCircleUI.gameObject.SetActive(true);
                // �ڷ���Ʈ UI�� ��ġ ����
                teleportCircleUI.position = pos;
                // �ڷ���Ʈ UI�� ���� ����
                teleportCircleUI.forward = hitInfo.normal;
                float distance = (pos - ARAVRInput.LHandPosition).magnitude;
                // �ڷ���Ʈ UI�� ���� ũ�⸦ ����
                teleportCircleUI.localScale = originScale * Mathf.Max(1, distance);
            }
            return true;
        }

        return false;
    }
}
