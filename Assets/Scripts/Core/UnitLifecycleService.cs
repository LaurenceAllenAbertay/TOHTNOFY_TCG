using System.Collections.Generic;
using UnityEngine;
using DDD.TNFY.TCG.Cards;
using DDD.TNFY.TCG.Effects;

namespace DDD.TNFY.TCG.Core
{
    public class UnitLifecycleService
    {
        private readonly GameState state;

        public UnitLifecycleService(GameState state)
        {
            this.state = state;
        }

        public BoardUnit PlaceNewUnit(UnitCardData card, int slotIndex)
        {
            BoardUnit unit = new BoardUnit(card, state.ActivePlayer, slotIndex);
            state.Board.PlaceUnit(state.ActivePlayer, slotIndex, unit);
            InitializeHealthToEffectiveMax(unit);

            if (card.HasPendingCurrentHealth)
            {
                int effectiveMax = unit.GetEffectiveMaxHealth(state);
                unit.CurrentHealth = System.Math.Min(card.PendingCurrentHealth, effectiveMax);
            }

            SyncQualifyingEnemyAuraHealth();

            return unit;
        }

        public BoardUnit AbsorbUnit(BoardUnit absorbedUnit, UnitCardData card, int slotIndex)
        {
            int inheritedAuraAttack = AuraCalculator.GetAttackBonus(absorbedUnit, state);
            int inheritedPermanentAttack = absorbedUnit.SourceCard.Attack + absorbedUnit.BonusAttack + inheritedAuraAttack;
            int inheritedMaxHealth = absorbedUnit.MaxHealth;
            int inheritedCurrentHealth = absorbedUnit.CurrentHealth;
            int inheritedTemporaryAttack = ConsumeStatusMagnitude(absorbedUnit, StatusEffectType.TemporaryAttackNextAttack);

            state.Board.RemoveUnit(absorbedUnit.Owner, slotIndex);

            BoardUnit unit = new BoardUnit(card, absorbedUnit.Owner, slotIndex);
            state.Board.PlaceUnit(absorbedUnit.Owner, slotIndex, unit);

            unit.BonusAttack = inheritedPermanentAttack + inheritedTemporaryAttack;
            unit.MaxHealth = card.Health + inheritedMaxHealth;

            int effectiveMax = unit.GetEffectiveMaxHealth(state);

            unit.CurrentHealth = System.Math.Min(inheritedCurrentHealth + card.Health, effectiveMax);
            unit.LastSyncedAuraHealthBonus = AuraCalculator.GetQualifyingEnemyAuraHealthBonus(unit, state);

            SyncQualifyingEnemyAuraHealth();

            return unit;
        }

        public void InitializeHealthToEffectiveMax(BoardUnit unit)
        {
            int effectiveMax = unit.GetEffectiveMaxHealth(state);
            unit.CurrentHealth = effectiveMax;
            unit.LastSyncedAuraHealthBonus = AuraCalculator.GetQualifyingEnemyAuraHealthBonus(unit, state);
        }

        public void SyncQualifyingEnemyAuraHealth()
        {
            SyncQualifyingEnemyAuraHealthForSide(PlayerSide.PlayerA);
            SyncQualifyingEnemyAuraHealthForSide(PlayerSide.PlayerB);
        }

        private void SyncQualifyingEnemyAuraHealthForSide(PlayerSide side)
        {
            foreach (BoardUnit unit in new List<BoardUnit>(state.Board.GetUnits(side)))
            {
                int currentAuraHealthBonus = AuraCalculator.GetQualifyingEnemyAuraHealthBonus(unit, state);
                int delta = currentAuraHealthBonus - unit.LastSyncedAuraHealthBonus;

                if (delta > 0)
                {
                    unit.CurrentHealth += delta;
                }

                unit.LastSyncedAuraHealthBonus = currentAuraHealthBonus;
            }
        }

        public void TopUpAllUnitsToEffectiveMaxHealth(PlayerSide side)
        {
            for (int slot = 0; slot < Board.SlotsPerSide; slot++)
            {
                BoardUnit unit = state.Board.GetUnit(side, slot);

                if (unit == null)
                {
                    continue;
                }

                int previousMax = unit.MaxHealth;
                int newMax = unit.GetEffectiveMaxHealth(state);
                int delta = newMax - previousMax;

                if (delta > 0)
                {
                    unit.CurrentHealth += delta;
                }
            }
        }

        public bool DamageLeader(PlayerSide side, int amount)
        {
            Player player = state.GetPlayer(side);

            if (ConsumeShieldIfPresent(player.Statuses))
            {
                Debug.Log($"[UnitLifecycleService] {side}'s leader had Shield — damage blocked and Shield consumed.");
                return false;
            }

            player.LeaderHealth -= amount;
            return true;
        }

        public static bool ConsumeShieldIfPresent(List<ActiveStatusEffect> statuses)
        {
            for (int i = 0; i < statuses.Count; i++)
            {
                if (statuses[i].Type == StatusEffectType.Shield)
                {
                    statuses.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        public void HealUnit(BoardUnit unit, int amount)
        {
            unit.CurrentHealth = System.Math.Min(unit.CurrentHealth + amount, unit.GetEffectiveMaxHealth(state));
        }

        public void HealLeader(PlayerSide side, int amount)
        {
            Player player = state.GetPlayer(side);
            player.LeaderHealth = System.Math.Min(player.LeaderHealth + amount, player.MaxLeaderHealth);
        }

        public bool TransformUnit(BoardUnit originalUnit, UnitCardData replacementCard)
        {
            if (originalUnit == null || replacementCard == null)
            {
                return false;
            }

            PlayerSide owner = originalUnit.Owner;
            int slotIndex = originalUnit.SlotIndex;

            state.Board.RemoveUnit(owner, slotIndex);

            BoardUnit replacementUnit = new BoardUnit(replacementCard, owner, slotIndex);
            replacementUnit.PlacedThisTurn = originalUnit.PlacedThisTurn;
            replacementUnit.HasMovedThisTurn = originalUnit.HasMovedThisTurn;
            replacementUnit.HasUsedGrantedEnemyMoveThisTurn = originalUnit.HasUsedGrantedEnemyMoveThisTurn;
            state.Board.PlaceUnit(owner, slotIndex, replacementUnit);
            InitializeHealthToEffectiveMax(replacementUnit);
            SyncQualifyingEnemyAuraHealth();

            return true;
        }

        public bool SpawnUnit(PlayerSide side, int slotIndex, UnitCardData card)
        {
            if (card == null)
            {
                return false;
            }

            if (state.Board.GetUnit(side, slotIndex) != null)
            {
                return false;
            }

            BoardUnit spawnedUnit = new BoardUnit(card, side, slotIndex);
            state.Board.PlaceUnit(side, slotIndex, spawnedUnit);
            InitializeHealthToEffectiveMax(spawnedUnit);
            SyncQualifyingEnemyAuraHealth();

            return true;
        }

        public void BounceUnit(BoardUnit unit)
        {
            Player owner = state.GetPlayer(unit.Owner);

            int auraAttackBonus = AuraCalculator.GetAttackBonus(unit, state);
            int effectiveMaxHealth = AuraCalculator.GetEffectiveMaxHealth(unit, state);
            int auraHealthBonus = effectiveMaxHealth - unit.MaxHealth;

            state.Board.RemoveUnit(unit.Owner, unit.SlotIndex);

            int permanentAttack = unit.SourceCard.Attack + unit.BonusAttack + auraAttackBonus;
            int permanentHealth = effectiveMaxHealth;
            bool hasPermanentStatChange = unit.BonusAttack != 0 || auraAttackBonus != 0 || unit.MaxHealth != unit.SourceCard.Health || auraHealthBonus != 0;
            bool isDamaged = unit.CurrentHealth != effectiveMaxHealth;

            CardData cardForHand = unit.SourceCard;

            if (hasPermanentStatChange || isDamaged)
            {
                int currentHealthForHand = System.Math.Max(1, unit.CurrentHealth);
                cardForHand =
                    unit.SourceCard.CreateStatOverrideClone(permanentAttack, permanentHealth, currentHealthForHand);
            }

            if (!owner.TryAddCardToHand(cardForHand))
            {
                Debug.Log($"[UnitLifecycleService] {unit.SourceCard.CardName} could not be bounced — {owner.Side}'s hand is already at the {Player.AbsoluteMaxHandSize}-card max, card is burned.");
            }
        }

        public void SilenceUnit(BoardUnit unit, int duration)
        {
            if (unit == null)
            {
                return;
            }

            if (unit.IsSilenced)
            {
                return;
            }

            unit.Statuses.Add(new ActiveStatusEffect(StatusEffectType.Silenced, duration));
        }

        public static bool ConsumeStatus(BoardUnit unit, StatusEffectType type)
        {
            for (int i = 0; i < unit.Statuses.Count; i++)
            {
                if (unit.Statuses[i].Type == type)
                {
                    unit.Statuses.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        public static int ConsumeStatusMagnitude(BoardUnit unit, StatusEffectType type)
        {
            for (int i = 0; i < unit.Statuses.Count; i++)
            {
                if (unit.Statuses[i].Type == type)
                {
                    int magnitude = unit.Statuses[i].Magnitude;
                    unit.Statuses.RemoveAt(i);
                    return magnitude;
                }
            }

            return 0;
        }
    }
}