using UnityEngine;

public class WeaponCollider : MonoBehaviour
{
    public BoxCollider weaponCol;
    void Start()
    {
        // ������ �浹 ������ ��Ȱ��ȭ�Ѵ�. 
        DeactivateCollider();
    }
    // �ݶ��̴� Ȱ��ȭ �Լ� 
    public void ActivateCollider()
    {
        weaponCol.enabled = true;
    }

    // �ݶ��̴� ��Ȱ��ȭ �Լ� 
    public void DeactivateCollider()
    {
        weaponCol.enabled = false;
    }
}
