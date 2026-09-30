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
            GameState state = context.GameState;
            Player owner = state.GetPlayer(context.SourceOwner);
            Player opponent = state.GetPlayer(context.SourceOwner.Opposite());
            EffectTarget target = context.ChosenTarget;
            BoardUnit unit = target.Kind == EffectTargetKind.Unit ? target.Unit : null;
            BoardUnit source = context.SourceUnit;

            switch (effect.action)
            {
                case EffectActionType.DrawCard:
                    for (int i = 0; i < effect.amount; i++)
                    {
                        CardData drawn = owner.DrawCard(out bool addedToHand);

                        if (drawn != null && !addedToHand)
                        {
                            state.RaiseCardBurnAnimationRequested(drawn, owner.Side);
                        }
                    }
                    break;

                case EffectActionType.GainMana:
                    owner.CurrentMana += effect.amount;
                    break;

                case EffectActionType.StunUnit:
                    unit?.Statuses.Add(new ActiveStatusEffect(StatusEffectType.Stunned, 1));
                    break;

                case EffectActionType.HealTarget:
                    if (unit != null)
                    {
                        phases.HealUnit(unit, effect.amount);
                    }
                    else if (target.Kind == EffectTargetKind.Leader)
                    {
                        phases.HealLeader(target.LeaderSide, effect.amount);
                    }
                    break;

                case EffectActionType.BuffAttack:
                    if (unit != null)
                    {
                        unit.BonusAttack += effect.amount;
                    }
                    break;

                case EffectActionType.AddTemporaryAttack:
                    unit?.Statuses.Add(new ActiveStatusEffect(StatusEffectType.TemporaryAttack, 1, effect.amount));
                    break;

                case EffectActionType.BuffMaxHealth:
                    if (unit != null)
                    {
                        unit.MaxHealth += effect.amount;
                        unit.CurrentHealth += effect.amount;
                        phases.SyncQualifyingEnemyAuraHealth();
                    }
                    break;

                case EffectActionType.GrantDoubleAttack:
                    unit?.Statuses.Add(new ActiveStatusEffect(StatusEffectType.DoubleAttackNextAttack, 1));
                    break;

                case EffectActionType.ApplyDelayedKill:
                    unit?.Statuses.Add(new ActiveStatusEffect(StatusEffectType.DelayedKill, effect.amount, 0, context.SourceOwner));
                    break;

                case EffectActionType.BounceUnit:
                    if (unit != null)
                    {
                        phases.BounceUnit(unit);
                    }
                    break;

                case EffectActionType.SilenceUnit:
                    phases.SilenceUnit(unit, Mathf.Max(1, effect.amount));
                    break;

                case EffectActionType.SwapAttackAndHealth:
                    phases.SwapAttackAndHealth(unit);
                    break;

                case EffectActionType.GrantRush:
                    unit?.GrantKeyword(Keyword.Rush);
                    break;

                case EffectActionType.GrantKeyword:
                    unit?.GrantKeyword(effect.keyword);
                    break;

                case EffectActionType.ApplyShield:
                    if (unit != null)
                    {
                        unit.Statuses.Add(new ActiveStatusEffect(StatusEffectType.Shield, 1));
                    }
                    else if (target.Kind == EffectTargetKind.Leader)
                    {
                        state.GetPlayer(target.LeaderSide).Statuses.Add(new ActiveStatusEffect(StatusEffectType.Shield, 1));
                    }
                    break;

                case EffectActionType.DealDamage:
                    if (unit != null)
                    {
                        phases.DamageUnit(unit, effect.amount, context.SourceOwner);
                    }
                    else if (target.Kind == EffectTargetKind.Leader)
                    {
                        phases.DamageLeader(target.LeaderSide, effect.amount);
                    }
                    break;

                case EffectActionType.ReduceOpponentMana:
                    opponent.Statuses.Add(new ActiveStatusEffect(StatusEffectType.OpponentManaReduction, 1, effect.amount));
                    break;

                case EffectActionType.SpawnUnit:
                    if (target.Kind == EffectTargetKind.Slot && effect.relevantCard is UnitCardData spawnCard)
                    {
                        phases.SpawnUnit(target.SlotSide, target.SlotIndex, spawnCard);
                    }
                    break;

                case EffectActionType.MoveAllyUnit:
                    state.HasPendingFreeMove = true;
                    state.PendingFreeMoveExcludedUnit = source;
                    break;

                case EffectActionType.MoveUnitToUnblockedSlot:
                    if (unit != null && phases.HasAnyLegalUnblockedSlot(unit.Owner, unit.SlotIndex))
                    {
                        state.HasPendingEnemyMoveGrantOnPlay = true;
                        state.PendingEnemyMoveGrantTarget = unit;
                    }
                    break;

                case EffectActionType.SwapUnitSlot:
                    phases.SwapUnitSlots(source, unit);
                    break;

                case EffectActionType.PullUnitOpposite:
                    phases.PullUnitOpposite(source, unit);
                    break;

                case EffectActionType.ApplyDecay:
                    for (int i = 0; unit != null && i < Mathf.Max(1, effect.amount); i++)
                    {
                        unit.Statuses.Add(new ActiveStatusEffect(StatusEffectType.Decaying, 1, 1, context.SourceOwner));
                    }
                    break;

                case EffectActionType.HealSelfByDamageDealt:
                    if (source != null && runtimeAmount != null)
                    {
                        phases.HealUnit(source, runtimeAmount.Value);
                    }
                    break;

                case EffectActionType.GrantNextItemDoubled:
                    owner.HasNextItemDoubled = true;
                    break;

                case EffectActionType.PushAlliesAway:
                    phases.PushAlliesAwayFrom(source);
                    break;

                case EffectActionType.HookClosestAllyLeft:
                    phases.HookClosestAllyLeft(source);
                    break;

                case EffectActionType.AddCardToHand:
                    if (effect.relevantCard == null || target.Kind != EffectTargetKind.Leader)
                    {
                        break;
                    }

                    Player recipient = state.GetPlayer(target.LeaderSide);

                    for (int i = 0; i < Mathf.Max(1, effect.amount); i++)
                    {
                        if (!recipient.TryAddCardToHand(effect.relevantCard))
                        {
                            state.RaiseCardBurnAnimationRequested(effect.relevantCard, recipient.Side);
                        }
                        else if (effect.trigger == EffectTriggerType.OnGameStart)
                        {
                            recipient.GameStartBonusCards.Add(effect.relevantCard);
                        }
                    }
                    break;

                case EffectActionType.StealRandomCard:
                    if (opponent.Hand.Count == 0)
                    {
                        break;
                    }

                    CardData stolen = opponent.Hand[rng.Next(opponent.Hand.Count)];
                    opponent.Hand.Remove(stolen);

                    if (!owner.TryAddCardToHand(stolen))
                    {
                        state.RaiseCardBurnAnimationRequested(stolen, owner.Side);
                    }
                    break;

                case EffectActionType.TransformCard:
                    phases.TransformUnit(unit, effect.relevantCard as UnitCardData);
                    break;

                case EffectActionType.RandomizeStats:
                    if (unit == null)
                    {
                        break;
                    }

                    int newAttack = rng.Next(effect.randomizeMin, effect.randomizeMax + 1);
                    int newHealth = rng.Next(effect.randomizeMin, effect.randomizeMax + 1);

                    unit.BonusAttack = newAttack - unit.SourceCard.Attack;
                    unit.MaxHealth = newHealth;
                    unit.CurrentHealth = newHealth;
                    unit.LastSyncedAuraHealthBonus = AuraCalculator.GetQualifyingEnemyAuraHealthBonus(unit, state);
                    phases.SyncQualifyingEnemyAuraHealth();
                    break;
            }
        }
    }
}