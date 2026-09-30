using System.Collections.Generic;
using DDD.TNFY.TCG.Core;

namespace DDD.TNFY.TCG.Effects
{
    public static class EffectTargeting
    {
        private static readonly System.Random rng = new System.Random();

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
                    BoardUnit opposing = sourceUnit != null ? state.Board.GetOpponentUnit(sourceOwner, sourceUnit.SlotIndex) : null;
                    return opposing != null ? EffectTarget.ForUnit(opposing) : EffectTarget.None;

                case TargetType.LowestHealthEnemy:
                    BoardUnit lowest = null;

                    foreach (BoardUnit candidate in state.Board.GetUnits(sourceOwner.Opposite()))
                    {
                        if (lowest == null || candidate.CurrentHealth < lowest.CurrentHealth)
                        {
                            lowest = candidate;
                        }
                    }

                    return lowest != null ? EffectTarget.ForUnit(lowest) : EffectTarget.None;

                case TargetType.RandomUnitEitherSide:
                    List<BoardUnit> allUnits = new List<BoardUnit>(state.Board.GetUnits(PlayerSide.PlayerA));
                    allUnits.AddRange(state.Board.GetUnits(PlayerSide.PlayerB));
                    return allUnits.Count > 0 ? EffectTarget.ForUnit(allUnits[rng.Next(allUnits.Count)]) : EffectTarget.None;

                default:
                    return EffectTarget.None;
            }
        }

        public static List<BoardUnit> ResolveGroupTargets(TargetType targetType, BoardUnit sourceUnit, PlayerSide sourceOwner, GameState state)
        {
            List<BoardUnit> result = new List<BoardUnit>();

            if (targetType == TargetType.AllAllyUnits || targetType == TargetType.AllUnits)
            {
                result.AddRange(state.Board.GetUnits(sourceOwner));
            }

            if (targetType == TargetType.AllEnemyUnits || targetType == TargetType.AllUnits)
            {
                result.AddRange(state.Board.GetUnits(sourceOwner.Opposite()));
            }

            if (targetType == TargetType.AdjacentUnits && sourceUnit != null)
            {
                foreach (int slot in new[] { sourceUnit.SlotIndex - 1, sourceUnit.SlotIndex + 1 })
                {
                    BoardUnit neighbour = slot >= 0 && slot < Board.SlotsPerSide ? state.Board.GetUnit(sourceUnit.Owner, slot) : null;

                    if (neighbour != null)
                    {
                        result.Add(neighbour);
                    }
                }
            }

            return result;
        }

        public static List<EffectTarget> ResolveGroupSlotTargets(TargetType targetType, BoardUnit sourceUnit, GameState state)
        {
            List<EffectTarget> result = new List<EffectTarget>();

            if (targetType != TargetType.AdjacentSlots || sourceUnit == null)
            {
                return result;
            }

            foreach (int slot in new[] { sourceUnit.SlotIndex - 1, sourceUnit.SlotIndex + 1 })
            {
                if (slot >= 0 && slot < Board.SlotsPerSide && state.Board.GetUnit(sourceUnit.Owner, slot) == null)
                {
                    result.Add(EffectTarget.ForSlot(sourceUnit.Owner, slot));
                }
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
            return targetType == TargetType.AnyUnit
                || targetType == TargetType.AnyAllyUnit
                || targetType == TargetType.AnyEnemyUnit
                || targetType == TargetType.AnyUnitOrLeader
                || targetType == TargetType.AnyEnemyUnitOrLeader
                || targetType == TargetType.AnyAllyUnitOrLeader
                || targetType == TargetType.EmptySlot
                || targetType == TargetType.AnySlot;
        }

        public static bool IsValidTarget(TargetType targetType, EffectTarget target, GameState state)
        {
            bool isUnit = target.Kind == EffectTargetKind.Unit;
            bool isLeader = target.Kind == EffectTargetKind.Leader;

            if (isUnit && (target.Unit == null || state.Board.GetUnit(target.Unit.Owner, target.Unit.SlotIndex) != target.Unit))
            {
                return false;
            }

            bool isAlly = isUnit ? target.Unit.Owner == state.ActivePlayer : isLeader && target.LeaderSide == state.ActivePlayer;

            switch (targetType)
            {
                case TargetType.None:
                case TargetType.AllEnemyUnits:
                case TargetType.AllAllyUnits:
                case TargetType.AllUnits:
                case TargetType.AdjacentUnits:
                case TargetType.AdjacentSlots:
                    return target.Kind == EffectTargetKind.None;

                case TargetType.Board:
                    return true;

                case TargetType.AnyUnit:
                case TargetType.RandomUnitEitherSide:
                    return isUnit;

                case TargetType.AnyAllyUnit:
                    return isUnit && isAlly;

                case TargetType.AnyEnemyUnit:
                case TargetType.OpposingEnemy:
                case TargetType.LowestHealthEnemy:
                    return isUnit && !isAlly;

                case TargetType.AllyLeader:
                    return isLeader && isAlly;

                case TargetType.EnemyLeader:
                    return isLeader && !isAlly;

                case TargetType.AnyUnitOrLeader:
                    return isUnit || isLeader;

                case TargetType.AnyAllyUnitOrLeader:
                    return (isUnit || isLeader) && isAlly;

                case TargetType.AnyEnemyUnitOrLeader:
                    return (isUnit || isLeader) && !isAlly;

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