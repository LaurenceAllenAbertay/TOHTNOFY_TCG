using System.Collections.Generic;
using DDD.TNFY.TCG.Cards;
using DDD.TNFY.TCG.Effects;
using UnityEngine;

namespace DDD.TNFY.TCG.Core
{
    public static class AIOpponentReplyModel
    {
        private static readonly List<UnitCardData> replyUnits = new List<UnitCardData>();
        private static readonly List<ItemCardData> removalItems = new List<ItemCardData>();

        private static UnitCardData[] cheapestKillerByMinAttack = new UnitCardData[0];
        private static UnitCardData[] cheapestSurvivorByMinHealth = new UnitCardData[0];
        private static UnitCardData[] strongestByMaxCost = new UnitCardData[0];
        private static UnitCardData cheapestBlocker;

        public static void ConfigureCardPool(IReadOnlyList<CardData> cardPool)
        {
            replyUnits.Clear();
            removalItems.Clear();
            cheapestBlocker = null;

            if (cardPool != null)
            {
                foreach (CardData card in cardPool)
                {
                    if (card is UnitCardData unit && IsUsableReplyUnit(unit))
                    {
                        replyUnits.Add(unit);
                    }
                    else if (card is ItemCardData item && IsUsableRemovalItem(item))
                    {
                        removalItems.Add(item);
                    }
                }
            }

            replyUnits.Sort((a, b) => a.ManaCost.CompareTo(b.ManaCost));
            removalItems.Sort((a, b) => a.ManaCost.CompareTo(b.ManaCost));

            int maxAttack = 0;
            int maxHealth = 0;
            int maxCost = 0;

            foreach (UnitCardData unit in replyUnits)
            {
                maxAttack = Mathf.Max(maxAttack, unit.Attack);
                maxHealth = Mathf.Max(maxHealth, unit.Health);
                maxCost = Mathf.Max(maxCost, unit.ManaCost);
            }

            cheapestKillerByMinAttack = new UnitCardData[maxAttack + 1];

            for (int attack = 1; attack <= maxAttack; attack++)
            {
                int requiredAttack = attack;
                cheapestKillerByMinAttack[attack] = FindCheapest(unit => !unit.HasKeyword(Keyword.Piercing) && unit.Attack >= requiredAttack, unit => unit.Health);
            }

            cheapestSurvivorByMinHealth = new UnitCardData[maxHealth + 1];

            for (int health = 1; health <= maxHealth; health++)
            {
                int requiredHealth = health;
                cheapestSurvivorByMinHealth[health] = FindCheapest(unit => !unit.HasKeyword(Keyword.Piercing) && unit.Health >= requiredHealth, unit => unit.Attack);
            }

            cheapestBlocker = FindCheapest(unit => !unit.HasKeyword(Keyword.Piercing), unit => Mathf.RoundToInt(AIHeuristics.GetPrintedStatValue(unit)));

            strongestByMaxCost = replyUnits.Count > 0 ? new UnitCardData[maxCost + 1] : new UnitCardData[0];

            for (int mana = 0; mana < strongestByMaxCost.Length; mana++)
            {
                UnitCardData strongest = null;

                foreach (UnitCardData unit in replyUnits)
                {
                    if (unit.ManaCost > mana)
                    {
                        break;
                    }

                    if (strongest == null || AIHeuristics.GetPrintedStatValue(unit) > AIHeuristics.GetPrintedStatValue(strongest))
                    {
                        strongest = unit;
                    }
                }

                strongestByMaxCost[mana] = strongest;
            }

            LogConfiguredPool(cardPool != null ? cardPool.Count : 0);
        }

        private static bool IsUsableReplyUnit(UnitCardData unit)
        {
            if (unit.Health <= 0)
            {
                return false;
            }

            foreach (CardEffect effect in unit.Effects)
            {
                if (effect.action == EffectActionType.RandomizeStatsOnDraw)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsUsableRemovalItem(ItemCardData item)
        {
            CardEffect effect = item.PrimaryEffect;

            if (effect == null)
            {
                return false;
            }

            bool removesUnit = effect.action == EffectActionType.BounceUnit
                || (effect.action == EffectActionType.DealDamage && effect.amount > 0);

            bool canPickEnemyUnit = effect.targetType == TargetType.AnyUnit
                || effect.targetType == TargetType.AnyEnemyUnit
                || effect.targetType == TargetType.AnyUnitOrLeader
                || effect.targetType == TargetType.AnyEnemyUnitOrLeader;

            return removesUnit && canPickEnemyUnit;
        }

        private static UnitCardData FindCheapest(System.Func<UnitCardData, bool> qualifies, System.Func<UnitCardData, int> tieBreakHigherIsBetter)
        {
            UnitCardData best = null;

            foreach (UnitCardData unit in replyUnits)
            {
                if (best != null && unit.ManaCost > best.ManaCost)
                {
                    break;
                }

                if (!qualifies(unit))
                {
                    continue;
                }

                if (best == null || tieBreakHigherIsBetter(unit) > tieBreakHigherIsBetter(best))
                {
                    best = unit;
                }
            }

            return best;
        }

        private static void LogConfiguredPool(int poolSize)
        {
            if (replyUnits.Count == 0)
            {
                Debug.LogError($"[AIOpponentReplyModel] Card pool ({poolSize} card(s)) contains no usable unit cards - the AI will assume the opponent can never play a unit. Check the Card Database assigned to MatchBootstrapper.");
                return;
            }

            List<string> killers = new List<string>();

            for (int attack = 1; attack < cheapestKillerByMinAttack.Length; attack++)
            {
                killers.Add($"{attack}->{DescribeCard(cheapestKillerByMinAttack[attack])}");
            }

            List<string> survivors = new List<string>();

            for (int health = 1; health < cheapestSurvivorByMinHealth.Length; health++)
            {
                survivors.Add($"{health}->{DescribeCard(cheapestSurvivorByMinHealth[health])}");
            }

            List<string> strongest = new List<string>();

            for (int mana = 0; mana < strongestByMaxCost.Length; mana++)
            {
                strongest.Add($"{mana}->{DescribeCard(strongestByMaxCost[mana])}");
            }

            Debug.Log($"[AIOpponentReplyModel] Built opponent reply tables from {poolSize} card(s): {replyUnits.Count} usable unit(s), {removalItems.Count} removal item(s) [{string.Join(", ", removalItems.ConvertAll(item => $"{item.CardName}({item.ManaCost}, {item.PrimaryEffect.action} {item.PrimaryEffect.amount})"))}].\n    Threat (cheapest killer) by target health: {string.Join(", ", killers)}\n    Wall (cheapest survivor) by health needed: {string.Join(", ", survivors)}\n    Chump (cheapest blocker): {DescribeCard(cheapestBlocker)}\n    OpenLane (strongest affordable) by mana: {string.Join(", ", strongest)}");
        }

        private static string DescribeCard(UnitCardData card)
        {
            return card != null ? $"{card.CardName} {card.Attack}/{card.Health} ({card.ManaCost})" : "none";
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
                if (state.Board.GetUnit(side, slot) != null || !phases.IsSlotLegalForPlacement(slot))
                {
                    continue;
                }

                if (state.Board.GetOpponentUnit(side, slot) == null)
                {
                    TryAddPlacement(state, side, slot, AIAbstractUnitCategory.OpenLane, actions, null, null);
                    continue;
                }

                UnitCardData threatCard = TryAddPlacement(state, side, slot, AIAbstractUnitCategory.Threat, actions, null, null);
                UnitCardData wallCard = TryAddPlacement(state, side, slot, AIAbstractUnitCategory.Wall, actions, threatCard, null);
                TryAddPlacement(state, side, slot, AIAbstractUnitCategory.Chump, actions, threatCard, wallCard);
            }

            Player actor = state.GetPlayer(side);

            for (int slot = 0; slot < Board.SlotsPerSide; slot++)
            {
                if (TryGetReplyRemoval(state, side, slot, out _, out ItemCardData removalItem) && CanAfford(actor, removalItem.ManaCost))
                {
                    actions.Add(AITurnAction.AbstractRemovalAt(slot));
                }
            }

            for (int slot = 0; slot < Board.SlotsPerSide; slot++)
            {
                if (phases.CanAttackWithUnit(slot))
                {
                    actions.Add(AITurnAction.AttackFrom(slot));
                }
            }

            AITurnActionEnumerator.AddLegalMoves(phases, actions);

            actions.Add(AITurnAction.EndPhaseAction);

            return actions;
        }

        private static UnitCardData TryAddPlacement(GameState state, PlayerSide side, int slot, AIAbstractUnitCategory category,
            List<AITurnAction> actions, UnitCardData alreadyOffered, UnitCardData alsoAlreadyOffered)
        {
            if (!TryGetReplyUnit(state, side, slot, category, out UnitCardData card))
            {
                return null;
            }

            if (card == alreadyOffered || card == alsoAlreadyOffered)
            {
                return null;
            }

            Player placer = state.GetPlayer(side);

            if (!CanAfford(placer, AuraCalculator.GetUnitCost(card, placer)))
            {
                return null;
            }

            actions.Add(AITurnAction.PlaceAbstractUnitAt(slot, category));
            return card;
        }

        private static bool CanAfford(Player placer, int manaCost)
        {
            return placer.Hand.Count > 0 && placer.CurrentMana >= manaCost;
        }

        public static bool TryGetReplyUnit(GameState state, PlayerSide side, int slot, AIAbstractUnitCategory category, out UnitCardData card)
        {
            card = null;

            BoardUnit facing = state.Board.GetOpponentUnit(side, slot);

            if (category == AIAbstractUnitCategory.OpenLane)
            {
                if (facing != null)
                {
                    return false;
                }

                card = StrongestAffordable(state.GetPlayer(side).CurrentMana);
                return card != null;
            }

            if (facing == null)
            {
                return false;
            }

            int facingAttack = facing.GetCurrentAttack(state);

            switch (category)
            {
                case AIAbstractUnitCategory.Chump:
                    if (facingAttack <= 0)
                    {
                        return false;
                    }

                    card = cheapestBlocker;
                    break;

                case AIAbstractUnitCategory.Wall:
                    card = Lookup(cheapestSurvivorByMinHealth, Mathf.Max(1, facingAttack + 1));
                    break;

                case AIAbstractUnitCategory.Threat:
                    card = Lookup(cheapestKillerByMinAttack, Mathf.Max(1, facing.CurrentHealth));
                    break;
            }

            return card != null;
        }

        private static UnitCardData Lookup(UnitCardData[] table, int index)
        {
            return index >= 0 && index < table.Length ? table[index] : null;
        }

        private static UnitCardData StrongestAffordable(int mana)
        {
            if (strongestByMaxCost.Length == 0 || mana < 0)
            {
                return null;
            }

            return strongestByMaxCost[Mathf.Min(mana, strongestByMaxCost.Length - 1)];
        }

        public static bool TryGetReplyRemoval(GameState state, PlayerSide side, int targetSlot, out BoardUnit target, out ItemCardData item)
        {
            item = null;
            target = state.Board.GetUnit(side.Opposite(), targetSlot);

            if (target == null || target.CurrentHealth <= 0)
            {
                return false;
            }

            foreach (ItemCardData candidate in removalItems)
            {
                CardEffect effect = candidate.PrimaryEffect;

                if (effect.action == EffectActionType.BounceUnit || effect.amount >= target.CurrentHealth)
                {
                    item = candidate;
                    return true;
                }
            }

            return false;
        }

        public static bool TryPlaceAbstractUnit(AITurnAction action, GameState state, PhaseManager phases)
        {
            PlayerSide side = state.ActivePlayer;
            int slot = action.SlotIndex;

            if (state.Board.GetUnit(side, slot) != null || !phases.IsSlotLegalForPlacement(slot))
            {
                return false;
            }

            if (!TryGetReplyUnit(state, side, slot, action.AbstractCategory, out UnitCardData card))
            {
                return false;
            }

            Player placer = state.GetPlayer(side);
            int manaCost = AuraCalculator.GetUnitCost(card, placer);

            if (!CanAfford(placer, manaCost))
            {
                return false;
            }

            if (!phases.SpawnUnit(side, slot, card))
            {
                return false;
            }

            placer.CurrentMana -= manaCost;
            placer.Hand.RemoveAt(placer.Hand.Count - 1);

            return true;
        }

        public static bool TryApplyAbstractRemoval(AITurnAction action, GameState state, PhaseManager phases)
        {
            PlayerSide side = state.ActivePlayer;

            if (!TryGetReplyRemoval(state, side, action.SlotIndex, out BoardUnit target, out ItemCardData item))
            {
                return false;
            }

            Player caster = state.GetPlayer(side);

            if (!CanAfford(caster, item.ManaCost))
            {
                return false;
            }

            caster.CurrentMana -= item.ManaCost;
            caster.Hand.RemoveAt(caster.Hand.Count - 1);

            EffectContext context = new EffectContext(state, side, null, EffectTarget.ForUnit(target));
            EffectExecutor.Execute(item.PrimaryEffect, context, phases);

            return true;
        }
    }
}