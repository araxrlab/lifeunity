using UnityEngine;
using System.Collections;
// ����ڰ� �߻� ��ư�� ������ ���� ��� �ʹ�.
// �ʿ� �Ӽ�: �Ѿ� ����, �Ѿ� ���� ȿ��, �Ѿ� �߻� ����
public class Gun : MonoBehaviour
{
    public Transform bulletImpact; // �Ѿ� ���� ȿ��
    ParticleSystem bulletEffect; // �Ѿ� ���� ��ƼŬ �ý���
    AudioSource bulletAudio; // �Ѿ� �߻� ����
    public Transform crosshair; // ũ�ν��� ���� �Ӽ�

    void Start()
    {
        // �Ѿ� ȿ�� ��ƼŬ �ý��� ������Ʈ ��������
        bulletEffect = bulletImpact.GetComponent<ParticleSystem>();
        // �Ѿ� ȿ�� ����� �ҽ� ������Ʈ ��������
        bulletAudio = bulletImpact.GetComponent<AudioSource>();
    }

    void Update()
    {
        // ũ�ν���� ǥ��
        ARAVRInput.DrawCrosshair(crosshair);

        // ����ڰ� IndexTrigger ��ư�� ������
        if (ARAVRInput.GetDown(ARAVRInput.Button.IndexTrigger))
        {
            // ��Ʈ�ѷ��� ���� ���
            ARAVRInput.PlayVibration(ARAVRInput.Controller.RTouch);

            // �Ѿ� ����� ���
            bulletAudio.Stop();
            bulletAudio.Play();

            // Ray�� ī�޶��� ��ġ���� �������� �����.
            Ray ray = new Ray(ARAVRInput.RHandPosition, ARAVRInput.RHandDirection);
            // Ray�� �浹 ������ �����ϱ� ���� ���� ����
            RaycastHit hitInfo;
            // �÷��̾� ���̾� ������
            int playerLayer = 1 << LayerMask.NameToLayer("Player");
            // Ÿ�� ���̾� ������
            int towerLayer = 1 << LayerMask.NameToLayer("Tower");
            int layerMask = playerLayer | towerLayer;

            // ���̸� ���. ���̰� �ε��� ������ hitInfo�� ����.
            if (Physics.Raycast(ray, out hitInfo, 200, ~layerMask))
            {
                // �Ѿ� ���� ȿ�� ó��
                // �Ѿ� ����Ʈ�� ���� ���̸� ���߰� ���
                bulletEffect.Stop();
                bulletEffect.Play();
                // �ε��� ������ �������� �Ѿ��� ����Ʈ ������ ����
                bulletImpact.forward = hitInfo.normal;
                // �ε��� ���� �ٷ� ������ ����Ʈ�� ���̵��� ����
                bulletImpact.position = hitInfo.point;

                // ray �� �ε��� ��ü�� drone �̶�� �ǰ� ó��
                if (hitInfo.transform.name.Contains("Drone"))
                {
                    DroneAI drone = hitInfo.transform.GetComponent<DroneAI>();
                    if (drone)
                    {
                        drone.OnDamageProcess();
                    }
                }
            }
        }
    }
}
