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
        public int LastSyncedAuraHealthBonus { get; set; }
        public bool PlacedThisTurn { get; set; }
        public bool HasMovedThisTurn { get; set; }
        public bool HasAttackedThisTurn { get; set; }
        public bool HasUsedGrantedEnemyMoveThisTurn { get; set; }
        public Keyword GrantedKeywords { get; set; }

        public List<ActiveStatusEffect> Statuses { get; } = new List<ActiveStatusEffect>();

        public bool IsSilenced => HasStatus(StatusEffectType.Silenced);

        public int GetCurrentAttack(GameState state)
        {
            return SourceCard.Attack + BonusAttack + AuraCalculator.GetAttackBonus(this, state) + GetPendingTemporaryAttackBonus();
        }

        private int GetPendingTemporaryAttackBonus()
        {
            int bonus = 0;

            foreach (ActiveStatusEffect status in Statuses)
            {
                if (status.Type == StatusEffectType.TemporaryAttackNextAttack)
                {
                    bonus += status.Magnitude;
                }
            }

            return bonus;
        }

        public int GetEffectiveMaxHealth(GameState state)
        {
            return AuraCalculator.GetEffectiveMaxHealth(this, state);
        }

        public bool HasKeyword(Keyword keyword, GameState state)
        {
            if (IsSilenced)
            {
                return false;
            }

            bool hasBaseKeyword = SourceCard.HasKeyword(keyword) || (GrantedKeywords & keyword) != 0;
            return hasBaseKeyword || AuraCalculator.HasAuraKeyword(this, state, keyword);
        }

        public bool HasStatus(StatusEffectType type)
        {
            foreach (ActiveStatusEffect status in Statuses)
            {
                if (status.Type == type)
                {
                    return true;
                }
            }

            return false;
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