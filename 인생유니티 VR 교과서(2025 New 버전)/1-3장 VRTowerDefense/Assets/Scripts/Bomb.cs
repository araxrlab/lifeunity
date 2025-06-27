using UnityEngine;

// ��ź�� �浹�� ������ �� �ֺ��� �ִ� ����� �����ϰ� �ʹ�. 
// �ʿ� �Ӽ�: ���� ȿ��, ���� ����
public class Bomb : MonoBehaviour
{
    // ���� ȿ�� 
    Transform explosion;
    ParticleSystem expEffect;
    AudioSource expAudio;
    // ���� ���� 
    public float range = 5;

    void Start()
    {
        // ������ Explosion ��ü�� ã�� transform �������� 
        explosion = GameObject.Find("Explosion").transform;
        // Explosion ��ü�� ParticleSystem ������Ʈ ������ 
        expEffect = explosion.GetComponent<ParticleSystem>();
        // Explosion ��ü�� AudioSource ������Ʈ ������ 
        expAudio = explosion.GetComponent<AudioSource>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        // ���̾� ����ũ �������� 
        int layerMask = 1 << LayerMask.NameToLayer("Drone");
        // ��ź�� �߽����� range ũ���� �ݰ� �ȿ� ���� ��� �˻� 
        Collider[] drones = Physics.OverlapSphere(transform.position, range, layerMask);
        // ���� �ȿ� �ִ� ����� ��� ���� 
        foreach (Collider drone in drones)
        {
            Destroy(drone.gameObject);
        }
        // ���� ȿ���� ��ġ ���� 
        explosion.position = transform.position;
        // ����Ʈ ��� 
        expEffect.Play();
        // ����Ʈ ���� ��� 
        expAudio.Play();
        // ��ź ���ֱ� 
        Destroy(gameObject);
    }
}
