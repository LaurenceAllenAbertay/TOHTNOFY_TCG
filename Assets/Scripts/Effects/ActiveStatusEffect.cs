using DDD.TNFY.TCG.Core;

namespace DDD.TNFY.TCG.Effects
{
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

        public ActiveStatusEffect Clone()
        {
            return new ActiveStatusEffect(Type, RemainingTriggers, Magnitude, SourceOwner);
        }
    }
}