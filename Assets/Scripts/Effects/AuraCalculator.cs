using System.Collections.Generic;
using UnityEngine;
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
                else if (aura.grant.action == EffectActionType.BuffAttackAndHealthPerQualifyingEnemy)
                {
                    bonus += CountQualifyingEnemies(unit, state, aura.grant.amount);
                }
                else if (aura.grant.action == EffectActionType.BuffAttackAndHealthPerAlliedDeath)
                {
                    bonus += CountAlliedDeathStacks(unit.Owner, state, aura.grant.amount);
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
                else if (aura.grant.action == EffectActionType.BuffAttackAndHealthPerQualifyingEnemy)
                {
                    bonus += CountQualifyingEnemies(unit, state, aura.grant.amount);
                }
                else if (aura.grant.action == EffectActionType.BuffAttackAndHealthPerAlliedDeath)
                {
                    bonus += CountAlliedDeathStacks(unit.Owner, state, aura.grant.amount);
                }
            }

            return unit.MaxHealth + bonus;
        }

        public static int GetQualifyingEnemyAuraHealthBonus(BoardUnit unit, GameState state)
        {
            int bonus = 0;

            foreach (LeaderAura aura in GetActiveAuras(unit, state))
            {
                if (aura.grant.action == EffectActionType.BuffAttackAndHealthPerQualifyingEnemy)
                {
                    bonus += CountQualifyingEnemies(unit, state, aura.grant.amount);
                }
                else if (aura.grant.action == EffectActionType.BuffAttackAndHealthPerAlliedDeath)
                {
                    bonus += CountAlliedDeathStacks(unit.Owner, state, aura.grant.amount);
                }
            }

            return bonus;
        }

        private static int CountAlliedDeathStacks(PlayerSide side, GameState state, int deathsPerStack)
        {
            if (deathsPerStack <= 0)
            {
                return 0;
            }

            Player owner = state.GetPlayer(side);
            return owner.AlliedUnitsDied / deathsPerStack;
        }

        private static int CountQualifyingEnemies(BoardUnit unit, GameState state, int healthThreshold)
        {
            PlayerSide enemySide = unit.Owner.Opposite();
            int qualifyingCount = 0;

            foreach (BoardUnit enemyUnit in state.Board.GetUnits(enemySide))
            {
                int enemyResolvedMaxHealth = enemyUnit.MaxHealth + enemyUnit.LastSyncedAuraHealthBonus;

                if (enemyResolvedMaxHealth >= healthThreshold)
                {
                    qualifyingCount++;
                }
            }

            return qualifyingCount;
        }

        public static int GetPreviewAttackBonus(PlayerSide side, GameState state, UnitCardData previewedCard = null)
        {
            int bonus = 0;

            foreach (LeaderAura aura in GetPreviewableAuras(side, state, previewedCard))
            {
                if (aura.grant.action == EffectActionType.BuffAttack)
                {
                    bonus += aura.grant.amount;
                }
                else if (aura.grant.action == EffectActionType.BuffAttackAndHealthPerAlliedDeath)
                {
                    bonus += CountAlliedDeathStacks(side, state, aura.grant.amount);
                }
            }

            return bonus;
        }

        public static int GetPreviewMaxHealthBonus(PlayerSide side, GameState state, UnitCardData previewedCard = null)
        {
            int bonus = 0;

            foreach (LeaderAura aura in GetPreviewableAuras(side, state, previewedCard))
            {
                if (aura.grant.action == EffectActionType.BuffMaxHealth)
                {
                    bonus += aura.grant.amount;
                }
                else if (aura.grant.action == EffectActionType.BuffAttackAndHealthPerAlliedDeath)
                {
                    bonus += CountAlliedDeathStacks(side, state, aura.grant.amount);
                }
            }

            return bonus;
        }

        private static IEnumerable<LeaderAura> GetPreviewableAuras(PlayerSide side, GameState state, UnitCardData previewedCard)
        {
            Player owner = state.GetPlayer(side);
            LeaderData leader = owner.Leader;

            if (leader != null)
            {
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

            if (previewedCard == null || previewedCard.Auras == null)
            {
                yield break;
            }

            foreach (LeaderAura aura in previewedCard.Auras)
            {
                if (aura.scope != AuraScope.Self && aura.scope != AuraScope.AllOwnUnits)
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

        public static bool TryGetHealthCostForManaShortfall(Player player, int manaShort, out int healthCost)
        {
            healthCost = 0;

            if (manaShort <= 0)
            {
                return false;
            }

            if (player.Leader == null || player.Leader.HealthToManaRatio <= 0)
            {
                return false;
            }

            if (player.HasStatus(StatusEffectType.LeaderDamageShield))
            {
                return false;
            }

            healthCost = manaShort * player.Leader.HealthToManaRatio;

            if (player.LeaderHealth - healthCost <= 0)
            {
                return false;
            }

            return true;
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

                if (sourceUnit == null)
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
                    bool conditionMet = IsConditionMet(aura.activationCondition, owner);
                    bool inScope = conditionMet && IsInScope(aura.scope, unit, state, sourceUnit);

                    if (!conditionMet)
                    {
                        continue;
                    }

                    if (!inScope)
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

                case AuraScope.Self:
                    return sourceUnit != null && unit == sourceUnit;

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