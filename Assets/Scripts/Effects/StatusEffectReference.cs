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
            { StatusEffectType.DelayedKill, "This unit will be destroyed when its remaining triggers reach 0." },
            { StatusEffectType.TemporaryAttackNextAttack, "This unit deals bonus damage on its next attack." },
        };

        public static bool TryGetDescription(StatusEffectType type, out string description)
        {
            return Descriptions.TryGetValue(type, out description);
        }

        public static IEnumerable<StatusEffectType> GetAllValues()
        {
            return Descriptions.Keys;
        }
    }
}