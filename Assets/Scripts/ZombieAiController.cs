using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class ZombieAiController : MonoBehaviour
{
    private enum ZombieType
    {
        Walker,
        Sprinter,
        Thrower
    }

    [Header("AI")]
    [SerializeField] private ZombieType zombieType = ZombieType.Walker;
    [SerializeField] private Transform target;
    [SerializeField] private float fallbackPlayerSpeed = 6f;

    [Header("Thrower")]
    [SerializeField] private float throwRange = 8f;
    [SerializeField] private float throwCooldown = 1.5f;
    [SerializeField] private GameObject projectilePrefab;

    private NavMeshAgent agent;
    private SimpleFirstPersonController playerController;
    private float nextThrowTime;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        ResolveTypeFromName();
    }

    private void Update()
    {
        ResolveTargetIfNeeded();
        if (target == null || !agent.isOnNavMesh)
        {
            return;
        }

        float playerSpeed = GetPlayerSpeed();

        if (zombieType == ZombieType.Thrower)
        {
            HandleThrower(playerSpeed);
            return;
        }

        agent.isStopped = false;
        agent.speed = zombieType == ZombieType.Walker
            ? playerSpeed * 0.5f
            : playerSpeed * 0.8f;
        agent.SetDestination(target.position);
    }

    private void HandleThrower(float playerSpeed)
    {
        float distanceToTarget = Vector3.Distance(transform.position, target.position);

        if (distanceToTarget > throwRange)
        {
            agent.isStopped = false;
            agent.speed = playerSpeed * 0.8f;
            agent.SetDestination(target.position);
            return;
        }

        agent.isStopped = true;
        FaceTarget();

        if (Time.time >= nextThrowTime)
        {
            ThrowProjectile(playerSpeed * 2f);
            nextThrowTime = Time.time + throwCooldown;
        }
    }

    private void ThrowProjectile(float speed)
    {
        Vector3 spawnPosition = transform.position + Vector3.up * 1.2f + transform.forward * 0.8f;
        GameObject spawnedProjectile = projectilePrefab != null
            ? Instantiate(projectilePrefab, spawnPosition, Quaternion.identity)
            : CreateFallbackProjectile(spawnPosition);

        Rigidbody projectileRigidbody = spawnedProjectile.GetComponent<Rigidbody>();
        if (projectileRigidbody == null)
        {
            projectileRigidbody = spawnedProjectile.AddComponent<Rigidbody>();
        }

        projectileRigidbody.useGravity = false;
        projectileRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        Vector3 direction = (target.position + Vector3.up * 0.9f - spawnPosition).normalized;
        projectileRigidbody.linearVelocity = direction * speed;

        ZombieThrownProjectile projectile = spawnedProjectile.GetComponent<ZombieThrownProjectile>();
        if (projectile == null)
        {
            projectile = spawnedProjectile.AddComponent<ZombieThrownProjectile>();
        }

        projectile.Initialize(transform);
    }

    private GameObject CreateFallbackProjectile(Vector3 spawnPosition)
    {
        GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectile.transform.position = spawnPosition;
        projectile.transform.localScale = Vector3.one * 0.35f;
        projectile.name = "Zombie Projectile";

        Collider projectileCollider = projectile.GetComponent<Collider>();
        if (projectileCollider != null)
        {
            projectileCollider.isTrigger = true;
        }

        Renderer projectileRenderer = projectile.GetComponent<Renderer>();
        if (projectileRenderer != null)
        {
            projectileRenderer.material.color = new Color(0.25f, 0.85f, 0.25f);
        }

        return projectile;
    }

    private void ResolveTargetIfNeeded()
    {
        if (target != null)
        {
            if (playerController == null)
            {
                playerController = target.GetComponent<SimpleFirstPersonController>();
            }

            return;
        }

        playerController = FindObjectOfType<SimpleFirstPersonController>();
        if (playerController != null)
        {
            target = playerController.transform;
        }
    }

    private float GetPlayerSpeed()
    {
        if (playerController != null)
        {
            return Mathf.Max(0.1f, playerController.MovementSpeed);
        }

        return fallbackPlayerSpeed;
    }

    private void ResolveTypeFromName()
    {
        string nameLower = gameObject.name.ToLowerInvariant();

        if (nameLower.Contains("thrower"))
        {
            zombieType = ZombieType.Thrower;
        }
        else if (nameLower.Contains("sprinter"))
        {
            zombieType = ZombieType.Sprinter;
        }
        else if (nameLower.Contains("walker"))
        {
            zombieType = ZombieType.Walker;
        }
    }

    private void FaceTarget()
    {
        Vector3 lookDirection = target.position - transform.position;
        lookDirection.y = 0f;

        if (lookDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(lookDirection.normalized),
            Time.deltaTime * 8f);
    }
}
