using System.Collections;
using UnityEngine;

public class GrabObject : MonoBehaviour
{
    // 필요 속성: 물체를 잡고 있는지 여부, 잡고 있는 물체, 잡을 물체의 종류, 잡을 수 있는 거리 
    // 물체를 잡고 있는지의 여부 
    bool isGrabbing = false;
    // 잡고 있는 물체 
    GameObject grabbedObject;
    // 잡을 물체의 종류 
    public LayerMask grabbedLayer;
    // 잡을 수 있는 거리 
    public float grabRange = 0.2f;
    // 이전 위치 
    Vector3 prevPos;
    // 던질 힘 
    float throwPower = 10;
    // 이전 회전 
    Quaternion prevRot;
    // 회전력 
    public float rotPower = 5;

    // 원거리에서 물체를 잡는 기능 활성화 여부 
    public bool isRemoteGrab = true;
    // 원거리에서 물체를 잡을 수 있는 거리 
    public float remoteGrabDistance = 20;

    void Update()
    {
        // 물체 잡기 
        // 1. 물체를 잡지 않고 있을 경우 
        if (isGrabbing == false)
        {
            // 잡기 시도 
            TryGrab();
        }
        else
        {
            // 물체 놓기
            TryUngrab();
        }
    }

    private void TryGrab()
    {
        // Grab 버튼을 누르면 일정 영역 안에 있는 폭탄을 잡는다. 
        // 1. Grab 버튼을 눌렀다면 
        if (ARAVRInput.GetDown(ARAVRInput.Button.HandTrigger, ARAVRInput.Controller.RTouch))
        {
            // 원거리 물체 잡기를 사용한다면 
            if (isRemoteGrab)
            {
                // 손 방향으로 Ray 제작 
                Ray ray = new Ray(ARAVRInput.RHandPosition, ARAVRInput.RHandDirection);
                RaycastHit hitInfo;

                // SphereCast를 이용해 물체 충돌을 체크 
                if (Physics.SphereCast(ray, 0.5f, out hitInfo, remoteGrabDistance,
                    grabbedLayer))
                {
                    // 잡은 상태로 전환 
                    isGrabbing = true;
                    // 잡은 물체에 대한 기억 
                    grabbedObject = hitInfo.transform.gameObject;
                    // 물체가 끌려오는 기능 실행 
                    StartCoroutine(GrabbingAnimation());
                }
                return;
            }

            // 2. 일정 영역 안에 폭탄이 있으니까 
            // 영역 안에 있는 모든 폭탄 검출 
            Collider[] hitObjects = Physics.OverlapSphere(ARAVRInput.RHandPosition,
            grabRange, grabbedLayer);

            // 가장 가까운 폭탄 인덱스 
            int closest = -1;
            float closestDistance = float.MaxValue;
            // 손과 가장 가까운 물체 선택 
            for (int i = 0; i < hitObjects.Length; i++)
            {
                // 손과 가장 가까운 물체와의 거리 
                var rigid = hitObjects[i].GetComponent<Rigidbody>();
                if (rigid == null)
                    continue;

                // 다음 물체와 손의 거리 
                Vector3 nextPos = hitObjects[i].transform.position;
                float nextDistance = Vector3.Distance(nextPos, ARAVRInput.RHandPosition);

                // 다음 물체와의 거리가 더 가깝다면 
                if (nextDistance < closestDistance)
                {
                    // 가장 가까운 물체 인덱스 교체 
                    closest = i;
                    closestDistance = nextDistance;
                }
            }

            // 3. 폭탄을 잡는다. 
            // 검출된 물체가 있을 경우 
            if (closest > -1)
            {
                // 잡은 상태로 전환 
                isGrabbing = true;
                // 잡은 물체에 대한 기억 
                grabbedObject = hitObjects[closest].gameObject;
                // 잡은 물체를 손의 자식으로 등록 
                grabbedObject.transform.parent = ARAVRInput.RHand;
                // 물리 기능 정지 
                grabbedObject.GetComponent<Rigidbody>().isKinematic = true;

                // 초기 위치 값 지정 
                prevPos = ARAVRInput.RHandPosition;
                // 초기 회전 값 지정 
                prevRot = ARAVRInput.RHand.rotation;
            }
        }
    }

    private void TryUngrab()
    {
        // 던질 방향
        Vector3 throwDirection = (ARAVRInput.RHandPosition - prevPos);
        // 위치 기억 
        prevPos = ARAVRInput.RHandPosition;
        // 쿼터니온 공식 
        // angle1 = Q1, angle2 = Q2 
        // angle1 + angle2 = Q1 * Q2 
        // -angle2 = Quaternion.Inverse(Q2) 
        // angle2 - angle1 = Quaternion.FromToRotation(Q1, Q2) = Q2 * Quaternion.Inverse(Q1) 
        // 회전 방향 = current - previous의 차 로 구함. - previous는 Inverse로 구함. 
        Quaternion deltaRotation = ARAVRInput.RHand.rotation * Quaternion.Inverse(prevRot);
        // 이전 회전 저장 
        prevRot = ARAVRInput.RHand.rotation;

        // 버튼을 놓았다면 
        if (ARAVRInput.GetUp(ARAVRInput.Button.HandTrigger, ARAVRInput.Controller.RTouch))
        {
            // 잡지 않은 상태로 전환 
            isGrabbing = false;
            // 물리 기능 활성화 
            grabbedObject.GetComponent<Rigidbody>().isKinematic = false;
            // 손에서 폭탄 떼어내기 
            grabbedObject.transform.parent = null;

            // 던지기 
            grabbedObject.GetComponent<Rigidbody>().linearVelocity = throwDirection * throwPower;

            // 각속도 = (1/dt) * dθ(특정 축 기준 변위 각도) 
            float angle;
            Vector3 axis;
            deltaRotation.ToAngleAxis(out angle, out axis);
            Vector3 angularVelocity = (1.0f / Time.deltaTime) * angle * axis;
            grabbedObject.GetComponent<Rigidbody>().angularVelocity = angularVelocity;

            // 잡은 물체가 없도록 설정 
            grabbedObject = null;
        }
    }

    IEnumerator GrabbingAnimation()
    {
        // 물리 기능 정지 
        grabbedObject.GetComponent<Rigidbody>().isKinematic = true;
        // 초기 위치 값 지정 
        prevPos = ARAVRInput.RHandPosition;
        // 초기 회전 값 지정 
        prevRot = ARAVRInput.RHand.rotation;
        Vector3 startLocation = grabbedObject.transform.position;
        Vector3 targetLocation = ARAVRInput.RHandPosition + ARAVRInput.RHandDirection * 0.1f;

        float currentTime = 0;
        float finishTime = 0.2f;
        // 경과율 
        float elapsedRate = currentTime / finishTime;
        while (elapsedRate < 1)
        {
            currentTime += Time.deltaTime;
            elapsedRate = currentTime / finishTime;
            grabbedObject.transform.position = Vector3.Lerp(startLocation, targetLocation,
            elapsedRate);
            yield return null;
        }
        // 잡은 물체를 손의 자식으로 등록 
        grabbedObject.transform.position = targetLocation;
        grabbedObject.transform.parent = ARAVRInput.RHand;
    }
}
