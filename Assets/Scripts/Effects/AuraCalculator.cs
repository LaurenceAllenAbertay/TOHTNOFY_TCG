using System.Collections.Generic;
using DDD.TNFY.TCG.Cards;
using DDD.TNFY.TCG.Core;

namespace DDD.TNFY.TCG.Effects
{
    public static class AuraCalculator
    {
        public static int GetAttackBonus(BoardUnit unit, GameState state)
        {
            int bonus = 0;

            foreach (LeaderAura aura in GetActiveAuras(unit, state))
            {
                if (aura.grant.action == EffectActionType.BuffAttack)
                {
                    bonus += aura.grant.amount;
                }
            }

            return bonus;
        }

        public static int GetEffectiveMaxHealth(BoardUnit unit, GameState state)
        {
            int bonus = 0;

            foreach (LeaderAura aura in GetActiveAuras(unit, state))
            {
                if (aura.grant.action == EffectActionType.BuffMaxHealth)
                {
                    bonus += aura.grant.amount;
                }
            }

            return unit.MaxHealth + bonus;
        }

        public static int GetPreviewAttackBonus(PlayerSide side, GameState state)
        {
            int bonus = 0;

            foreach (LeaderAura aura in GetPreviewableAuras(side, state))
            {
                if (aura.grant.action == EffectActionType.BuffAttack)
                {
                    bonus += aura.grant.amount;
                }
            }

            return bonus;
        }

        public static int GetPreviewMaxHealthBonus(PlayerSide side, GameState state)
        {
            int bonus = 0;

            foreach (LeaderAura aura in GetPreviewableAuras(side, state))
            {
                if (aura.grant.action == EffectActionType.BuffMaxHealth)
                {
                    bonus += aura.grant.amount;
                }
            }

            return bonus;
        }

        private static IEnumerable<LeaderAura> GetPreviewableAuras(PlayerSide side, GameState state)
        {
            Player owner = state.GetPlayer(side);
            LeaderData leader = owner.Leader;

            if (leader == null)
            {
                yield break;
            }

            foreach (LeaderAura aura in leader.Auras)
            {
                if (aura.scope != AuraScope.AllOwnUnits)
                {
                    continue;
                }

                if (!IsConditionMet(aura.activationCondition, owner))
                {
                    continue;
                }

                yield return aura;
            }
        }

        public static bool HasAuraKeyword(BoardUnit unit, GameState state, Keyword keyword)
        {
            foreach (LeaderAura aura in GetActiveAuras(unit, state))
            {
                if (aura.grant.action == EffectActionType.GrantKeyword && aura.grant.keyword == keyword)
                {
                    return true;
                }
            }

            return false;
        }

        // --- Player-scoped leader rules (not unit auras, but computed live for the same reason) ---

        public static int GetUnitCost(UnitCardData card, Player player)
        {
            int baseCost = card.ManaCost;

            if (player.Leader == null)
            {
                return baseCost;
            }

            if (player.Leader.FirstUnitCostDiscount <= 0)
            {
                return baseCost;
            }

            if (player.HasUsedFirstUnitDiscountThisTurn)
            {
                return baseCost;
            }

            return System.Math.Max(0, baseCost - player.Leader.FirstUnitCostDiscount);
        }

        private static System.Collections.Generic.IEnumerable<LeaderAura> GetActiveAuras(BoardUnit unit, GameState state)
        {
            Player owner = state.GetPlayer(unit.Owner);
            LeaderData leader = owner.Leader;

            if (leader != null)
            {
                foreach (LeaderAura aura in leader.Auras)
                {
                    if (!IsConditionMet(aura.activationCondition, owner))
                    {
                        continue;
                    }

                    if (!IsInScope(aura.scope, unit, state, null))
                    {
                        continue;
                    }

                    yield return aura;
                }
            }

            for (int i = 0; i < Board.SlotsPerSide; i++)
            {
                BoardUnit sourceUnit = state.Board.GetUnit(unit.Owner, i);

                if (sourceUnit == null || sourceUnit == unit)
                {
                    continue;
                }

                IReadOnlyList<LeaderAura> unitAuras = sourceUnit.SourceCard.Auras;

                if (unitAuras == null)
                {
                    continue;
                }

                foreach (LeaderAura aura in unitAuras)
                {
                    if (!IsConditionMet(aura.activationCondition, owner))
                    {
                        continue;
                    }

                    if (!IsInScope(aura.scope, unit, state, sourceUnit))
                    {
                        continue;
                    }

                    yield return aura;
                }
            }
        }

        private static bool IsConditionMet(AuraActivationCondition condition, Player owner)
        {
            switch (condition)
            {
                case AuraActivationCondition.Always:
                    return true;

                case AuraActivationCondition.OnceMaxManaReached:
                    return owner.HasReachedMaxMana;

                default:
                    return false;
            }
        }

        private static bool IsInScope(AuraScope scope, BoardUnit unit, GameState state, BoardUnit sourceUnit)
        {
            switch (scope)
            {
                case AuraScope.AllOwnUnits:
                    return true;

                case AuraScope.EdgeUnits:
                    return IsEdgeUnit(unit, state);

                case AuraScope.AdjacentToSource:
                    return sourceUnit != null && unit != sourceUnit && System.Math.Abs(unit.SlotIndex - sourceUnit.SlotIndex) == 1;

                default:
                    return false;
            }
        }

        private static bool IsEdgeUnit(BoardUnit unit, GameState state)
        {
            const int leftmostSlot = 0;
            int rightmostSlot = Board.SlotsPerSide - 1;

            return unit.SlotIndex == leftmostSlot || unit.SlotIndex == rightmostSlot;
        }
    }
}