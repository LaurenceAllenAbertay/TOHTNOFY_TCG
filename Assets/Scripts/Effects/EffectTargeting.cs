using DDD.TNFY.TCG.Core;

namespace DDD.TNFY.TCG.Effects
{
    public static class EffectTargeting
    {
        public static bool IsValidTarget(TargetType targetType, EffectTarget target, GameState state)
        {
            switch (targetType)
            {
                case TargetType.None:
                    return target.Kind == EffectTargetKind.None;

                case TargetType.Board:
                    return true;

                case TargetType.AnyUnit:
                    return target.Kind == EffectTargetKind.Unit;

                case TargetType.AllyUnit:
                    return target.Kind == EffectTargetKind.Unit && target.Unit.Owner == state.ActivePlayer;

                case TargetType.EnemyUnit:
                    return target.Kind == EffectTargetKind.Unit && target.Unit.Owner != state.ActivePlayer;

                case TargetType.AllyLeader:
                    return target.Kind == EffectTargetKind.Leader && target.LeaderSide == state.ActivePlayer;

                case TargetType.EnemyLeader:
                    return target.Kind == EffectTargetKind.Leader && target.LeaderSide != state.ActivePlayer;

                case TargetType.AnyUnitOrLeader:
                    return target.Kind == EffectTargetKind.Unit || target.Kind == EffectTargetKind.Leader;

                case TargetType.EnemyUnitOrLeader:
                    if (target.Kind == EffectTargetKind.Unit)
                    {
                        return target.Unit.Owner != state.ActivePlayer;
                    }
                    if (target.Kind == EffectTargetKind.Leader)
                    {
                        return target.LeaderSide != state.ActivePlayer;
                    }
                    return false;

                case TargetType.EmptySlot:
                    return target.Kind == EffectTargetKind.Slot && state.Board.GetUnit(target.SlotSide, target.SlotIndex) == null;

                case TargetType.AnySlot:
                    return target.Kind == EffectTargetKind.Slot;

                default:
                    return false;
            }
        }
    }
}