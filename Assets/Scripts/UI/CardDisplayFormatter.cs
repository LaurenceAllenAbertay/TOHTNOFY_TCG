using DDD.TNFY.TCG.Cards;
using DDD.TNFY.TCG.Core;
using DDD.TNFY.TCG.Effects;

namespace DDD.TNFY.TCG.UI
{
    public static class CardDisplayFormatter
    {
        public static string GetCostText(CardData card)
        {
            return card.ManaCost.ToString();
        }

        public static string GetNameText(CardData card)
        {
            return card.CardName;
        }

        public static string GetAbilityText(CardData card)
        {
            return card.AbilityText;
        }

        public static string GetAbilityText(BoardUnit unit)
        {
            if (unit.IsSilenced)
            {
                return "Silenced";
            }

            return unit.SourceCard.AbilityText;
        }

        public static string GetAttackText(UnitCardData card)
        {
            return card.Attack.ToString();
        }

        public static string GetHealthText(UnitCardData card)
        {
            return card.Health.ToString();
        }

        public static string GetAttackText(UnitCardData card, PlayerSide side, GameState state)
        {
            int bonus = AuraCalculator.GetPreviewAttackBonus(side, state);
            return (card.Attack + bonus).ToString();
        }

        public static string GetHealthText(UnitCardData card, PlayerSide side, GameState state)
        {
            int bonus = AuraCalculator.GetPreviewMaxHealthBonus(side, state);
            return (card.Health + bonus).ToString();
        }

        public static string GetAttackText(BoardUnit unit, GameState state)
        {
            return unit.GetCurrentAttack(state).ToString();
        }

        public static string GetHealthText(BoardUnit unit, GameState state)
        {
            int effectiveMax = unit.GetEffectiveMaxHealth(state);
            return $"{unit.CurrentHealth}/{effectiveMax}";
        }
    }
}