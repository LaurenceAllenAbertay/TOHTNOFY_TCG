using System.Collections.Generic;
using DDD.TNFY.TCG.Cards;
using DDD.TNFY.TCG.Effects;

namespace DDD.TNFY.TCG.Core
{
    public static class AIHeuristics
    {
        private static readonly HashSet<EffectActionType> HarmfulActions = new HashSet<EffectActionType>
        {
            EffectActionType.StunUnit,
            EffectActionType.DealDamage,
            EffectActionType.BounceUnit,
            EffectActionType.ApplyDelayedKill,
            EffectActionType.SilenceUnit,
            EffectActionType.ApplyDecay,
            EffectActionType.PullUnitOpposite,
            EffectActionType.PushAlliesAway,
            EffectActionType.ReduceOpponentMana,
        };

        public const float WinScore = 1000000f;
        public const float LossScore = -1000000f;

        public static float EvaluateState(GameState state, PlayerSide aiSide)
        {
            if (state.IsGameOver)
            {
                if (state.Winner == aiSide) return WinScore;
                if (state.Winner == aiSide.Opposite()) return LossScore;
                return 0f;
            }

            PlayerSide enemySide = aiSide.Opposite();
            Player ai = state.GetPlayer(aiSide);
            Player enemy = state.GetPlayer(enemySide);

            float score = 0f;
            score += (ai.LeaderHealth - enemy.LeaderHealth) * 2f;
            score += EvaluateBoardPresence(state, aiSide) - EvaluateBoardPresence(state, enemySide);
            score += (ai.Hand.Count - enemy.Hand.Count) * 2f;
            score += ai.CurrentMana * 1.5f;

            return score;
        }

        private static float EvaluateBoardPresence(GameState state, PlayerSide side)
        {
            float total = 0f;

            foreach (BoardUnit unit in state.Board.GetUnits(side))
            {
                float unitValue = unit.GetCurrentAttack(state) + unit.CurrentHealth;
                total += unitValue;

                if (unit.HasKeyword(Keyword.Taunt, state))
                {
                    total += 4f;
                }

                if (IsHangingInLane(state, unit))
                {
                    total -= unitValue;
                }
            }

            return total;
        }

        private static bool IsHangingInLane(GameState state, BoardUnit unit)
        {
            BoardUnit opposing = state.Board.GetOpponentUnit(unit.Owner, unit.SlotIndex);

            if (opposing == null)
            {
                return false;
            }

            int opposingAttack = opposing.GetCurrentAttack(state);
            int ourAttack = unit.GetCurrentAttack(state);

            bool theyKillUs = opposingAttack >= unit.CurrentHealth;
            bool weKillThem = ourAttack >= opposing.CurrentHealth;

            return theyKillUs && !weKillThem;
        }

        public static float ScoreAction(GameState state, PlayerSide aiSide, AITurnAction action)
        {
            switch (action.Kind)
            {
                case AITurnActionKind.PlayUnit:
                    return ScoreUnitPlacement(state, aiSide, action.UnitCard, action.SlotIndex);

                case AITurnActionKind.PlayItem:
                    return ScoreTargetedAction(aiSide, action.ItemCard.ManaCost * 10f, action.ItemCard.PrimaryEffect, action.Target);

                case AITurnActionKind.ResolveTargetedEffect:
                    return state.PendingTargetedEffect != null
                        ? ScoreTargetedAction(aiSide, 0f, state.PendingTargetedEffect, action.Target)
                        : 0f;

                case AITurnActionKind.Attack:
                    return ScoreAttack(state, aiSide, action.SlotIndex);

                case AITurnActionKind.Move:
                    return ScoreMove(state, aiSide, action.FromSlot, action.ToSlot);

                case AITurnActionKind.ResolveCardChoice:
                    return action.ChosenCard != null ? action.ChosenCard.ManaCost * 10f : 0f;

                case AITurnActionKind.EndPhase:
                    return 0f;

                default:
                    return 0f;
            }
        }

        private static float ScoreUnitPlacement(GameState state, PlayerSide aiSide, UnitCardData unitCard, int slot)
        {
            int cost = AuraCalculator.GetUnitCost(unitCard, state.GetPlayer(aiSide));
            float score = cost * 10f;

            BoardUnit opposing = state.Board.GetOpponentUnit(aiSide, slot);

            if (opposing == null)
            {
                score += unitCard.Attack * 2f;
            }
            else
            {
                int opposingAttack = opposing.GetCurrentAttack(state);
                bool weKillThem = unitCard.Attack >= opposing.CurrentHealth;
                bool theyKillUs = opposingAttack >= unitCard.Health;

                if (weKillThem && !theyKillUs)
                {
                    score += 30f;
                }
                else if (weKillThem && theyKillUs)
                {
                    score += 8f;
                }
                else if (!weKillThem && theyKillUs)
                {
                    score -= 25f;
                }
                else
                {
                    score += unitCard.Attack - opposingAttack;
                }
            }

            if (unitCard.HasKeyword(Keyword.Taunt))
            {
                score += 4f;
            }

            return score;
        }

        private static float ScoreTargetedAction(PlayerSide aiSide, float baseScore, CardEffect effect, EffectTarget target)
        {
            float score = baseScore;
            bool isHarmful = HarmfulActions.Contains(effect.action);

            bool targetIsEnemy = (target.Kind == EffectTargetKind.Unit && target.Unit.Owner != aiSide)
                || (target.Kind == EffectTargetKind.Leader && target.LeaderSide != aiSide);

            bool targetIsAlly = (target.Kind == EffectTargetKind.Unit && target.Unit.Owner == aiSide)
                || (target.Kind == EffectTargetKind.Leader && target.LeaderSide == aiSide);

            if (isHarmful && targetIsEnemy)
            {
                score += 15f + effect.amount;
            }
            else if (!isHarmful && targetIsAlly)
            {
                score += 15f + effect.amount;
            }
            else
            {
                score -= 20f;
            }

            return score;
        }

        private static float ScoreAttack(GameState state, PlayerSide aiSide, int slot)
        {
            BoardUnit attacker = state.Board.GetUnit(aiSide, slot);
            return attacker == null ? float.NegativeInfinity : ScoreLaneForUnit(state, aiSide, attacker, slot);
        }

        private static float ScoreMove(GameState state, PlayerSide aiSide, int fromSlot, int toSlot)
        {
            BoardUnit unit = state.Board.GetUnit(aiSide, fromSlot);

            if (unit == null)
            {
                return float.NegativeInfinity;
            }

            float currentLaneScore = ScoreLaneForUnit(state, aiSide, unit, fromSlot);
            float destinationLaneScore = ScoreLaneForUnit(state, aiSide, unit, toSlot);
            float moveManaCost = state.GetPlayer(aiSide).Leader != null ? state.GetPlayer(aiSide).Leader.MoveManaCost : 0;

            return (destinationLaneScore - currentLaneScore) - moveManaCost;
        }

        private static float ScoreLaneForUnit(GameState state, PlayerSide aiSide, BoardUnit unit, int slot)
        {
            BoardUnit opposing = state.Board.GetOpponentUnit(aiSide, slot);
            int ourAttack = unit.GetCurrentAttack(state);

            if (opposing == null)
            {
                return 20f + ourAttack;
            }

            int opposingAttack = opposing.GetCurrentAttack(state);
            bool weKillThem = ourAttack >= opposing.CurrentHealth;
            bool theyKillUs = opposingAttack >= unit.CurrentHealth;

            if (weKillThem && !theyKillUs)
            {
                return 30f;
            }

            if (theyKillUs && !weKillThem)
            {
                return -30f;
            }

            if (weKillThem && theyKillUs)
            {
                return 5f;
            }

            return ourAttack - opposingAttack;
        }
    }
}