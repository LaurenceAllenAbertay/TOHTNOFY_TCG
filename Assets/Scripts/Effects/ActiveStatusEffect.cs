namespace DDD.TNFY.TCG.Effects
{
    public class ActiveStatusEffect
    {
        public StatusEffectType Type { get; }
        public int RemainingTriggers { get; set; }
        public int Magnitude { get; }

        public ActiveStatusEffect(StatusEffectType type, int remainingTriggers, int magnitude = 0)
        {
            Type = type;
            RemainingTriggers = remainingTriggers;
            Magnitude = magnitude;
        }
    }
}