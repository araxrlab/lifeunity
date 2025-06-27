using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 폭탄이 충돌해 폭발할 때 주변에 있는 드론들을 제거하고 싶다.
// 필요 속성: 폭발 효과, 폭발 영역
public class Bomb : MonoBehaviour
{
    // 폭발 효과
    Transform explosion;
    ParticleSystem expEffect;
    AudioSource expAudio;

    // 폭발 영역
    public float range = 5;

    void Start()
    {
        // 씬에서 Explosion 객체 찾아 transform 가져오기
        explosion = GameObject.Find("Explosion").transform;
        // Explosion 객체의 ParticleSystem 컴포넌트 얻어오기
        expEffect = explosion.GetComponent<ParticleSystem>();
        // Explosion 객체의 AudioSource 컴포넌트 얻어오기
        expAudio = explosion.GetComponent<AudioSource>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        // 레이어 마스크 가져오기
        int layerMask = 1 << LayerMask.NameToLayer("Drone");

        // 폭탄을 중심으로 range 크기의 반경 안에 들어온 드론 검사
        Collider[] drones = Physics.OverlapSphere(transform.position, range, layerMask);

        // 영역 안에 있는 드론을 모두 제거
        foreach (Collider drone in drones)
        {
            Destroy(drone.gameObject);
        }

        // 폭발 효과의 위치 지정
        explosion.position = transform.position;

        // 이펙트 재생
        expEffect.Play();
        // 이펙트 사운드 재생
        expAudio.Play();

        // 폭탄 없애기
        Destroy(gameObject);
    }
}
