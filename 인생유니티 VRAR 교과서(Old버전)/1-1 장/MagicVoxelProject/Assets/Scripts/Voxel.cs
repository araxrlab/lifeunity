using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// 1. ������ ������ �������� ���ư��� ��� �Ѵ�.
// �ʿ� �Ӽ�: ���ư� �ӵ�
// 2. ���� �ð��� ������ ������ �����ϰ� �ʹ�.
// �ʿ� �Ӽ�: ������ ������ �ð�, ��� �ð�
public class Voxel : MonoBehaviour
{
    // 1. ������ ���ư� �ӵ� ���ϱ�
    public float speed = 5;
    // ������ ������ �ð�
    public float destoryTime = 3.0f;
    // ��� �ð�
    float currentTime;

    void OnEnable()
    {
        currentTime = 0;
        // 2. ������ ������ ã�´�.
        Vector3 direction = Random.insideUnitSphere;
        // 3. ������ �������� ���ư��� �ӵ��� �ش�.
        Rigidbody rb = gameObject.GetComponent<Rigidbody>();
        rb.velocity = direction * speed;
    }

    void Update()
    {
        // ���� �ð��� ������ ������ �����ϰ� �ʹ�.
        // 1. �ð��� �귯�� �Ѵ�.
        currentTime += Time.deltaTime;
        // 2. ���� �ð��� �����ϱ�.
        // ���� ��� �ð��� ���� �ð��� �ʰ��ߴٸ�
        if (currentTime > destoryTime)
        {
            // 3. Voxel�� ��Ȱ��ȭ��Ų��.
            gameObject.SetActive(false);
            // 4. ������Ʈ Ǯ�� �ٽ� �־��ش�.
            VoxelMaker.voxelPool.Add(gameObject);
        }
    }
}
