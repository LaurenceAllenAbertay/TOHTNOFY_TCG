namespace DDD.TNFY.TCG.Effects
{
    public enum EffectTriggerType
    {
        OnPlay,
        OnTurnStart,
        OnTurnEnd,
        OnDeath,
        UnitDied,
        UnitKilled,
        OnAttack,
        OnDraw,
        OnAllyDeath,
        OnDamaged,
        OnMove,
        OnGameStart
    }

    public enum EffectActionType
    {
        DrawCard = 0,
        GainMana = 1,
        StunUnit = 2,
        HealTarget = 3,
        BuffAttack = 4,
        BuffMaxHealth = 5,
        GrantDoubleAttack = 6,
        ApplyDelayedKill = 7,
        BounceUnit = 8,
        GrantRush = 9,
        ApplyShield = 10,
        DealDamage = 11,
        ReduceOpponentMana = 12,
        GrantKeyword = 13,
        MoveAllyUnit = 15,
        SwapUnitSlot = 16,
        PullUnitOpposite = 18,
        ApplyDecay = 19,
        HealSelfByDamageDealt = 20,
        GrantNextItemDoubled = 21,
        PushAlliesAway = 22,
        AddCardToHand = 23,
        StealRandomCard = 25,
        RandomizeStatsOnDraw = 26,
        TransformCard = 28,
        ChooseXCards = 29,
        HookClosestAllyLeft = 30,
        SilenceUnit = 31,
        SwapAttackAndHealth = 33,
        RandomizeStats = 34,
        AttackAgainOnKill = 35,
        BuffAttackAndHealthPerQualifyingEnemy = 36,
        ChooseFixedCard = 37,
        BuffAttackAndHealthPerAlliedDeath = 39,
        MoveUnitToUnblockedSlot = 40,
        SpawnUnit = 41,
        AddTemporaryAttack = 42
    }

    public enum TargetType
    {
        None = 0,
        AnyUnit = 1,
        AnyAllyUnit = 2,
        AnyEnemyUnit = 3,
        OpposingEnemy = 4,
        AllyLeader = 5,
        EnemyLeader = 6,
        AnyUnitOrLeader = 7,
        AnyEnemyUnitOrLeader = 8,
        AnyAllyUnitOrLeader = 9,
        EmptySlot = 10,
        AnySlot = 11,
        Board = 12,
        Self = 13,
        AllEnemyUnits = 14,
        AllAllyUnits = 15,
        AllUnits = 16,
        AdjacentUnits = 17,
        AdjacentSlots = 18,
        LowestHealthEnemy = 19,
        RandomUnitEitherSide = 20
    }

    public enum AuraScope
    {
        AllOwnUnits,
        EdgeUnits,
        AdjacentToSource,
        Board,
        Self
    }

    public enum AuraActivationCondition
    {
        Always,
        OnceMaxManaReached
    }
}