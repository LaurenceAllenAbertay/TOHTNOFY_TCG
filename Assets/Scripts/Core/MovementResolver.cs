using System.Collections.Generic;
using UnityEngine;
using DDD.TNFY.TCG.Cards;
using DDD.TNFY.TCG.Effects;

namespace DDD.TNFY.TCG.Core
{
    public class MovementResolver
    {
        private readonly GameState state;

        public MovementResolver(GameState state)
        {
            this.state = state;
        }

        public bool CanAnyUnitMove()
        {
            for (int fromSlot = 0; fromSlot < Board.SlotsPerSide; fromSlot++)
            {
                BoardUnit unit = state.Board.GetUnit(state.ActivePlayer, fromSlot);

                if (unit == null) continue;

                for (int toSlot = 0; toSlot < Board.SlotsPerSide; toSlot++)
                {
                    if (fromSlot == toSlot) continue;

                    if (CanMoveUnit(fromSlot, toSlot))
                    {
                        Debug.Log($"[MovementResolver] CanAnyUnitMove: {unit.SourceCard.CardName} can move {fromSlot} -> {toSlot}.");
                        return true;
                    }
                }
            }

            if (HasAvailableGrantedEnemyMove(state.ActivePlayer))
            {
                Debug.Log("[MovementResolver] CanAnyUnitMove: a granted enemy-move ability is available.");
                return true;
            }

            Debug.Log("[MovementResolver] CanAnyUnitMove: no legal moves found.");
            return false;
        }

        public bool TryMoveUnit(int fromSlot, int toSlot)
        {
            if (!CanMoveUnit(fromSlot, toSlot)) return false;

            PlayerSide side = state.ActivePlayer;
            BoardUnit unit = state.Board.GetUnit(side, fromSlot);
            bool isNimble = unit.HasKeyword(Keyword.Nimble, state);

            Player mover = state.GetPlayer(side);
            LeaderData moverLeader = mover.Leader;

            if (moverLeader != null && moverLeader.MoveManaCost > 0)
            {
                mover.CurrentMana -= moverLeader.MoveManaCost;
            }

            state.Board.RemoveUnit(side, fromSlot);
            state.Board.PlaceUnit(side, toSlot, unit);

            unit.HasMovedThisTurn = true;

            if (!isNimble)
            {
                state.HasUsedMoveThisTurn = true;
            }

            GrantCodyMoveBonusIfApplicable(unit);
            OnUnitRelocated(unit);

            return true;
        }

        public bool MoveUnitFree(PlayerSide side, int fromSlot, int toSlot)
        {
            if (state.ActivePlayer != side) return false;
            if (!CanMoveUnit(fromSlot, toSlot, ignoreMoveLimitAndCost: true)) return false;

            BoardUnit unit = state.Board.GetUnit(side, fromSlot);

            state.Board.RemoveUnit(side, fromSlot);
            state.Board.PlaceUnit(side, toSlot, unit);

            GrantCodyMoveBonusIfApplicable(unit);
            OnUnitRelocated(unit);

            return true;
        }

        public bool HasAnyLegalUnblockedSlot(PlayerSide side, int fromSlot)
        {
            for (int toSlot = 0; toSlot < Board.SlotsPerSide; toSlot++)
            {
                if (toSlot == fromSlot) continue;

                if (IsUnblockedPathClear(side, fromSlot, toSlot))
                {
                    return true;
                }
            }

            return false;
        }

        public bool CanMoveGrantedEnemyUnitFree(BoardUnit unit, int toSlot)
        {
            if (unit == null) return false;

            return IsUnblockedPathClear(unit.Owner, unit.SlotIndex, toSlot);
        }

        public bool MoveGrantedEnemyUnitFree(BoardUnit unit, int toSlot)
        {
            if (!CanMoveGrantedEnemyUnitFree(unit, toSlot)) return false;

            PlayerSide side = unit.Owner;
            int fromSlot = unit.SlotIndex;

            state.Board.RemoveUnit(side, fromSlot);
            state.Board.PlaceUnit(side, toSlot, unit);

            GrantCodyMoveBonusIfApplicable(unit);
            OnUnitRelocated(unit);

            return true;
        }

        private bool IsUnblockedPathClear(PlayerSide side, int fromSlot, int toSlot)
        {
            if (state.Board.GetUnit(side, toSlot) != null) return false;
            if (fromSlot == toSlot) return false;

            int distance = toSlot - fromSlot;
            int step = distance > 0 ? 1 : -1;

            for (int slot = fromSlot + step; slot != toSlot; slot += step)
            {
                if (state.Board.GetUnit(side, slot) != null) return false;
            }

            return true;
        }

        public bool HookClosestAllyLeft(BoardUnit sourceUnit)
        {
            if (sourceUnit == null)
            {
                Debug.Log("[MovementResolver] HookClosestAllyLeft FAIL: sourceUnit is null.");
                return false;
            }

            PlayerSide side = sourceUnit.Owner;
            int destinationSlot = sourceUnit.SlotIndex - 1;

            if (destinationSlot < 0)
            {
                Debug.Log($"[MovementResolver] {sourceUnit.SourceCard.CardName}'s Hook has no slot to its left — fizzling.");
                return false;
            }

            BoardUnit closestAlly = null;

            for (int slot = destinationSlot; slot >= 0; slot--)
            {
                BoardUnit candidate = state.Board.GetUnit(side, slot);
                if (candidate != null)
                {
                    closestAlly = candidate;
                    break;
                }
            }

            if (closestAlly == null)
            {
                Debug.Log($"[MovementResolver] {sourceUnit.SourceCard.CardName}'s Hook found no ally unit to its left — fizzling.");
                return false;
            }

            if (closestAlly.SlotIndex == destinationSlot)
            {
                Debug.Log($"[MovementResolver] {sourceUnit.SourceCard.CardName}'s Hook: closest ally {closestAlly.SourceCard.CardName} is already in the destination slot — nothing to do.");
                return false;
            }

            if (closestAlly.HasKeyword(Keyword.Unmoving, state))
            {
                Debug.Log($"[MovementResolver] {sourceUnit.SourceCard.CardName}'s Hook FAIL: {closestAlly.SourceCard.CardName} is Unmoving.");
                return false;
            }

            int fromSlot = closestAlly.SlotIndex;

            state.Board.RemoveUnit(side, fromSlot);
            state.Board.PlaceUnit(side, destinationSlot, closestAlly);

            GrantCodyMoveBonusIfApplicable(closestAlly);
            OnUnitRelocated(closestAlly);

            Debug.Log($"[MovementResolver] {sourceUnit.SourceCard.CardName}'s Hook moved {closestAlly.SourceCard.CardName} from slot {fromSlot} to slot {destinationSlot}.");

            return true;
        }

        public void PushAlliesAwayFrom(BoardUnit sourceUnit)
        {
            if (sourceUnit == null) return;

            PlayerSide side = sourceUnit.Owner;
            int sourceSlot = sourceUnit.SlotIndex;

            List<BoardUnit> leftGroup = new List<BoardUnit>();
            List<BoardUnit> rightGroup = new List<BoardUnit>();

            for (int i = 0; i < Board.SlotsPerSide; i++)
            {
                if (i == sourceSlot) continue;

                BoardUnit unit = state.Board.GetUnit(side, i);
                if (unit == null) continue;

                if (i < sourceSlot)
                {
                    leftGroup.Add(unit);
                }
                else
                {
                    rightGroup.Add(unit);
                }
            }

            foreach (BoardUnit unit in leftGroup)
            {
                TryPushUnitOneSlot(side, unit, -1);
            }

            for (int i = rightGroup.Count - 1; i >= 0; i--)
            {
                TryPushUnitOneSlot(side, rightGroup[i], 1);
            }
        }

        private void TryPushUnitOneSlot(PlayerSide side, BoardUnit unit, int direction)
        {
            if (unit.HasKeyword(Keyword.Unmoving, state)) return;

            int fromSlot = unit.SlotIndex;
            int toSlot = fromSlot + direction;

            if (toSlot < 0 || toSlot >= Board.SlotsPerSide) return;
            if (state.Board.GetUnit(side, toSlot) != null) return;

            state.Board.RemoveUnit(side, fromSlot);
            state.Board.PlaceUnit(side, toSlot, unit);

            GrantCodyMoveBonusIfApplicable(unit);
            OnUnitRelocated(unit);
        }

        public bool SwapUnitSlots(BoardUnit unitA, BoardUnit unitB)
        {
            if (unitA == null || unitB == null || unitA == unitB) return false;
            if (unitA.HasKeyword(Keyword.Unmoving, state) || unitB.HasKeyword(Keyword.Unmoving, state)) return false;

            PlayerSide sideA = unitA.Owner;
            PlayerSide sideB = unitB.Owner;
            int slotA = unitA.SlotIndex;
            int slotB = unitB.SlotIndex;

            state.Board.RemoveUnit(sideA, slotA);
            state.Board.RemoveUnit(sideB, slotB);

            state.Board.PlaceUnit(sideA, slotB, unitA);
            state.Board.PlaceUnit(sideB, slotA, unitB);

            GrantCodyMoveBonusIfApplicable(unitA);
            GrantCodyMoveBonusIfApplicable(unitB);
            OnUnitRelocated(unitA);
            OnUnitRelocated(unitB);

            return true;
        }

        public bool PullUnitOpposite(BoardUnit sourceUnit, BoardUnit targetUnit)
        {
            if (sourceUnit == null || targetUnit == null) return false;
            if (sourceUnit.Owner == targetUnit.Owner) return false;
            if (targetUnit.HasKeyword(Keyword.Unmoving, state)) return false;

            int destinationSlot = sourceUnit.SlotIndex;
            PlayerSide targetSide = targetUnit.Owner;

            if (targetUnit.SlotIndex == destinationSlot)
            {
                return false;
            }

            BoardUnit occupant = state.Board.GetUnit(targetSide, destinationSlot);
            if (occupant != null)
            {
                return false;
            }

            state.Board.RemoveUnit(targetSide, targetUnit.SlotIndex);
            state.Board.PlaceUnit(targetSide, destinationSlot, targetUnit);

            OnUnitRelocated(targetUnit);

            return true;
        }

        private void GrantCodyMoveBonusIfApplicable(BoardUnit unit)
        {
            if (unit.Owner != state.ActivePlayer) return;

            Player owner = state.GetPlayer(unit.Owner);
            LeaderData ownerLeader = owner.Leader;

            if (ownerLeader != null && ownerLeader.MoveTemporaryAttackBonus > 0)
            {
                unit.Statuses.Add(new ActiveStatusEffect(StatusEffectType.TemporaryAttackNextAttack, 1, ownerLeader.MoveTemporaryAttackBonus));
            }
        }

        private void OnUnitRelocated(BoardUnit relocatedUnit)
        {
            if (relocatedUnit == null)
            {
                return;
            }

            state.RaiseUnitMoved(relocatedUnit);

            if (relocatedUnit.IsSilenced)
            {
                return;
            }

            PlayerSide relentlessSide = relocatedUnit.Owner.Opposite();
            BoardUnit relentlessUnit = state.Board.GetUnit(relentlessSide, relocatedUnit.SlotIndex);

            if (relentlessUnit == null || relentlessUnit.IsSilenced || !relentlessUnit.HasKeyword(Keyword.Relentless, state))
            {
                return;
            }

            if (relentlessUnit.HasKeyword(Keyword.Unmoving, state))
            {
                Debug.Log($"[MovementResolver] {relentlessUnit.SourceCard.CardName} has Relentless but is Unmoving — cannot follow.");
                return;
            }

            int relentlessFromSlot = relentlessUnit.SlotIndex;
            int relentlessToSlot = relocatedUnit.SlotIndex;

            if (relentlessFromSlot == relentlessToSlot)
            {
                return;
            }

            if (state.Board.GetUnit(relentlessSide, relentlessToSlot) != null)
            {
                Debug.Log($"[MovementResolver] {relentlessUnit.SourceCard.CardName}'s Relentless FAIL: slot {relentlessToSlot} is occupied.");
                return;
            }

            state.Board.RemoveUnit(relentlessSide, relentlessFromSlot);
            state.Board.PlaceUnit(relentlessSide, relentlessToSlot, relentlessUnit);

            Debug.Log($"[MovementResolver] {relentlessUnit.SourceCard.CardName}'s Relentless followed {relocatedUnit.SourceCard.CardName} from slot {relentlessFromSlot} to slot {relentlessToSlot}.");

            OnUnitRelocated(relentlessUnit);
        }

        public bool TrySlippyDodge(BoardUnit defender)
        {
            if (defender == null || defender.IsSilenced || !defender.HasKeyword(Keyword.Slippy, state))
            {
                return false;
            }

            if (defender.HasKeyword(Keyword.Unmoving, state))
            {
                Debug.Log($"[MovementResolver] {defender.SourceCard.CardName} has Slippy but is Unmoving — cannot dodge.");
                return false;
            }

            PlayerSide side = defender.Owner;
            int fromSlot = defender.SlotIndex;
            int leftSlot = fromSlot - 1;
            int rightSlot = fromSlot + 1;

            int destinationSlot;

            if (leftSlot >= 0 && state.Board.GetUnit(side, leftSlot) == null)
            {
                destinationSlot = leftSlot;
            }
            else if (rightSlot < Board.SlotsPerSide && state.Board.GetUnit(side, rightSlot) == null)
            {
                destinationSlot = rightSlot;
            }
            else
            {
                Debug.Log($"[MovementResolver] {defender.SourceCard.CardName}'s Slippy FAIL: both left and right slots are blocked or off-board — staying to take the hit.");
                return false;
            }

            state.Board.RemoveUnit(side, fromSlot);
            state.Board.PlaceUnit(side, destinationSlot, defender);

            Debug.Log($"[MovementResolver] {defender.SourceCard.CardName}'s Slippy dodged from slot {fromSlot} to slot {destinationSlot}.");

            OnUnitRelocated(defender);

            return true;
        }

        public bool CanMoveUnit(int fromSlot, int toSlot, bool ignoreMoveLimitAndCost = false)
        {
            PlayerSide side = state.ActivePlayer;
            BoardUnit unit = state.Board.GetUnit(side, fromSlot);

            if (unit == null) return false;
            if (unit.HasKeyword(Keyword.Unmoving, state)) return false;
            if (unit.IsStunned) return false;

            bool isNimble = unit.HasKeyword(Keyword.Nimble, state);

            if (!ignoreMoveLimitAndCost)
            {
                if (unit.PlacedThisTurn && !isNimble) return false;
                if (!isNimble && state.HasUsedMoveThisTurn) return false;
                if (isNimble && unit.HasMovedThisTurn) return false;

                Player mover = state.GetPlayer(side);
                int moveManaCost = mover.Leader != null ? mover.Leader.MoveManaCost : 0;
                if (mover.CurrentMana < moveManaCost) return false;
            }

            return IsMoveRangeLegal(side, unit, fromSlot, toSlot);
        }

        private bool IsMoveRangeLegal(PlayerSide side, BoardUnit unit, int fromSlot, int toSlot)
        {
            if (state.Board.GetUnit(side, toSlot) != null) return false;
            if (fromSlot == toSlot) return false;

            if (unit.HasKeyword(Keyword.Teleport, state))
            {
                Debug.Log($"[MovementResolver] {unit.SourceCard.CardName} is using Teleport — range check bypassed, moving {fromSlot} -> {toSlot}.");
                return true;
            }

            int distance = toSlot - fromSlot;
            int maxRange = unit.HasKeyword(Keyword.Agile, state) ? 2 : 1;

            if (System.Math.Abs(distance) > maxRange) return false;

            return IsUnblockedPathClear(side, fromSlot, toSlot);
        }

        public bool HasAvailableGrantedEnemyMove(PlayerSide controllingSide)
        {
            return FindAvailableEnemyMoveGranter(controllingSide) != null;
        }

        private BoardUnit FindAvailableEnemyMoveGranter(PlayerSide controllingSide)
        {
            for (int i = 0; i < Board.SlotsPerSide; i++)
            {
                BoardUnit unit = state.Board.GetUnit(controllingSide, i);

                if (unit == null) continue;
                if (!(unit.SourceCard is UnitCardData unitCard)) continue;
                if (!unitCard.GrantsEnemyUnitMove) continue;
                if (unit.HasUsedGrantedEnemyMoveThisTurn) continue;

                return unit;
            }

            return null;
        }

        public bool CanMoveEnemyUnitViaGrantedAbility(PlayerSide controllingSide, int fromSlot, int toSlot)
        {
            if (FindAvailableEnemyMoveGranter(controllingSide) == null) return false;

            PlayerSide enemySide = controllingSide.Opposite();
            BoardUnit unit = state.Board.GetUnit(enemySide, fromSlot);

            if (unit == null) return false;

            return IsMoveRangeLegal(enemySide, unit, fromSlot, toSlot);
        }

        public bool MoveEnemyUnitViaGrantedAbility(PlayerSide controllingSide, int fromSlot, int toSlot)
        {
            if (!CanMoveEnemyUnitViaGrantedAbility(controllingSide, fromSlot, toSlot)) return false;

            BoardUnit granter = FindAvailableEnemyMoveGranter(controllingSide);
            PlayerSide enemySide = controllingSide.Opposite();
            BoardUnit unit = state.Board.GetUnit(enemySide, fromSlot);

            state.Board.RemoveUnit(enemySide, fromSlot);
            state.Board.PlaceUnit(enemySide, toSlot, unit);

            granter.HasUsedGrantedEnemyMoveThisTurn = true;

            OnUnitRelocated(unit);

            return true;
        }
    }
}