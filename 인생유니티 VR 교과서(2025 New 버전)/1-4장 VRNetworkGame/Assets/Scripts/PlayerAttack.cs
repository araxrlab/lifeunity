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
        // �� NetworkBehaivour �� ���� ��ȭ ���� ��ü�� �����Ѵ�. 
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        if (HasStateAuthority)
        {
            // ���� ü���� �ִ� ü������ ä���. 
            curHP = maxHP;
        }
    }

    void Update()
    {
        // �Է±����� ���� ����ڰ� ���ݹ�ư�� Ŭ���ϸ� 
        if (HasInputAuthority && ARAVRInput.GetDown(ARAVRInput.Button.One))
        {
            // �ִϸ��̼� ó�� 
            anim.SetTrigger("Attack");
            // ���� RPC �� �̺�Ʈ ���� 
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

    // �������� ����Ǵ� ���� RPC 
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_ServerAttackAnimation()
    {
        RPC_ClientAttackAnimation();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_ClientAttackAnimation()
    {
        // �ڽ��� ĳ���Ͱ� �ƴҰ�� �ִϸ��̼� ��� 
        if (HasInputAuthority == false)
            anim.SetTrigger("Attack");
    }

    // ������ ���� RPC 
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_ServerDamage(float pow)
    {
        curHP = Mathf.Max(0, curHP - pow);
    }

    private void OnTriggerEnter(Collider other)
    {
        // ����, �ڽ��� ĳ�����̸鼭 �ε��� ����� dagger �϶� ó�� 
        if (HasInputAuthority && other.gameObject.name.Contains("dagger"))
        {
            // ������ ó�� �Լ��� RPC�� ȣ���Ѵ�. 
            RPC_ServerDamage(attackPower);

            PlayerAttack pa = other.transform.root.GetComponent<PlayerAttack>();
            // ������ �ݶ��̴��� ��Ȱ��ȭ�Ѵ�. 
            pa.weaponCol.enabled = false;
        }
    }
}
