using UnityEngine;

public class Billboard : MonoBehaviour
{
    public Transform canvas;

    void Update()
    {
        canvas.forward = Camera.main.transform.forward;
    }
}
