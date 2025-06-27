using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ���콺 �Է¿� ���� ī�޶� ȸ����Ű�� �ʹ�.
// �ʿ� �Ӽ�: ���� ����, ���콺 ����
public class CamRotate : MonoBehaviour
{
    // ���� ����
    Vector3 angle;
    // ���콺 ����
    public float sensitivity = 200;

    void Start()
    {
        // ������ �� ���� ī�޶��� ������ �����Ѵ�.
        angle.y = -Camera.main.transform.eulerAngles.x;
        angle.x = Camera.main.transform.eulerAngles.y;
        angle.z = Camera.main.transform.eulerAngles.z;
    }

    void Update()
    {
        // ���콺 �Է¿� ���� ī�޶� ȸ����Ű�� �ʹ�.
        // 1. ������� ���콺 �Է��� ���;� �Ѵ�.
        // ���콺�� �¿� �Է��� �޴´�.
        float x = Input.GetAxis("Mouse X");
        float y = Input.GetAxis("Mouse Y");

        // 2. ������ �ʿ��ϴ�.
        // �̵� ���Ŀ� ������ �� �Ӽ����� ȸ�� ���� ������Ų��.
        angle.x += x * sensitivity * Time.deltaTime;
        angle.y += y * sensitivity * Time.deltaTime;

        // 3. ȸ����Ű�� �ʹ�.
        // ī�޶��� ȸ�� ���� ���� ������� ȸ�� ���� �Ҵ��Ѵ�.
        transform.eulerAngles = new Vector3(-angle.y, angle.x, transform.eulerAngles.z);
    }
}
