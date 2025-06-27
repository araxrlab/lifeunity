using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerFire : MonoBehaviour
{
    public GameObject bulletFactory;

    // 탄창에 넣을 총알 개수
    public int poolSize = 10;
    // 오브젝트풀 배열
    public List<GameObject> bulletObjectPool;

    // 태어 날 때 오브젝트풀(탄창) 에 총알을 하나씩 생성하여 넣고 싶다
    // 1. 태어 날 때
    void Start()
    {
        // 2. 탄창을 총알 담을 수 있는 크기로 만들어 준다.
        bulletObjectPool = new List<GameObject>();
        // 3. 탄창에 넣을 총알 개수 만큼 반복하여
        for (int i = 0; i < poolSize; i++)
        {
            // 4. 총알공장에서 총알 생성한다.
            GameObject bullet = Instantiate(bulletFactory);
            // 5. 총알을 오브젝트풀에 넣고싶다.
            bulletObjectPool.Add(bullet);
            // 비활성화 시키자.
            bullet.SetActive(false);
        }

// 실행 되는 플랫폼이 안드로이드일 경우 조이스틱을 활성화 시킨다.
#if UNITY_ANDROID
        GameObject.Find("Joystick canvas XYBZ").SetActive(true);
#elif UNITY_EDITOR || UNITY_STANDALONE
        GameObject.Find("Joystick canvas XYBZ").SetActive(false);
#endif
    }

    //목표 : 발사 버튼을 누르면 탄창에 있는 총알 중 비활성화 된 녀석을 발사 하고 싶다.
    void Update()
    {
        // 유니티데이터와 PC (Mac, Windows, Linux) 환경일때 동작
#if UNITY_EDITOR || UNITY_STANDALONE
        //1.발사 버튼을 눌렀으니까
        if (Input.GetButtonDown("Fire1"))
        {
            Fire();
        }
#endif
    }

    public void Fire()
    {
        //2.탄창 안에 있는 총알이 있다면
        if (bulletObjectPool.Count > 0)
        {
            //3.비활성화 된 총알을 하나 가져온다.
            GameObject bullet = bulletObjectPool[0];
            //4.총알을 발사하고 싶다.(활성화시킨다.)
            bullet.SetActive(true);
            //오브젝트풀에서 총알제거
            bulletObjectPool.Remove(bullet);
            // 총알을 위치 시키기
            bullet.transform.position = transform.position;
        }
    }
}
