using System;

namespace DDD.TNFY.TCG.Effects
{
    [Serializable]
    public class LeaderAura
    {
        public AuraScope scope;
        public AuraActivationCondition activationCondition;
        public AuraGrant grant;
    }
}