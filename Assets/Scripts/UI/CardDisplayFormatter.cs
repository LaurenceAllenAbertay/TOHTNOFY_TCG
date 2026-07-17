using DDD.TNFY.TCG.Cards;
using DDD.TNFY.TCG.Core;

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

        public static string GetAttackText(UnitCardData card)
        {
            return card.Attack.ToString();
        }

        public static string GetHealthText(UnitCardData card)
        {
            return card.Health.ToString();
        }

        public static string GetAttackText(BoardUnit unit)
        {
            return unit.CurrentAttack.ToString();
        }

        public static string GetHealthText(BoardUnit unit)
        {
            return unit.CurrentHealth.ToString();
        }
    }
}