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

        public static List<BoardUnit> ResolveGroupTargets(TargetType targetType, BoardUnit sourceUnit, PlayerSide sourceOwner, GameState state)
        {
            List<BoardUnit> result = new List<BoardUnit>();

            switch (targetType)
            {
                case TargetType.AllEnemyUnits:
                    result.AddRange(state.Board.GetUnits(sourceOwner.Opposite()));
                    break;

                case TargetType.AllAllyUnits:
                    result.AddRange(state.Board.GetUnits(sourceOwner));
                    break;

                case TargetType.AllUnits:
                    result.AddRange(state.Board.GetUnits(sourceOwner));
                    result.AddRange(state.Board.GetUnits(sourceOwner.Opposite()));
                    break;

                case TargetType.AdjacentUnits:
                    if (sourceUnit == null)
                    {
                        break;
                    }

                    int slotIndex = sourceUnit.SlotIndex;
                    PlayerSide side = sourceUnit.Owner;

                    BoardUnit leftNeighbor = slotIndex - 1 >= 0 ? state.Board.GetUnit(side, slotIndex - 1) : null;
                    BoardUnit rightNeighbor = slotIndex + 1 < Board.SlotsPerSide ? state.Board.GetUnit(side, slotIndex + 1) : null;

                    if (leftNeighbor != null)
                    {
                        result.Add(leftNeighbor);
                    }

                    if (rightNeighbor != null)
                    {
                        result.Add(rightNeighbor);
                    }

                    break;
            }

            return result;
        }

        public static List<EffectTarget> ResolveGroupSlotTargets(TargetType targetType, BoardUnit sourceUnit, GameState state)
        {
            List<EffectTarget> result = new List<EffectTarget>();

            switch (targetType)
            {
                case TargetType.AdjacentSlots:
                    if (sourceUnit == null)
                    {
                        break;
                    }

                    int slotIndex = sourceUnit.SlotIndex;
                    PlayerSide side = sourceUnit.Owner;

                    bool leftIsEmpty = slotIndex - 1 >= 0 && state.Board.GetUnit(side, slotIndex - 1) == null;
                    bool rightIsEmpty = slotIndex + 1 < Board.SlotsPerSide && state.Board.GetUnit(side, slotIndex + 1) == null;

                    if (leftIsEmpty)
                    {
                        result.Add(EffectTarget.ForSlot(side, slotIndex - 1));
                    }

                    if (rightIsEmpty)
                    {
                        result.Add(EffectTarget.ForSlot(side, slotIndex + 1));
                    }

                    break;
            }

            return result;
        }

        public static bool IsGroupSlotTarget(TargetType targetType)
        {
            return targetType == TargetType.AdjacentSlots;
        }

        public static bool IsGroupTarget(TargetType targetType)
        {
            return targetType == TargetType.AllEnemyUnits
                || targetType == TargetType.AllAllyUnits
                || targetType == TargetType.AllUnits
                || targetType == TargetType.AdjacentUnits;
        }

        public static bool RequiresClick(TargetType targetType)
        {
            return targetType != TargetType.None
                && targetType != TargetType.Board
                && targetType != TargetType.Self
                && targetType != TargetType.AllyLeader
                && targetType != TargetType.EnemyLeader
                && targetType != TargetType.OpposingEnemy
                && targetType != TargetType.LowestHealthEnemy
                && targetType != TargetType.RandomUnitEitherSide
                && !IsGroupTarget(targetType)
                && !IsGroupSlotTarget(targetType);
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

                case TargetType.AnyAllyUnit:
                    return target.Kind == EffectTargetKind.Unit && target.Unit.Owner == state.ActivePlayer;

                case TargetType.AnyEnemyUnit:
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

                case TargetType.AnyAllyUnitOrLeader:
                    if (target.Kind == EffectTargetKind.Unit)
                    {
                        return target.Unit.Owner == state.ActivePlayer;
                    }
                    if (target.Kind == EffectTargetKind.Leader)
                    {
                        return target.LeaderSide == state.ActivePlayer;
                    }
                    return false;

                case TargetType.AllEnemyUnits:
                case TargetType.AllAllyUnits:
                case TargetType.AllUnits:
                case TargetType.AdjacentUnits:
                case TargetType.AdjacentSlots:
                    return target.Kind == EffectTargetKind.None;

                case TargetType.AnyEnemyUnitOrLeader:
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