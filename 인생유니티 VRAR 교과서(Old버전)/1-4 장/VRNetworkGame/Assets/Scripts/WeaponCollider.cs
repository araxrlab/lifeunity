using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeaponCollider : MonoBehaviour
{
    public BoxCollider weaponCol;

    void Start()
    {
        DeActivateCollider();
    }
    
    public void ActivateCollider()
    {
        weaponCol.enabled = true;
    }

    public void DeActivateCollider()
    {
        weaponCol.enabled = false;
    }
}
