using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 목표 : 적이 다른 물체와 충돌 했을 때 폭발 효과를 발생 시키고 싶다.
// 순서 : 1. 적이 다른 물체와 충돌 했으니까.
//        2. 폭발 효과 공장에서 폭발 효과를 하나 만들어야 한다.
//        3. 폭발 효과를 발생(위치) 시키고 싶다.
//필요한 속성 : 폭발 공장 주소(외부에서 값을 넣어준다.)
public class Enemy : MonoBehaviour
{
    public float speed = 5;

    GameObject player;
    Vector3 dir;
    //폭발 공장 주소(외부에서 값을 넣어준다.)
    public GameObject explosionFactory;

    // Start is called before the first frame update
    void Start()
    {
        player = GameObject.Find("Player");
        if (player != null)
        {
            int random = Random.Range(0, 100);

            if (random < 50)
            {
                dir = player.transform.position - transform.position;
                dir.Normalize();
            }
            else
            {
                dir = Vector3.down;
            }
        }
        else
        {
            dir = Vector3.down;
        }
    }

    // Update is called once per frame
    void Update()
    {
        transform.position += dir * speed * Time.deltaTime;
    }

    //1. 적이 다른 물체와 충돌 했으니까.
    private void OnCollisionEnter(Collision collision)
    {
        // 에너미를 잡을 때마다 현재 점수 표시하고 싶다.
        // 1. 씬에서 ScoreManager 객체를 찾아오자
        GameObject smObject = GameObject.Find("ScoreManager");
        // 2. ScoreManager 게임오브젝트에서 얻어 온다
        ScoreManager sm = smObject.GetComponent<ScoreManager>();
        // 3. ScoreManager 의 Get/Set 함수로 수정
        sm.SetScore(sm.GetScore() + 1);
        //2.폭발 효과 공장에서 폭발 효과를 하나 만들어야 한다.
        GameObject explosion = Instantiate(explosionFactory);
        //3.폭발 효과를 발생(위치) 시키고 싶다.
        explosion.transform.position = transform.position;
        Destroy(collision.gameObject);
        Destroy(gameObject);
    }
}