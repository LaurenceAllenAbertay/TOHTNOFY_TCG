using UnityEngine;

namespace DDD.TNFY.TCG.Core
{
    public static class AITurnActionApplier
    {
        public static bool Apply(AITurnAction action, PhaseManager phases)
        {
            switch (action.Kind)
            {
                case AITurnActionKind.PlayUnit:
                    return phases.TryPlayUnit(action.UnitCard, action.SlotIndex);

                case AITurnActionKind.PlayItem:
                    return phases.TryPlayItem(action.ItemCard, action.Target);

                case AITurnActionKind.Attack:
                    return phases.TryAttackWithUnit(action.SlotIndex);

                case AITurnActionKind.Move:
                    return phases.TryMoveUnit(action.FromSlot, action.ToSlot);

                case AITurnActionKind.ResolveCardChoice:
                    return phases.TryResolvePendingCardChoice(action.ChosenCard);

                case AITurnActionKind.ResolveTargetedEffect:
                    return phases.TryResolvePendingTargetedEffect(action.Target);

                case AITurnActionKind.EndPhase:
                    phases.EndActionPhase();
                    return true;

                default:
                    Debug.LogWarning($"[AITurnActionApplier] Unhandled action kind {action.Kind}.");
                    return false;
            }
        }
    }
}