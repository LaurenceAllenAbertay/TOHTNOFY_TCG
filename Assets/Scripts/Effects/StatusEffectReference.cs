using System.Collections.Generic;

namespace DDD.TNFY.TCG.Effects
{
    public static class StatusEffectReference
    {
        private static readonly Dictionary<StatusEffectType, string> Descriptions = new Dictionary<StatusEffectType, string>
        {
            { StatusEffectType.Stunned, "This unit cannot attack or move this turn." },
            { StatusEffectType.Silenced, "This unit's keywords and abilities are disabled." },
            { StatusEffectType.Decaying, "This unit takes 1 damage per stack at the start of its owner's turn." },
            { StatusEffectType.DelayedKill, "This unit will be destroyed next round." },
            { StatusEffectType.DoubleAttackNextAttack, "The next time this unit attacks this turn, it attacks twice." },
            { StatusEffectType.Shield, "Blocks the next instance of damage, then breaks." },
            { StatusEffectType.OpponentManaReduction, "Reduces this player's mana by a set amount at the start of their next turn." },
            { StatusEffectType.TemporaryAttack, "This unit has bonus Attack until the end of this turn." }
        };

        private static readonly Dictionary<StatusEffectType, string> DisplayNames = new Dictionary<StatusEffectType, string>
        {
            { StatusEffectType.Stunned, "Stunned" },
            { StatusEffectType.Silenced, "Silenced" },
            { StatusEffectType.Decaying, "Decaying" },
            { StatusEffectType.DelayedKill, "Delayed Kill" },
            { StatusEffectType.DoubleAttackNextAttack, "Double Attack" },
            { StatusEffectType.Shield, "Shield" },
            { StatusEffectType.OpponentManaReduction, "Mana Reduction" },
            { StatusEffectType.TemporaryAttack, "Temporary Attack" }
        };

        public static bool TryGetDescription(StatusEffectType type, out string description)
        {
            return Descriptions.TryGetValue(type, out description);
        }

        public static string GetDisplayName(StatusEffectType type)
        {
            return DisplayNames.TryGetValue(type, out string displayName) ? displayName : type.ToString();
        }

        public static IEnumerable<StatusEffectType> GetAllValues()
        {
            return Descriptions.Keys;
        }
    }
}