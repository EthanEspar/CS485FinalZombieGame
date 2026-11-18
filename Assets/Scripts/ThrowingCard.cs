using System;
using System.Collections.Generic;
using UnityEngine;

public class ThrowingCard : MonoBehaviour
{
    public enum Suit
    {
        Spades,
        Hearts,
        Clubs,
        Diamonds,
        None
    }

    public enum Rank
    {
        Joker = 0,
        Two = 2,
        Three = 3,
        Four = 4,
        Five = 5,
        Six = 6,
        Seven = 7,
        Eight = 8,
        Nine = 9,
        Ten = 10,
        Jack = 11,
        Queen = 12,
        King = 13,
        Ace = 14
    }

    [Serializable]
    public struct Card
    {
        public Suit suit;
        public Rank rank;

        public Card(Suit suit, Rank rank)
        {
            this.suit = suit;
            this.rank = rank;
        }

        public bool IsJoker => rank == Rank.Joker;
        public float BaseDamage => IsJoker ? 0f : (int)rank;
        public string CardName => IsJoker ? "Joker" : rank + " of " + suit;
    }

    [Header("Card Throwing")]
    [SerializeField] private CardProjectile cardPrefab;
    [SerializeField] private Transform throwPoint;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float throwForce = 28f;
    [SerializeField] private float throwCooldown = 0.20f;
    [SerializeField] private bool throwWithLeftClick = true;

    [Header("Card Artwork")]
    [SerializeField] private CardArtworkLibrary artworkLibrary;

    [Header("Damage Modifiers")]
    [SerializeField] private float additiveDamage;
    [SerializeField] private float multiplicativeDamage = 1f;

    [Header("Demo Options")]
    [SerializeField] private bool showDebugUI = true;
    [SerializeField] private bool allowDebugKeys = true;

    [Header("Card Testing")]
    [Tooltip("When enabled, every throw uses the selected test card without consuming the shuffled deck.")]
    [SerializeField] private bool forceSpecificCard;
    [SerializeField] private Suit testSuit = Suit.Spades;
    [SerializeField] private Rank testRank = Rank.Ace;

    private readonly List<Card> deck = new List<Card>(54);
    private int nextCardIndex;
    private float nextThrowTime;

    public int CardsRemaining => deck.Count - nextCardIndex;
    public float AdditiveDamage => additiveDamage;
    public float MultiplicativeDamage => multiplicativeDamage;

    public event Action<Card, int> OnCardThrown;
    public event Action OnDeckReshuffled;

    private void Awake()
    {
        if (artworkLibrary == null)
        {
            artworkLibrary = Resources.Load<CardArtworkLibrary>("Deck01Artwork");
        }

        CreateAndShuffleDeck();
    }

    private void Update()
    {
        if (throwWithLeftClick &&
            Cursor.lockState == CursorLockMode.Locked &&
            Input.GetMouseButtonDown(0))
        {
            ThrowNextCard();
        }

        if (!allowDebugKeys)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            AddDamage(5f);
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SetDamageMultiplier(multiplicativeDamage + 0.5f);
        }

        if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            ResetDamageModifiers();
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            EmptyDeckForTesting();
        }
    }

    public void Configure(CardProjectile newPrefab, Transform newThrowPoint, Camera newCamera)
    {
        cardPrefab = newPrefab;
        throwPoint = newThrowPoint;
        playerCamera = newCamera;
    }

    public bool ThrowNextCard()
    {
        if (Time.time < nextThrowTime)
        {
            return false;
        }

        if (throwPoint == null)
        {
            Debug.LogWarning("ThrowingCard needs a Throw Point.");
            return false;
        }

        if (CardsRemaining <= 0)
        {
            CreateAndShuffleDeck();
        }

        Card drawnCard;

        if (forceSpecificCard)
        {
            Suit selectedSuit = testRank == Rank.Joker
                ? Suit.None
                : testSuit == Suit.None
                    ? Suit.Spades
                    : testSuit;

            drawnCard = new Card(selectedSuit, testRank);
        }
        else
        {
            drawnCard = deck[nextCardIndex];
            nextCardIndex++;
        }

        float finalDamage = Mathf.Max(
            0f,
            (drawnCard.BaseDamage + additiveDamage) * multiplicativeDamage
        );

        Camera aimCamera = playerCamera != null ? playerCamera : Camera.main;
        Vector3 throwDirection = aimCamera != null
            ? aimCamera.transform.forward
            : throwPoint.forward;

        CardProjectile projectile = CreateProjectile(throwDirection);
        projectile.Initialize(
            drawnCard,
            finalDamage,
            throwDirection,
            throwForce,
            transform,
            artworkLibrary != null
                ? artworkLibrary.GetSprite(drawnCard.suit, drawnCard.rank)
                : null
        );

        nextThrowTime = Time.time + throwCooldown;
        OnCardThrown?.Invoke(drawnCard, CardsRemaining);

        Debug.Log(
            (forceSpecificCard ? "TEST: Threw " : "Threw ") + drawnCard.CardName +
            " | Damage: " + finalDamage.ToString("0.##") +
            " | Cards remaining: " + CardsRemaining
        );

        return true;
    }

    public bool TryGetNextCard(out Card card)
    {
        if (CardsRemaining > 0)
        {
            card = deck[nextCardIndex];
            return true;
        }

        card = default;
        return false;
    }

    public void SetAdditiveDamage(float amount)
    {
        additiveDamage = amount;
    }

    public void AddDamage(float amount)
    {
        additiveDamage += amount;
    }

    public void SetDamageMultiplier(float multiplier)
    {
        multiplicativeDamage = Mathf.Max(0f, multiplier);
    }

    public void ResetDamageModifiers()
    {
        additiveDamage = 0f;
        multiplicativeDamage = 1f;
    }

    public void EmptyDeckForTesting()
    {
        nextCardIndex = deck.Count;
        Debug.Log("Deck marked empty. The next throw will create and shuffle a new 54-card deck.");
    }

    private CardProjectile CreateProjectile(Vector3 direction)
    {
        if (cardPrefab != null)
        {
            CardProjectile projectile = Instantiate(
                cardPrefab,
                throwPoint.position,
                Quaternion.LookRotation(direction)
            );

            projectile.gameObject.SetActive(true);
            return projectile;
        }

        GameObject cardObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cardObject.name = "Thrown Card";
        cardObject.transform.SetPositionAndRotation(
            throwPoint.position,
            Quaternion.LookRotation(direction)
        );
        cardObject.transform.localScale = new Vector3(0.55f, 0.80f, 0.035f);

        Collider cardCollider = cardObject.GetComponent<Collider>();
        cardCollider.isTrigger = true;

        Rigidbody cardBody = cardObject.AddComponent<Rigidbody>();
        cardBody.useGravity = false;
        cardBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        return cardObject.AddComponent<CardProjectile>();
    }

    private void CreateAndShuffleDeck()
    {
        deck.Clear();

        Suit[] suits =
        {
            Suit.Spades,
            Suit.Hearts,
            Suit.Clubs,
            Suit.Diamonds
        };

        foreach (Suit suit in suits)
        {
            for (int rankValue = 2; rankValue <= 14; rankValue++)
            {
                deck.Add(new Card(suit, (Rank)rankValue));
            }
        }

        deck.Add(new Card(Suit.None, Rank.Joker));
        deck.Add(new Card(Suit.None, Rank.Joker));

        for (int i = deck.Count - 1; i > 0; i--)
        {
            int randomIndex = UnityEngine.Random.Range(0, i + 1);
            Card temporaryCard = deck[i];
            deck[i] = deck[randomIndex];
            deck[randomIndex] = temporaryCard;
        }

        nextCardIndex = 0;
        OnDeckReshuffled?.Invoke();
        Debug.Log("A new 54-card deck was shuffled.");
    }

    private void OnGUI()
    {
        if (!showDebugUI)
        {
            return;
        }

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.UpperLeft,
            fontSize = 16,
            padding = new RectOffset(14, 14, 12, 12)
        };

        string nextCardText;
        if (forceSpecificCard)
        {
            Suit selectedSuit = testRank == Rank.Joker
                ? Suit.None
                : testSuit == Suit.None
                    ? Suit.Spades
                    : testSuit;
            nextCardText = new Card(selectedSuit, testRank).CardName + " (TEST MODE)";
        }
        else if (TryGetNextCard(out Card nextCard))
        {
            nextCardText = nextCard.CardName;
        }
        else
        {
            nextCardText = "Deck empty - next throw reshuffles";
        }

        string text =
            "CARD WEAPON\n" +
            "Next card: " + nextCardText + "\n" +
            "Cards remaining: " + CardsRemaining + " / 54\n" +
            "Additive damage: +" + additiveDamage.ToString("0.##") + "\n" +
            "Damage multiplier: x" + multiplicativeDamage.ToString("0.##") + "\n\n" +
            "Left Click: Throw\n" +
            "1: Add +5 damage   2: Add x0.5\n" +
            "0: Reset modifiers   R: Empty deck test";

        GUI.Box(new Rect(18f, 18f, 370f, 205f), text, boxStyle);
    }
}
