using System.Collections.Generic;
using DDD.TNFY.TCG.Effects;
using UnityEngine;

namespace DDD.TNFY.TCG.Cards
{
    public enum CardRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }

    public enum CardCategory
    {
        Any,
        Unit,
        Item
    }

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

        public static bool TryGetColor(CardRarity rarity, out Color color) => Colors.TryGetValue(rarity, out color);
    }

    public abstract class CardData : ScriptableObject
    {
        [SerializeField] private string cardId;
        [SerializeField] private string cardName;
        [SerializeField] protected int manaCost;
        [SerializeField] private CardRarity rarity;
        [SerializeField] private Sprite cardArt;
        [TextArea(2, 4)]
        [SerializeField] private string abilityText;
        [SerializeField] private List<CardEffect> effects = new List<CardEffect>();

        public string CardId => cardId;
        public string CardName => cardName;
        public int ManaCost => manaCost;
        public CardRarity Rarity => rarity;
        public Sprite CardArt => cardArt;
        public string AbilityText => abilityText;
        public IReadOnlyList<CardEffect> Effects => effects;
    }
}