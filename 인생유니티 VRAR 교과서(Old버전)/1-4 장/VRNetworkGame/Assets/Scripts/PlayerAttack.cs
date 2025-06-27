using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using UnityEngine.UI;

public class PlayerAttack : MonoBehaviourPun
{
    public Animator anim;
    public Slider hpSlider;
    public float maxHP = 10.0f;
    public float attackPower = 2.0f;
    public BoxCollider weaponCol;

    float curHP = 0;

    void Start()
    {
        // 최초 체력 상태는 최대치 상태로 한다.
        curHP = maxHP;

        // 체력 슬라이더에 현재 체력 상태를 반영한다.
        hpSlider.value = curHP / maxHP;
    }

    void Update()
    {
        // 오른손 트리거 버튼을 당기면 공격 애니메이션을 실행한다.
        //if(OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
        if(Input.GetMouseButtonDown(0))
        {
            if (photonView.IsMine)
            {
                photonView.RPC("AttackAnimation", RpcTarget.AllBuffered);
            }
        }
    }

    // 공격 실행 함수 + 동기화 애트리뷰트
    [PunRPC]
    public void AttackAnimation()
    {
        anim.SetTrigger("Attack");
    }

    // 데미지 처리 함수 + 동기화 애트리뷰트
    [PunRPC]
    public void Damaged(float pow)
    {
        // 0을 하한으로 공격력만큼을 현재 체력에서 감소시킨다.
        curHP = Mathf.Max(0, curHP - pow);

        // 변경된 현재 체력을 슬라이더에 반영한다.
        hpSlider.value = curHP / maxHP;
    }

    private void OnTriggerEnter(Collider other)
    {
        // 나의 검에 닿은 대상의 이름이 "Player"라는 글자를 포함하고 있다면...
        if (photonView.IsMine && other.gameObject.name.Contains("Player"))
        {
            // 상대방 캐릭터에 있는 포톤뷰 컴포넌트를 이용해서 데미지 처리 함수를 실행시키겠다.
            PhotonView pv = other.gameObject.GetComponent<PhotonView>();
            pv.RPC("Damaged", RpcTarget.AllBuffered, attackPower);

            // 내 검의 박스 콜라이더를 비활성화시킨다.
            weaponCol.enabled = false;
        }

    }
}
