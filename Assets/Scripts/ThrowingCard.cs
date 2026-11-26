using System;
using System.Collections;
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
        public bool IsFaceCard => rank == Rank.Jack || rank == Rank.Queen || rank == Rank.King;
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

    [Header("Throw Animation")]
    [SerializeField] private bool useThrowAnimation = true;
    [SerializeField] private FirstPersonHands firstPersonHands;
    [SerializeField] private float throwAnimationDuration = 0.30f;
    [SerializeField, Range(0.1f, 0.9f)] private float throwReleasePoint = 0.55f;
    [SerializeField] private Vector3 heldCardPosition = new Vector3(0.42f, -0.34f, 0.85f);
    [SerializeField] private Vector3 throwWindupOffset = new Vector3(0.14f, -0.10f, -0.14f);
    [SerializeField] private Vector3 throwReleaseOffset = new Vector3(-0.34f, 0.16f, 0.32f);
    [SerializeField] private Vector3 heldCardRotation = new Vector3(0f, 0f, -18f);
    [SerializeField] private float throwVisualSpin = 150f;
    [SerializeField] private float heldCardHeight = 0.28f;

    [Header("Shuffle / Reload Animation")]
    [SerializeField] private KeyCode shuffleKey = KeyCode.R;
    [SerializeField] private bool useShuffleAnimation = true;
    [SerializeField] private float shuffleAnimationDuration = 1.25f;
    [SerializeField] private int shufflePasses = 4;
    [SerializeField] private Vector3 shufflePosition = new Vector3(0f, -0.30f, 0.90f);

    [Header("Demo Options")]
    [SerializeField] private bool showGameHUD = true;
    [SerializeField] private bool showDebugUI = true;
    [SerializeField] private bool allowDebugKeys = true;

    [Header("Card Testing")]
    [Tooltip("When enabled, every throw uses the selected test card without consuming the shuffled deck.")]
    [SerializeField] private bool forceSpecificCard;
    [SerializeField] private Suit testSuit = Suit.Spades;
    [SerializeField] private Rank testRank = Rank.Ace;

    private readonly List<Card> deck = new List<Card>(54);
    private ModifierManager modifierManager;
    private PlayerHealth playerHealth;
    private int nextCardIndex;
    private float nextThrowTime;
    private Suit lastSuit = Suit.None;
    private int suitStreak;
    private Card currentCard;
    private bool hasCurrentCard;
    private Vector2 modifierScrollPosition;
    private bool isThrowAnimating;
    private bool isShuffling;
    private GameObject heldCardVisual;
    private SpriteRenderer heldCardRenderer;
    private readonly List<GameObject> shuffleCardVisuals = new List<GameObject>();

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

        playerHealth = GetComponent<PlayerHealth>();
        ResolveFirstPersonHands();

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

        if (Cursor.lockState == CursorLockMode.Locked &&
            Input.GetKeyDown(shuffleKey))
        {
            StartShuffle();
        }

        if (!allowDebugKeys)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1)) AddDamage(5f);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SetDamageMultiplier(multiplicativeDamage + 0.5f);
        if (Input.GetKeyDown(KeyCode.Alpha0)) ResetDamageModifiers();
        if (Input.GetKeyDown(KeyCode.T)) EmptyDeckForTesting();
    }

    public void Configure(CardProjectile newPrefab, Transform newThrowPoint, Camera newCamera)
    {
        cardPrefab = newPrefab;
        throwPoint = newThrowPoint;
        playerCamera = newCamera;
        ResolveFirstPersonHands();
    }

    public bool ThrowNextCard()
    {
        if (Time.time < nextThrowTime || isThrowAnimating || isShuffling)
        {
            return false;
        }

        if (throwPoint == null)
        {
            Debug.LogWarning("ThrowingCard needs a Throw Point.");
            return false;
        }

        ResolveModifierManager();

        if (!forceSpecificCard && CardsRemaining <= 0)
        {
            StartShuffle();
            return false;
        }

        if (useThrowAnimation)
        {
            StartCoroutine(ThrowAnimationRoutine());
        }
        else
        {
            PerformCardThrow();
        }

        return true;
    }

    public bool StartShuffle()
    {
        if (isThrowAnimating || isShuffling || Time.timeScale <= 0f)
        {
            return false;
        }

        if (useShuffleAnimation)
        {
            StartCoroutine(ShuffleAnimationRoutine());
        }
        else
        {
            CreateAndShuffleDeck();
        }

        return true;
    }

    private void PerformCardThrow()
    {
        ResolveModifierManager();
        int numberOfCards = 1 + (modifierManager != null ? modifierManager.ExtraCardsPerThrow : 0);
        if (!forceSpecificCard)
        {
            numberOfCards = Mathf.Min(numberOfCards, CardsRemaining);
        }
        Camera aimCamera = playerCamera != null ? playerCamera : Camera.main;
        Vector3 forward = aimCamera != null ? aimCamera.transform.forward : throwPoint.forward;

        for (int i = 0; i < numberOfCards; i++)
        {
            Card drawnCard = DrawCard();
            currentCard = drawnCard;
            hasCurrentCard = true;
            UpdateSuitStreak(drawnCard);

            float finalDamage = Mathf.Max(
                0f,
                (drawnCard.BaseDamage + additiveDamage) * multiplicativeDamage
            );

            if (!drawnCard.IsJoker && modifierManager != null)
            {
                finalDamage = modifierManager.ModifyNormalCardDamage(finalDamage);
                finalDamage *= modifierManager.MultiCardDamageMultiplier;

                if (modifierManager.UsesSuitStreak && suitStreak > 1)
                {
                    int streakBonusCards = Mathf.Min(
                        suitStreak,
                        modifierManager.MaximumSuitStreak
                    ) - 1;
                    finalDamage *= 1f +
                        streakBonusCards * modifierManager.SuitStreakDamagePerCard;
                }
            }

            // Keep multi-card throws close enough that both cards can hit the
            // same aimed target while still being visibly separate.
            float horizontalAngle = (i - (numberOfCards - 1) * 0.5f) * 2f;
            Vector3 direction = Quaternion.AngleAxis(horizontalAngle, Vector3.up) * forward;
            CardProjectile projectile = CreateProjectile(direction);
            float finalForce = modifierManager != null
                ? modifierManager.GetThrowForce(throwForce)
                : throwForce;

            projectile.Initialize(
                drawnCard,
                finalDamage,
                direction,
                finalForce,
                transform,
                artworkLibrary != null
                    ? artworkLibrary.GetSprite(drawnCard.suit, drawnCard.rank)
                    : null,
                modifierManager
            );

            OnCardThrown?.Invoke(drawnCard, CardsRemaining);
            Debug.Log(
                (forceSpecificCard ? "TEST: Threw " : "Threw ") + drawnCard.CardName +
                " | Damage: " + finalDamage.ToString("0.##") +
                " | Cards remaining: " + CardsRemaining
            );
        }

        nextThrowTime = Time.time + (modifierManager != null
            ? modifierManager.GetThrowCooldown(throwCooldown)
            : throwCooldown);
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

    public void SetAdditiveDamage(float amount) => additiveDamage = amount;
    public void AddDamage(float amount) => additiveDamage += amount;
    public void SetDamageMultiplier(float multiplier) => multiplicativeDamage = Mathf.Max(0f, multiplier);

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

    private IEnumerator ThrowAnimationRoutine()
    {
        isThrowAnimating = true;
        ResolveFirstPersonHands();

        if (firstPersonHands != null)
        {
            firstPersonHands.PlayThrow(throwAnimationDuration);
        }

        Card previewCard = GetPreviewCard();
        ShowHeldCard(previewCard);

        float duration = Mathf.Max(0.05f, throwAnimationDuration);
        float elapsed = 0f;
        bool released = false;
        Vector3 windupPosition = heldCardPosition + throwWindupOffset;
        Vector3 releasePosition = heldCardPosition + throwReleaseOffset;

        while (elapsed < duration)
        {
            float normalizedTime = Mathf.Clamp01(elapsed / duration);

            if (heldCardVisual != null)
            {
                if (normalizedTime < 0.35f)
                {
                    float windupTime = normalizedTime / 0.35f;
                    heldCardVisual.transform.localPosition = Vector3.Lerp(
                        heldCardPosition,
                        windupPosition,
                        SmoothStep(windupTime)
                    );
                }
                else
                {
                    float releaseTime = (normalizedTime - 0.35f) / 0.65f;
                    heldCardVisual.transform.localPosition = Vector3.Lerp(
                        windupPosition,
                        releasePosition,
                        SmoothStep(releaseTime)
                    );
                }

                heldCardVisual.transform.localRotation = Quaternion.Euler(
                    heldCardRotation + Vector3.forward * (throwVisualSpin * normalizedTime)
                );
            }

            if (!released && normalizedTime >= throwReleasePoint)
            {
                released = true;
                PerformCardThrow();
                HideHeldCard();
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!released)
        {
            PerformCardThrow();
        }

        HideHeldCard();
        isThrowAnimating = false;
    }

    private IEnumerator ShuffleAnimationRoutine()
    {
        isShuffling = true;
        HideHeldCard();
        ResolveFirstPersonHands();

        if (firstPersonHands != null)
        {
            firstPersonHands.PlayShuffle(shuffleAnimationDuration, shufflePasses);
        }

        Card previewCard = GetPreviewCard();
        EnsureShuffleVisuals(previewCard);

        float duration = Mathf.Max(0.15f, shuffleAnimationDuration);
        float elapsed = 0f;
        int passes = Mathf.Max(1, shufflePasses);

        while (elapsed < duration)
        {
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float phase = normalizedTime * passes * Mathf.PI * 2f;

            for (int i = 0; i < shuffleCardVisuals.Count; i++)
            {
                GameObject visual = shuffleCardVisuals[i];
                if (visual == null)
                {
                    continue;
                }

                float cardPhase = phase + i * Mathf.PI * 0.72f;
                float horizontal = Mathf.Sin(cardPhase) * 0.24f;
                float vertical = Mathf.Abs(Mathf.Cos(cardPhase)) * 0.07f;

                visual.transform.localPosition = shufflePosition + new Vector3(
                    horizontal,
                    vertical + i * 0.018f,
                    i * 0.012f
                );
                visual.transform.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    Mathf.Sin(cardPhase) * 28f + (i - 1) * 8f
                );
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        HideShuffleVisuals();
        CreateAndShuffleDeck();
        isShuffling = false;
    }

    private Card GetPreviewCard()
    {
        if (forceSpecificCard)
        {
            Suit selectedSuit = testRank == Rank.Joker
                ? Suit.None
                : testSuit == Suit.None ? Suit.Spades : testSuit;
            return new Card(selectedSuit, testRank);
        }

        if (TryGetNextCard(out Card previewCard))
        {
            return previewCard;
        }

        return new Card(Suit.None, Rank.Joker);
    }

    private void ShowHeldCard(Card previewCard)
    {
        EnsureHeldCardVisual();
        Sprite sprite = artworkLibrary != null
            ? artworkLibrary.GetSprite(previewCard.suit, previewCard.rank)
            : null;

        ApplyVisualSprite(heldCardVisual, heldCardRenderer, sprite);
        heldCardVisual.transform.localPosition = heldCardPosition;
        heldCardVisual.transform.localRotation = Quaternion.Euler(heldCardRotation);
        heldCardVisual.SetActive(true);
    }

    private void HideHeldCard()
    {
        if (heldCardVisual != null)
        {
            heldCardVisual.SetActive(false);
        }
    }

    private void EnsureHeldCardVisual()
    {
        if (heldCardVisual != null)
        {
            return;
        }

        heldCardVisual = CreateAnimationCardVisual("Held Card Animation", 1000);
        heldCardRenderer = heldCardVisual.GetComponent<SpriteRenderer>();
        heldCardVisual.SetActive(false);
    }

    private void EnsureShuffleVisuals(Card previewCard)
    {
        Sprite sprite = artworkLibrary != null
            ? artworkLibrary.GetSprite(previewCard.suit, previewCard.rank)
            : null;

        while (shuffleCardVisuals.Count < 3)
        {
            GameObject visual = CreateAnimationCardVisual(
                "Shuffle Card " + (shuffleCardVisuals.Count + 1),
                1001 + shuffleCardVisuals.Count
            );
            shuffleCardVisuals.Add(visual);
        }

        for (int i = 0; i < shuffleCardVisuals.Count; i++)
        {
            GameObject visual = shuffleCardVisuals[i];
            SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
            ApplyVisualSprite(visual, renderer, sprite);
            visual.transform.localPosition = shufflePosition + Vector3.right * ((i - 1) * 0.10f);
            visual.transform.localRotation = Quaternion.Euler(0f, 0f, (i - 1) * 8f);
            visual.SetActive(true);
        }
    }

    private void HideShuffleVisuals()
    {
        foreach (GameObject visual in shuffleCardVisuals)
        {
            if (visual != null)
            {
                visual.SetActive(false);
            }
        }
    }

    private GameObject CreateAnimationCardVisual(string visualName, int sortingOrder)
    {
        Camera animationCamera = playerCamera != null ? playerCamera : Camera.main;
        Transform parent = animationCamera != null
            ? animationCamera.transform
            : throwPoint;

        GameObject visual = new GameObject(visualName);
        visual.transform.SetParent(parent, false);

        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = sortingOrder;
        renderer.color = Color.white;
        return visual;
    }

    private void ApplyVisualSprite(
        GameObject visual,
        SpriteRenderer renderer,
        Sprite sprite)
    {
        renderer.sprite = sprite;

        if (sprite != null)
        {
            float scale = heldCardHeight / Mathf.Max(0.01f, sprite.bounds.size.y);
            visual.transform.localScale = Vector3.one * scale;
        }
        else
        {
            visual.transform.localScale = Vector3.one * heldCardHeight;
        }
    }

    private float SmoothStep(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private Card DrawCard()
    {
        if (forceSpecificCard)
        {
            Suit selectedSuit = testRank == Rank.Joker
                ? Suit.None
                : testSuit == Suit.None ? Suit.Spades : testSuit;
            return new Card(selectedSuit, testRank);
        }

        if (CardsRemaining <= 0)
        {
            CreateAndShuffleDeck();
        }

        Card card = deck[nextCardIndex];
        nextCardIndex++;
        return card;
    }

    private void UpdateSuitStreak(Card card)
    {
        if (card.IsJoker)
        {
            lastSuit = Suit.None;
            suitStreak = 0;
            return;
        }

        // Card Streak is deliberately deterministic so the reward can always
        // be tested. Every non-Joker continues the streak; Jokers reset it.
        suitStreak++;
        lastSuit = card.suit;
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
        Suit[] suits = { Suit.Spades, Suit.Hearts, Suit.Clubs, Suit.Diamonds };

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
            (deck[i], deck[randomIndex]) = (deck[randomIndex], deck[i]);
        }

        nextCardIndex = 0;
        ResolveModifierManager();
        modifierManager?.OnDeckReshuffled();
        OnDeckReshuffled?.Invoke();
        Debug.Log("A new 54-card deck was shuffled.");
    }

    private void ResolveModifierManager()
    {
        if (modifierManager == null)
        {
            modifierManager = ModifierManager.Instance;
        }
    }

    private void ResolveFirstPersonHands()
    {
        if (firstPersonHands == null)
        {
            firstPersonHands = GetComponent<FirstPersonHands>();
        }

        if (firstPersonHands == null)
        {
            firstPersonHands = gameObject.AddComponent<FirstPersonHands>();
        }

        Camera handsCamera = playerCamera != null ? playerCamera : Camera.main;
        firstPersonHands.Configure(handsCamera);
    }

    private void OnDisable()
    {
        HideHeldCard();
        HideShuffleVisuals();
        isThrowAnimating = false;
        isShuffling = false;
    }

    private void OnGUI()
    {
        if (!showGameHUD)
        {
            return;
        }

        ResolveModifierManager();
        if (playerHealth == null)
        {
            playerHealth = GetComponent<PlayerHealth>();
        }

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.UpperLeft,
            fontSize = 16,
            padding = new RectOffset(14, 14, 12, 12)
        };

        GUIStyle headerStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 19,
            fontStyle = FontStyle.Bold
        };

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.UpperLeft,
            fontSize = 15,
            wordWrap = true
        };

        string nextCardText;
        if (forceSpecificCard)
        {
            Suit selectedSuit = testRank == Rank.Joker
                ? Suit.None
                : testSuit == Suit.None ? Suit.Spades : testSuit;
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

        DrawPlayerAndCardPanel(nextCardText, boxStyle, headerStyle, labelStyle);
        DrawActiveModifierPanel(boxStyle, headerStyle, labelStyle);

        if (isShuffling)
        {
            GUIStyle shuffleStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 24,
                fontStyle = FontStyle.Bold
            };

            GUI.Box(
                new Rect(Screen.width * 0.5f - 120f, Screen.height - 105f, 240f, 52f),
                "SHUFFLING...",
                shuffleStyle
            );
        }
    }

    private void DrawPlayerAndCardPanel(
        string nextCardText,
        GUIStyle boxStyle,
        GUIStyle headerStyle,
        GUIStyle labelStyle)
    {
        Rect panel = new Rect(18f, 18f, 400f, showDebugUI ? 265f : 225f);
        GUI.Box(panel, string.Empty, boxStyle);
        GUI.Label(new Rect(panel.x + 14f, panel.y + 8f, 360f, 30f), "PLAYER & CARD", headerStyle);

        float health = playerHealth != null ? playerHealth.CurrentHealth : 0f;
        float maximumHealth = playerHealth != null ? playerHealth.MaximumHealth : 100f;
        float healthPercent = maximumHealth > 0f ? Mathf.Clamp01(health / maximumHealth) : 0f;

        GUI.Label(
            new Rect(panel.x + 14f, panel.y + 42f, 360f, 24f),
            "HP: " + health.ToString("0") + " / " + maximumHealth.ToString("0"),
            labelStyle
        );

        Rect healthBackground = new Rect(panel.x + 14f, panel.y + 68f, panel.width - 28f, 18f);
        GUI.Box(healthBackground, string.Empty);
        Color previousColor = GUI.color;
        GUI.color = Color.Lerp(new Color(0.85f, 0.15f, 0.15f), new Color(0.15f, 0.85f, 0.25f), healthPercent);
        GUI.DrawTexture(
            new Rect(healthBackground.x + 2f, healthBackground.y + 2f,
                (healthBackground.width - 4f) * healthPercent, healthBackground.height - 4f),
            Texture2D.whiteTexture
        );
        GUI.color = previousColor;

        string currentCardText = hasCurrentCard ? currentCard.CardName : "No card thrown yet";
        int currentDeckPosition = forceSpecificCard ? 0 : Mathf.Clamp(nextCardIndex, 0, 54);
        string deckPositionText = forceSpecificCard
            ? "Test card mode"
            : currentDeckPosition + " / 54";

        string cardDetails =
            "Current card: " + currentCardText + "\n" +
            "Next card: " + nextCardText + "\n" +
            "Deck position: " + deckPositionText + "\n" +
            "Cards remaining: " + CardsRemaining + " / 54\n" +
            "Damage: +" + additiveDamage.ToString("0.##") +
            " additive, x" + multiplicativeDamage.ToString("0.##") + " base multiplier";

        GUI.Label(
            new Rect(panel.x + 14f, panel.y + 94f, panel.width - 28f, 118f),
            cardDetails,
            labelStyle
        );

        if (showDebugUI)
        {
            GUI.Label(
                new Rect(panel.x + 14f, panel.y + 218f, panel.width - 28f, 38f),
                "Left Click: Throw   |   R: Shuffle / reload   |   T: Empty-deck test",
                labelStyle
            );
        }
    }

    private void DrawActiveModifierPanel(
        GUIStyle boxStyle,
        GUIStyle headerStyle,
        GUIStyle labelStyle)
    {
        float panelWidth = 400f;
        float panelHeight = Mathf.Max(250f, Mathf.Min(560f, Screen.height - 125f));
        Rect panel = new Rect(Screen.width - panelWidth - 18f, 105f, panelWidth, panelHeight);
        GUI.Box(panel, string.Empty, boxStyle);
        GUI.Label(
            new Rect(panel.x + 14f, panel.y + 8f, panel.width - 28f, 30f),
            "PICKED BUFFS / ACTIVE MODIFIERS",
            headerStyle
        );

        IReadOnlyList<ModifierData> active = modifierManager != null
            ? modifierManager.ActiveModifiers
            : null;
        int activeCount = active != null ? active.Count : 0;
        float contentHeight = Mathf.Max(
            panelHeight - 58f,
            70f + activeCount * 76f + 125f
        );

        Rect viewRect = new Rect(panel.x + 10f, panel.y + 44f, panel.width - 20f, panel.height - 54f);
        modifierScrollPosition = GUI.BeginScrollView(
            viewRect,
            modifierScrollPosition,
            new Rect(0f, 0f, viewRect.width - 20f, contentHeight)
        );

        float y = 0f;
        if (activeCount == 0)
        {
            GUI.Label(
                new Rect(6f, y, viewRect.width - 32f, 45f),
                "No buffs selected yet. Clear Round 1 to choose your first modifier.",
                labelStyle
            );
            y += 55f;
        }
        else
        {
            for (int i = 0; i < activeCount; i++)
            {
                ModifierData modifier = active[i];
                GUI.Box(new Rect(2f, y, viewRect.width - 28f, 68f), string.Empty);
                GUI.Label(
                    new Rect(10f, y + 5f, viewRect.width - 44f, 24f),
                    (i + 1) + ". " + modifier.displayName,
                    headerStyle
                );
                GUI.Label(
                    new Rect(10f, y + 30f, viewRect.width - 44f, 34f),
                    modifier.description,
                    labelStyle
                );
                y += 76f;
            }
        }

        GUI.Label(new Rect(6f, y, viewRect.width - 32f, 26f), "CURRENT SUIT POWERS", headerStyle);
        y += 28f;

        string suitText = modifierManager == null
            ? "Spades: Pierce\nHearts: Heal\nClubs: Stun and knockback\nDiamonds: Critical hit"
            :
                "Spades: " + SuitEffectLabel(modifierManager.GetSuitEffect(Suit.Spades)) + "\n" +
                "Hearts: " + SuitEffectLabel(modifierManager.GetSuitEffect(Suit.Hearts)) + "\n" +
                "Clubs: " + SuitEffectLabel(modifierManager.GetSuitEffect(Suit.Clubs)) + "\n" +
                "Diamonds: " + SuitEffectLabel(modifierManager.GetSuitEffect(Suit.Diamonds));

        GUI.Label(new Rect(8f, y, viewRect.width - 36f, 92f), suitText, labelStyle);
        GUI.EndScrollView();
    }

    private string SuitEffectLabel(SuitEffect effect)
    {
        switch (effect)
        {
            case SuitEffect.Pierce:
                return "Pierce";
            case SuitEffect.Heal:
                return "Heal player";
            case SuitEffect.StunAndKnockback:
                return "Stun and knockback";
            case SuitEffect.CriticalHit:
                return "Critical-hit chance";
            default:
                return effect.ToString();
        }
    }
}
