using System.Collections.Generic;
using DDD.TNFY.TCG.Cards;
using DDD.TNFY.TCG.Effects;

namespace DDD.TNFY.TCG.Core
{
    public static class AITurnActionEnumerator
    {
        public static List<AITurnAction> EnumerateLegalActions(GameState state, PhaseManager phases, PlayerSide aiSide)
        {
            List<AITurnAction> actions = new List<AITurnAction>();

            if (phases.HasBlockingPendingTargetedEffect())
            {
                EnumeratePendingInteractionActions(state, aiSide, actions);
                return actions;
            }

            if (state.IsGameOver || state.CurrentPhase != TurnPhase.Action || state.ActivePlayer != aiSide)
            {
                return actions;
            }

            Player active = state.GetPlayer(aiSide);

            foreach (CardData card in active.Hand)
            {
                if (card is UnitCardData unitCard)
                {
                    EnumerateUnitPlacements(state, phases, aiSide, unitCard, actions);
                }
                else if (card is ItemCardData itemCard && itemCard.PrimaryEffect != null)
                {
                    EnumerateItemPlays(state, phases, aiSide, itemCard, actions);
                }
            }

            for (int slot = 0; slot < Board.SlotsPerSide; slot++)
            {
                if (phases.CanAttackWithUnit(slot))
                {
                    actions.Add(AITurnAction.AttackFrom(slot));
                }
            }

            for (int fromSlot = 0; fromSlot < Board.SlotsPerSide; fromSlot++)
            {
                for (int toSlot = 0; toSlot < Board.SlotsPerSide; toSlot++)
                {
                    if (fromSlot == toSlot)
                    {
                        continue;
                    }

                    if (phases.CanMoveUnit(fromSlot, toSlot))
                    {
                        actions.Add(AITurnAction.MoveFromTo(fromSlot, toSlot));
                    }
                }
            }

            actions.Add(AITurnAction.EndPhaseAction);

            return actions;
        }

        private static void EnumeratePendingInteractionActions(GameState state, PlayerSide aiSide, List<AITurnAction> actions)
        {
            if (state.PendingCardChoiceOptions != null)
            {
                foreach (CardData option in state.PendingCardChoiceOptions)
                {
                    actions.Add(AITurnAction.ResolveCardChoiceWith(option));
                }

                return;
            }

            if (state.PendingTargetedEffect != null)
            {
                foreach (EffectTarget candidate in EnumerateCandidateTargets(state, aiSide, state.PendingTargetedEffect.targetType))
                {
                    if (EffectTargeting.IsValidTarget(state.PendingTargetedEffect.targetType, candidate, state))
                    {
                        actions.Add(AITurnAction.ResolveTargetedEffectWith(candidate));
                    }
                }
            }
        }

        private static void EnumerateUnitPlacements(GameState state, PhaseManager phases, PlayerSide aiSide, UnitCardData unitCard, List<AITurnAction> actions)
        {
            for (int slot = 0; slot < Board.SlotsPerSide; slot++)
            {
                if (state.Board.GetUnit(aiSide, slot) != null)
                {
                    continue;
                }

                if (phases.CanPlayUnit(unitCard, slot))
                {
                    actions.Add(AITurnAction.PlayUnitAt(unitCard, slot));
                }
            }
        }

        private static void EnumerateItemPlays(GameState state, PhaseManager phases, PlayerSide aiSide, ItemCardData itemCard, List<AITurnAction> actions)
        {
            foreach (EffectTarget candidateTarget in EnumerateCandidateTargets(state, aiSide, itemCard.PrimaryEffect.targetType))
            {
                if (phases.CanPlayItem(itemCard, candidateTarget))
                {
                    actions.Add(AITurnAction.PlayItemAt(itemCard, candidateTarget));
                }
            }
        }

        public static IEnumerable<EffectTarget> EnumerateCandidateTargets(GameState state, PlayerSide aiSide, TargetType targetType)
        {
            if (targetType == TargetType.None || targetType == TargetType.Board || targetType == TargetType.Self
                || EffectTargeting.IsGroupTarget(targetType))
            {
                yield return EffectTarget.None;
                yield break;
            }

            if (targetType == TargetType.AllyLeader)
            {
                yield return EffectTarget.ForLeader(aiSide);
                yield break;
            }

            if (targetType == TargetType.EnemyLeader)
            {
                yield return EffectTarget.ForLeader(aiSide.Opposite());
                yield break;
            }

            for (int slot = 0; slot < Board.SlotsPerSide; slot++)
            {
                BoardUnit allyUnit = state.Board.GetUnit(aiSide, slot);
                if (allyUnit != null)
                {
                    yield return EffectTarget.ForUnit(allyUnit);
                }

                BoardUnit enemyUnit = state.Board.GetUnit(aiSide.Opposite(), slot);
                if (enemyUnit != null)
                {
                    yield return EffectTarget.ForUnit(enemyUnit);
                }
            }

            if (targetType == TargetType.AnyUnitOrLeader || targetType == TargetType.AnyEnemyUnitOrLeader)
            {
                yield return EffectTarget.ForLeader(aiSide.Opposite());
            }

            if (targetType == TargetType.AnyUnitOrLeader || targetType == TargetType.AnyAllyUnitOrLeader)
            {
                yield return EffectTarget.ForLeader(aiSide);
            }
        }
    }
}