using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(BoxCollider))]
public class CardProjectile : MonoBehaviour
{
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private float rotationSpeed = 720f;

    private Rigidbody cardRigidbody;
    private ThrowingCard.Card card;
    private float damage;
    private Transform owner;
    private Sprite artwork;
    private bool hasHit;

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
        Sprite cardArtwork)
    {
        card = newCard;
        damage = newDamage;
        owner = cardOwner;
        artwork = cardArtwork;
        hasHit = false;

        gameObject.name = card.CardName;
        ApplyCardAppearance();

        cardRigidbody.AddForce(
            direction.normalized * throwForce,
            ForceMode.Impulse
        );

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

    private void OnTriggerEnter(Collider other)
    {
        if (hasHit ||
            other.transform == owner ||
            other.transform.IsChildOf(owner))
        {
            return;
        }

        ICardDamageable target = FindDamageable(other);

        if (target != null)
        {
            hasHit = true;

            if (card.IsJoker)
            {
                float jokerDamage = target.CurrentHealth * 0.5f;
                target.TakeDamage(jokerDamage);
            }
            else
            {
                target.TakeDamage(damage);
            }
        }

        Destroy(gameObject);
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
                : redSuit
                    ? new Color(0.92f, 0.20f, 0.22f)
                    : Color.white;
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
            case ThrowingCard.Rank.Jack:
                return "J";
            case ThrowingCard.Rank.Queen:
                return "Q";
            case ThrowingCard.Rank.King:
                return "K";
            case ThrowingCard.Rank.Ace:
                return "A";
            default:
                return ((int)rank).ToString();
        }
    }
}

public interface ICardDamageable
{
    bool IsBoss { get; }
    float CurrentHealth { get; }
    void TakeDamage(float amount);
}
