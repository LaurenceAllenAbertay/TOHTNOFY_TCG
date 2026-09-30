using System;
using DDD.TNFY.TCG.Cards;
using UnityEngine.Serialization;

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
        [FormerlySerializedAs("grantedCard")]
        public CardData relevantCard;
        public Keyword keyword;
        public CardCategory cardCategory;
        public int randomizeMin;
        public int randomizeMax;
        public CardData[] fixedChoiceOptions;
    }

    [Serializable]
    public class LeaderAura
    {
        public AuraScope scope;
        public AuraActivationCondition activationCondition;
        public AuraGrant grant;
    }

    [Serializable]
    public class AuraGrant
    {
        public EffectActionType action;
        public int amount;
        public Keyword keyword;
    }
}