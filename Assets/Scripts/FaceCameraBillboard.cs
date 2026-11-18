using UnityEngine;

public class FaceCameraBillboard : MonoBehaviour
{
    private Camera targetCamera;

    private void LateUpdate()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            return;
        }

        Vector3 direction = transform.position - targetCamera.transform.position;
        transform.rotation = Quaternion.LookRotation(direction);
    }
}
