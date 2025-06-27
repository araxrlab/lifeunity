using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

// ī�޶��� �ü��� ó���ϱ� ���� ���
public class GazePointerCtrl : MonoBehaviour
{
    public Transform uiCanvas;
    public Image gazeImg;
    // ���� �ð� ���� �ü��� �ӹ��� ���� �����ֱ� ���� UI 
    // �ü��� �ӹ��� ���� ��ȭ�� ǥ���ϱ� ���� UI image ������Ʈ 
    // UI �⺻ �������� �����صα� ���� �� 
    Vector3 defaultScale;
    // UI ī�޶� 1m�� �� ���� 
    public float uiScaleVal = 1f;

    // ���ͷ����� �Ͼ�� ������Ʈ�� �ü��� ������ true, ���� ������ false 
    bool isHitObj;
    // ���� �������� �ü��� �ӹ����� ������Ʈ ������ ��� ���� ����
    GameObject prevHitObj;
    // ���� �������� �ü��� �ӹ����� ������Ʈ ������ ��� ���� ���� 
    GameObject curHitObj;
    // �ü��� �ӹ����� �ð��� �����ϱ� ���� ���� 
    float curGazeTime = 0;
    // �ü��� �ӹ� �ð��� üũ�ϱ� ���� ���� �ð� 3��(�ʿ信 ���� ����) 
    public float gazeChargeTime = 3f;

    public Video360Play vp360; // 360 ���Ǿ �߰��� ���� �÷��� ��� 

    void Start()
    {
        // ������Ʈ�� ���� �⺻ ������ �� 
        defaultScale = uiCanvas.localScale;
        // �ü��� �����ϴ��� üũ�ϱ� ���� ���� �ʱ�ȭ
        curGazeTime = 0;
    }

    void Update()
    {
        // ĵ���� ������Ʈ�� �������� �Ÿ��� ���� �����Ѵ�. 
        // 1. ī�޶� �������� ���� ������ ��ǥ�� ���Ѵ�. 
        Vector3 dir = transform.TransformPoint(Vector3.forward);
        // 2. ī�޶� �������� ������ ���̸� �����Ѵ�. 
        Ray ray = new Ray(transform.position, dir);
        RaycastHit hitInfo; // ��Ʈ�� ������Ʈ�� ������ ��´�. 

        // 3. ���̿� �ε��� ��쿡�� �Ÿ� ���� �̿��� uiCanvas�� ũ�⸦ �����Ѵ�. 
        if (Physics.Raycast(ray, out hitInfo))
        {
            uiCanvas.localScale = defaultScale * uiScaleVal * hitInfo.distance;
            uiCanvas.position = transform.forward * hitInfo.distance;
            if (hitInfo.transform.tag == "GazeObj")
            {
                isHitObj = true;
                curHitObj = hitInfo.transform.gameObject;
            }
        }
        else // 4. �ƹ��͵� �ε����� ������ �⺻ ������ ������ uiCanvas�� ũ�⸦ �����Ѵ�. 
        {
            uiCanvas.localScale = defaultScale * uiScaleVal;
            uiCanvas.position = transform.position + dir;
        }
        // 5. uiCanvas�� �׻� ī�޶� ������Ʈ�� �ٶ󺸰� �Ѵ�. 
        uiCanvas.forward = transform.forward * -1;

        // GazeObj�� ���̰� ����� �� ���� 
        if (isHitObj)
        {
            // ���� �����Ӱ� ���� �������� ������Ʈ�� ���ƾ� �ð� ���� 
            if (curHitObj == prevHitObj)
            {
                // ���ͷ����� �߻��ؾ� �ϴ� ������Ʈ�� �ü��� ������ �ִٸ� �ð� ���� 
                curGazeTime += Time.deltaTime;
            }
            else
            {
                // ���� �������� ���� ������ ������Ʈ�Ѵ�. 
                prevHitObj = curHitObj;
            }

            // hit�� ������Ʈ�� VideoPlayer ������Ʈ�� ���� �ִ��� Ȯ���Ѵ�. 
            HitObjChecker(curHitObj, true);
        }
        // �ü��� ����ų� GazeObj�� �ƴ϶�� �ð��� �ʱ�ȭ 
        else
        {
            if (prevHitObj != null)
            {
                HitObjChecker(prevHitObj, false);
                prevHitObj = null;
            }
            curGazeTime = 0;
        }
        // �ü��� �ӹ� �ð��� 0�� �ִ� ���̷� �Ѵ�.
        curGazeTime = Mathf.Clamp(curGazeTime, 0, gazeChargeTime);
        // ui Image�� fillAmount�� ������Ʈ�Ѵ�. 
        gazeImg.fillAmount = curGazeTime / gazeChargeTime;

        isHitObj = false; // ��� ó���� ������ isHitObj�� false�� �Ѵ�. 
        curHitObj = null; // curHitObj ������ �����. 
    }

    // ��Ʈ�� ������Ʈ Ÿ�Ժ��� �۵� ����� �����Ѵ�. 
    void HitObjChecker(GameObject hitObj, bool isActive)
    {
        // hit�� ���� �÷��̾� ������Ʈ�� ���� �ִ��� Ȯ���Ѵ�. 
        if (hitObj.GetComponent<VideoPlayer>())
        {
            if (isActive)
            {
                hitObj.GetComponent<VideoFrame>().CheckVideoFrame(true);
            }
            else
            {
                hitObj.GetComponent<VideoFrame>().CheckVideoFrame(false);
            }
        }

        // ������ �ð��� �Ǹ� 360 ���Ǿ Ư�� Ŭ�� ��ȣ�� ������ �÷����Ѵ�. 
        if (gazeImg.fillAmount >= 1)
        {
            // ���� �÷��̾ ���� Mesh_Collider ������Ʈ�� �̸��� ���� ����/���� �������� ��� 
            if (hitObj.name.Contains("Right"))
            {
                vp360.SwapVideoClip(true); // ���� ���� 
            }
            else if (hitObj.name.Contains("Left"))
            {
                vp360.SwapVideoClip(false); // ���� ���� 
            }
            else
            {
                // 360 ���Ǿ Ư�� Ŭ�� ��ȣ�� ������ �÷����Ѵ�. 
                vp360.SetVideoPlay(hitObj.transform.GetSiblingIndex());
            }
            curGazeTime = 0;    // ���� �ð��� �ʱ�ȭ�� �ڵ尡 �ݺ��ؼ� �Ҹ��� ���� ����
        }
    }
}
