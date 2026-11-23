using UnityEngine;

public class ZombieThrownProjectile : MonoBehaviour
{
    [SerializeField] private float lifetimeSeconds = 6f;

    private Transform owner;

    public void Initialize(Transform newOwner)
    {
        owner = newOwner;
        Destroy(gameObject, lifetimeSeconds);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (owner != null &&
            (other.transform == owner || other.transform.IsChildOf(owner)))
        {
            return;
        }

        Destroy(gameObject);
    }
}
