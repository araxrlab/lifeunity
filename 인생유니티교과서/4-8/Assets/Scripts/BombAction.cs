using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BombAction : MonoBehaviour
{
    // 폭발 이펙트 프리팹 변수
    public GameObject bombEffect;

    // 수류탄 데미지
    public int attackPower = 10;

    // 폭발 효과 반경
    public float explosionRadius = 5f;

    // 충돌했을 때의 처리
    private void OnCollisionEnter(Collision collision)
    {
        // 폭발 효과 반경 내에서 레이어가 "Enemy"인 모든 게임 오브젝트들의 Collider 컴포넌트를 배열에 저장한다.
        Collider[] cols = Physics.OverlapSphere(transform.position, explosionRadius, 1 << 10);

        // 저장된 Collider 배열에 있는 모든 에너미에게 수류탄 데미지를 적용한다.
        for(int i = 0; i < cols.Length; i++)
        {
            cols[i].GetComponent<EnemyFSM>().HitEnemy(attackPower);
        }

        // 이펙트 프리팹을 생성한다.
        GameObject eff = Instantiate(bombEffect);

        // 이펙트 프리팹의 위치는 수류탄 오브젝트 자신의 위치와 동일하다.
        eff.transform.position = transform.position;

        // 자기 자신을 제거한다.
        Destroy(gameObject);
    }
}
