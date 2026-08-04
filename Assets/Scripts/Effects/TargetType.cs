namespace DDD.TNFY.TCG.Effects
{
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
}