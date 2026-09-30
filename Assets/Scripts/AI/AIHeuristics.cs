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

        private const float AttackValueWeight = 2f;
        private const float HealthValueWeight = 1f;
        private const float CardCostValueWeight = 1f;
        private const float SilencedCardCostFactor = 0.5f;
        private const float TauntValue = 4f;
        private const float HandCardBaseValue = 1f;
        private const float CastableUnitHandFactor = 0.7f;
        private const float LeaderHealthWeight = 1f;
        private const int LowLeaderHealthThreshold = 15;
        private const float LowLeaderHealthExtraWeight = 2f;
        private const float LeaderShieldValue = 3f;
        private const float HealthPaymentOrderingPenalty = 10f;

        public static float EvaluateState(GameState state, PlayerSide aiSide)
        {
            if (state.IsGameOver)
            {
                if (state.Winner == aiSide) return WinScore;
                if (state.Winner == aiSide.Opposite()) return LossScore;
                return 0f;
            }

            Player ai = state.GetPlayer(aiSide);
            Player enemy = state.GetPlayer(aiSide.Opposite());

            return LeaderTerm(ai, enemy) + BoardTerm(state, aiSide) + HandTerm(state, ai, enemy);
        }

        public static string DescribeEvaluation(GameState state, PlayerSide aiSide)
        {
            if (state.IsGameOver)
            {
                return $"game over (winner={state.Winner}) -> {EvaluateState(state, aiSide):F1}";
            }

            Player ai = state.GetPlayer(aiSide);
            Player enemy = state.GetPlayer(aiSide.Opposite());

            return $"total={EvaluateState(state, aiSide):F1} = leader {LeaderTerm(ai, enemy):F1} (AI {ai.LeaderHealth} vs enemy {enemy.LeaderHealth}) + board {BoardTerm(state, aiSide):F1} (AI {EvaluateBoardPresence(state, aiSide):F1} vs enemy {EvaluateBoardPresence(state, aiSide.Opposite()):F1}) + hand {HandTerm(state, ai, enemy):F1} (AI {ai.Hand.Count} vs enemy {enemy.Hand.Count} cards). activePlayer={state.ActivePlayer}, phase={state.CurrentPhase}.";
        }

        private static float LeaderTerm(Player ai, Player enemy)
        {
            return LeaderValue(ai) - LeaderValue(enemy);
        }

        private static float LeaderValue(Player player)
        {
            int health = player.LeaderHealth;
            float value = health * LeaderHealthWeight - System.Math.Max(0, LowLeaderHealthThreshold - health) * LowLeaderHealthExtraWeight;

            if (player.HasStatus(StatusEffectType.Shield))
            {
                value += LeaderShieldValue;
            }

            return value;
        }

        private static float BoardTerm(GameState state, PlayerSide aiSide)
        {
            return EvaluateBoardPresence(state, aiSide) - EvaluateBoardPresence(state, aiSide.Opposite());
        }

        private static float HandTerm(GameState state, Player ai, Player enemy)
        {
            return HandValue(state, ai) - HandValue(state, enemy);
        }

        private static float HandValue(GameState state, Player player)
        {
            bool canCastNow = state.ActivePlayer == player.Side;
            int manaLeft = player.CurrentMana;
            float total = 0f;

            foreach (CardData card in player.Hand)
            {
                float cardValue = HandCardBaseValue + card.ManaCost * CardCostValueWeight;

                if (canCastNow && card is UnitCardData unitCard)
                {
                    int cost = AuraCalculator.GetUnitCost(unitCard, player);
                    float castableValue = (unitCard.Attack * AttackValueWeight + unitCard.Health * HealthValueWeight + cost * CardCostValueWeight) * CastableUnitHandFactor;

                    if (cost <= manaLeft && castableValue > cardValue)
                    {
                        cardValue = castableValue;
                        manaLeft -= cost;
                    }
                }

                total += cardValue;
            }

            return total;
        }

        private static float EvaluateBoardPresence(GameState state, PlayerSide side)
        {
            float total = 0f;

            foreach (BoardUnit unit in state.Board.GetUnits(side))
            {
                if (side != state.ActivePlayer && IsHangingInLane(state, unit))
                {
                    continue;
                }

                total += GetUnitValue(state, unit);
            }

            return total;
        }

        private static float GetUnitValue(GameState state, BoardUnit unit)
        {
            float attackValue = unit.GetCurrentAttack(state) * AttackValueWeight * GetAttackLaneCount(state, unit);
            float healthValue = unit.CurrentHealth * HealthValueWeight;
            float cardValue = GetUnitManaCost(state, unit) * CardCostValueWeight * (unit.IsSilenced ? SilencedCardCostFactor : 1f);
            float tauntValue = unit.HasKeyword(Keyword.Taunt, state) ? TauntValue : 0f;

            return attackValue + healthValue + cardValue + tauntValue;
        }

        private static int GetAttackLaneCount(GameState state, BoardUnit unit)
        {
            if (!unit.HasKeyword(Keyword.BifurcatedAttack, state))
            {
                return 1;
            }

            return CountBifurcatedLanes(unit.SlotIndex);
        }

        private static int CountBifurcatedLanes(int slot)
        {
            int lanes = 0;

            if (slot - 1 >= 0) lanes++;
            if (slot + 1 < Board.SlotsPerSide) lanes++;

            return lanes;
        }

        private static int GetUnitManaCost(GameState state, BoardUnit unit)
        {
            return unit.SourceCard.ManaCost;
        }

        public static float GetPrintedStatValue(UnitCardData card)
        {
            return card.Attack * AttackValueWeight + card.Health * HealthValueWeight;
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

        public static bool RequiresHealthPayment(GameState state, PlayerSide side, AITurnAction action, out int healthCost)
        {
            healthCost = 0;
            Player player = state.GetPlayer(side);
            int cost;

            if (action.Kind == AITurnActionKind.PlayUnit)
            {
                cost = AuraCalculator.GetUnitCost(action.UnitCard, player);
            }
            else if (action.Kind == AITurnActionKind.PlayItem)
            {
                cost = action.ItemCard.ManaCost;
            }
            else
            {
                return false;
            }

            return AuraCalculator.TryGetHealthCostForManaShortfall(player, cost - player.CurrentMana, out healthCost);
        }

        public static float ScoreAction(GameState state, PlayerSide aiSide, AITurnAction action)
        {
            float healthPaymentPenalty = RequiresHealthPayment(state, aiSide, action, out int healthCost)
                ? healthCost * HealthPaymentOrderingPenalty
                : 0f;

            switch (action.Kind)
            {
                case AITurnActionKind.PlayUnit:
                    return ScoreUnitPlacement(state, aiSide, action.UnitCard, action.SlotIndex) - healthPaymentPenalty;

                case AITurnActionKind.PlayItem:
                    return ScoreTargetedAction(state, aiSide, action.ItemCard.ManaCost * 10f, action.ItemCard.PrimaryEffect, action.Target) - healthPaymentPenalty;

                case AITurnActionKind.ResolveTargetedEffect:
                    return state.PendingTargetedEffect != null
                        ? ScoreTargetedAction(state, aiSide, 0f, state.PendingTargetedEffect, action.Target)
                        : 0f;

                case AITurnActionKind.Attack:
                    return ScoreAttack(state, aiSide, action.SlotIndex);

                case AITurnActionKind.Move:
                    return ScoreMove(state, aiSide, action.FromSlot, action.ToSlot);

                case AITurnActionKind.ResolveCardChoice:
                    return action.ChosenCard != null ? action.ChosenCard.ManaCost * 10f : 0f;

                case AITurnActionKind.PlaceAbstractUnit:
                    return ScoreAbstractPlacement(state, aiSide, action);

                case AITurnActionKind.AbstractRemoval:
                    return ScoreAbstractRemoval(state, aiSide, action);

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

            if (unitCard.HasKeyword(Keyword.BifurcatedAttack))
            {
                score += ScoreBifurcatedPlacementLanes(state, aiSide, unitCard, slot);

                bool opposingKillsUs = opposing != null
                    && !(opposing.HasKeyword(Keyword.Piercing, state) && !unitCard.HasKeyword(Keyword.Taunt))
                    && opposing.GetCurrentAttack(state) >= unitCard.Health;

                if (opposingKillsUs)
                {
                    score -= 25f;
                }
            }
            else
            {
                score += ScoreSingleLanePlacement(state, unitCard, opposing);
            }

            if (unitCard.HasKeyword(Keyword.Taunt))
            {
                score += TauntValue;
            }

            return score;
        }

        private static float ScoreSingleLanePlacement(GameState state, UnitCardData unitCard, BoardUnit opposing)
        {
            bool newUnitPiercesPast = unitCard.HasKeyword(Keyword.Piercing) && !IsBlockedByTaunt(state, opposing);

            if (opposing == null)
            {
                return unitCard.Attack * 2f;
            }

            int opposingAttack = opposing.GetCurrentAttack(state);
            bool opposingPiercesPast = opposing.HasKeyword(Keyword.Piercing, state) && !unitCard.HasKeyword(Keyword.Taunt);
            bool theyKillUs = !opposingPiercesPast && opposingAttack >= unitCard.Health;
            bool weKillThem = !newUnitPiercesPast && unitCard.Attack >= opposing.CurrentHealth;
            float killBonus = GetUnitManaCost(state, opposing) * CardCostValueWeight;

            if (newUnitPiercesPast)
            {
                return unitCard.Attack * 2f - (theyKillUs ? 25f : 0f);
            }

            if (weKillThem && !theyKillUs)
            {
                return 30f + killBonus;
            }

            if (weKillThem && theyKillUs)
            {
                return 8f + killBonus;
            }

            if (!weKillThem && theyKillUs)
            {
                return -25f;
            }

            return unitCard.Attack - opposingAttack;
        }

        private static float ScoreBifurcatedPlacementLanes(GameState state, PlayerSide aiSide, UnitCardData unitCard, int slot)
        {
            float total = 0f;

            foreach (int lane in GetBifurcatedLanes(slot))
            {
                BoardUnit target = state.Board.GetOpponentUnit(aiSide, lane);

                if (target == null)
                {
                    total += unitCard.Attack * 2f;
                }
                else if (unitCard.Attack >= target.CurrentHealth)
                {
                    total += 15f + GetUnitManaCost(state, target) * CardCostValueWeight;
                }
            }

            return total;
        }

        private static IEnumerable<int> GetBifurcatedLanes(int slot)
        {
            if (slot - 1 >= 0) yield return slot - 1;
            if (slot + 1 < Board.SlotsPerSide) yield return slot + 1;
        }

        private static float ScoreAbstractPlacement(GameState state, PlayerSide side, AITurnAction action)
        {
            if (!AIOpponentReplyModel.TryGetReplyUnit(state, side, action.SlotIndex, action.AbstractCategory, out UnitCardData replyCard))
            {
                return float.NegativeInfinity;
            }

            int attack = replyCard.Attack;
            float score = replyCard.ManaCost * 10f;

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

        private static float ScoreAbstractRemoval(GameState state, PlayerSide side, AITurnAction action)
        {
            if (!AIOpponentReplyModel.TryGetReplyRemoval(state, side, action.SlotIndex, out BoardUnit target, out ItemCardData removalItem))
            {
                return float.NegativeInfinity;
            }

            return removalItem.ManaCost * 10f + 15f + GetUnitValue(state, target);
        }

        private static float ScoreTargetedAction(GameState state, PlayerSide aiSide, float baseScore, CardEffect effect, EffectTarget target)
        {
            float score = baseScore;

            bool targetIsEnemy = (target.Kind == EffectTargetKind.Unit && target.Unit.Owner != aiSide)
                || (target.Kind == EffectTargetKind.Leader && target.LeaderSide != aiSide);

            bool targetIsAlly = (target.Kind == EffectTargetKind.Unit && target.Unit.Owner == aiSide)
                || (target.Kind == EffectTargetKind.Leader && target.LeaderSide == aiSide);

            if (effect.action == EffectActionType.SwapAttackAndHealth && target.Kind == EffectTargetKind.Unit)
            {
                int attack = target.Unit.GetCurrentAttack(state);
                int health = target.Unit.CurrentHealth;
                bool raisesAttack = health > attack;
                bool helpsUs = (targetIsAlly && raisesAttack) || (targetIsEnemy && attack > health);

                return score + (helpsUs ? 15f + System.Math.Abs(health - attack) : -20f);
            }

            bool isHarmful = HarmfulActions.Contains(effect.action);

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
            if (!unit.HasKeyword(Keyword.BifurcatedAttack, state))
            {
                return ScoreHitOnLane(state, aiSide, unit, slot, slot);
            }

            float total = 0f;

            foreach (int lane in GetBifurcatedLanes(slot))
            {
                total += ScoreHitOnLane(state, aiSide, unit, slot, lane);
            }

            return total;
        }

        private static float ScoreHitOnLane(GameState state, PlayerSide aiSide, BoardUnit unit, int ownSlot, int targetLane)
        {
            BoardUnit target = state.Board.GetOpponentUnit(aiSide, targetLane);
            BoardUnit facing = state.Board.GetOpponentUnit(aiSide, ownSlot);
            int ourAttack = unit.GetCurrentAttack(state);

            if (target == null)
            {
                return 20f + ourAttack;
            }

            bool theyKillUs = facing != null && !HitsLeaderDirectly(state, facing, unit) && facing.GetCurrentAttack(state) >= unit.CurrentHealth;

            if (HitsLeaderDirectly(state, unit, target))
            {
                return 20f + ourAttack - (theyKillUs ? 30f : 0f);
            }

            bool weKillThem = ourAttack >= target.CurrentHealth;
            float killBonus = GetUnitManaCost(state, target) * CardCostValueWeight;

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

            return ourAttack - target.GetCurrentAttack(state);
        }
    }
}