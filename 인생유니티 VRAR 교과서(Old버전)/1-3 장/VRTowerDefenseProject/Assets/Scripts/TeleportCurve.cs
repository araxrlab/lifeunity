using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TeleportCurve : MonoBehaviour
{
    // 텔레포트를 표시할 UI
    public Transform teleportCircleUI;
    // 선을 그릴 라인 렌더러
    LineRenderer lr;
    // 최초 텔레포트 UI 크기
    Vector3 originScale = Vector3.one * 0.02f;
    // 커브의 부드러운 정도
    public int lineSmooth = 40;
    // 커브의 길이
    public float curveLength = 50;
    // 커브의 중력
    public float gravity = -60;
    // 곡선 시뮬레이션의 간격 및 시간
    public float simulateTime = 0.02f;
    // 곡선을 이루는 점들을 기억할 리스트
    List<Vector3> lines = new List<Vector3>();

    void Start()
    {
        // 시작할 때 비활성화한다.
        teleportCircleUI.gameObject.SetActive(false);
        // 라인 렌더러 컴포넌트 얻어오기
        lr = GetComponent<LineRenderer>();
        // 라인 렌더러의 선 너비 지정
        lr.startWidth = 0.0f;
        lr.endWidth = 0.2f;
    }

    void Update()
    {
        // 왼쪽 컨트롤러의 One 버튼을 누르면
        if (ARAVRInput.GetDown(ARAVRInput.Button.One, ARAVRInput.Controller.LTouch))
        {
            // 라인 렌더러 컴포넌트 활성화
            lr.enabled = true;
        }
        // 왼쪽 컨트롤러의 One 버튼에서 손을 떼면
        else if (ARAVRInput.GetUp(ARAVRInput.Button.One, ARAVRInput.Controller.LTouch))
        {
            // 라인 렌더러 비활성화
            lr.enabled = false;

            // 텔레포트 UI가 활성화돼 있을 때
            if (teleportCircleUI.gameObject.activeSelf)
            {
                GetComponent<CharacterController>().enabled = false;
                // 텔레포트 UI 위치로 순간 이동
                transform.position = teleportCircleUI.position + Vector3.up;
                GetComponent<CharacterController>().enabled = true;
            }

            // 텔레포트 UI 비활성화
            teleportCircleUI.gameObject.SetActive(false);
        }
        // 왼쪽 컨트롤러의 One 버튼을 누르고 있을 때
        else if (ARAVRInput.Get(ARAVRInput.Button.One, ARAVRInput.Controller.LTouch))
        {
            // 주어진 길이 크기의 커브를 만들고 싶다.
            MakeLines();
        }
    }

    // 라인 렌더러를 이용해 점을 만들고 선을 그린다.
    void MakeLines()
    {
        // 리스트에 담긴 위치 정보들을 비워준다.
        lines.RemoveRange(0, lines.Count);
        // 선이 진행될 방향을 정한다.
        Vector3 dir = ARAVRInput.LHandDirection * curveLength;
        // 선이 그려질 위치의 초깃값을 설정한다.
        Vector3 pos = ARAVRInput.LHandPosition;
        // 최초 위치를 리스트에 담는다.
        lines.Add(pos);
        // lineSmooth 개수만큼 반복한다.
        for (int i = 0; i < lineSmooth; i++)
        {
            // 현재 위치 기억
            Vector3 lastPos = pos;
            // 중력을 적용한 속도 계산
            // v = v0 + at
            dir.y += gravity * simulateTime;
            // 등속 운동으로 다음 위치 계산
            //P = P0 + vt
            pos += dir * simulateTime;
            // Ray 충돌 체크가 일어났으면 
            if (CheckHitRay(lastPos, ref pos))
            {
                // 충돌 지점을 등록하고 종료
                lines.Add(pos);
                break;
            }
            else
            {
                // 텔레포트 UI 비활성화
                teleportCircleUI.gameObject.SetActive(false);
            }
            // 구한 위치를 등록
            lines.Add(pos);
        }

        // 라인 렌더러가 표현할 점의 개수를 등록된 개수의 크기로 할당
        lr.positionCount = lines.Count;
        // 라인 렌더러에 구해진 점의 정보를 지정
        lr.SetPositions(lines.ToArray());
    }

    // 앞 점의 위치와 다음 점의 위치를 받아 레이의 충돌을 체크
    private bool CheckHitRay(Vector3 lastPos, ref Vector3 pos)
    {
        // 앞 점 lastPos에서 다음 점 pos로 향하는 벡터 계산
        Vector3 rayDir = pos - lastPos;
        Ray ray = new Ray(lastPos, rayDir);
        RaycastHit hitInfo;
        // Raycast할 때 레이의 크기를 앞 점과 다음 점 사이의 거리로 한정한다.
        if (Physics.Raycast(ray, out hitInfo, rayDir.magnitude))
        {
            // 다음 점의 위치를 충돌한 지점으로 설정
            pos = hitInfo.point;
            int layer = LayerMask.NameToLayer("Terrain");

            // Terrain 레이어와 충돌했을 경우에만 텔레포트 UI가 표시되도록 한다.
            if (hitInfo.transform.gameObject.layer == layer)
            {
                // 텔레포트 UI 활성화
                teleportCircleUI.gameObject.SetActive(true);
                // 텔레포트 UI의 위치 지정
                teleportCircleUI.position = pos;
                // 텔레포트 UI의 방향 설정
                teleportCircleUI.forward = hitInfo.normal;
                float distance = (pos - ARAVRInput.LHandPosition).magnitude;
                // 텔레포트 UI가 보일 크기를 설정
                teleportCircleUI.localScale = originScale * Mathf.Max(1, distance);
            }
            return true;
        }

        return false;
    }
}
