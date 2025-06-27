using Fusion;
using UnityEngine;
using UnityEngine.UI;

public class PlayerAttack : NetworkBehaviour
{
    public Animator anim;
    public float maxHP = 10;
    public float attackPower = 2;
    public Slider hpSlider;
    public BoxCollider weaponCol;
    [Networked] float curHP { get; set; }
    private ChangeDetector _changeDetector;

    void Start()
    {
        // 이 NetworkBehaivour 를 위한 변화 감지 객체를 생성한다. 
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        if (HasStateAuthority)
        {
            // 현재 체력을 최대 체력으로 채운다. 
            curHP = maxHP;
        }
    }

    void Update()
    {
        // 입력권한을 갖는 사용자가 공격버튼을 클릭하면 
        if (HasInputAuthority && ARAVRInput.GetDown(ARAVRInput.Button.One))
        {
            // 애니메이션 처리 
            anim.SetTrigger("Attack");
            // 서버 RPC 로 이벤트 전송 
            RPC_ServerAttackAnimation();
        }
    }

    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            switch (change)
            {
                case nameof(curHP):
                    hpSlider.value = curHP / maxHP;
                    break;
            }
        }
    }

    // 서버에서 실행되는 서버 RPC 
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_ServerAttackAnimation()
    {
        RPC_ClientAttackAnimation();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_ClientAttackAnimation()
    {
        // 자신의 캐릭터가 아닐경우 애니메이션 재생 
        if (HasInputAuthority == false)
            anim.SetTrigger("Attack");
    }

    // 데미지 서버 RPC 
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_ServerDamage(float pow)
    {
        curHP = Mathf.Max(0, curHP - pow);
    }

    private void OnTriggerEnter(Collider other)
    {
        // 만일, 자신의 캐릭터이면서 부딪힌 대상이 dagger 일때 처리 
        if (HasInputAuthority && other.gameObject.name.Contains("dagger"))
        {
            // 데미지 처리 함수를 RPC로 호출한다. 
            RPC_ServerDamage(attackPower);

            PlayerAttack pa = other.transform.root.GetComponent<PlayerAttack>();
            // 무기의 콜라이더를 비활성화한다. 
            pa.weaponCol.enabled = false;
        }
    }
}
