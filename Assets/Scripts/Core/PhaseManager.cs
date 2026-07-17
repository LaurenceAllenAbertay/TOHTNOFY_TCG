using System.Collections.Generic;
using UnityEngine;
using DDD.TNFY.TCG.Cards;
using DDD.TNFY.TCG.Effects;

namespace DDD.TNFY.TCG.Core
{
    public class PhaseManager
    {
        private readonly GameState state;

        public PhaseManager(GameState state)
        {
            this.state = state;
        }

        public void StartMatch()
        {
            ApplyLeaderHealth(state.PlayerA);
            ApplyLeaderHealth(state.PlayerB);

            state.PlayerA.CurrentMana = 0;
            state.PlayerA.MaxManaThisGame = 0;
            state.PlayerB.CurrentMana = 0;
            state.PlayerB.MaxManaThisGame = 0;

            state.ActivePlayer = state.FirstPlayer;
            state.TurnNumber = 1;
            EnterMulliganPhase();
        }

        private void ApplyLeaderHealth(Player player)
        {
            if (player.Leader == null)
            {
                return;
            }

            player.MaxLeaderHealth = player.Leader.MaxHealth;
            player.LeaderHealth = player.Leader.MaxHealth;
        }

        public void EnterMulliganPhase()
        {
            state.CurrentPhase = TurnPhase.Mulligan;
            DrawInitialHand(state.ActivePlayer);
        }

        public void DrawInitialHand(PlayerSide side)
        {
            Player player = state.GetPlayer(side);
            int drawCount = side == state.FirstPlayer ? 4 : 5;

            for (int i = 0; i < drawCount; i++)
            {
                CardData drawn = player.DrawCard();
                if (drawn != null)
                {
                    TriggerOnDraw(side, drawn);
                }
            }
        }

        public void ResolveMulligan(PlayerSide side, List<CardData> cardsToMulligan)
        {
            Player player = state.GetPlayer(side);

            foreach (CardData card in cardsToMulligan)
            {
                player.Hand.Remove(card);
            }

            for (int i = 0; i < cardsToMulligan.Count; i++)
            {
                if (player.Deck.Count == 0) break;

                CardData drawn = player.DrawCard();
                if (drawn != null)
                {
                    TriggerOnDraw(side, drawn);
                }
            }

            foreach (CardData card in cardsToMulligan)
            {
                player.Deck.Add(card);
            }

            ListShuffler.Shuffle(player.Deck);
        }

        public void ResolveMulliganAndAdvance(PlayerSide side, List<CardData> cardsToMulligan)
        {
            ResolveMulligan(side, cardsToMulligan);

            if (side == state.FirstPlayer)
            {
                state.ActivePlayer = state.FirstPlayer.Opposite();
                EnterMulliganPhase();
            }
            else
            {
                state.ActivePlayer = state.FirstPlayer;
                EnterDrawPhase();
            }
        }

        public void EnterDrawPhase()
        {
            state.CurrentPhase = TurnPhase.Draw;

            Player active = state.GetActivePlayerData();

            ApplyAndConsumeManaReduction(active);

            active.MaxManaThisGame = System.Math.Min(active.MaxManaThisGame + 1, Player.MaxMana);
            active.CurrentMana = System.Math.Max(0, active.MaxManaThisGame - active.PendingManaReduction);
            active.PendingManaReduction = 0;

            if (active.MaxManaThisGame >= Player.MaxMana)
            {
                active.HasReachedMaxMana = true;
            }

            active.OwnTurnCount++;
            TryTriggerPeriodicItemDraw(active);

            int drawCount = state.TurnNumber == 1 && state.ActivePlayer == state.FirstPlayer ? 0 : 1;

            for (int i = 0; i < drawCount; i++)
            {
                CardData drawn = active.DrawCard();
                if (drawn != null)
                {
                    TriggerOnDraw(state.ActivePlayer, drawn);
                }
            }

            TickDelayedKills(active);

            EnterPlayPhase();
        }

        public void EnterPlayPhase()
        {
            state.CurrentPhase = TurnPhase.Play;
        }

        private void TryTriggerPeriodicItemDraw(Player player)
        {
            if (player.Leader == null || player.Leader.ItemDrawIntervalTurns <= 0)
            {
                return;
            }

            if (player.OwnTurnCount % player.Leader.ItemDrawIntervalTurns != 0)
            {
                return;
            }

            player.DrawRandomItemCard();
        }

        public bool TryPlayUnit(UnitCardData card, int slotIndex)
        {
            if (!CanPlayUnit(card, slotIndex)) return false;

            Player active = state.GetActivePlayerData();
            int effectiveCost = AuraCalculator.GetUnitCost(card, active);

            active.CurrentMana -= effectiveCost;

            if (active.Leader != null && active.Leader.FirstUnitCostDiscount > 0)
            {
                active.HasUsedFirstUnitDiscountThisTurn = true;
            }

            active.Hand.Remove(card);

            BoardUnit unit = new BoardUnit(card, state.ActivePlayer, slotIndex);
            state.Board.PlaceUnit(state.ActivePlayer, slotIndex, unit);

            TriggerOnPlay(unit);

            return true;
        }

        public bool CanPlayUnit(UnitCardData card, int slotIndex)
        {
            Player active = state.GetActivePlayerData();
            int effectiveCost = AuraCalculator.GetUnitCost(card, active);

            if (active.CurrentMana < effectiveCost) return false;
            if (state.Board.GetUnit(state.ActivePlayer, slotIndex) != null) return false;
            if (!active.Hand.Contains(card)) return false;
            if (!IsSlotLegalForPlacement(slotIndex)) return false;

            return true;
        }

        private bool IsSlotLegalForPlacement(int slotIndex)
        {
            PlayerSide opponentSide = state.ActivePlayer.Opposite();
            List<int> tauntSlots = new List<int>();

            for (int i = 0; i < Board.SlotsPerSide; i++)
            {
                BoardUnit opposingUnit = state.Board.GetUnit(opponentSide, i);
                if (opposingUnit != null && opposingUnit.HasKeyword(Keyword.Taunt, state))
                {
                    tauntSlots.Add(i);
                }
            }

            List<int> openTauntSlots = new List<int>();
            foreach (int tauntSlot in tauntSlots)
            {
                if (state.Board.GetUnit(state.ActivePlayer, tauntSlot) == null)
                {
                    openTauntSlots.Add(tauntSlot);
                }
            }

            if (openTauntSlots.Count == 0)
            {
                return true;
            }

            return openTauntSlots.Contains(slotIndex);
        }

        public void EnterAttackPhase()
        {
            state.CurrentPhase = TurnPhase.Attack;

            TickLeaderDamageShield(state.GetPlayer(state.ActivePlayer.Opposite()));

            for (int i = 0; i < Board.SlotsPerSide; i++)
            {
                BoardUnit attacker = state.Board.GetUnit(state.ActivePlayer, i);

                if (attacker == null) continue;

                bool wasStunned = ConsumeStunIfPresent(attacker);
                bool hadDoubleAttack = ConsumeStatus(attacker, StatusEffectType.DoubleAttackNextAttack);

                if (attacker.PlacedThisTurn && !attacker.HasKeyword(Keyword.Rush, state)) continue;
                if (wasStunned) continue;

                int attackCount = hadDoubleAttack ? 2 : 1;

                for (int attackIndex = 0; attackIndex < attackCount; attackIndex++)
                {
                    if (attacker.HasKeyword(Keyword.BifurcatedAttack, state))
                    {
                        int beforeSlot = i - 1;
                        int afterSlot = i + 1;

                        if (beforeSlot >= 0)
                        {
                            ResolveAttack(attacker, beforeSlot);
                        }

                        if (afterSlot < Board.SlotsPerSide)
                        {
                            ResolveAttack(attacker, afterSlot);
                        }
                    }
                    else
                    {
                        ResolveAttack(attacker, i);
                    }
                }
            }

            CheckWinCondition();

            if (!state.IsGameOver)
            {
                EnterMovePhase();
            }
        }

        private void ResolveAttack(BoardUnit attacker, int targetSlot)
        {
            BoardUnit defender = state.Board.GetOpponentUnit(state.ActivePlayer, targetSlot);
            int attackerCurrentAttack = attacker.GetCurrentAttack(state);

            if (defender != null)
            {
                defender.CurrentHealth -= attackerCurrentAttack;

                if (defender.CurrentHealth <= 0)
                {
                    KillUnit(defender, state.ActivePlayer);
                }
            }
            else
            {
                DamageLeader(state.ActivePlayer.Opposite(), attackerCurrentAttack);
            }
        }

        public void DamageLeader(PlayerSide side, int amount)
        {
            Player player = state.GetPlayer(side);

            if (player.HasStatus(StatusEffectType.LeaderDamageShield))
            {
                return;
            }

            player.LeaderHealth -= amount;
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

        public void KillUnit(BoardUnit unit, PlayerSide killer)
        {
            state.Board.RemoveUnit(unit.Owner, unit.SlotIndex);
            TriggerLeaderEffects(EffectTriggerType.UnitDied, killer, unit);
            TriggerLeaderEffectsFor(state.GetPlayer(killer), EffectTriggerType.UnitKilled, killer, unit);
        }

        public void BounceUnit(BoardUnit unit)
        {
            Player owner = state.GetPlayer(unit.Owner);

            state.Board.RemoveUnit(unit.Owner, unit.SlotIndex);
            owner.Hand.Add(unit.SourceCard);
        }

        private bool ConsumeStunIfPresent(BoardUnit unit)
        {
            return ConsumeStatus(unit, StatusEffectType.Stunned);
        }

        private static bool ConsumeStatus(BoardUnit unit, StatusEffectType type)
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

        private void TickDelayedKills(Player owner)
        {
            for (int slot = 0; slot < Board.SlotsPerSide; slot++)
            {
                BoardUnit unit = state.Board.GetUnit(owner.Side, slot);
                if (unit == null) continue;

                for (int i = unit.Statuses.Count - 1; i >= 0; i--)
                {
                    ActiveStatusEffect status = unit.Statuses[i];
                    if (status.Type != StatusEffectType.DelayedKill) continue;

                    status.RemainingTriggers--;

                    if (status.RemainingTriggers <= 0)
                    {
                        PlayerSide killer = status.SourceOwner ?? unit.Owner.Opposite();
                        KillUnit(unit, killer);
                        break;
                    }
                }
            }
        }

        private void TickLeaderDamageShield(Player owner)
        {
            for (int i = owner.Statuses.Count - 1; i >= 0; i--)
            {
                if (owner.Statuses[i].Type != StatusEffectType.LeaderDamageShield) continue;

                owner.Statuses[i].RemainingTriggers--;

                if (owner.Statuses[i].RemainingTriggers <= 0)
                {
                    owner.Statuses.RemoveAt(i);
                }
            }
        }

        private void ApplyAndConsumeManaReduction(Player active)
        {
            for (int i = active.Statuses.Count - 1; i >= 0; i--)
            {
                if (active.Statuses[i].Type != StatusEffectType.OpponentManaReduction) continue;

                active.PendingManaReduction = active.Statuses[i].Magnitude;
                active.Statuses.RemoveAt(i);
                break;
            }
        }

        public void EnterMovePhase()
        {
            state.CurrentPhase = TurnPhase.Move;
        }

        public bool TryMoveUnit(int fromSlot, int toSlot)
        {
            if (!CanMoveUnit(fromSlot, toSlot)) return false;

            PlayerSide side = state.ActivePlayer;
            BoardUnit unit = state.Board.GetUnit(side, fromSlot);
            bool isNimble = unit.HasKeyword(Keyword.Nimble, state);

            state.Board.RemoveUnit(side, fromSlot);
            state.Board.PlaceUnit(side, toSlot, unit);

            unit.HasMovedThisTurn = true;

            if (!isNimble)
            {
                state.HasUsedMoveThisTurn = true;
            }

            return true;
        }

        public bool CanMoveUnit(int fromSlot, int toSlot)
        {
            PlayerSide side = state.ActivePlayer;
            BoardUnit unit = state.Board.GetUnit(side, fromSlot);

            if (unit == null) return false;

            bool isNimble = unit.HasKeyword(Keyword.Nimble, state);

            if (unit.PlacedThisTurn && !isNimble) return false;
            if (state.Board.GetUnit(side, toSlot) != null) return false;
            if (!isNimble && state.HasUsedMoveThisTurn) return false;
            if (isNimble && unit.HasMovedThisTurn) return false;

            int distance = toSlot - fromSlot;
            int maxRange = unit.HasKeyword(Keyword.Agile, state) ? 2 : 1;

            if (distance == 0 || System.Math.Abs(distance) > maxRange) return false;

            int step = distance > 0 ? 1 : -1;
            for (int slot = fromSlot + step; slot != toSlot; slot += step)
            {
                if (state.Board.GetUnit(side, slot) != null) return false;
            }

            return true;
        }

        public void EnterTurnEndPhase()
        {
            state.CurrentPhase = TurnPhase.TurnEnd;
        }

        public void EndTurn()
        {
            for (int i = 0; i < Board.SlotsPerSide; i++)
            {
                BoardUnit unit = state.Board.GetUnit(state.ActivePlayer, i);
                if (unit != null)
                {
                    unit.PlacedThisTurn = false;
                    unit.HasMovedThisTurn = false;
                }
            }

            state.HasUsedMoveThisTurn = false;
            state.PlayerA.TriggeredOncePerTurnEffects.Clear();
            state.PlayerB.TriggeredOncePerTurnEffects.Clear();
            state.PlayerA.HasUsedFirstUnitDiscountThisTurn = false;
            state.PlayerB.HasUsedFirstUnitDiscountThisTurn = false;

            if (state.ActivePlayer == state.FirstPlayer.Opposite())
            {
                state.TurnNumber++;
            }

            state.ActivePlayer = state.ActivePlayer.Opposite();
            EnterDrawPhase();
        }

        public bool TryPlayItem(ItemCardData card, EffectTarget target)
        {
            if (!CanPlayItem(card, target)) return false;

            Player active = state.GetActivePlayerData();

            active.CurrentMana -= card.ManaCost;
            active.Hand.Remove(card);

            CardEffect effect = card.PrimaryEffect;
            EffectContext context = new EffectContext(state, state.ActivePlayer, null, target);
            EffectExecutor.Execute(effect, context, this);

            return true;
        }

        public bool CanPlayItem(ItemCardData card, EffectTarget target)
        {
            Player active = state.GetActivePlayerData();

            if (!active.CanAfford(card))
            {
                Debug.Log($"[PhaseManager] CanPlayItem FAIL: cannot afford. CurrentMana={active.CurrentMana}, ManaCost={card.ManaCost}");
                return false;
            }
            if (!active.Hand.Contains(card))
            {
                Debug.Log("[PhaseManager] CanPlayItem FAIL: card not in active player's hand.");
                return false;
            }
            if (state.CurrentPhase != TurnPhase.Play)
            {
                Debug.Log($"[PhaseManager] CanPlayItem FAIL: wrong phase, current phase={state.CurrentPhase}");
                return false;
            }

            CardEffect effect = card.PrimaryEffect;
            if (effect == null)
            {
                Debug.Log("[PhaseManager] CanPlayItem FAIL: card.PrimaryEffect is null (no Effects configured on the asset).");
                return false;
            }

            bool valid = EffectTargeting.IsValidTarget(effect.targetType, target, state);
            Debug.Log($"[PhaseManager] CanPlayItem: targetType={effect.targetType}, target.Kind={target.Kind}, IsValidTarget={valid}");
            return valid;
        }

        private void TriggerOnPlay(BoardUnit unit)
        {
            EffectContext context = new EffectContext(state, unit.Owner, unit);

            foreach (CardEffect effect in unit.SourceCard.Effects)
            {
                if (effect.trigger == EffectTriggerType.OnPlay)
                {
                    EffectExecutor.Execute(effect, context, this);
                }
            }

            TriggerLeaderEffects(EffectTriggerType.OnPlay, unit.Owner, unit);
        }

        private void TriggerLeaderEffects(EffectTriggerType trigger, PlayerSide triggeringPlayer, BoardUnit sourceUnit)
        {
            TriggerLeaderEffectsFor(state.PlayerA, trigger, triggeringPlayer, sourceUnit);
            TriggerLeaderEffectsFor(state.PlayerB, trigger, triggeringPlayer, sourceUnit);
        }

        private void TriggerLeaderEffectsFor(Player leaderOwner, EffectTriggerType trigger, PlayerSide triggeringPlayer, BoardUnit sourceUnit)
        {
            if (leaderOwner.Leader == null)
            {
                return;
            }

            EffectTarget defaultTarget = EffectTarget.ForLeader(leaderOwner.Side);
            EffectContext context = new EffectContext(state, leaderOwner.Side, sourceUnit, defaultTarget, triggeringPlayer);

            foreach (CardEffect effect in leaderOwner.Leader.Effects)
            {
                if (effect.trigger != trigger)
                {
                    continue;
                }

                if (effect.oncePerTurn && leaderOwner.TriggeredOncePerTurnEffects.Contains(effect))
                {
                    continue;
                }

                EffectExecutor.Execute(effect, context, this);

                if (effect.oncePerTurn)
                {
                    leaderOwner.TriggeredOncePerTurnEffects.Add(effect);
                }
            }
        }

        private void TriggerOnDraw(PlayerSide side, CardData card)
        {
        }

        private void CheckWinCondition()
        {
            if (state.PlayerA.LeaderHealth <= 0)
            {
                state.IsGameOver = true;
                state.Winner = PlayerSide.PlayerB;
            }
            else if (state.PlayerB.LeaderHealth <= 0)
            {
                state.IsGameOver = true;
                state.Winner = PlayerSide.PlayerA;
            }
        }
    }
}