using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneSelector : MonoBehaviour
{
    // 활성화하고자 하는 씬의 이름을 받아서 해당 씬으로 변경하는 함수
    public void ARSCeneChange(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
    
}
