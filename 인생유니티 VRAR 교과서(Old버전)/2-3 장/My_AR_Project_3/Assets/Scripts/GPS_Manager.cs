using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Android;

public class GPS_Manager : MonoBehaviour
{
    public static GPS_Manager instance;
    public Text latitude_text;
    public Text longitude_text;
    public float maxWaitTime = 10.0f;
    public float resendTime = 1.0f;

    public float latitude;
    public float longitude;
    float waitTime = 0;

    public bool receiveGPS = false;

    private void Awake()
    {
        if(instance == null)
        {
            instance = this;
        }
    }
    
    void Start()
    {
        StartCoroutine(GPS_On());
    }

    IEnumerator GPS_On()
    {
        // 만일, 위치 정보 수신에 대해 사용자의 허가를 받지 못했다면...
        if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
        {
            // 허가를 요청하는 팝업 띄운다.
            Permission.RequestUserPermission(Permission.FineLocation);

            // 동의 받았는지 확인될 때까지 잠시 대기한다.
            while(!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            {
                yield return null;
            }
        }
       
        // 만일, 사용자의 GPS 장치가 켜져있지 않다면, 함수를 종료한다.
        if(!Input.location.isEnabledByUser)
        {
            latitude_text.text = "GPS off";
            longitude_text.text = "GPS off";

            yield break;
        }

        // 위치 데이터를 요청한다.
        Input.location.Start();

        // 만일, 위치 데이터를 받으려고 하는 중이라면 대기한다.
        while(Input.location.status == LocationServiceStatus.Initializing && waitTime < maxWaitTime)
        {
            yield return new WaitForSeconds(1.0f);
            waitTime++;
        }

        if(Input.location.status == LocationServiceStatus.Failed)
        {
            // 위치 정보 수신에 실패했다고 표시한다.
            latitude_text.text = "위치 정보 수신 실패!";
            longitude_text.text = "위치 정보 수신 실패!";
        }

        if(waitTime >= maxWaitTime)
        {
            latitude_text.text = "응답 대기 시간 초과!";
            longitude_text.text = "응답 대기 시간 초과!";
        }

        receiveGPS = true;

        while (receiveGPS)
        {
            // 수신된 위치 정보 데이터를 UI에 출력한다.
            LocationInfo li = Input.location.lastData;
            latitude = li.latitude;
            longitude = li.longitude;

            latitude_text.text = "위도: " + latitude.ToString();
            longitude_text.text = "경도: " + longitude.ToString();

            yield return new WaitForSeconds(resendTime);
        }
    }
}
