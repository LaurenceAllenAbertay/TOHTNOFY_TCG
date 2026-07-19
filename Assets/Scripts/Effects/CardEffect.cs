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
        public bool targetsOwnHand;
        public CardCategory cardCategory;
    }
}