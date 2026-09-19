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

        private const float ManaCostValueWeight = 0.5f;

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
                float unitValue = GetUnitValue(state, unit);
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

        private static float GetUnitValue(GameState state, BoardUnit unit)
        {
            return unit.GetCurrentAttack(state) + unit.CurrentHealth + GetUnitManaCost(state, unit) * ManaCostValueWeight;
        }

        private static int GetUnitManaCost(GameState state, BoardUnit unit)
        {
            if (AIOpponentReplyModel.IsAbstractUnit(unit))
            {
                return AIOpponentReplyModel.EstimateManaCostForStats(unit.GetCurrentAttack(state), unit.GetEffectiveMaxHealth(state));
            }

            return unit.SourceCard.ManaCost;
        }

        private static bool IsBlockedByTaunt(GameState state, BoardUnit defender)
        {
            return defender != null && defender.HasKeyword(Keyword.Taunt, state);
        }

        private static bool HitsLeaderDirectly(GameState state, BoardUnit attacker, BoardUnit defender)
        {
            return attacker.HasKeyword(Keyword.Piercing, state) && !IsBlockedByTaunt(state, defender);
        }

        private static bool IsHangingInLane(GameState state, BoardUnit unit)
        {
            BoardUnit opposing = state.Board.GetOpponentUnit(unit.Owner, unit.SlotIndex);

            if (opposing == null)
            {
                return false;
            }

            bool theyKillUs = !HitsLeaderDirectly(state, opposing, unit) && opposing.GetCurrentAttack(state) >= unit.CurrentHealth;
            bool weKillThem = !HitsLeaderDirectly(state, unit, opposing) && unit.GetCurrentAttack(state) >= opposing.CurrentHealth;

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

                case AITurnActionKind.PlaceAbstractUnit:
                    return ScoreAbstractPlacement(state, aiSide, action);

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
            bool newUnitPiercesPast = unitCard.HasKeyword(Keyword.Piercing) && !IsBlockedByTaunt(state, opposing);

            if (opposing == null)
            {
                score += unitCard.Attack * 2f;
            }
            else
            {
                int opposingAttack = opposing.GetCurrentAttack(state);
                bool opposingPiercesPast = opposing.HasKeyword(Keyword.Piercing, state) && !unitCard.HasKeyword(Keyword.Taunt);
                bool theyKillUs = !opposingPiercesPast && opposingAttack >= unitCard.Health;
                bool weKillThem = !newUnitPiercesPast && unitCard.Attack >= opposing.CurrentHealth;
                float killBonus = GetUnitManaCost(state, opposing) * ManaCostValueWeight;

                if (newUnitPiercesPast)
                {
                    score += unitCard.Attack * 2f;

                    if (theyKillUs)
                    {
                        score -= 25f;
                    }
                }
                else if (weKillThem && !theyKillUs)
                {
                    score += 30f + killBonus;
                }
                else if (weKillThem && theyKillUs)
                {
                    score += 8f + killBonus;
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

        private static float ScoreAbstractPlacement(GameState state, PlayerSide side, AITurnAction action)
        {
            if (!AIOpponentReplyModel.TryGetAbstractStats(state, side, action.SlotIndex, action.AbstractCategory,
                    out int attack, out int _, out int manaCost))
            {
                return float.NegativeInfinity;
            }

            float score = manaCost * 10f;

            switch (action.AbstractCategory)
            {
                case AIAbstractUnitCategory.OpenLane:
                    score += attack * 2f;
                    break;

                case AIAbstractUnitCategory.Threat:
                    score += 30f;
                    break;

                case AIAbstractUnitCategory.Chump:
                    score -= 25f;
                    break;

                case AIAbstractUnitCategory.Wall:
                    BoardUnit facing = state.Board.GetOpponentUnit(side, action.SlotIndex);
                    score += attack - (facing != null ? facing.GetCurrentAttack(state) : 0);
                    break;
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
            bool theyKillUs = !HitsLeaderDirectly(state, opposing, unit) && opposingAttack >= unit.CurrentHealth;

            if (HitsLeaderDirectly(state, unit, opposing))
            {
                return 20f + ourAttack - (theyKillUs ? 30f : 0f);
            }

            bool weKillThem = ourAttack >= opposing.CurrentHealth;
            float killBonus = GetUnitManaCost(state, opposing) * ManaCostValueWeight;

            if (weKillThem && !theyKillUs)
            {
                return 30f + killBonus;
            }

            if (theyKillUs && !weKillThem)
            {
                return -30f;
            }

            if (weKillThem && theyKillUs)
            {
                return 5f + killBonus;
            }

            return ourAttack - opposingAttack;
        }
    }
}