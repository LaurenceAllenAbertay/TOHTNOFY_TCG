using System;

namespace DDD.TNFY.TCG.Effects
{
    [Serializable]
    public class CardEffect
    {
        public EffectTriggerType trigger;
        public EffectActionType action;
        public TargetType targetType;
        public int amount;
        public bool oncePerTurn;
        public bool mandatoryTarget = true;
    }
}