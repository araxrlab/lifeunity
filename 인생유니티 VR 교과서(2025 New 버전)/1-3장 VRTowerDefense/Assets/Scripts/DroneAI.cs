using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class DroneAI : MonoBehaviour
{
    // ����� ���� ��� ���� 
    enum DroneState
    {
        Idle,
        Move,
        Attack,
        Damage,
        Die
    }
    // �ʱ� ���� ���´� Idle�� ���� 
    DroneState state = DroneState.Idle;
    // ��� ������ ���� �ð� 
    public float idleDelayTime = 2;
    // ��� �ð� 
    float currentTime;
    // �̵� �ӵ� 
    public float moveSpeed = 1;
    // Ÿ�� ��ġ 
    Transform tower;
    // �� ã�⸦ ������ ������̼� �޽� ������Ʈ 
    NavMeshAgent agent;
    // ���� ���� 
    public float attackRange = 5;

    // ���� ���� �ð� 
    public float attackDelayTime = 2;

    // private �Ӽ������� �����Ϳ� ����ȴ�. 
    [SerializeField]
    // ü�� 
    int hp = 3;
    // ���� ȿ�� 
    Transform explosion;
    ParticleSystem expEffect;
    AudioSource expAudio;

    void Start()
    {
        // Ÿ�� ã�� 
        tower = GameObject.Find("ARA_Tower").transform;
        // NavMeshAGent ������Ʈ �������� 
        agent = GetComponent<NavMeshAgent>();
        agent.enabled = false;
        // agent�� �ӵ� ���� 
        agent.speed = moveSpeed;

        explosion = GameObject.Find("Explosion").transform;
        expEffect = explosion.GetComponent<ParticleSystem>();
        expAudio = explosion.GetComponent<AudioSource>();
    }

    void Update()
    {
        print("current State : " + state);
        switch (state)
        {
            case DroneState.Idle:
                Idle();
                break;
            case DroneState.Move:
                Move();
                break;
            case DroneState.Attack:
                Attack();
                break;
            case DroneState.Damage:
                //Damage();
                break;
            case DroneState.Die:
                Die();
                break;
        }
    }

    // ���� �ð� ���� ��ٷȴٰ� ���¸� �������� ��ȯ�ϱ� 
    private void Idle() 
    {
        // 1. �ð��� �귯�� �Ѵ�. 
        currentTime += Time.deltaTime;
        // 2. ���� ��� �ð��� ��� �ð��� �ʰ��ߴٸ� 
        if (currentTime > idleDelayTime)
        {
            // 3. ���¸� �̵����� ��ȯ
            state = DroneState.Move;
            // agent Ȱ��ȭ 
            agent.enabled = true;
        }
    }

    // Ÿ���� ���� �̵��ϰ� �ʹ�.
    private void Move() 
    {
        // ������̼��� ������ ���� 
        agent.SetDestination(tower.position);
        // ���� ���� �ȿ� ������ ���� ���·� ��ȯ 
        if (Vector3.Distance(transform.position, tower.position) < attackRange)
        {
            state = DroneState.Attack;
            // agent�� ���� ���� 
            agent.enabled = false;
            // �ٷ� ������ �� �ֵ��� ���� �ð� ���� 
            currentTime = attackDelayTime;
        }
    }
    private void Attack() 
    {
        // 1. �ð��� �帥��. 
        currentTime += Time.deltaTime;
        // 2. ��� �ð��� ���� ���� �ð��� �ʰ��ϸ� 
        if (currentTime > attackDelayTime)
        {
            // 3. ���� -> Tower�� HP�� ȣ���� ������ ó���� �Ѵ�. 
            Tower.Instance.HP--;
            // 4. ��� �ð� �ʱ�ȭ 
            currentTime = 0;
        }
    }

    IEnumerator Damage() 
    {
        // 1. �� ã�� ���� 
        agent.enabled = false;
        // 2. �ڽ� ��ü�� MeshRenderer���� ���� ������ 
        Material mat = GetComponentInChildren<MeshRenderer>().material;
        // 3. ���� ���� ���� 
        Color originalColor = mat.color;
        // 4. ������ �� ���� 
        mat.SetColor("_Color", Color.red);
        // 5. 0.1�� ��ٸ��� 
        yield return new WaitForSeconds(0.1f);
        // 6. ������ ���� ������� 
        // 7. ���¸� Idle�� ��ȯ 
        mat.SetColor("_Color", originalColor);
        state = DroneState.Idle;
        // 8. ��� �ð� �ʱ�ȭ 
        currentTime = 0;
    }

    private void Die() { }

    // �ǰ� ���� �˸� �̺�Ʈ �Լ� 
    public void OnDamageProcess()
    {
        // ü���� ���ҽ�Ű�� ���� �ʾҴٸ� ���¸� �������� ��ȯ�ϰ� �ʹ�. 
        // 1. ü�� ���� 
        hp--;
        // 2. ���� ���� �ʾҴٸ� 
        if (hp > 0)
        {
            // 3. ���¸� �������� ��ȯ 
            state = DroneState.Damage;
            // �ڷ�ƾ ȣ��
            StopAllCoroutines();
            StartCoroutine(Damage());
        }
        // �׾��ٸ� ���� ȿ���� �߻���Ű�� ����� ���ش�. 
        else
        {
            // ���� ȿ���� ��ġ ���� 
            // ����Ʈ ��� 
            expEffect.Play();
            explosion.position = transform.position;
            // ����Ʈ ���� ��� 
            expAudio.Play();
            // ��� ���ֱ� 
            Destroy(gameObject);
        }
    }
}
