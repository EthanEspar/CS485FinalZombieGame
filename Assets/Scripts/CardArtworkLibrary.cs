using System;
using UnityEngine;

[CreateAssetMenu(fileName = "CardArtworkLibrary", menuName = "Card Weapon/Card Artwork Library")]
public class CardArtworkLibrary : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public ThrowingCard.Suit suit;
        public ThrowingCard.Rank rank;
        public Sprite sprite;
    }

    [SerializeField] private Entry[] entries = Array.Empty<Entry>();

    public Sprite GetSprite(ThrowingCard.Suit suit, ThrowingCard.Rank rank)
    {
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i].rank == rank &&
                (rank == ThrowingCard.Rank.Joker || entries[i].suit == suit))
            {
                return entries[i].sprite;
            }
        }

        return null;
    }

    public void SetEntries(Entry[] newEntries)
    {
        entries = newEntries ?? Array.Empty<Entry>();
    }
}
