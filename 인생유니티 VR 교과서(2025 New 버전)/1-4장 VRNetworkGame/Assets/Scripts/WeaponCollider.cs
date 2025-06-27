using UnityEngine;

public class WeaponCollider : MonoBehaviour
{
    public BoxCollider weaponCol;
    void Start()
    {
        // 무기의 충돌 영역을 비활성화한다. 
        DeactivateCollider();
    }
    // 콜라이더 활성화 함수 
    public void ActivateCollider()
    {
        weaponCol.enabled = true;
    }

    // 콜라이더 비활성화 함수 
    public void DeactivateCollider()
    {
        weaponCol.enabled = false;
    }
}
