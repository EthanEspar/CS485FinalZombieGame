using UnityEngine;

public class ZombieThrownProjectile : MonoBehaviour
{
    [SerializeField] private float lifetimeSeconds = 6f;
    [SerializeField] private float damage = 10f;

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

        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damage);
        }

        Destroy(gameObject);
    }
}
