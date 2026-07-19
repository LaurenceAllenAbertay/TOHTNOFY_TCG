using System.Collections.Generic;
using DDD.TNFY.TCG.Cards;
using DDD.TNFY.TCG.Core;
using UnityEngine;

namespace DDD.TNFY.TCG.Effects
{
    public static class EffectExecutor
    {
        public static void Execute(CardEffect effect, EffectContext context, PhaseManager phases, int? runtimeAmount = null)
        {
            switch (effect.action)
            {
                case EffectActionType.DrawCard:
                    ExecuteDrawCard(effect, context);
                    break;

                case EffectActionType.GainMana:
                    ExecuteGainMana(effect, context);
                    break;

                case EffectActionType.StunUnit:
                    ExecuteStunUnit(effect, context);
                    break;

                case EffectActionType.HealTarget:
                    ExecuteHealTarget(effect, context, phases);
                    break;

                case EffectActionType.BuffAttack:
                    ExecuteBuffAttack(effect, context);
                    break;

                case EffectActionType.BuffMaxHealth:
                    ExecuteBuffMaxHealth(effect, context);
                    break;

                case EffectActionType.GrantDoubleAttack:
                    ExecuteGrantDoubleAttack(context);
                    break;

                case EffectActionType.ApplyDelayedKill:
                    ExecuteApplyDelayedKill(effect, context);
                    break;

                case EffectActionType.BounceUnit:
                    ExecuteBounceUnit(context, phases);
                    break;

                case EffectActionType.GrantRush:
                    ExecuteGrantRush(context);
                    break;

                case EffectActionType.GrantKeyword:
                    ExecuteGrantKeyword(effect, context);
                    break;

                case EffectActionType.ApplyLeaderDamageShield:
                    ExecuteApplyLeaderDamageShield(effect, context);
                    break;

                case EffectActionType.DealDamageToTarget:
                    ExecuteDealDamageToTarget(effect, context, phases);
                    break;

                case EffectActionType.ReduceOpponentMana:
                    ExecuteReduceOpponentMana(effect, context);
                    break;

                case EffectActionType.HealAdjacentUnits:
                    ExecuteHealAdjacentUnits(effect, context, phases);
                    break;

                case EffectActionType.MoveAllyUnit:
                    ExecuteMoveAllyUnit(context);
                    break;

                case EffectActionType.SwapUnitSlot:
                    ExecuteSwapUnitSlot(context, phases);
                    break;

                case EffectActionType.DealDamageToAllEnemyUnits:
                    ExecuteDealDamageToAllEnemyUnits(effect, context, phases);
                    break;

                case EffectActionType.PullUnitOpposite:
                    ExecutePullUnitOpposite(context, phases);
                    break;

                case EffectActionType.ApplyDecay:
                    ExecuteApplyDecay(context);
                    break;

                case EffectActionType.HealSelfByDamageDealt:
                    ExecuteHealSelfByDamageDealt(context, phases, runtimeAmount);
                    break;

                case EffectActionType.GrantNextItemDoubled:
                    ExecuteGrantNextItemDoubled(context);
                    break;

                case EffectActionType.PushAlliesAway:
                    ExecutePushAlliesAway(context, phases);
                    break;

                case EffectActionType.AddCardToHand:
                    ExecuteAddCardToHand(effect, context);
                    break;

                case EffectActionType.DamageOwnLeader:
                    ExecuteDamageOwnLeader(effect, context, phases);
                    break;

                case EffectActionType.StealRandomCard:
                    ExecuteStealRandomCard(context);
                    break;

                case EffectActionType.BounceUnitOpposite:
                    ExecuteBounceUnitOpposite(context, phases);
                    break;

                case EffectActionType.TransformCard:
                    ExecuteTransformCard(effect, context, phases);
                    break;
            }
        }

        private static void ExecuteDrawCard(CardEffect effect, EffectContext context)
        {
            Player owner = context.GameState.GetPlayer(context.SourceOwner);

            for (int i = 0; i < effect.amount; i++)
            {
                owner.DrawCard();
            }
        }

        private static void ExecuteGainMana(CardEffect effect, EffectContext context)
        {
            Player owner = context.GameState.GetPlayer(context.SourceOwner);
            owner.CurrentMana += effect.amount;
        }

        private static void ExecuteStunUnit(CardEffect effect, EffectContext context)
        {
            if (context.ChosenTarget.Kind != EffectTargetKind.Unit)
            {
                return;
            }

            context.ChosenTarget.Unit.Statuses.Add(new ActiveStatusEffect(StatusEffectType.Stunned, 1));
        }

        private static void ExecuteHealTarget(CardEffect effect, EffectContext context, PhaseManager phases)
        {
            if (context.ChosenTarget.Kind == EffectTargetKind.Unit)
            {
                phases.HealUnit(context.ChosenTarget.Unit, effect.amount);
            }
            else if (context.ChosenTarget.Kind == EffectTargetKind.Leader)
            {
                phases.HealLeader(context.ChosenTarget.LeaderSide, effect.amount);
            }
        }

        private static void ExecuteBuffAttack(CardEffect effect, EffectContext context)
        {
            if (context.ChosenTarget.Kind != EffectTargetKind.Unit)
            {
                return;
            }

            context.ChosenTarget.Unit.BonusAttack += effect.amount;
        }

        private static void ExecuteBuffMaxHealth(CardEffect effect, EffectContext context)
        {
            if (context.ChosenTarget.Kind != EffectTargetKind.Unit)
            {
                return;
            }

            BoardUnit unit = context.ChosenTarget.Unit;
            unit.MaxHealth += effect.amount;
            unit.CurrentHealth += effect.amount;
        }

        private static void ExecuteGrantDoubleAttack(EffectContext context)
        {
            if (context.ChosenTarget.Kind != EffectTargetKind.Unit)
            {
                return;
            }

            context.ChosenTarget.Unit.Statuses.Add(new ActiveStatusEffect(StatusEffectType.DoubleAttackNextAttack, 1));
        }

        private static void ExecuteApplyDelayedKill(CardEffect effect, EffectContext context)
        {
            if (context.ChosenTarget.Kind != EffectTargetKind.Unit)
            {
                return;
            }

            context.ChosenTarget.Unit.Statuses.Add(new ActiveStatusEffect(StatusEffectType.DelayedKill, effect.amount, sourceOwner: context.SourceOwner));
        }

        private static void ExecuteBounceUnit(EffectContext context, PhaseManager phases)
        {
            if (context.ChosenTarget.Kind != EffectTargetKind.Unit)
            {
                return;
            }

            phases.BounceUnit(context.ChosenTarget.Unit);
        }

        private static void ExecuteBounceUnitOpposite(EffectContext context, PhaseManager phases)
        {
            if (context.SourceUnit == null)
            {
                return;
            }

            phases.BounceUnitOpposite(context.SourceUnit);
        }

        private static void ExecuteGrantRush(EffectContext context)
        {
            if (context.ChosenTarget.Kind != EffectTargetKind.Unit)
            {
                return;
            }

            context.ChosenTarget.Unit.GrantKeyword(Keyword.Rush);
        }

        private static void ExecuteGrantKeyword(CardEffect effect, EffectContext context)
        {
            if (context.ChosenTarget.Kind != EffectTargetKind.Unit)
            {
                return;
            }

            context.ChosenTarget.Unit.GrantKeyword(effect.keyword);
        }

        private static void ExecuteApplyLeaderDamageShield(CardEffect effect, EffectContext context)
        {
            if (context.ChosenTarget.Kind != EffectTargetKind.Leader)
            {
                return;
            }

            Player player = context.GameState.GetPlayer(context.ChosenTarget.LeaderSide);
            player.Statuses.Add(new ActiveStatusEffect(StatusEffectType.LeaderDamageShield, 1));
        }

        private static void ExecuteDealDamageToTarget(CardEffect effect, EffectContext context, PhaseManager phases)
        {
            if (context.ChosenTarget.Kind == EffectTargetKind.Unit)
            {
                BoardUnit unit = context.ChosenTarget.Unit;
                unit.CurrentHealth -= effect.amount;

                if (unit.CurrentHealth <= 0)
                {
                    phases.KillUnit(unit, context.SourceOwner);
                }
            }
            else if (context.ChosenTarget.Kind == EffectTargetKind.Leader)
            {
                phases.DamageLeader(context.ChosenTarget.LeaderSide, effect.amount);
            }
        }

        private static void ExecuteReduceOpponentMana(CardEffect effect, EffectContext context)
        {
            Player opponent = context.GameState.GetPlayer(context.SourceOwner.Opposite());
            opponent.Statuses.Add(new ActiveStatusEffect(StatusEffectType.OpponentManaReduction, 1, effect.amount));
        }

        private static void ExecuteHealAdjacentUnits(CardEffect effect, EffectContext context, PhaseManager phases)
        {
            if (context.SourceUnit == null)
            {
                return;
            }

            int slotIndex = context.SourceUnit.SlotIndex;
            PlayerSide side = context.SourceUnit.Owner;

            BoardUnit leftNeighbor = slotIndex - 1 >= 0 ? context.Board.GetUnit(side, slotIndex - 1) : null;
            BoardUnit rightNeighbor = slotIndex + 1 < Board.SlotsPerSide ? context.Board.GetUnit(side, slotIndex + 1) : null;

            if (leftNeighbor != null)
            {
                phases.HealUnit(leftNeighbor, effect.amount);
            }

            if (rightNeighbor != null)
            {
                phases.HealUnit(rightNeighbor, effect.amount);
            }
        }

        private static void ExecuteMoveAllyUnit(EffectContext context)
        {
            context.GameState.HasPendingFreeMove = true;
            context.GameState.PendingFreeMoveExcludedUnit = context.SourceUnit;
            Debug.Log($"[EffectExecutor] Pending free move granted, excluding {context.SourceUnit?.SourceCard?.CardName}");
        }

        private static void ExecuteSwapUnitSlot(EffectContext context, PhaseManager phases)
        {
            if (context.SourceUnit == null)
            {
                return;
            }

            if (context.ChosenTarget.Kind != EffectTargetKind.Unit)
            {
                return;
            }

            phases.SwapUnitSlots(context.SourceUnit, context.ChosenTarget.Unit);
        }

        private static void ExecuteDealDamageToAllEnemyUnits(CardEffect effect, EffectContext context, PhaseManager phases)
        {
            PlayerSide enemySide = context.SourceOwner.Opposite();

            List<BoardUnit> targets = new List<BoardUnit>();

            for (int i = 0; i < Board.SlotsPerSide; i++)
            {
                BoardUnit unit = context.Board.GetUnit(enemySide, i);

                if (unit != null)
                {
                    targets.Add(unit);
                }
            }

            foreach (BoardUnit unit in targets)
            {
                unit.CurrentHealth -= effect.amount;

                if (unit.CurrentHealth <= 0)
                {
                    phases.KillUnit(unit, context.SourceOwner);
                }
            }
        }

        private static void ExecutePullUnitOpposite(EffectContext context, PhaseManager phases)
        {
            if (context.SourceUnit == null)
            {
                return;
            }

            if (context.ChosenTarget.Kind != EffectTargetKind.Unit)
            {
                return;
            }

            phases.PullUnitOpposite(context.SourceUnit, context.ChosenTarget.Unit);
        }

        private static void ExecuteApplyDecay(EffectContext context)
        {
            if (context.ChosenTarget.Kind != EffectTargetKind.Unit)
            {
                return;
            }

            context.ChosenTarget.Unit.Statuses.Add(new ActiveStatusEffect(StatusEffectType.Decaying, 1, 1, context.SourceOwner));
        }

        private static void ExecuteHealSelfByDamageDealt(EffectContext context, PhaseManager phases, int? runtimeAmount)
        {
            if (context.SourceUnit == null || runtimeAmount == null)
            {
                return;
            }

            phases.HealUnit(context.SourceUnit, runtimeAmount.Value);
        }

        private static void ExecuteGrantNextItemDoubled(EffectContext context)
        {
            Player owner = context.GameState.GetPlayer(context.SourceOwner);
            owner.HasNextItemDoubled = true;
        }

        private static void ExecutePushAlliesAway(EffectContext context, PhaseManager phases)
        {
            if (context.SourceUnit == null)
            {
                return;
            }

            phases.PushAlliesAwayFrom(context.SourceUnit);
        }

        private static void ExecuteAddCardToHand(CardEffect effect, EffectContext context)
        {
            if (effect.relevantCard == null)
            {
                return;
            }

            PlayerSide recipientSide = effect.targetsOwnHand ? context.SourceOwner : context.SourceOwner.Opposite();
            Player recipient = context.GameState.GetPlayer(recipientSide);

            if (!recipient.TryAddCardToHand(effect.relevantCard))
            {
                Debug.Log($"[EffectExecutor] {effect.relevantCard.CardName} could not be added — {recipient.Side}'s hand is already at the {Player.AbsoluteMaxHandSize}-card max, card is burned.");
            }
        }

        private static void ExecuteDamageOwnLeader(CardEffect effect, EffectContext context, PhaseManager phases)
        {
            phases.DamageLeader(context.SourceOwner, effect.amount);
        }

        private static void ExecuteTransformCard(CardEffect effect, EffectContext context, PhaseManager phases)
        {
            if (context.ChosenTarget.Kind != EffectTargetKind.Unit)
            {
                return;
            }

            if (!(effect.relevantCard is UnitCardData transformCard))
            {
                return;
            }

            phases.TransformUnit(context.ChosenTarget.Unit, transformCard);
        }

        private static void ExecuteStealRandomCard(EffectContext context)
        {
            Player thief = context.GameState.GetPlayer(context.SourceOwner);
            Player victim = context.GameState.GetPlayer(context.SourceOwner.Opposite());

            if (victim.Hand.Count == 0)
            {
                return;
            }

            System.Random rng = new System.Random();
            CardData stolen = victim.Hand[rng.Next(victim.Hand.Count)];
            victim.Hand.Remove(stolen);

            if (!thief.TryAddCardToHand(stolen))
            {
                Debug.Log($"[EffectExecutor] {stolen.CardName} was stolen but burned — {thief.Side}'s hand is already at the {Player.AbsoluteMaxHandSize}-card max.");
            }
        }
    }
}