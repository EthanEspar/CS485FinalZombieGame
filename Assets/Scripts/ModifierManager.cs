using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-150)]
public class ModifierManager : MonoBehaviour
{
    public static ModifierManager Instance { get; private set; }

    [Header("Base Suit Effects")]
    [SerializeField] private float heartHealing = 3f;
    [SerializeField] private float clubStunSeconds = 1f;
    [SerializeField] private float clubKnockback = 2.5f;
    [SerializeField, Range(0f, 1f)] private float diamondCriticalChance = 0.20f;
    [SerializeField] private float criticalDamageMultiplier = 2f;
    [SerializeField] private int spadePierceCount = 1;

    [Header("Mechanic Settings")]
    [SerializeField] private float homingRange = 40f;
    [SerializeField] private float homingStrength = 20f;
    [SerializeField] private float explosionRadius = 4.5f;
    [SerializeField] private float explosionDamageFraction = 0.75f;
    [SerializeField] private float returnDelay = 0.45f;
    [SerializeField] private int maximumSuitStreak = 6;
    [SerializeField] private float suitStreakDamagePerCard = 0.10f;
    [SerializeField] private int cursedThrowInterval = 4;
    [SerializeField] private float cursedCooldownPenalty = 1.8f;

    [Header("Available Round Rewards")]
    [SerializeField] private List<ModifierData> modifierPool = new List<ModifierData>();

    private readonly HashSet<ModifierType> selectedUniqueModifiers =
        new HashSet<ModifierType>();

    private readonly List<ModifierData> activeModifiers =
        new List<ModifierData>();

    private readonly Dictionary<ThrowingCard.Suit, SuitEffect> suitEffects =
        new Dictionary<ThrowingCard.Suit, SuitEffect>();

    private SimpleFirstPersonController playerController;
    private int cursedThrowCounter;

    public float DamageMultiplier { get; private set; } = 1f;
    public float ThrowCooldownMultiplier { get; private set; } = 1f;
    public float ThrowForceMultiplier { get; private set; } = 1f;
    public float EnemySpeedMultiplier { get; private set; } = 1f;
    public float KnockbackMultiplier { get; private set; } = 1f;
    public float BonusCriticalChance { get; private set; }
    public bool UsesWildDamage { get; private set; }
    public bool UsesHoming { get; private set; }
    public bool UsesDoubleThrow { get; private set; }
    public bool UsesGlobalPiercing { get; private set; }
    public bool UsesRicochet { get; private set; }
    public bool UsesExplosiveFaceCards { get; private set; }
    public bool UsesReturningCards { get; private set; }
    public bool UsesSuitStreak { get; private set; }
    public bool UsesChaosDeck { get; private set; }
    public bool UsesCursedDeck { get; private set; }
    public bool UsesJokersBargain { get; private set; }

    public float HeartHealing => heartHealing;
    public float ClubStunSeconds => clubStunSeconds;
    public float ClubKnockback => clubKnockback * KnockbackMultiplier;
    public float CriticalDamageMultiplier => criticalDamageMultiplier;
    public float HomingRange => homingRange;
    public float HomingStrength => homingStrength;
    public float ExplosionRadius => explosionRadius;
    public float ExplosionDamageFraction => explosionDamageFraction;
    public float ReturnDelay => returnDelay;
    public float JokerHealthFraction => UsesJokersBargain ? 0.75f : 0.5f;
    public int ExtraCardsPerThrow => UsesDoubleThrow ? 1 : 0;
    public int GlobalPierceCount => UsesGlobalPiercing ? 2 : 0;
    public int RicochetCount => UsesRicochet ? 3 : 0;
    public float MultiCardDamageMultiplier => UsesDoubleThrow ? 0.70f : 1f;
    public float SuitStreakDamagePerCard => suitStreakDamagePerCard;
    public int MaximumSuitStreak => maximumSuitStreak;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureDefaultModifiers();
        ResetSuitEffects();
        ResolvePlayerController();
    }

    public IReadOnlyList<ModifierData> ModifierPool => modifierPool;
    public IReadOnlyList<ModifierData> ActiveModifiers => activeModifiers;

    public List<ModifierData> GetRandomChoices(int count)
    {
        List<ModifierData> candidates = new List<ModifierData>();

        foreach (ModifierData modifier in modifierPool)
        {
            if (modifier == null)
            {
                continue;
            }

            if (modifier.canStack || !selectedUniqueModifiers.Contains(modifier.type))
            {
                candidates.Add(modifier);
            }
        }

        List<ModifierData> choices = new List<ModifierData>();
        while (choices.Count < count && candidates.Count > 0)
        {
            int index = Random.Range(0, candidates.Count);
            choices.Add(candidates[index]);
            candidates.RemoveAt(index);
        }

        return choices;
    }

    public void ApplyModifier(ModifierData modifier)
    {
        if (modifier == null)
        {
            return;
        }

        if (!modifier.canStack)
        {
            selectedUniqueModifiers.Add(modifier.type);
        }

        activeModifiers.Add(modifier);

        ResolvePlayerController();

        switch (modifier.type)
        {
            case ModifierType.HeavyHand:
                DamageMultiplier *= 1.50f;
                ApplyPlayerSpeedMultiplier(0.75f);
                break;

            case ModifierType.RapidDeal:
                ThrowCooldownMultiplier *= 0.60f;
                DamageMultiplier *= 0.75f;
                break;

            case ModifierType.HeavyCards:
                DamageMultiplier *= 1.40f;
                ThrowForceMultiplier *= 0.80f;
                KnockbackMultiplier *= 1.75f;
                break;

            case ModifierType.HighVelocity:
                ThrowForceMultiplier *= 1.50f;
                ThrowCooldownMultiplier *= 1.25f;
                break;

            case ModifierType.GlassCannon:
                DamageMultiplier *= 2f;
                EnemySpeedMultiplier *= 1.25f;
                break;

            case ModifierType.CarefulAim:
                BonusCriticalChance += 0.50f;
                ThrowCooldownMultiplier *= 1.20f;
                break;

            case ModifierType.WildDamage:
                UsesWildDamage = true;
                break;

            case ModifierType.HomingCards:
                UsesHoming = true;
                DamageMultiplier *= 0.90f;
                break;

            case ModifierType.DoubleThrow:
                UsesDoubleThrow = true;
                break;

            case ModifierType.PiercingCards:
                UsesGlobalPiercing = true;
                DamageMultiplier *= 0.90f;
                break;

            case ModifierType.Ricochet:
                UsesRicochet = true;
                break;

            case ModifierType.ExplosiveFaceCards:
                UsesExplosiveFaceCards = true;
                ThrowCooldownMultiplier *= 1.15f;
                break;

            case ModifierType.ReturningCards:
                UsesReturningCards = true;
                break;

            case ModifierType.SuitStreak:
                UsesSuitStreak = true;
                break;

            case ModifierType.ChaosDeck:
                UsesChaosDeck = true;
                ShuffleSuitEffects();
                break;

            case ModifierType.CursedDeck:
                UsesCursedDeck = true;
                DamageMultiplier *= 1.40f;
                break;

            case ModifierType.JokersBargain:
                UsesJokersBargain = true;
                break;
        }

        Debug.Log("Selected modifier: " + modifier.displayName);
    }

    public float ModifyNormalCardDamage(float damage)
    {
        float result = damage * DamageMultiplier;

        if (UsesJokersBargain)
        {
            result *= 0.85f;
        }

        if (UsesWildDamage)
        {
            float wildMultiplier = Random.Range(0.50f, 2.25f);
            result *= wildMultiplier;
            Debug.Log("Wild Damage multiplier: x" + wildMultiplier.ToString("0.00"));
        }

        return Mathf.Max(0f, result);
    }

    public float GetThrowCooldown(float baseCooldown)
    {
        float cooldown = baseCooldown * ThrowCooldownMultiplier;

        if (UsesCursedDeck)
        {
            cursedThrowCounter++;
            int interval = Mathf.Max(2, cursedThrowInterval);
            if (cursedThrowCounter % interval == 0)
            {
                cooldown *= cursedCooldownPenalty;
                Debug.Log("Cursed Deck slowed this throw.");
            }
        }

        return Mathf.Max(0.03f, cooldown);
    }

    public float GetThrowForce(float baseForce)
    {
        return Mathf.Max(1f, baseForce * ThrowForceMultiplier);
    }

    public bool RollCritical(ThrowingCard.Suit suit)
    {
        float chance = BonusCriticalChance;

        if (GetSuitEffect(suit) == SuitEffect.CriticalHit)
        {
            chance += diamondCriticalChance;
        }

        return Random.value <= Mathf.Clamp01(chance);
    }

    public int GetPierceCount(ThrowingCard.Suit suit)
    {
        int count = GlobalPierceCount;

        if (GetSuitEffect(suit) == SuitEffect.Pierce)
        {
            count += spadePierceCount;
        }

        return count;
    }

    public SuitEffect GetSuitEffect(ThrowingCard.Suit suit)
    {
        if (suitEffects.TryGetValue(suit, out SuitEffect effect))
        {
            return effect;
        }

        return SuitEffect.Pierce;
    }

    public void OnDeckReshuffled()
    {
        if (UsesChaosDeck)
        {
            ShuffleSuitEffects();
            Debug.Log("Chaos Deck shuffled the suit abilities.");
        }
    }

    public void OnRoundStarted()
    {
        if (UsesChaosDeck)
        {
            ShuffleSuitEffects();
            Debug.Log("Chaos Deck reassigned the suit powers for this round.");
        }
    }

    private void ApplyPlayerSpeedMultiplier(float multiplier)
    {
        if (playerController != null)
        {
            playerController.MultiplyExternalSpeed(multiplier);
        }
    }

    private void ResolvePlayerController()
    {
        if (playerController == null)
        {
            playerController = FindAnyObjectByType<SimpleFirstPersonController>();
        }
    }

    private void ResetSuitEffects()
    {
        suitEffects[ThrowingCard.Suit.Spades] = SuitEffect.Pierce;
        suitEffects[ThrowingCard.Suit.Hearts] = SuitEffect.Heal;
        suitEffects[ThrowingCard.Suit.Clubs] = SuitEffect.StunAndKnockback;
        suitEffects[ThrowingCard.Suit.Diamonds] = SuitEffect.CriticalHit;
    }

    private void ShuffleSuitEffects()
    {
        List<SuitEffect> effects = new List<SuitEffect>
        {
            SuitEffect.Pierce,
            SuitEffect.Heal,
            SuitEffect.StunAndKnockback,
            SuitEffect.CriticalHit
        };

        for (int i = effects.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            (effects[i], effects[randomIndex]) = (effects[randomIndex], effects[i]);
        }

        suitEffects[ThrowingCard.Suit.Spades] = effects[0];
        suitEffects[ThrowingCard.Suit.Hearts] = effects[1];
        suitEffects[ThrowingCard.Suit.Clubs] = effects[2];
        suitEffects[ThrowingCard.Suit.Diamonds] = effects[3];
    }

    private void EnsureDefaultModifiers()
    {
        if (modifierPool != null && modifierPool.Count > 0)
        {
            return;
        }

        modifierPool = new List<ModifierData>
        {
            new ModifierData(ModifierType.HeavyHand, "Heavy Hand", "+50% damage, but -25% movement speed.", true),
            new ModifierData(ModifierType.RapidDeal, "Rapid Deal", "Throw 40% faster, but deal 25% less damage.", true),
            new ModifierData(ModifierType.HeavyCards, "Heavy Cards", "+40% damage and strong knockback, but cards travel 20% slower.", true),
            new ModifierData(ModifierType.HighVelocity, "High Velocity", "Cards travel 50% faster, but cooldown is 25% longer.", true),
            new ModifierData(ModifierType.GlassCannon, "Glass Cannon", "Double damage, but zombies move 25% faster.", true),
            new ModifierData(ModifierType.CarefulAim, "Careful Aim", "+50% critical chance, but cooldown is 20% longer.", true),
            new ModifierData(ModifierType.WildDamage, "Wild Damage", "Every normal card deals between 50% and 225% damage.", false),
            new ModifierData(ModifierType.HomingCards, "Homing Cards", "Cards strongly track zombies across the arena, but deal 10% less damage.", false),
            new ModifierData(ModifierType.DoubleThrow, "Double Throw", "Throw two cards at once; each deals 70% damage.", false),
            new ModifierData(ModifierType.PiercingCards, "Piercing Deck", "All cards pierce two extra targets, but deal 10% less damage.", false),
            new ModifierData(ModifierType.Ricochet, "Ricochet", "Cards bounce to three nearby zombies.", false),
            new ModifierData(ModifierType.ExplosiveFaceCards, "Royal Explosion", "Face cards cause a large 75%-damage explosion; cooldown is 15% longer.", false),
            new ModifierData(ModifierType.ReturningCards, "Returning Cards", "Cards return after 0.45 seconds and no longer disappear when hitting walls.", false),
            new ModifierData(ModifierType.SuitStreak, "Card Streak", "Each consecutive non-Joker card gains +10% damage, up to +50%. A Joker resets it.", false),
            new ModifierData(ModifierType.ChaosDeck, "Chaos Deck", "Suit abilities are reassigned every round and every shuffle.", false),
            new ModifierData(ModifierType.CursedDeck, "Cursed Deck", "+40% damage, but every fourth throw has a longer cooldown.", false),
            new ModifierData(ModifierType.JokersBargain, "Joker's Bargain", "Jokers remove 75% HP, but normal cards deal 15% less damage.", false)
        };
    }
}
