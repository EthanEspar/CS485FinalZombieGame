using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(BoxCollider))]
public class CardProjectile : MonoBehaviour
{
    [SerializeField] private float lifetime = 8f;
    [SerializeField] private float rotationSpeed = 720f;

    private readonly HashSet<ICardDamageable> hitTargets =
        new HashSet<ICardDamageable>();
    private Rigidbody cardRigidbody;
    private ThrowingCard.Card card;
    private float damage;
    private Transform owner;
    private Sprite artwork;
    private ModifierManager modifierManager;
    private PlayerHealth ownerHealth;
    private int pierceRemaining;
    private int ricochetRemaining;
    private float launchTime;
    private bool returning;
    private bool hasExploded;

    private void Awake()
    {
        cardRigidbody = GetComponent<Rigidbody>();
    }

    public void Initialize(
        ThrowingCard.Card newCard,
        float newDamage,
        Vector3 direction,
        float throwForce,
        Transform cardOwner,
        Sprite cardArtwork,
        ModifierManager newModifierManager = null)
    {
        card = newCard;
        damage = newDamage;
        owner = cardOwner;
        artwork = cardArtwork;
        modifierManager = newModifierManager != null
            ? newModifierManager
            : ModifierManager.Instance;
        ownerHealth = owner != null ? owner.GetComponent<PlayerHealth>() : null;
        pierceRemaining = modifierManager != null
            ? modifierManager.GetPierceCount(card.suit)
            : 0;
        ricochetRemaining = modifierManager != null
            ? modifierManager.RicochetCount
            : 0;
        launchTime = Time.time;
        returning = false;
        hasExploded = false;
        hitTargets.Clear();

        gameObject.name = card.CardName;
        ApplyCardAppearance();
        cardRigidbody.AddForce(direction.normalized * throwForce, ForceMode.Impulse);
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        transform.Rotate(
            Vector3.forward,
            rotationSpeed * Time.deltaTime,
            Space.Self
        );
    }

    private void FixedUpdate()
    {
        if (modifierManager == null || cardRigidbody == null)
        {
            return;
        }

        if (modifierManager.UsesReturningCards &&
            !returning &&
            Time.time >= launchTime + modifierManager.ReturnDelay)
        {
            returning = true;
        }

        Transform target = null;
        if (returning)
        {
            target = owner;
        }
        else if (modifierManager.UsesHoming)
        {
            ZombieHealth zombie = FindNearestZombie(modifierManager.HomingRange);
            target = zombie != null ? zombie.transform : null;
        }

        if (target == null)
        {
            return;
        }

        Vector3 targetPosition = target.position + (returning ? Vector3.up * 0.8f : Vector3.up * 0.6f);
        Vector3 desiredDirection = (targetPosition - transform.position).normalized;
        float speed = Mathf.Max(4f, cardRigidbody.linearVelocity.magnitude);
        Vector3 desiredVelocity = desiredDirection * speed;
        cardRigidbody.linearVelocity = Vector3.Lerp(
            cardRigidbody.linearVelocity,
            desiredVelocity,
            modifierManager.HomingStrength * Time.fixedDeltaTime
        );

        if (cardRigidbody.linearVelocity.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(cardRigidbody.linearVelocity.normalized);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        bool hitOwner = owner != null &&
            (other.transform == owner || other.transform.IsChildOf(owner));

        if (hitOwner)
        {
            if (returning)
            {
                Destroy(gameObject);
            }

            return;
        }

        ICardDamageable target = FindDamageable(other);
        if (target == null)
        {
            if (!returning)
            {
                Destroy(gameObject);
            }
            return;
        }

        MonoBehaviour targetBehaviour = target as MonoBehaviour;
        if (!hitTargets.Add(target))
        {
            return;
        }

        ApplyDamageAndSuitEffect(target, targetBehaviour);

        if (!hasExploded &&
            card.IsFaceCard &&
            modifierManager != null &&
            modifierManager.UsesExplosiveFaceCards)
        {
            hasExploded = true;
            Explode(target);
        }

        if (ricochetRemaining > 0)
        {
            ZombieHealth nextZombie = FindNearestZombie(modifierManager.HomingRange);
            if (nextZombie != null)
            {
                ricochetRemaining--;
                RedirectToward(nextZombie.transform);
                return;
            }
        }

        if (pierceRemaining > 0)
        {
            pierceRemaining--;
            return;
        }

        if (modifierManager != null && modifierManager.UsesReturningCards)
        {
            returning = true;
            return;
        }

        Destroy(gameObject);
    }

    private void ApplyDamageAndSuitEffect(
        ICardDamageable target,
        MonoBehaviour targetBehaviour)
    {
        float appliedDamage;

        if (card.IsJoker)
        {
            float fraction = modifierManager != null
                ? modifierManager.JokerHealthFraction
                : 0.5f;
            appliedDamage = target.CurrentHealth * fraction;
        }
        else
        {
            appliedDamage = damage;
            if (modifierManager != null && modifierManager.RollCritical(card.suit))
            {
                appliedDamage *= modifierManager.CriticalDamageMultiplier;
                Debug.Log(card.CardName + " landed a critical hit!");
            }
        }

        target.TakeDamage(appliedDamage);

        if (card.IsJoker || modifierManager == null)
        {
            return;
        }

        SuitEffect effect = modifierManager.GetSuitEffect(card.suit);
        if (effect == SuitEffect.Heal)
        {
            ownerHealth?.Heal(modifierManager.HeartHealing);
        }
        else if (effect == SuitEffect.StunAndKnockback)
        {
            ZombieHealth zombieHealth = targetBehaviour as ZombieHealth;
            if (zombieHealth != null)
            {
                zombieHealth.ApplyStun(modifierManager.ClubStunSeconds);
                Vector3 direction = cardRigidbody.linearVelocity.sqrMagnitude > 0.01f
                    ? cardRigidbody.linearVelocity.normalized
                    : transform.forward;
                zombieHealth.ApplyKnockback(direction, modifierManager.ClubKnockback);
            }
        }
    }

    private void Explode(ICardDamageable primaryTarget)
    {
        Collider[] colliders = Physics.OverlapSphere(
            transform.position,
            modifierManager.ExplosionRadius
        );
        HashSet<ICardDamageable> explosionTargets =
            new HashSet<ICardDamageable>();

        foreach (Collider nearby in colliders)
        {
            ICardDamageable damageable = FindDamageable(nearby);
            MonoBehaviour behaviour = damageable as MonoBehaviour;
            if (damageable == null || behaviour == null)
            {
                continue;
            }

            if (damageable == primaryTarget || !explosionTargets.Add(damageable))
            {
                continue;
            }

            damageable.TakeDamage(damage * modifierManager.ExplosionDamageFraction);
        }
    }

    private ZombieHealth FindNearestZombie(float range)
    {
        ZombieHealth nearest = null;
        float nearestDistanceSquared = range * range;

        foreach (ZombieHealth zombie in ZombieHealth.ActiveZombies)
        {
            if (zombie == null || hitTargets.Contains(zombie))
            {
                continue;
            }

            float distanceSquared = (zombie.transform.position - transform.position).sqrMagnitude;
            if (distanceSquared < nearestDistanceSquared)
            {
                nearestDistanceSquared = distanceSquared;
                nearest = zombie;
            }
        }

        return nearest;
    }

    private void RedirectToward(Transform target)
    {
        float speed = Mathf.Max(4f, cardRigidbody.linearVelocity.magnitude);
        Vector3 direction = (target.position + Vector3.up * 0.6f - transform.position).normalized;
        cardRigidbody.linearVelocity = direction * speed;
        transform.rotation = Quaternion.LookRotation(direction);
    }

    private ICardDamageable FindDamageable(Collider other)
    {
        MonoBehaviour[] components = other.GetComponentsInParent<MonoBehaviour>();
        foreach (MonoBehaviour component in components)
        {
            if (component is ICardDamageable damageable)
            {
                return damageable;
            }
        }

        return null;
    }

    private void ApplyCardAppearance()
    {
        if (artwork != null)
        {
            CreateArtworkVisual();
            return;
        }

        Renderer cardRenderer = GetComponent<Renderer>();
        bool redSuit = card.suit == ThrowingCard.Suit.Hearts ||
                       card.suit == ThrowingCard.Suit.Diamonds;

        if (cardRenderer != null)
        {
            cardRenderer.material.color = card.IsJoker
                ? new Color(1f, 0.82f, 0.18f)
                : redSuit ? new Color(0.92f, 0.20f, 0.22f) : Color.white;
        }

        GameObject labelObject = new GameObject("Card Label");
        labelObject.transform.SetParent(transform, false);
        labelObject.transform.localPosition = new Vector3(0f, 0f, -0.56f);
        labelObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        TextMesh textMesh = labelObject.AddComponent<TextMesh>();
        textMesh.text = card.IsJoker ? "JOKER" : RankLabel(card.rank);
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.characterSize = 0.20f;
        textMesh.fontSize = 60;
        textMesh.color = redSuit ? Color.white : Color.black;
    }

    private void CreateArtworkVisual()
    {
        Renderer rootRenderer = GetComponent<Renderer>();
        if (rootRenderer != null)
        {
            rootRenderer.enabled = false;
        }

        GameObject artworkObject = new GameObject("Card Artwork");
        artworkObject.transform.SetParent(transform, false);
        artworkObject.transform.localPosition = new Vector3(0f, 0f, -0.56f);

        SpriteRenderer spriteRenderer = artworkObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = artwork;
        spriteRenderer.color = Color.white;

        Vector2 spriteSize = artwork.bounds.size;
        artworkObject.transform.localScale = new Vector3(
            1f / Mathf.Max(0.01f, spriteSize.x),
            1f / Mathf.Max(0.01f, spriteSize.y),
            1f
        );
    }

    private string RankLabel(ThrowingCard.Rank rank)
    {
        switch (rank)
        {
            case ThrowingCard.Rank.Jack: return "J";
            case ThrowingCard.Rank.Queen: return "Q";
            case ThrowingCard.Rank.King: return "K";
            case ThrowingCard.Rank.Ace: return "A";
            default: return ((int)rank).ToString();
        }
    }
}

public interface ICardDamageable
{
    bool IsBoss { get; }
    float CurrentHealth { get; }
    void TakeDamage(float amount);
}
