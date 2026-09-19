using System.Collections.Generic;
using DDD.TNFY.TCG.Cards;
using UnityEngine;

namespace DDD.TNFY.TCG.Core
{
    public static class AIOpponentReplyModel
    {
        private static UnitCardData abstractUnitTemplate;

        private static UnitCardData AbstractUnitTemplate
        {
            get
            {
                if (abstractUnitTemplate == null)
                {
                    abstractUnitTemplate = ScriptableObject.CreateInstance<UnitCardData>();
                    abstractUnitTemplate.name = "AI Abstract Opponent Unit";
                    abstractUnitTemplate.hideFlags = HideFlags.HideAndDontSave;
                    Debug.Log("[AIOpponentReplyModel] Created the shared abstract unit template (0/0, no keywords, no effects).");
                }

                return abstractUnitTemplate;
            }
        }

        public static List<AITurnAction> EnumerateActions(GameState state, PhaseManager phases, PlayerSide side)
        {
            List<AITurnAction> actions = new List<AITurnAction>();

            if (state.IsGameOver)
            {
                return actions;
            }

            if (phases.HasBlockingPendingTargetedEffect())
            {
                return AITurnActionEnumerator.EnumerateLegalActions(state, phases, side);
            }

            if (state.CurrentPhase != TurnPhase.Action || state.ActivePlayer != side)
            {
                return actions;
            }

            for (int slot = 0; slot < Board.SlotsPerSide; slot++)
            {
                if (state.Board.GetUnit(side, slot) != null)
                {
                    continue;
                }

                if (state.Board.GetOpponentUnit(side, slot) == null)
                {
                    TryAddPlacement(state, side, slot, AIAbstractUnitCategory.OpenLane, actions);
                    continue;
                }

                TryAddPlacement(state, side, slot, AIAbstractUnitCategory.Chump, actions);
                TryAddPlacement(state, side, slot, AIAbstractUnitCategory.Wall, actions);
                TryAddPlacement(state, side, slot, AIAbstractUnitCategory.Threat, actions);
            }

            for (int slot = 0; slot < Board.SlotsPerSide; slot++)
            {
                if (phases.CanAttackWithUnit(slot))
                {
                    actions.Add(AITurnAction.AttackFrom(slot));
                }
            }

            actions.Add(AITurnAction.EndPhaseAction);

            return actions;
        }

        private static void TryAddPlacement(GameState state, PlayerSide side, int slot, AIAbstractUnitCategory category, List<AITurnAction> actions)
        {
            if (!TryGetAbstractStats(state, side, slot, category, out _, out _, out int manaCost))
            {
                return;
            }

            if (!CanAfford(state.GetPlayer(side), manaCost))
            {
                return;
            }

            actions.Add(AITurnAction.PlaceAbstractUnitAt(slot, category));
        }

        private static bool CanAfford(Player placer, int manaCost)
        {
            return placer.Hand.Count > 0 && placer.CurrentMana >= manaCost;
        }

        public static bool TryGetAbstractStats(GameState state, PlayerSide side, int slot, AIAbstractUnitCategory category,
            out int attack, out int health, out int manaCost)
        {
            attack = 0;
            health = 0;
            manaCost = 0;

            BoardUnit facing = state.Board.GetOpponentUnit(side, slot);

            if (category == AIAbstractUnitCategory.OpenLane)
            {
                if (facing != null)
                {
                    return false;
                }

                int size = Mathf.Max(1, state.GetPlayer(side).MaxManaThisGame / 2);
                attack = size;
                health = size;
            }
            else
            {
                if (facing == null)
                {
                    return false;
                }

                int facingAttack = facing.GetCurrentAttack(state);
                int facingHealth = facing.CurrentHealth;

                switch (category)
                {
                    case AIAbstractUnitCategory.Chump:
                        if (facingAttack <= 0)
                        {
                            return false;
                        }

                        attack = 1;
                        health = 1;
                        break;

                    case AIAbstractUnitCategory.Wall:
                        attack = Mathf.Max(0, facingHealth - 1);
                        health = Mathf.Max(0, facingAttack) + 1;
                        break;

                    case AIAbstractUnitCategory.Threat:
                        attack = Mathf.Max(1, facingHealth);
                        health = Mathf.Max(0, facingAttack) + 1;
                        break;

                    default:
                        return false;
                }
            }

            manaCost = EstimateManaCostForStats(attack, health);
            return true;
        }

        public static int EstimateManaCostForStats(int attack, int health)
        {
            return Mathf.Max(1, Mathf.CeilToInt((attack + health) / 2f));
        }

        public static bool IsAbstractUnit(BoardUnit unit)
        {
            return unit != null && abstractUnitTemplate != null && unit.SourceCard == abstractUnitTemplate;
        }

        public static bool TryPlaceAbstractUnit(AITurnAction action, GameState state, PhaseManager phases)
        {
            PlayerSide side = state.ActivePlayer;
            int slot = action.SlotIndex;

            if (state.Board.GetUnit(side, slot) != null)
            {
                return false;
            }

            if (!TryGetAbstractStats(state, side, slot, action.AbstractCategory, out int attack, out int health, out int manaCost))
            {
                return false;
            }

            Player placer = state.GetPlayer(side);

            if (!CanAfford(placer, manaCost))
            {
                return false;
            }

            if (!phases.SpawnUnit(side, slot, AbstractUnitTemplate))
            {
                return false;
            }

            BoardUnit unit = state.Board.GetUnit(side, slot);

            unit.BonusAttack = attack - unit.GetCurrentAttack(state);
            unit.MaxHealth = health;
            int auraHealthBonus = unit.GetEffectiveMaxHealth(state) - health;
            unit.MaxHealth = Mathf.Max(1, health - auraHealthBonus);
            unit.CurrentHealth = unit.GetEffectiveMaxHealth(state);

            placer.CurrentMana -= manaCost;
            placer.Hand.RemoveAt(placer.Hand.Count - 1);

            phases.SyncQualifyingEnemyAuraHealth();

            return true;
        }
    }
}