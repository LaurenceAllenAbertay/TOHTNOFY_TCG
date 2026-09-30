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
            return SumBonus(GetActiveAuras(unit, state), unit, unit.Owner, state, EffectActionType.BuffAttack);
        }

        public static int GetEffectiveMaxHealth(BoardUnit unit, GameState state)
        {
            return unit.MaxHealth + SumBonus(GetActiveAuras(unit, state), unit, unit.Owner, state, EffectActionType.BuffMaxHealth);
        }

        public static int GetQualifyingEnemyAuraHealthBonus(BoardUnit unit, GameState state)
        {
            return SumBonus(GetActiveAuras(unit, state), unit, unit.Owner, state, null);
        }

        public static int GetPreviewAttackBonus(PlayerSide side, GameState state, UnitCardData previewedCard = null)
        {
            return SumBonus(GetPreviewableAuras(side, state, previewedCard), null, side, state, EffectActionType.BuffAttack);
        }

        public static int GetPreviewMaxHealthBonus(PlayerSide side, GameState state, UnitCardData previewedCard = null)
        {
            return SumBonus(GetPreviewableAuras(side, state, previewedCard), null, side, state, EffectActionType.BuffMaxHealth);
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

        public static int GetUnitCost(UnitCardData card, Player player)
        {
            return card.ManaCost;
        }

        public static bool TryGetHealthCostForManaShortfall(Player player, int manaShort, out int healthCost)
        {
            healthCost = 0;

            if (manaShort <= 0 || player.Leader == null || player.Leader.HealthToManaRatio <= 0 || player.HasStatus(StatusEffectType.Shield))
            {
                return false;
            }

            healthCost = manaShort * player.Leader.HealthToManaRatio;
            return player.LeaderHealth - healthCost > 0;
        }

        private static int SumBonus(IEnumerable<LeaderAura> auras, BoardUnit unit, PlayerSide side, GameState state, EffectActionType? flatBonusAction)
        {
            int bonus = 0;

            foreach (LeaderAura aura in auras)
            {
                AuraGrant grant = aura.grant;

                if (grant.action == flatBonusAction)
                {
                    bonus += grant.amount;
                }
                else if (grant.action == EffectActionType.BuffAttackAndHealthPerAlliedDeath && grant.amount > 0)
                {
                    bonus += state.GetPlayer(side).AlliedUnitsDied / grant.amount;
                }
                else if (grant.action == EffectActionType.BuffAttackAndHealthPerQualifyingEnemy && unit != null)
                {
                    bonus += CountQualifyingEnemies(unit, state, grant.amount);
                }
            }

            return bonus;
        }

        private static int CountQualifyingEnemies(BoardUnit unit, GameState state, int healthThreshold)
        {
            int count = 0;

            foreach (BoardUnit enemy in state.Board.GetUnits(unit.Owner.Opposite()))
            {
                if (enemy.MaxHealth + enemy.LastSyncedAuraHealthBonus >= healthThreshold)
                {
                    count++;
                }
            }

            return count;
        }

        private static IEnumerable<LeaderAura> GetActiveAuras(BoardUnit unit, GameState state)
        {
            Player owner = state.GetPlayer(unit.Owner);

            if (owner.Leader != null)
            {
                foreach (LeaderAura aura in owner.Leader.Auras)
                {
                    if (IsConditionMet(aura.activationCondition, owner) && IsInScope(aura.scope, unit, null))
                    {
                        yield return aura;
                    }
                }
            }

            foreach (BoardUnit source in state.Board.GetUnits(unit.Owner))
            {
                if (source.IsSilenced)
                {
                    continue;
                }

                foreach (LeaderAura aura in source.SourceCard.Auras)
                {
                    if (IsConditionMet(aura.activationCondition, owner) && IsInScope(aura.scope, unit, source))
                    {
                        yield return aura;
                    }
                }
            }
        }

        private static IEnumerable<LeaderAura> GetPreviewableAuras(PlayerSide side, GameState state, UnitCardData previewedCard)
        {
            Player owner = state.GetPlayer(side);

            if (owner.Leader != null)
            {
                foreach (LeaderAura aura in owner.Leader.Auras)
                {
                    if (aura.scope == AuraScope.AllOwnUnits && IsConditionMet(aura.activationCondition, owner))
                    {
                        yield return aura;
                    }
                }
            }

            if (previewedCard == null)
            {
                yield break;
            }

            foreach (LeaderAura aura in previewedCard.Auras)
            {
                if ((aura.scope == AuraScope.Self || aura.scope == AuraScope.AllOwnUnits) && IsConditionMet(aura.activationCondition, owner))
                {
                    yield return aura;
                }
            }
        }

        private static bool IsConditionMet(AuraActivationCondition condition, Player owner)
        {
            return condition == AuraActivationCondition.Always || (condition == AuraActivationCondition.OnceMaxManaReached && owner.HasReachedMaxMana);
        }

        private static bool IsInScope(AuraScope scope, BoardUnit unit, BoardUnit sourceUnit) => scope switch
        {
            AuraScope.AllOwnUnits => true,
            AuraScope.EdgeUnits => unit.SlotIndex == 0 || unit.SlotIndex == Board.SlotsPerSide - 1,
            AuraScope.AdjacentToSource => sourceUnit != null && unit != sourceUnit && Mathf.Abs(unit.SlotIndex - sourceUnit.SlotIndex) == 1,
            AuraScope.Self => unit == sourceUnit,
            _ => false
        };
    }
}