using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class DroneAI : MonoBehaviour
{
    // 드론의 상태 상수 정의 
    enum DroneState
    {
        Idle,
        Move,
        Attack,
        Damage,
        Die
    }
    // 초기 시작 상태는 Idle로 설정 
    DroneState state = DroneState.Idle;
    // 대기 상태의 지속 시간 
    public float idleDelayTime = 2;
    // 경과 시간 
    float currentTime;
    // 이동 속도 
    public float moveSpeed = 1;
    // 타워 위치 
    Transform tower;
    // 길 찾기를 수행할 내비게이션 메시 에이전트 
    NavMeshAgent agent;
    // 공격 범위 
    public float attackRange = 5;

    // 공격 지연 시간 
    public float attackDelayTime = 2;

    // private 속성이지만 에디터에 노출된다. 
    [SerializeField]
    // 체력 
    int hp = 3;
    // 폭발 효과 
    Transform explosion;
    ParticleSystem expEffect;
    AudioSource expAudio;

    void Start()
    {
        // 타워 찾기 
        tower = GameObject.Find("ARA_Tower").transform;
        // NavMeshAGent 컴포넌트 가져오기 
        agent = GetComponent<NavMeshAgent>();
        agent.enabled = false;
        // agent의 속도 설정 
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

    // 일정 시간 동안 기다렸다가 상태를 공격으로 전환하기 
    private void Idle() 
    {
        // 1. 시간이 흘러야 한다. 
        currentTime += Time.deltaTime;
        // 2. 만약 경과 시간이 대기 시간을 초과했다면 
        if (currentTime > idleDelayTime)
        {
            // 3. 상태를 이동으로 전환
            state = DroneState.Move;
            // agent 활성화 
            agent.enabled = true;
        }
    }

    // 타워를 향해 이동하고 싶다.
    private void Move() 
    {
        // 내비게이션할 목적지 설정 
        agent.SetDestination(tower.position);
        // 공격 범위 안에 들어오면 공격 상태로 전환 
        if (Vector3.Distance(transform.position, tower.position) < attackRange)
        {
            state = DroneState.Attack;
            // agent의 동작 정지 
            agent.enabled = false;
            // 바로 공격할 수 있도록 공격 시간 설정 
            currentTime = attackDelayTime;
        }
    }
    private void Attack() 
    {
        // 1. 시간이 흐른다. 
        currentTime += Time.deltaTime;
        // 2. 경과 시간이 공격 지연 시간을 초과하면 
        if (currentTime > attackDelayTime)
        {
            // 3. 공격 -> Tower의 HP를 호출해 데미지 처리를 한다. 
            Tower.Instance.HP--;
            // 4. 경과 시간 초기화 
            currentTime = 0;
        }
    }

    IEnumerator Damage() 
    {
        // 1. 길 찾기 중지 
        agent.enabled = false;
        // 2. 자식 객체의 MeshRenderer에서 재질 얻어오기 
        Material mat = GetComponentInChildren<MeshRenderer>().material;
        // 3. 원래 색을 저장 
        Color originalColor = mat.color;
        // 4. 재질의 색 변경 
        mat.SetColor("_Color", Color.red);
        // 5. 0.1초 기다리기 
        yield return new WaitForSeconds(0.1f);
        // 6. 재질의 색을 원래대로 
        // 7. 상태를 Idle로 전환 
        mat.SetColor("_Color", originalColor);
        state = DroneState.Idle;
        // 8. 경과 시간 초기화 
        currentTime = 0;
    }

    private void Die() { }

    // 피격 상태 알림 이벤트 함수 
    public void OnDamageProcess()
    {
        // 체력을 감소시키고 죽지 않았다면 상태를 데미지로 전환하고 싶다. 
        // 1. 체력 감소 
        hp--;
        // 2. 만약 죽지 않았다면 
        if (hp > 0)
        {
            // 3. 상태를 데미지로 전환 
            state = DroneState.Damage;
            // 코루틴 호출
            StopAllCoroutines();
            StartCoroutine(Damage());
        }
        // 죽었다면 폭발 효과를 발생시키고 드론을 없앤다. 
        else
        {
            // 폭발 효과의 위치 지정 
            // 이펙트 재생 
            expEffect.Play();
            explosion.position = transform.position;
            // 이펙트 사운드 재생 
            expAudio.Play();
            // 드론 없애기 
            Destroy(gameObject);
        }
    }
}
