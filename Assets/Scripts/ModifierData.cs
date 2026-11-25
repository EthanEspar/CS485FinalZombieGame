using System;

public enum ModifierType
{
    HeavyHand,
    RapidDeal,
    HeavyCards,
    HighVelocity,
    GlassCannon,
    CarefulAim,
    WildDamage,
    HomingCards,
    DoubleThrow,
    PiercingCards,
    Ricochet,
    ExplosiveFaceCards,
    ReturningCards,
    SuitStreak,
    ChaosDeck,
    CursedDeck,
    JokersBargain
}

public enum SuitEffect
{
    Pierce,
    Heal,
    StunAndKnockback,
    CriticalHit
}

[Serializable]
public class ModifierData
{
    public ModifierType type;
    public string displayName;
    public string description;
    public bool canStack;

    public ModifierData(
        ModifierType newType,
        string newName,
        string newDescription,
        bool newCanStack)
    {
        type = newType;
        displayName = newName;
        description = newDescription;
        canStack = newCanStack;
    }
}
