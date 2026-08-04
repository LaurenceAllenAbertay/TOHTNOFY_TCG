using System.Collections.Generic;
using UnityEngine;

namespace DDD.TNFY.TCG.Cards
{
    public static class CardRarityReference
    {
        private static readonly Dictionary<CardRarity, Color> Colors = new Dictionary<CardRarity, Color>
        {
            { CardRarity.Common, new Color(0.75f, 0.75f, 0.75f) },
            { CardRarity.Uncommon, new Color(0.2f, 0.8f, 0.2f) },
            { CardRarity.Rare, new Color(0.2f, 0.5f, 0.95f) },
            { CardRarity.Epic, new Color(0.65f, 0.25f, 0.9f) },
            { CardRarity.Legendary, new Color(0.95f, 0.6f, 0.1f) }
        };

        public static bool TryGetColor(CardRarity rarity, out Color color)
        {
            return Colors.TryGetValue(rarity, out color);
        }

        public static IEnumerable<CardRarity> GetAllValues()
        {
            return Colors.Keys;
        }
    }
}