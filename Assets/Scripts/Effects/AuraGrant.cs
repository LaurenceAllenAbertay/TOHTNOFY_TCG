using System;
using DDD.TNFY.TCG.Cards;

namespace DDD.TNFY.TCG.Effects
{
    [Serializable]
    public class AuraGrant
    {
        public EffectActionType action;
        public int amount;
        public Keyword keyword;
    }
}