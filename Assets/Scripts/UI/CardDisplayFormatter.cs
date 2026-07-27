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

        public static string GetCurrentCostText(CardData card, PlayerSide side, GameState state)
        {
            if (card is UnitCardData unitCard)
            {
                Player player = state.GetPlayer(side);
                return AuraCalculator.GetUnitCost(unitCard, player).ToString();
            }

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

            string baseText = unit.SourceCard.AbilityText;
            string grantedKeywordText = GetGrantedKeywordText(unit);

            return baseText + grantedKeywordText;
        }

        private static string GetGrantedKeywordText(BoardUnit unit)
        {
            string result = "";

            foreach (Keyword keyword in KeywordReference.GetAllValues())
            {
                if ((unit.GrantedKeywords & keyword) == 0)
                {
                    continue;
                }

                if (unit.SourceCard.HasKeyword(keyword))
                {
                    continue;
                }

                result += "\n" + keyword.ToString();
            }

            return result;
        }

        public static string GetAttackText(UnitCardData card)
        {
            return card.Attack.ToString();
        }

        public static string GetHealthText(UnitCardData card)
        {
            if (card.HasPendingCurrentHealth)
            {
                int currentHealth = System.Math.Min(card.PendingCurrentHealth, card.Health);
                return $"{currentHealth}/{card.Health}";
            }

            return card.Health.ToString();
        }

        public static string GetAttackText(UnitCardData card, PlayerSide side, GameState state)
        {
            int bonus = AuraCalculator.GetPreviewAttackBonus(side, state, card);
            return (card.Attack + bonus).ToString();
        }

        public static string GetHealthText(UnitCardData card, PlayerSide side, GameState state)
        {
            int bonus = AuraCalculator.GetPreviewMaxHealthBonus(side, state, card);
            int maxHealth = card.Health + bonus;

            if (card.HasPendingCurrentHealth)
            {
                int currentHealth = System.Math.Min(card.PendingCurrentHealth, maxHealth);
                return $"{currentHealth}/{maxHealth}";
            }

            return maxHealth.ToString();
        }

        public static string GetAttackText(BoardUnit unit, GameState state)
        {
            return unit.GetCurrentAttack(state).ToString();
        }

        public static string GetHealthText(BoardUnit unit, GameState state)
        {
            return unit.CurrentHealth.ToString();
        }

        public static UnityEngine.Color GetHealthColor(int currentHealth, int maxHealth, UnityEngine.Color fullHealthColor)
        {
            return currentHealth < maxHealth ? UnityEngine.Color.red : fullHealthColor;
        }
    }
}