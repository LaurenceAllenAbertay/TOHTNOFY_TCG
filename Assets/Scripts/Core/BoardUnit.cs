using System.Collections.Generic;
using DDD.TNFY.TCG.Cards;
using DDD.TNFY.TCG.Effects;

namespace DDD.TNFY.TCG.Core
{
    public class BoardUnit
    {
        public UnitCardData SourceCard { get; }
        public PlayerSide Owner { get; }
        public int SlotIndex { get; set; }
        public int BonusAttack { get; set; }
        public int MaxHealth { get; set; }
        public int CurrentHealth { get; set; }
        public bool PlacedThisTurn { get; set; }
        public bool HasMovedThisTurn { get; set; }
        public Keyword GrantedKeywords { get; set; }

        public List<ActiveStatusEffect> Statuses { get; } = new List<ActiveStatusEffect>();

        public int GetCurrentAttack(GameState state)
        {
            return SourceCard.Attack + BonusAttack + AuraCalculator.GetAttackBonus(this, state);
        }

        public int GetEffectiveMaxHealth(GameState state)
        {
            return AuraCalculator.GetEffectiveMaxHealth(this, state);
        }

        public bool HasKeyword(Keyword keyword, GameState state)
        {
            bool hasBaseKeyword = SourceCard.HasKeyword(keyword) || (GrantedKeywords & keyword) != 0;
            return hasBaseKeyword || AuraCalculator.HasAuraKeyword(this, state, keyword);
        }

        public void GrantKeyword(Keyword keyword)
        {
            GrantedKeywords |= keyword;
        }

        public BoardUnit(UnitCardData sourceCard, PlayerSide owner, int slotIndex)
        {
            SourceCard = sourceCard;
            Owner = owner;
            SlotIndex = slotIndex;
            MaxHealth = sourceCard.Health;
            CurrentHealth = sourceCard.Health;
            PlacedThisTurn = true;
        }
    }
}