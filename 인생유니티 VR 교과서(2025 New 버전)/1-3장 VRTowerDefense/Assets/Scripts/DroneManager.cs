using UnityEngine;

public class DroneManager : MonoBehaviour
{
    // ���� �ð��� ���� 
    public float minTime = 1;
    public float maxTime = 5;
    // ���� �ð� 
    float createTime;
    // ��� �ð� 
    float currentTime;
    // ����� ������ ��ġ 
    public Transform[] spawnPoints;
    //��� ���� 
    public GameObject droneFactory;

    void Start()
    {
        // ���� �ð��� ���� �������� ���� 
        createTime = Random.Range(minTime, maxTime);
    }

    void Update()
    {
        // 1. �ð��� �귯�� �Ѵ�. 
        currentTime += Time.deltaTime;
        // 2. ���� ��� �ð��� ���� �ð��� �ʰ��ߴٸ� 
        if (currentTime > createTime)
        {
            // 3. ��� ���� 
            GameObject drone = Instantiate(droneFactory);
            // 4. ��� ��ġ ���� 
            // �������� spawnPoints �� �ϳ��� �̴´�. 
            int index = Random.Range(0, spawnPoints.Length);
            // ����� ��ġ�� �������� ���� spawnPoint �� ��ġ�� �Ҵ� 
            drone.transform.position = spawnPoints[index].position;
            // 5. ��� �ð� �ʱ�ȭ 
            currentTime = 0;
            // 6. ���� �ð� ���Ҵ� 
            createTime = Random.Range(minTime, maxTime);
        }
    }
}
