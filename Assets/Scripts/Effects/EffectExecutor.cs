using DDD.TNFY.TCG.Cards;
using DDD.TNFY.TCG.Core;
using UnityEngine;

namespace DDD.TNFY.TCG.Effects
{
    public static class EffectExecutor
    {
        private static readonly System.Random rng = new System.Random();

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

                case EffectActionType.AddTemporaryAttack:
                    ExecuteAddTemporaryAttack(effect, context);
                    break;

                case EffectActionType.BuffMaxHealth:
                    ExecuteBuffMaxHealth(effect, context, phases);
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

                case EffectActionType.SilenceUnit:
                    ExecuteSilenceUnit(effect, context, phases);
                    break;

                case EffectActionType.SwapAttackAndHealth:
                    ExecuteSwapAttackAndHealth(context, phases);
                    break;

                case EffectActionType.GrantRush:
                    ExecuteGrantRush(context);
                    break;

                case EffectActionType.GrantKeyword:
                    ExecuteGrantKeyword(effect, context);
                    break;

                case EffectActionType.ApplyShield:
                    ExecuteApplyShield(effect, context);
                    break;

                case EffectActionType.DealDamage:
                    ExecuteDealDamage(effect, context, phases);
                    break;

                case EffectActionType.ReduceOpponentMana:
                    ExecuteReduceOpponentMana(effect, context);
                    break;

                case EffectActionType.MoveAllyUnit:
                    ExecuteMoveAllyUnit(context);
                    break;

                case EffectActionType.SwapUnitSlot:
                    ExecuteSwapUnitSlot(context, phases);
                    break;

                case EffectActionType.PullUnitOpposite:
                    ExecutePullUnitOpposite(context, phases);
                    break;

                case EffectActionType.ApplyDecay:
                    ExecuteApplyDecay(effect, context);
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

                case EffectActionType.StealRandomCard:
                    ExecuteStealRandomCard(context);
                    break;

                case EffectActionType.TransformCard:
                    ExecuteTransformCard(effect, context, phases);
                    break;

                case EffectActionType.HookClosestAllyLeft:
                    ExecuteHookClosestAllyLeft(context, phases);
                    break;

                case EffectActionType.RandomizeStats:
                    ExecuteRandomizeStats(effect, context, phases);
                    break;

                case EffectActionType.MoveUnitToUnblockedSlot:
                    ExecuteMoveUnitToUnblockedSlot(context, phases);
                    break;

                case EffectActionType.SpawnUnit:
                    ExecuteSpawnUnit(effect, context, phases);
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

        private static void ExecuteAddTemporaryAttack(CardEffect effect, EffectContext context)
        {
            if (context.ChosenTarget.Kind != EffectTargetKind.Unit)
            {
                return;
            }

            context.ChosenTarget.Unit.Statuses.Add(new ActiveStatusEffect(StatusEffectType.TemporaryAttackThisTurn, 1, effect.amount));
        }

        private static void ExecuteBuffMaxHealth(CardEffect effect, EffectContext context, PhaseManager phases)
        {
            if (context.ChosenTarget.Kind != EffectTargetKind.Unit)
            {
                return;
            }

            BoardUnit unit = context.ChosenTarget.Unit;
            unit.MaxHealth += effect.amount;
            unit.CurrentHealth += effect.amount;

            phases.SyncQualifyingEnemyAuraHealth();
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

        private static void ExecuteSilenceUnit(CardEffect effect, EffectContext context, PhaseManager phases)
        {
            if (context.ChosenTarget.Kind != EffectTargetKind.Unit)
            {
                return;
            }

            int duration = System.Math.Max(1, effect.amount);
            phases.SilenceUnit(context.ChosenTarget.Unit, duration);
        }

        private static void ExecuteSwapAttackAndHealth(EffectContext context, PhaseManager phases)
        {
            if (context.ChosenTarget.Kind != EffectTargetKind.Unit)
            {
                return;
            }

            phases.SwapAttackAndHealth(context.ChosenTarget.Unit);
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

        private static void ExecuteApplyShield(CardEffect effect, EffectContext context)
        {
            if (context.ChosenTarget.Kind == EffectTargetKind.Unit)
            {
                context.ChosenTarget.Unit.Statuses.Add(new ActiveStatusEffect(StatusEffectType.Shield, 1));
                return;
            }

            if (context.ChosenTarget.Kind == EffectTargetKind.Leader)
            {
                Player player = context.GameState.GetPlayer(context.ChosenTarget.LeaderSide);
                player.Statuses.Add(new ActiveStatusEffect(StatusEffectType.Shield, 1));
            }
        }

        private static void ExecuteDealDamage(CardEffect effect, EffectContext context, PhaseManager phases)
        {
            if (context.ChosenTarget.Kind == EffectTargetKind.Unit)
            {
                phases.DamageUnit(context.ChosenTarget.Unit, effect.amount, context.SourceOwner, DamageSourceType.Effect);
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

        private static void ExecuteSpawnUnit(CardEffect effect, EffectContext context, PhaseManager phases)
        {
            if (context.ChosenTarget.Kind != EffectTargetKind.Slot)
            {
                return;
            }

            if (!(effect.relevantCard is UnitCardData spawnCard))
            {
                Debug.Log("[EffectExecutor] SpawnUnit FAIL: effect has no UnitCardData set as relevantCard.");
                return;
            }

            phases.SpawnUnit(context.ChosenTarget.SlotSide, context.ChosenTarget.SlotIndex, spawnCard);
        }

        private static void ExecuteMoveAllyUnit(EffectContext context)
        {
            context.GameState.HasPendingFreeMove = true;
            context.GameState.PendingFreeMoveExcludedUnit = context.SourceUnit;
            Debug.Log($"[EffectExecutor] Pending free move granted, excluding {context.SourceUnit?.SourceCard?.CardName}");
        }

        private static void ExecuteMoveUnitToUnblockedSlot(EffectContext context, PhaseManager phases)
        {
            if (context.ChosenTarget.Kind != EffectTargetKind.Unit)
            {
                Debug.Log("[EffectExecutor] MoveUnitToUnblockedSlot: no unit found — fizzling.");
                return;
            }

            BoardUnit target = context.ChosenTarget.Unit;

            if (!phases.HasAnyLegalUnblockedSlot(target.Owner, target.SlotIndex))
            {
                Debug.Log($"[EffectExecutor] MoveUnitToUnblockedSlot: {target.SourceCard.CardName} has no legal unblocked slot to move to — fizzling.");
                return;
            }

            context.GameState.HasPendingEnemyMoveGrantOnPlay = true;
            context.GameState.PendingEnemyMoveGrantTarget = target;

            Debug.Log($"[EffectExecutor] MoveUnitToUnblockedSlot: pending grant armed for {target.SourceCard.CardName} at slot {target.SlotIndex}.");
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

        private static void ExecuteApplyDecay(CardEffect effect, EffectContext context)
        {
            if (context.ChosenTarget.Kind != EffectTargetKind.Unit)
            {
                return;
            }

            int stacksToApply = System.Math.Max(1, effect.amount);

            for (int i = 0; i < stacksToApply; i++)
            {
                context.ChosenTarget.Unit.Statuses.Add(new ActiveStatusEffect(StatusEffectType.Decaying, 1, 1, context.SourceOwner));
            }
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

        private static void ExecuteHookClosestAllyLeft(EffectContext context, PhaseManager phases)
        {
            if (context.SourceUnit == null)
            {
                return;
            }

            phases.HookClosestAllyLeft(context.SourceUnit);
        }

        private static void ExecuteRandomizeStats(CardEffect effect, EffectContext context, PhaseManager phases)
        {
            if (context.ChosenTarget.Kind != EffectTargetKind.Unit)
            {
                return;
            }

            BoardUnit unit = context.ChosenTarget.Unit;

            int newAttack = rng.Next(effect.randomizeMin, effect.randomizeMax + 1);
            int newHealth = rng.Next(effect.randomizeMin, effect.randomizeMax + 1);

            unit.BonusAttack = newAttack - unit.SourceCard.Attack;
            unit.MaxHealth = newHealth;
            unit.CurrentHealth = newHealth;
            unit.LastSyncedAuraHealthBonus = AuraCalculator.GetQualifyingEnemyAuraHealthBonus(unit, context.GameState);

            Debug.Log($"[EffectExecutor] RandomizeStats: {unit.SourceCard.CardName} (slot {unit.SlotIndex}) rolled Attack={newAttack}, Health={newHealth}.");

            phases.SyncQualifyingEnemyAuraHealth();
        }

        private static void ExecuteAddCardToHand(CardEffect effect, EffectContext context)
        {
            if (effect.relevantCard == null)
            {
                return;
            }

            if (context.ChosenTarget.Kind != EffectTargetKind.Leader)
            {
                Debug.Log($"[EffectExecutor] AddCardToHand FAIL: {effect.relevantCard.CardName}'s effect needs targetType AllyLeader or EnemyLeader, but resolved target.Kind={context.ChosenTarget.Kind} — fizzling.");
                return;
            }

            PlayerSide recipientSide = context.ChosenTarget.LeaderSide;
            Player recipient = context.GameState.GetPlayer(recipientSide);

            int copies = Mathf.Max(1, effect.amount);

            for (int i = 0; i < copies; i++)
            {
                if (!recipient.TryAddCardToHand(effect.relevantCard))
                {
                    Debug.Log($"[EffectExecutor] {effect.relevantCard.CardName} could not be added — {recipient.Side}'s hand is already at the {Player.AbsoluteMaxHandSize}-card max, card is burned.");
                    continue;
                }

                if (effect.trigger == EffectTriggerType.OnGameStart)
                {
                    recipient.GameStartBonusCards.Add(effect.relevantCard);
                    Debug.Log($"[EffectExecutor] {effect.relevantCard.CardName} added to {recipient.Side}'s hand via OnGameStart - marking it exempt from mulligan.");
                }
            }
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

            CardData stolen = victim.Hand[rng.Next(victim.Hand.Count)];
            victim.Hand.Remove(stolen);

            if (!thief.TryAddCardToHand(stolen))
            {
                Debug.Log($"[EffectExecutor] {stolen.CardName} was stolen but burned — {thief.Side}'s hand is already at the {Player.AbsoluteMaxHandSize}-card max.");
            }
        }
    }
}