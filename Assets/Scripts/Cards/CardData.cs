using System.Collections.Generic;
using DDD.TNFY.TCG.Effects;
using UnityEngine;

namespace DDD.TNFY.TCG.Cards
{
    public enum CardRarity
    {
        Common,
        Rare,
        Epic,
        Legendary
    }

    public abstract class CardData : ScriptableObject
    {
        [SerializeField] private string cardId;
        [SerializeField] private string cardName;
        [SerializeField] private int manaCost;
        [SerializeField] private CardRarity rarity;
        [SerializeField] private Sprite cardArt;
        [TextArea(2, 4)]
        [SerializeField] private string abilityText;

        [SerializeField]
        private List<CardEffect> effects = new List<CardEffect>();

        public string CardId => cardId;
        public string CardName => cardName;
        public int ManaCost => manaCost;
        public CardRarity Rarity => rarity;
        public Sprite CardArt => cardArt;
        public string AbilityText => abilityText;
        public IReadOnlyList<CardEffect> Effects => effects;
    }
}