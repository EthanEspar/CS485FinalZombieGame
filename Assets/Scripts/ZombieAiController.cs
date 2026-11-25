using UnityEngine;

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

    [Header("Walker and Sprinter Melee Damage")]
    [SerializeField] private float meleeAttackRange = 2.1f;
    [SerializeField] private float meleeAttackCooldown = 1f;
    [SerializeField] private float walkerDamage = 8f;
    [SerializeField] private float sprinterDamage = 12f;

    private SimpleFirstPersonController playerController;
    private PlayerHealth playerHealth;
    private float nextThrowTime;
    private float nextMeleeAttackTime;

    private void Awake()
    {
        ResolveTypeFromName();
    }

    private void Update()
    {
        ResolveTargetIfNeeded();
        if (target == null)
        {
            return;
        }

        // Check melee range before moving so zombies stop and attack nearby players.
        if (zombieType != ZombieType.Thrower && HandleMeleeAttack())
        {
            return;
        }

        float playerSpeed = GetPlayerSpeed();
        float enemySpeedMultiplier = ModifierManager.Instance != null
            ? ModifierManager.Instance.EnemySpeedMultiplier
            : 1f;
        playerSpeed *= enemySpeedMultiplier;

        if (zombieType == ZombieType.Thrower)
        {
            HandleThrower(playerSpeed);
            return;
        }

        float movementSpeed = zombieType == ZombieType.Walker
            ? playerSpeed * 0.5f
            : playerSpeed * 0.8f;
        MoveTowardsTarget(movementSpeed);
    }

    private bool HandleMeleeAttack()
    {
        ResolvePlayerHealth();

        Vector3 closestPlayerPoint = target.position;
        CharacterController playerCollider = playerController != null
            ? playerController.GetComponent<CharacterController>()
            : null;

        if (playerCollider != null)
        {
            closestPlayerPoint = playerCollider.ClosestPoint(transform.position);
        }

        Vector3 zombieCenter = transform.position + Vector3.up * 0.9f;
        float distanceToTarget = Vector3.Distance(zombieCenter, closestPlayerPoint);

        if (distanceToTarget > meleeAttackRange)
        {
            return false;
        }

        FaceTarget();

        if (Time.time < nextMeleeAttackTime)
        {
            return true;
        }

        if (playerHealth != null)
        {
            float damage = zombieType == ZombieType.Sprinter
                ? sprinterDamage
                : walkerDamage;
            playerHealth.TakeDamage(damage);
            Debug.Log(gameObject.name + " dealt " + damage + " melee damage.");
        }

        nextMeleeAttackTime = Time.time + meleeAttackCooldown;
        return true;
    }

    private void HandleThrower(float playerSpeed)
    {
        float distanceToTarget = Vector3.Distance(transform.position, target.position);

        if (distanceToTarget > throwRange)
        {
            MoveTowardsTarget(playerSpeed * 0.8f);
            return;
        }

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

            ResolvePlayerHealth();

            return;
        }

        playerController = FindAnyObjectByType<SimpleFirstPersonController>();
        if (playerController != null)
        {
            target = playerController.transform;
            ResolvePlayerHealth();
        }
    }

    private void ResolvePlayerHealth()
    {
        if (playerHealth != null)
        {
            return;
        }

        if (playerController != null)
        {
            playerHealth = playerController.GetComponent<PlayerHealth>();
        }

        if (playerHealth == null && target != null)
        {
            playerHealth = target.GetComponentInParent<PlayerHealth>();
        }

        if (playerHealth == null)
        {
            playerHealth = FindAnyObjectByType<PlayerHealth>();
        }

        // This fallback keeps melee damage functional in scenes that were
        // created before PlayerHealth was added to the project.
        if (playerHealth == null && playerController != null)
        {
            playerHealth = playerController.gameObject.AddComponent<PlayerHealth>();
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

    private void MoveTowardsTarget(float movementSpeed)
    {
        Vector3 direction = target.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector3 movement = direction.normalized * movementSpeed * Time.deltaTime;
        transform.position += movement;
        FaceTarget();
    }
}
