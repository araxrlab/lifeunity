using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//목표 : 배경 스크롤이 되도록 하고 싶다.
//필요속성 : 매터리얼, 스크롤속도
//순서 : 1. 살아 있는 동안 계속 하고 싶다.
      //2. 방향이 필요하다.
      //3. 스크롤을 하고 싶다.
public class Background : MonoBehaviour
{
    //배경매터리얼
    public Material bgMaterial;
    //스크롤속도
    public float scrollSpeed = 0.2f;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // 1. 살아 있는 동안 계속 하고 싶다.
    void Update()
    {
        //2. 방향이 필요하다.
        Vector2 direction = Vector2.up;
        //3. 스크롤을 하고 싶다. P = P0 + vt
        bgMaterial.mainTextureOffset += direction * scrollSpeed * Time.deltaTime;
    }
}
