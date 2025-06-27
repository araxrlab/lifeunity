using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Tower : MonoBehaviour
{
    // ������ ǥ���� UI
    public Transform damageUI;
    public Image damageImage;
    // Ÿ���� ���� HP
    public int initialHP = 10;
    // ���� hp ����
    int _hp = 0;

    // _hp�� get/set ������Ƽ
    public int HP
    {
        get
        {
            return _hp;
        }
        set
        {
            _hp = value;
            // ������ ���� ���� �ڷ�ƾ ����
            StopAllCoroutines();
            // �����Ÿ��� ó���� �ڷ�ƾ ȣ��
            StartCoroutine(DamageEvent());

            // hp�� 0 �����̸� ����
            if (_hp <= 0)
            {
                Destroy(gameObject);
            }
        }
    }

    // �����Ÿ��� �ð�
    public float damageTime = 0.1f;
    // Tower �� �̱��� ��ü
    public static Tower Instance;

    void Awake()
    {
        // �̱��� ��ü �� �Ҵ�
        if (Instance == null)
        {
            Instance = this;
        }
    }

    void Start()
    {
        _hp = initialHP;
        // ī�޶��� nearClipPlane ���� ����صд�.
        float z = Camera.main.nearClipPlane + 0.01f;
        // damageUI ��ü�� �θ� ī�޶�� ����
        damageUI.parent = Camera.main.transform;
        // damageUI�� ��ġ�� X, Y�� 0, Z ���� ī�޶��� near ������ ����
        damageUI.localPosition = new Vector3(0, 0, z);
        // damageImage�� ������ �ʵ��� �ʱ⿡ ��Ȱ��ȭ�� ���´�.
        damageImage.enabled = false;
    }



    // ������ ó���� ���� �ڷ�ƾ �Լ�
    IEnumerator DamageEvent()
    {
        // damageImage ������Ʈ�� Ȱ��ȭ
        damageImage.enabled = true;
        // damageTime��ŭ ��ٸ���.
        yield return new WaitForSeconds(damageTime);

        // �ٽ� ������� ��Ȱ��ȭ�Ѵ�.
        damageImage.enabled = false;
    }
}
