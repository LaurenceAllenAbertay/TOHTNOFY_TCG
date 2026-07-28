using System.Collections.Generic;
using DDD.TNFY.TCG.Core;

namespace DDD.TNFY.TCG.Effects
{
    public static class EffectTargeting
    {
        public static EffectTarget ResolveImmediateTarget(TargetType targetType, BoardUnit sourceUnit, PlayerSide sourceOwner, GameState state)
        {
            switch (targetType)
            {
                case TargetType.Self:
                    return EffectTarget.ForUnit(sourceUnit);

                case TargetType.AllyLeader:
                    return EffectTarget.ForLeader(sourceOwner);

                case TargetType.EnemyLeader:
                    return EffectTarget.ForLeader(sourceOwner.Opposite());

                case TargetType.OpposingEnemy:
                    if (sourceUnit == null)
                    {
                        return EffectTarget.None;
                    }

                    BoardUnit opposingUnit = state.Board.GetOpponentUnit(sourceOwner, sourceUnit.SlotIndex);

                    return opposingUnit != null ? EffectTarget.ForUnit(opposingUnit) : EffectTarget.None;

                case TargetType.LowestHealthEnemy:
                    BoardUnit lowestHealthEnemy = null;

                    foreach (BoardUnit candidate in state.Board.GetUnits(sourceOwner.Opposite()))
                    {
                        if (lowestHealthEnemy == null || candidate.CurrentHealth < lowestHealthEnemy.CurrentHealth)
                        {
                            lowestHealthEnemy = candidate;
                        }
                    }

                    return lowestHealthEnemy != null ? EffectTarget.ForUnit(lowestHealthEnemy) : EffectTarget.None;

                case TargetType.RandomUnitEitherSide:
                    List<BoardUnit> allUnits = new List<BoardUnit>(state.Board.GetUnits(PlayerSide.PlayerA));
                    allUnits.AddRange(state.Board.GetUnits(PlayerSide.PlayerB));

                    if (allUnits.Count == 0)
                    {
                        return EffectTarget.None;
                    }

                    System.Random rng = new System.Random();
                    BoardUnit randomUnit = allUnits[rng.Next(allUnits.Count)];

                    return EffectTarget.ForUnit(randomUnit);

                default:
                    return EffectTarget.None;
            }
        }

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

                case TargetType.OpposingEnemy:
                    return target.Kind == EffectTargetKind.Unit && target.Unit.Owner != state.ActivePlayer;

                case TargetType.LowestHealthEnemy:
                    return target.Kind == EffectTargetKind.Unit && target.Unit.Owner != state.ActivePlayer;

                case TargetType.RandomUnitEitherSide:
                    return target.Kind == EffectTargetKind.Unit;

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