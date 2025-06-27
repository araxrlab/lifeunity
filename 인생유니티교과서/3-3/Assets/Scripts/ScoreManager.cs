using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// 유니티 UI 를 사용하기위한 네임스페이스
using UnityEngine.UI;
// 목표 : 점수를 관리하며 화면에 표시를 하고 싶다.
// 필요속성 : 점수 UI, 현재점수, 최고점수
// 순서 : 
public class ScoreManager : MonoBehaviour
{
    // 필요속성 : 점수 UI, 현재점수, 최고점수
    // 현재 점수 UI
    public Text currentScoreUI;
    // 현재 점수
    private int currentScore;
    // 최고 점수 UI
    public Text bestScoreUI;
    // 최고 점수
    private int bestScore;
    // 싱글톤 객체
    public static ScoreManager Instance = null;

    public int Score
    {
        get
        {
            return currentScore;
        }
        set
        {
            // 3.ScoreManager 클래스의 속성에 값을 할당 한다.
            currentScore = value;
            // 4.화면에 현재 점수 표시하기
            currentScoreUI.text = "현재점수 : " + currentScore;

            //목표: 최고 점수를 표시하고 싶다.
            //1.현재 점수가 최고 점수 보다 크니까
            //  -> 만약 현재 점수가 최고 점수를 초과 하였다면”
            if (currentScore > bestScore)
            {
                //2.최고 점수가 갱신 시킨다.
                bestScore = currentScore;
                //3.최고 점수 UI 에 표시
                bestScoreUI.text = "최고점수 : " + bestScore;
                // 목표 : 최고점수를 저장하고싶다.
                PlayerPrefs.SetInt("Best Score", bestScore);
            }
        }
    }

    // 싱글톤 객체에 값이 없으면 생성된 자기 자신을 할당
    void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        // 목표 : 최고점수 불러와서 bestScore 변수에 할당하고 화면에 표시한다.
        // 순서 : 1. 최고점수 불러와서 bestScore 에 넣어주기
        bestScore = PlayerPrefs.GetInt("Best Score", 0);
        //        2. 최고점수 화면에 표시하기
        bestScoreUI.text = "최고점수 : " + bestScore;
    }

    // currentScore 에 값을 넣고 화면에 표시하기
    public void SetScore(int value)
    {
        // 3.ScoreManager 클래스의 속성에 값을 할당 한다.
        currentScore = value;
        // 4.화면에 현재 점수 표시하기
        currentScoreUI.text = "현재점수 : " + currentScore;

        //목표: 최고 점수를 표시하고 싶다.
        //1.현재 점수가 최고 점수 보다 크니까
        //  -> 만약 현재 점수가 최고 점수를 초과 하였다면”
        if (currentScore > bestScore)
        {
            //2.최고 점수가 갱신 시킨다.
            bestScore = currentScore;
            //3.최고 점수 UI 에 표시
            bestScoreUI.text = "최고점수 : " + bestScore;
            // 목표 : 최고점수를 저장하고싶다.
            PlayerPrefs.SetInt("Best Score", bestScore);
        }
    }

    // currentScore 값 가져오기
    public int GetScore()
    {
        return currentScore;
    }
}
