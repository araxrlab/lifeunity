using UnityEngine;

public class Gun : MonoBehaviour
{
    public Transform bulletImpact; // �Ѿ� ���� ȿ�� 
    ParticleSystem bulletEffect; // �Ѿ� ���� ��ƼŬ �ý��� 
    AudioSource bulletAudio;    // �Ѿ� �߻� ����
    // crosshair�� ���� �Ӽ� 
    public Transform crosshair;

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

            // Ray�� ī�޶��� ��ġ�κ��� �������� �����. 
            Ray ray = new Ray(ARAVRInput.RHandPosition, ARAVRInput.RHandDirection);
            // Ray�� �浹 ������ �����ϱ� ���� ���� ���� 
            RaycastHit hitInfo;
            // �÷��̾� ���̾� ������ 
            int playerLayer = 1 << LayerMask.NameToLayer("Player");
            // Ÿ�� ���̾� ������ 
            int towerLayer = 1 << LayerMask.NameToLayer("Tower");
            int layerMask = playerLayer | towerLayer;

            // Ray�� ���. ray�� �ε��� ������ hitInfo�� ����. 
            if (Physics.Raycast(ray, out hitInfo, 200, ~layerMask))
            {
                // �Ѿ� ����Ʈ ����ǰ� ������ ���߰� ��� 
                bulletEffect.Stop();
                bulletEffect.Play();
                // �ε��� ���� �ٷ� ������ ����Ʈ�� ���̵��� ���� 
                bulletImpact.position = hitInfo.point;
                // �ε��� ������ �������� �Ѿ� ����Ʈ�� ������ ���� 
                bulletImpact.forward = hitInfo.normal;

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
