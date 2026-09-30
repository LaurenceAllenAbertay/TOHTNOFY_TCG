using System.Collections.Generic;
using DDD.TNFY.TCG.Core;

namespace DDD.TNFY.TCG.Effects
{
    public enum StatusEffectType
    {
        Stunned = 0,
        DoubleAttackNextAttack = 1,
        DelayedKill = 2,
        Shield = 3,
        OpponentManaReduction = 4,
        TemporaryAttack = 6,
        Decaying = 7,
        Silenced = 8
    }

    public class ActiveStatusEffect
    {
        public StatusEffectType Type { get; }
        public int RemainingTriggers { get; set; }
        public int Magnitude { get; }
        public PlayerSide? SourceOwner { get; }

        public ActiveStatusEffect(StatusEffectType type, int remainingTriggers, int magnitude = 0, PlayerSide? sourceOwner = null)
        {
            Type = type;
            RemainingTriggers = remainingTriggers;
            Magnitude = magnitude;
            SourceOwner = sourceOwner;
        }

        public ActiveStatusEffect Clone() => new ActiveStatusEffect(Type, RemainingTriggers, Magnitude, SourceOwner);
    }

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

        public static bool TryGetDescription(StatusEffectType type, out string description) => Descriptions.TryGetValue(type, out description);

        public static string GetDisplayName(StatusEffectType type) => type switch
        {
            StatusEffectType.DelayedKill => "Delayed Kill",
            StatusEffectType.DoubleAttackNextAttack => "Double Attack",
            StatusEffectType.OpponentManaReduction => "Mana Reduction",
            StatusEffectType.TemporaryAttack => "Temporary Attack",
            _ => type.ToString()
        };

        public static IEnumerable<StatusEffectType> GetAllValues() => Descriptions.Keys;
    }
}