using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DDD.TNFY.TCG.Cards;
using DDD.TNFY.TCG.Effects;

namespace DDD.TNFY.TCG.Core
{
    public class PhaseManager
    {
        private readonly GameState state;
        private readonly MonoBehaviour coroutineRunner;

        public PhaseManager(GameState state, MonoBehaviour coroutineRunner)
        {
            this.state = state;
            this.coroutineRunner = coroutineRunner;
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
            const int drawCount = 4;

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

            if (active.MaxManaThisGame >= Player.MaxMana && !active.HasReachedMaxMana)
            {
                active.HasReachedMaxMana = true;
                TopUpAllUnitsToEffectiveMaxHealth(active.Side);
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
                else if (active.Deck.Count == 0)
                {
                    active.FatigueDamageTaken++;
                    active.LeaderHealth -= active.FatigueDamageTaken;

                    Debug.Log($"[PhaseManager] {state.ActivePlayer}'s deck is empty — fatigue dealt {active.FatigueDamageTaken} damage directly to their leader (bypassing shields/reductions). LeaderHealth is now {active.LeaderHealth}.");

                    CheckWinCondition();

                    if (state.IsGameOver)
                    {
                        Debug.Log($"[PhaseManager] {state.ActivePlayer} was defeated by fatigue.");
                        return;
                    }
                }
            }

            TickDelayedKills(active);
            TickDecay(active);
            TriggerOnTurnStart(active);
        }

        private void TriggerOnTurnStart(Player owner)
        {
            state.IsResolvingTurnStartEffects = true;
            ContinueTurnStartScan(owner, state.TurnStartScanSlot);
        }

        private void ContinueTurnStartScan(Player owner, int fromSlot)
        {
            for (int slot = fromSlot; slot < Board.SlotsPerSide; slot++)
            {
                BoardUnit unit = state.Board.GetUnit(owner.Side, slot);
                if (unit == null) continue;
                if (unit.IsSilenced) continue;

                foreach (CardEffect effect in unit.SourceCard.Effects)
                {
                    if (effect.trigger != EffectTriggerType.OnTurnStart)
                    {
                        continue;
                    }

                    if (effect.action == EffectActionType.HealSelfByDamageDealt)
                    {
                        ResolveTurnStartDrain(unit, effect);
                        continue;
                    }

                    if (RequiresChosenTarget(effect.targetType))
                    {
                        state.TurnStartScanSlot = slot + 1;
                        DeferTargetedEffect(effect, unit, EffectTriggerType.OnTurnStart, excludeSource: false);

                        if (state.PendingTargetedEffect != null)
                        {
                            return;
                        }

                        continue;
                    }

                    EffectTarget immediateTarget = EffectTargeting.ResolveImmediateTarget(effect.targetType, unit, unit.Owner, state);
                    Debug.Log($"[PhaseManager] {unit.SourceCard.CardName}'s OnTurnStart effect (targetType={effect.targetType}) resolved immediately, target.Kind={immediateTarget.Kind}, LeaderSide={immediateTarget.LeaderSide}.");
                    EffectContext context = new EffectContext(state, unit.Owner, unit, immediateTarget);
                    EffectExecutor.Execute(effect, context, this);
                }
            }

            state.IsResolvingTurnStartEffects = false;
            state.TurnStartScanSlot = 0;
            EnterPlayPhase();
        }

        private void ResolveTurnStartDrain(BoardUnit source, CardEffect healEffect)
        {
            BoardUnit target = FindFirstEnemyUnit(source.Owner);

            if (target == null)
            {
                return;
            }

            int damageDealt = System.Math.Min(healEffect.amount, target.CurrentHealth);
            target.CurrentHealth -= healEffect.amount;

            if (target.CurrentHealth <= 0)
            {
                KillUnit(target, source.Owner);
            }

            EffectTarget selfTarget = EffectTarget.ForUnit(source);
            EffectContext context = new EffectContext(state, source.Owner, source, selfTarget);
            EffectExecutor.Execute(healEffect, context, this, damageDealt);
        }

        private BoardUnit FindFirstEnemyUnit(PlayerSide side)
        {
            PlayerSide enemySide = side.Opposite();

            for (int i = 0; i < Board.SlotsPerSide; i++)
            {
                BoardUnit unit = state.Board.GetUnit(enemySide, i);

                if (unit != null)
                {
                    return unit;
                }
            }

            return null;
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

            CancelPendingTargetedEffectIfNonMandatory();

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
            InitializeHealthToEffectiveMax(unit);
            SyncQualifyingEnemyAuraHealth();

            TriggerOnPlay(unit);

            return true;
        }

        private void InitializeHealthToEffectiveMax(BoardUnit unit)
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
                    Debug.Log($"[PhaseManager] SyncQualifyingEnemyAuraHealth: {unit.SourceCard.CardName} (slot {unit.SlotIndex}, {side}) qualifying-aura health bonus grew by {delta} ({unit.LastSyncedAuraHealthBonus}->{currentAuraHealthBonus}), CurrentHealth now {unit.CurrentHealth}.");
                }

                unit.LastSyncedAuraHealthBonus = currentAuraHealthBonus;
            }
        }

        private void TopUpAllUnitsToEffectiveMaxHealth(PlayerSide side)
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

        public bool CanPlayUnit(UnitCardData card, int slotIndex)
        {
            if (HasBlockingPendingTargetedEffect()) return false;
            if (state.CurrentPhase != TurnPhase.Play) return false;

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

        public bool CanAnyUnitAttack()
        {
            for (int i = 0; i < Board.SlotsPerSide; i++)
            {
                BoardUnit unit = state.Board.GetUnit(state.ActivePlayer, i);

                if (unit == null) continue;
                if (unit.PlacedThisTurn && !unit.HasKeyword(Keyword.Rush, state)) continue;
                if (IsStunned(unit)) continue;
                if (unit.GetCurrentAttack(state) <= 0) continue;

                Debug.Log($"[PhaseManager] CanAnyUnitAttack: {unit.SourceCard.CardName} (Slot={i}) can attack this turn.");
                return true;
            }

            Debug.Log("[PhaseManager] CanAnyUnitAttack: no eligible attackers found.");
            return false;
        }

        private static bool IsStunned(BoardUnit unit)
        {
            for (int i = 0; i < unit.Statuses.Count; i++)
            {
                if (unit.Statuses[i].Type == StatusEffectType.Stunned)
                {
                    return true;
                }
            }

            return false;
        }

        private const float AttackAnimationDuration = 0.5f;
        private const float BannerWaitTimeout = 3f;

        public bool CanAnyUnitMove()
        {
            for (int fromSlot = 0; fromSlot < Board.SlotsPerSide; fromSlot++)
            {
                BoardUnit unit = state.Board.GetUnit(state.ActivePlayer, fromSlot);

                if (unit == null) continue;

                for (int toSlot = 0; toSlot < Board.SlotsPerSide; toSlot++)
                {
                    if (fromSlot == toSlot) continue;

                    if (CanMoveUnit(fromSlot, toSlot))
                    {
                        Debug.Log($"[PhaseManager] CanAnyUnitMove: {unit.SourceCard.CardName} can move {fromSlot} -> {toSlot}.");
                        return true;
                    }
                }
            }

            if (HasAvailableGrantedEnemyMove(state.ActivePlayer))
            {
                Debug.Log("[PhaseManager] CanAnyUnitMove: a granted enemy-move ability is available.");
                return true;
            }

            Debug.Log("[PhaseManager] CanAnyUnitMove: no legal moves found.");
            return false;
        }

        public void EnterAttackPhase()
        {
            CancelPendingTargetedEffectIfNonMandatory();

            if (HasBlockingPendingTargetedEffect())
            {
                Debug.LogWarning("[PhaseManager] EnterAttackPhase blocked: an On-Play effect is still awaiting a target.");
                return;
            }

            if (state.HasPendingFreeMove)
            {
                Debug.Log("[PhaseManager] Unused pending free move expired at end of Play phase.");
            }

            state.HasPendingFreeMove = false;
            state.PendingFreeMoveExcludedUnit = null;

            TickLeaderDamageShield(state.GetPlayer(state.ActivePlayer.Opposite()));

            if (!CanAnyUnitAttack())
            {
                Debug.Log("[PhaseManager] EnterAttackPhase: no eligible attackers — skipping attack phase entirely, banner will not show.");
                FinishAttackPhase();
                return;
            }

            state.CurrentPhase = TurnPhase.Attack;

            if (coroutineRunner != null)
            {
                coroutineRunner.StartCoroutine(WaitForBannerThenRunAttackPhase());
            }
            else
            {
                RunAttackPhaseInstant();
            }
        }

        private IEnumerator WaitForBannerThenRunAttackPhase()
        {
            bool bannerFinished = false;

            void OnBannerFinished()
            {
                bannerFinished = true;
            }

            state.BannerAnimationFinished += OnBannerFinished;

            float elapsed = 0f;

            while (!bannerFinished && elapsed < BannerWaitTimeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            state.BannerAnimationFinished -= OnBannerFinished;

            if (!bannerFinished)
            {
                Debug.LogWarning("[PhaseManager] WaitForBannerThenRunAttackPhase: timed out waiting for BannerAnimationFinished — proceeding anyway.");
            }
            else
            {
                Debug.Log($"[PhaseManager] WaitForBannerThenRunAttackPhase: banner signalled finished after {elapsed:F2}s.");
            }

            yield return RunAttackPhaseSequence();
        }

        private IEnumerator RunAttackPhaseSequence()
        {
            for (int i = 0; i < Board.SlotsPerSide; i++)
            {
                BoardUnit attacker = state.Board.GetUnit(state.ActivePlayer, i);

                if (attacker == null) continue;

                bool wasStunned = ConsumeStunIfPresent(attacker);
                bool hadDoubleAttack = ConsumeStatus(attacker, StatusEffectType.DoubleAttackNextAttack);
                int temporaryAttackBonus = ConsumeStatusMagnitude(attacker, StatusEffectType.TemporaryAttackNextAttack);

                if (attacker.PlacedThisTurn && !attacker.HasKeyword(Keyword.Rush, state)) continue;
                if (wasStunned) continue;

                int effectiveAttack = attacker.GetCurrentAttack(state) + temporaryAttackBonus;

                if (effectiveAttack <= 0)
                {
                    Debug.Log($"[PhaseManager] {attacker.SourceCard.CardName} (Slot={i}) has 0 effective attack — skipping attack animation, resolving combat immediately.");
                    bool chainAttackNoAnim = ResolveAttackerCombat(attacker, i, hadDoubleAttack, temporaryAttackBonus);

                    if (state.IsGameOver)
                    {
                        break;
                    }

                    if (chainAttackNoAnim)
                    {
                        ResolveChainAttack(attacker, i, temporaryAttackBonus);

                        if (state.IsGameOver)
                        {
                            break;
                        }
                    }

                    continue;
                }

                state.CurrentlyAttackingUnit = attacker;
                yield return new WaitForSeconds(AttackAnimationDuration);

                if (state.Board.GetUnit(attacker.Owner, attacker.SlotIndex) != attacker)
                {
                    state.CurrentlyAttackingUnit = null;
                    continue;
                }

                bool shouldChainAttack = ResolveAttackerCombat(attacker, i, hadDoubleAttack, temporaryAttackBonus);

                state.CurrentlyAttackingUnit = null;

                if (state.IsGameOver)
                {
                    break;
                }

                if (shouldChainAttack)
                {
                    yield return null;

                    if (state.Board.GetUnit(attacker.Owner, attacker.SlotIndex) == attacker)
                    {
                        int chainEffectiveAttack = attacker.GetCurrentAttack(state) + temporaryAttackBonus;

                        if (chainEffectiveAttack <= 0)
                        {
                            Debug.Log($"[PhaseManager] {attacker.SourceCard.CardName}'s chained attack has 0 effective attack — skipping animation, resolving immediately.");
                            ResolveChainAttack(attacker, i, temporaryAttackBonus);
                        }
                        else
                        {
                            state.CurrentlyAttackingUnit = attacker;
                            yield return new WaitForSeconds(AttackAnimationDuration);

                            if (state.Board.GetUnit(attacker.Owner, attacker.SlotIndex) == attacker)
                            {
                                ResolveChainAttack(attacker, i, temporaryAttackBonus);
                            }

                            state.CurrentlyAttackingUnit = null;
                        }
                    }

                    if (state.IsGameOver)
                    {
                        break;
                    }
                }
            }

            FinishAttackPhase();
        }

        private void RunAttackPhaseInstant()
        {
            for (int i = 0; i < Board.SlotsPerSide; i++)
            {
                BoardUnit attacker = state.Board.GetUnit(state.ActivePlayer, i);

                if (attacker == null) continue;

                bool wasStunned = ConsumeStunIfPresent(attacker);
                bool hadDoubleAttack = ConsumeStatus(attacker, StatusEffectType.DoubleAttackNextAttack);
                int temporaryAttackBonus = ConsumeStatusMagnitude(attacker, StatusEffectType.TemporaryAttackNextAttack);

                if (attacker.PlacedThisTurn && !attacker.HasKeyword(Keyword.Rush, state)) continue;
                if (wasStunned) continue;

                bool shouldChainAttack = ResolveAttackerCombat(attacker, i, hadDoubleAttack, temporaryAttackBonus);

                if (state.IsGameOver) break;

                if (shouldChainAttack)
                {
                    ResolveChainAttack(attacker, i, temporaryAttackBonus);

                    if (state.IsGameOver) break;
                }
            }

            FinishAttackPhase();
        }

        private bool ResolveAttackerCombat(BoardUnit attacker, int slotIndex, bool hadDoubleAttack, int temporaryAttackBonus)
        {
            int attackCount = hadDoubleAttack ? 2 : 1;
            bool shouldChainAttack = false;

            for (int attackIndex = 0; attackIndex < attackCount; attackIndex++)
            {
                bool killedDefender;

                if (attacker.HasKeyword(Keyword.BifurcatedAttack, state))
                {
                    int beforeSlot = slotIndex - 1;
                    int afterSlot = slotIndex + 1;
                    killedDefender = false;

                    if (beforeSlot >= 0)
                    {
                        killedDefender |= ResolveAttack(attacker, beforeSlot, temporaryAttackBonus);
                    }

                    if (afterSlot < Board.SlotsPerSide)
                    {
                        killedDefender |= ResolveAttack(attacker, afterSlot, temporaryAttackBonus);
                    }
                }
                else
                {
                    killedDefender = ResolveAttack(attacker, slotIndex, temporaryAttackBonus);
                }

                if (state.IsGameOver) break;

                Debug.Log($"[PhaseManager] {attacker.SourceCard.CardName} ResolveAttackerCombat: attackIndex={attackIndex}, killedDefender={killedDefender}, HasAttackAgainOnKill={HasAttackAgainOnKill(attacker)}.");

                if (killedDefender && attacker.CurrentHealth > 0 && HasAttackAgainOnKill(attacker))
                {
                    shouldChainAttack = true;
                }
            }

            return shouldChainAttack;
        }

        private void ResolveChainAttack(BoardUnit attacker, int slotIndex, int temporaryAttackBonus)
        {
            Debug.Log($"[PhaseManager] {attacker.SourceCard.CardName} killed its target — chaining a bonus attack into slot {slotIndex}.");
            ResolveAttack(attacker, slotIndex, temporaryAttackBonus);
        }

        private static bool HasAttackAgainOnKill(BoardUnit attacker)
        {
            if (attacker.IsSilenced)
            {
                return false;
            }

            foreach (CardEffect effect in attacker.SourceCard.Effects)
            {
                if (effect.trigger == EffectTriggerType.OnAttack && effect.action == EffectActionType.AttackAgainOnKill)
                {
                    return true;
                }
            }

            return false;
        }

        private void FinishAttackPhase()
        {
            CheckWinCondition();

            if (!state.IsGameOver)
            {
                EnterMovePhase();
            }
        }

        private bool ResolveAttack(BoardUnit attacker, int targetSlot, int temporaryAttackBonus = 0)
        {
            int attackerCurrentAttack = attacker.GetCurrentAttack(state) + temporaryAttackBonus;

            if (attacker.HasKeyword(Keyword.Piercing, state))
            {
                DamageLeader(state.ActivePlayer.Opposite(), attackerCurrentAttack);
                TriggerOnAttack(attacker, null, attackerCurrentAttack);
                return false;
            }

            BoardUnit defender = state.Board.GetOpponentUnit(state.ActivePlayer, targetSlot);
            bool killedDefender = false;

            if (defender != null)
            {
                defender.CurrentHealth -= attackerCurrentAttack;

                Debug.Log($"[PhaseManager] {defender.SourceCard.CardName} took {attackerCurrentAttack} damage from {attacker.SourceCard.CardName}, CurrentHealth={defender.CurrentHealth}.");

                if (defender.CurrentHealth <= 0)
                {
                    KillUnit(defender, state.ActivePlayer);
                    killedDefender = true;
                }
                else if (defender.HasKeyword(Keyword.Retaliate, state))
                {
                    int retaliateDamage = defender.GetCurrentAttack(state);
                    attacker.CurrentHealth -= retaliateDamage;

                    Debug.Log($"[PhaseManager] {defender.SourceCard.CardName} (Retaliate) survived and dealt {retaliateDamage} back to {attacker.SourceCard.CardName}, CurrentHealth={attacker.CurrentHealth}.");

                    if (attacker.CurrentHealth <= 0)
                    {
                        KillUnit(attacker, defender.Owner);
                    }
                }
            }
            else
            {
                DamageLeader(state.ActivePlayer.Opposite(), attackerCurrentAttack);
            }

            if (attacker.CurrentHealth <= 0)
            {
                Debug.Log($"[PhaseManager] {attacker.SourceCard.CardName} died before its OnAttack effects could resolve — skipping TriggerOnAttack.");
                return killedDefender;
            }

            TriggerOnAttack(attacker, defender, attackerCurrentAttack);

            return killedDefender;
        }

        private void TriggerOnAttack(BoardUnit attacker, BoardUnit defender, int damageDealt)
        {
            if (attacker.IsSilenced)
            {
                return;
            }

            EffectTarget selfTarget = EffectTarget.ForUnit(attacker);
            EffectContext context = new EffectContext(state, attacker.Owner, attacker, selfTarget);

            foreach (CardEffect effect in attacker.SourceCard.Effects)
            {
                if (effect.trigger != EffectTriggerType.OnAttack)
                {
                    continue;
                }

                if (effect.action == EffectActionType.ApplyDecay)
                {
                    if (defender != null)
                    {
                        EffectContext decayContext = new EffectContext(state, attacker.Owner, attacker, EffectTarget.ForUnit(defender));
                        EffectExecutor.Execute(effect, decayContext, this);
                    }

                    continue;
                }

                EffectExecutor.Execute(effect, context, this, damageDealt);
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

        public bool TransformUnit(BoardUnit originalUnit, UnitCardData replacementCard)
        {
            if (originalUnit == null || replacementCard == null)
            {
                return false;
            }

            PlayerSide owner = originalUnit.Owner;
            int slotIndex = originalUnit.SlotIndex;

            Debug.Log($"[PhaseManager] TransformUnit: {originalUnit.SourceCard.CardName} PlacedThisTurn={originalUnit.PlacedThisTurn}, HasMovedThisTurn={originalUnit.HasMovedThisTurn} before transform.");

            state.Board.RemoveUnit(owner, slotIndex);

            BoardUnit replacementUnit = new BoardUnit(replacementCard, owner, slotIndex);
            replacementUnit.PlacedThisTurn = originalUnit.PlacedThisTurn;
            replacementUnit.HasMovedThisTurn = originalUnit.HasMovedThisTurn;
            replacementUnit.HasUsedGrantedEnemyMoveThisTurn = originalUnit.HasUsedGrantedEnemyMoveThisTurn;
            state.Board.PlaceUnit(owner, slotIndex, replacementUnit);
            InitializeHealthToEffectiveMax(replacementUnit);
            SyncQualifyingEnemyAuraHealth();

            Debug.Log($"[PhaseManager] {originalUnit.SourceCard.CardName} was transformed into {replacementCard.CardName} in slot {slotIndex} for {owner}. Replacement PlacedThisTurn={replacementUnit.PlacedThisTurn}.");

            return true;
        }

        public void KillUnit(BoardUnit unit, PlayerSide killer)
        {
            state.Board.RemoveUnit(unit.Owner, unit.SlotIndex);
            TriggerOnDeath(unit);
            TriggerOnAllyDeath(unit);
            TriggerLeaderEffects(EffectTriggerType.UnitDied, killer, unit);
            TriggerLeaderEffectsFor(state.GetPlayer(killer), EffectTriggerType.UnitKilled, killer, unit);
        }

        private void TriggerOnAllyDeath(BoardUnit deadUnit)
        {
            List<BoardUnit> survivingAllies = new List<BoardUnit>(state.Board.GetUnits(deadUnit.Owner));

            foreach (BoardUnit ally in survivingAllies)
            {
                if (ally.IsSilenced)
                {
                    continue;
                }

                foreach (CardEffect effect in ally.SourceCard.Effects)
                {
                    if (effect.trigger != EffectTriggerType.OnAllyDeath)
                    {
                        continue;
                    }

                    EffectTarget immediateTarget = EffectTargeting.ResolveImmediateTarget(effect.targetType, ally, ally.Owner, state);
                    Debug.Log($"[PhaseManager] {deadUnit.SourceCard.CardName}'s death triggered {ally.SourceCard.CardName}'s OnAllyDeath effect (targetType={effect.targetType}), target.Kind={immediateTarget.Kind}.");
                    EffectContext context = new EffectContext(state, ally.Owner, ally, immediateTarget);
                    EffectExecutor.Execute(effect, context, this);
                }
            }
        }

        private void TriggerOnDeath(BoardUnit unit)
        {
            if (unit.IsSilenced)
            {
                Debug.Log($"[PhaseManager] {unit.SourceCard.CardName} is Silenced — skipping OnDeath effects.");
            }
            else
            {
                foreach (CardEffect effect in unit.SourceCard.Effects)
                {
                    if (effect.trigger != EffectTriggerType.OnDeath)
                    {
                        continue;
                    }

                    if (RequiresChosenTarget(effect.targetType))
                    {
                        Debug.LogWarning($"[PhaseManager] {unit.SourceCard.CardName}'s On-Death effect requires a chosen target, which isn't supported yet since the source unit is already off the board — skipping.");
                        continue;
                    }

                    EffectTarget immediateTarget = EffectTargeting.ResolveImmediateTarget(effect.targetType, unit, unit.Owner, state);
                    Debug.Log($"[PhaseManager] {unit.SourceCard.CardName}'s OnDeath effect (targetType={effect.targetType}) resolved immediately, target.Kind={immediateTarget.Kind}, LeaderSide={immediateTarget.LeaderSide}.");
                    EffectContext context = new EffectContext(state, unit.Owner, unit, immediateTarget);
                    EffectExecutor.Execute(effect, context, this);
                }
            }

            if (unit.HasKeyword(Keyword.Unstable, state))
            {
                TriggerUnstable(unit);
            }
        }

        private void TriggerUnstable(BoardUnit unit)
        {
            PlayerSide enemySide = unit.Owner.Opposite();
            List<BoardUnit> candidates = new List<BoardUnit>(state.Board.GetUnits(enemySide));

            Debug.Log($"[PhaseManager] {unit.SourceCard.CardName} (Unstable) died, found {candidates.Count} enemy unit(s) as possible targets.");

            if (candidates.Count == 0)
            {
                Debug.Log($"[PhaseManager] {unit.SourceCard.CardName}'s Unstable has no valid enemy target — fizzling.");
                return;
            }

            System.Random rng = new System.Random();
            BoardUnit target = candidates[rng.Next(candidates.Count)];

            Debug.Log($"[PhaseManager] {unit.SourceCard.CardName}'s Unstable dealing 1 damage to {target.SourceCard.CardName} in slot {target.SlotIndex}.");

            target.CurrentHealth -= 1;

            if (target.CurrentHealth <= 0)
            {
                KillUnit(target, unit.Owner);
            }
        }

        public void BounceUnit(BoardUnit unit)
        {
            Player owner = state.GetPlayer(unit.Owner);

            state.Board.RemoveUnit(unit.Owner, unit.SlotIndex);

            int permanentAttack = unit.SourceCard.Attack + unit.BonusAttack;
            int permanentHealth = unit.MaxHealth;
            bool hasPermanentStatChange = unit.BonusAttack != 0 || unit.MaxHealth != unit.SourceCard.Health;

            CardData cardForHand = unit.SourceCard;

            if (hasPermanentStatChange)
            {
                cardForHand = unit.SourceCard.CreateStatOverrideClone(permanentAttack, permanentHealth);
                Debug.Log($"[PhaseManager] {unit.SourceCard.CardName} bounced with permanently modified stats — cloned as {permanentAttack}/{permanentHealth}.");
            }

            if (!owner.TryAddCardToHand(cardForHand))
            {
                Debug.Log($"[PhaseManager] {unit.SourceCard.CardName} could not be bounced — {owner.Side}'s hand is already at the {Player.AbsoluteMaxHandSize}-card max, card is burned.");
            }
        }

        public void SilenceUnit(BoardUnit unit)
        {
            if (unit == null)
            {
                Debug.Log("[PhaseManager] SilenceUnit FAIL: unit is null.");
                return;
            }

            if (unit.IsSilenced)
            {
                Debug.Log($"[PhaseManager] {unit.SourceCard.CardName} is already Silenced — not stacking a second instance.");
                return;
            }

            unit.Statuses.Add(new ActiveStatusEffect(StatusEffectType.Silenced, 1));

            Debug.Log($"[PhaseManager] {unit.SourceCard.CardName} was Silenced through the end of their owner's next turn.");
        }

        public bool SwapAttackAndHealth(BoardUnit unit)
        {
            if (unit == null)
            {
                Debug.Log("[PhaseManager] SwapAttackAndHealth FAIL: unit is null.");
                return false;
            }

            if (unit.HasKeyword(Keyword.Unmoving, state))
            {
                Debug.Log($"[PhaseManager] SwapAttackAndHealth FAIL: {unit.SourceCard.CardName} is Unmoving.");
                return false;
            }

            int oldCurrentAttack = unit.GetCurrentAttack(state);
            int oldCurrentHealth = unit.CurrentHealth;
            int oldEffectiveMaxHealth = unit.GetEffectiveMaxHealth(state);

            int auraAttackBonus = AuraCalculator.GetAttackBonus(unit, state);
            int auraMaxHealthBonus = oldEffectiveMaxHealth - unit.MaxHealth;

            unit.BonusAttack = oldCurrentHealth - unit.SourceCard.Attack - auraAttackBonus;
            unit.MaxHealth = oldCurrentAttack - auraMaxHealthBonus;
            unit.CurrentHealth = unit.GetEffectiveMaxHealth(state);
            unit.LastSyncedAuraHealthBonus = AuraCalculator.GetQualifyingEnemyAuraHealthBonus(unit, state);

            Debug.Log($"[PhaseManager] {unit.SourceCard.CardName}'s Attack and Health swapped: Attack {oldCurrentAttack}->{unit.GetCurrentAttack(state)}, Health {oldCurrentHealth}/{oldEffectiveMaxHealth}->{unit.CurrentHealth}/{unit.GetEffectiveMaxHealth(state)}.");

            if (unit.CurrentHealth <= 0)
            {
                Debug.Log($"[PhaseManager] {unit.SourceCard.CardName}'s swap left it at 0 or less Health — dying.");
                KillUnit(unit, unit.Owner);
                return true;
            }

            SyncQualifyingEnemyAuraHealth();

            return true;
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

        private static int ConsumeStatusMagnitude(BoardUnit unit, StatusEffectType type)
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


        private void TickDecay(Player owner)
        {
            for (int slot = 0; slot < Board.SlotsPerSide; slot++)
            {
                BoardUnit unit = state.Board.GetUnit(owner.Side, slot);
                if (unit == null) continue;

                int decayStacks = 0;

                foreach (ActiveStatusEffect status in unit.Statuses)
                {
                    if (status.Type == StatusEffectType.Decaying)
                    {
                        decayStacks++;
                    }
                }

                if (decayStacks == 0) continue;

                PlayerSide? killer = null;

                foreach (ActiveStatusEffect status in unit.Statuses)
                {
                    if (status.Type == StatusEffectType.Decaying)
                    {
                        killer = status.SourceOwner;
                        break;
                    }
                }

                unit.CurrentHealth -= decayStacks;

                if (unit.CurrentHealth <= 0)
                {
                    KillUnit(unit, killer ?? unit.Owner.Opposite());
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
            if (!CanAnyUnitMove())
            {
                Debug.Log("[PhaseManager] EnterMovePhase: no legal moves available — skipping Move phase entirely, banner will not show.");
                PassMoveToEndTurn();
                return;
            }

            state.CurrentPhase = TurnPhase.Move;
        }

        public bool TryMoveUnit(int fromSlot, int toSlot)
        {
            if (!CanMoveUnit(fromSlot, toSlot)) return false;

            PlayerSide side = state.ActivePlayer;
            BoardUnit unit = state.Board.GetUnit(side, fromSlot);
            bool isNimble = unit.HasKeyword(Keyword.Nimble, state);

            Player mover = state.GetPlayer(side);
            LeaderData moverLeader = mover.Leader;

            if (moverLeader != null && moverLeader.MoveManaCost > 0)
            {
                mover.CurrentMana -= moverLeader.MoveManaCost;
            }

            state.Board.RemoveUnit(side, fromSlot);
            state.Board.PlaceUnit(side, toSlot, unit);

            unit.HasMovedThisTurn = true;

            if (!isNimble)
            {
                state.HasUsedMoveThisTurn = true;
            }

            GrantCodyMoveBonusIfApplicable(unit);

            return true;
        }

        public bool MoveUnitFree(PlayerSide side, int fromSlot, int toSlot)
        {
            if (state.ActivePlayer != side) return false;
            if (!CanMoveUnit(fromSlot, toSlot, ignoreMoveLimitAndCost: true)) return false;

            BoardUnit unit = state.Board.GetUnit(side, fromSlot);

            state.Board.RemoveUnit(side, fromSlot);
            state.Board.PlaceUnit(side, toSlot, unit);

            GrantCodyMoveBonusIfApplicable(unit);

            return true;
        }

        public bool HookClosestAllyLeft(BoardUnit sourceUnit)
        {
            if (sourceUnit == null)
            {
                Debug.Log("[PhaseManager] HookClosestAllyLeft FAIL: sourceUnit is null.");
                return false;
            }

            PlayerSide side = sourceUnit.Owner;
            int destinationSlot = sourceUnit.SlotIndex - 1;

            if (destinationSlot < 0)
            {
                Debug.Log($"[PhaseManager] {sourceUnit.SourceCard.CardName}'s Hook has no slot to its left — fizzling.");
                return false;
            }

            BoardUnit closestAlly = null;

            for (int slot = destinationSlot; slot >= 0; slot--)
            {
                BoardUnit candidate = state.Board.GetUnit(side, slot);
                if (candidate != null)
                {
                    closestAlly = candidate;
                    break;
                }
            }

            if (closestAlly == null)
            {
                Debug.Log($"[PhaseManager] {sourceUnit.SourceCard.CardName}'s Hook found no ally unit to its left — fizzling.");
                return false;
            }

            if (closestAlly.SlotIndex == destinationSlot)
            {
                Debug.Log($"[PhaseManager] {sourceUnit.SourceCard.CardName}'s Hook: closest ally {closestAlly.SourceCard.CardName} is already in the destination slot — nothing to do.");
                return false;
            }

            if (closestAlly.HasKeyword(Keyword.Unmoving, state))
            {
                Debug.Log($"[PhaseManager] {sourceUnit.SourceCard.CardName}'s Hook FAIL: {closestAlly.SourceCard.CardName} is Unmoving.");
                return false;
            }

            int fromSlot = closestAlly.SlotIndex;

            state.Board.RemoveUnit(side, fromSlot);
            state.Board.PlaceUnit(side, destinationSlot, closestAlly);

            GrantCodyMoveBonusIfApplicable(closestAlly);

            Debug.Log($"[PhaseManager] {sourceUnit.SourceCard.CardName}'s Hook moved {closestAlly.SourceCard.CardName} from slot {fromSlot} to slot {destinationSlot}.");

            return true;
        }

        public void PushAlliesAwayFrom(BoardUnit sourceUnit)
        {
            if (sourceUnit == null) return;

            PlayerSide side = sourceUnit.Owner;
            int sourceSlot = sourceUnit.SlotIndex;

            List<BoardUnit> leftGroup = new List<BoardUnit>();
            List<BoardUnit> rightGroup = new List<BoardUnit>();

            for (int i = 0; i < Board.SlotsPerSide; i++)
            {
                if (i == sourceSlot) continue;

                BoardUnit unit = state.Board.GetUnit(side, i);
                if (unit == null) continue;

                if (i < sourceSlot)
                {
                    leftGroup.Add(unit);
                }
                else
                {
                    rightGroup.Add(unit);
                }
            }

            foreach (BoardUnit unit in leftGroup)
            {
                TryPushUnitOneSlot(side, unit, -1);
            }

            for (int i = rightGroup.Count - 1; i >= 0; i--)
            {
                TryPushUnitOneSlot(side, rightGroup[i], 1);
            }
        }

        private void TryPushUnitOneSlot(PlayerSide side, BoardUnit unit, int direction)
        {
            if (unit.HasKeyword(Keyword.Unmoving, state)) return;

            int fromSlot = unit.SlotIndex;
            int toSlot = fromSlot + direction;

            if (toSlot < 0 || toSlot >= Board.SlotsPerSide) return;
            if (state.Board.GetUnit(side, toSlot) != null) return;

            state.Board.RemoveUnit(side, fromSlot);
            state.Board.PlaceUnit(side, toSlot, unit);

            GrantCodyMoveBonusIfApplicable(unit);
        }

        public bool SwapUnitSlots(BoardUnit unitA, BoardUnit unitB)
        {
            if (unitA == null || unitB == null || unitA == unitB) return false;
            if (unitA.HasKeyword(Keyword.Unmoving, state) || unitB.HasKeyword(Keyword.Unmoving, state)) return false;

            PlayerSide sideA = unitA.Owner;
            PlayerSide sideB = unitB.Owner;
            int slotA = unitA.SlotIndex;
            int slotB = unitB.SlotIndex;

            state.Board.RemoveUnit(sideA, slotA);
            state.Board.RemoveUnit(sideB, slotB);

            state.Board.PlaceUnit(sideA, slotB, unitA);
            state.Board.PlaceUnit(sideB, slotA, unitB);

            GrantCodyMoveBonusIfApplicable(unitA);
            GrantCodyMoveBonusIfApplicable(unitB);

            return true;
        }

        public bool BounceUnitOpposite(BoardUnit sourceUnit)
        {
            if (sourceUnit == null) return false;

            PlayerSide enemySide = sourceUnit.Owner.Opposite();
            BoardUnit opposingUnit = state.Board.GetUnit(enemySide, sourceUnit.SlotIndex);

            if (opposingUnit == null) return false;

            BounceUnit(opposingUnit);
            return true;
        }

        public bool PullUnitOpposite(BoardUnit sourceUnit, BoardUnit targetUnit)
        {
            if (sourceUnit == null || targetUnit == null) return false;
            if (sourceUnit.Owner == targetUnit.Owner) return false;
            if (targetUnit.HasKeyword(Keyword.Unmoving, state)) return false;

            int destinationSlot = sourceUnit.SlotIndex;
            PlayerSide targetSide = targetUnit.Owner;

            if (targetUnit.SlotIndex == destinationSlot)
            {
                return false;
            }

            BoardUnit occupant = state.Board.GetUnit(targetSide, destinationSlot);
            if (occupant != null)
            {
                return false;
            }

            state.Board.RemoveUnit(targetSide, targetUnit.SlotIndex);
            state.Board.PlaceUnit(targetSide, destinationSlot, targetUnit);

            return true;
        }

        private void GrantCodyMoveBonusIfApplicable(BoardUnit unit)
        {
            Player owner = state.GetPlayer(unit.Owner);
            LeaderData ownerLeader = owner.Leader;

            if (ownerLeader != null && ownerLeader.MoveTemporaryAttackBonus > 0)
            {
                unit.Statuses.Add(new ActiveStatusEffect(StatusEffectType.TemporaryAttackNextAttack, 1, ownerLeader.MoveTemporaryAttackBonus));
            }
        }

        public bool CanMoveUnit(int fromSlot, int toSlot, bool ignoreMoveLimitAndCost = false)
        {
            PlayerSide side = state.ActivePlayer;
            BoardUnit unit = state.Board.GetUnit(side, fromSlot);

            if (unit == null) return false;
            if (unit.HasKeyword(Keyword.Unmoving, state)) return false;

            bool isNimble = unit.HasKeyword(Keyword.Nimble, state);

            if (!ignoreMoveLimitAndCost)
            {
                if (unit.PlacedThisTurn && !isNimble) return false;
                if (!isNimble && state.HasUsedMoveThisTurn) return false;
                if (isNimble && unit.HasMovedThisTurn) return false;

                Player mover = state.GetPlayer(side);
                int moveManaCost = mover.Leader != null ? mover.Leader.MoveManaCost : 0;
                if (mover.CurrentMana < moveManaCost) return false;
            }

            return IsMoveRangeLegal(side, unit, fromSlot, toSlot);
        }

        private bool IsMoveRangeLegal(PlayerSide side, BoardUnit unit, int fromSlot, int toSlot)
        {
            if (state.Board.GetUnit(side, toSlot) != null) return false;
            if (fromSlot == toSlot) return false;

            if (unit.HasKeyword(Keyword.Teleport, state))
            {
                Debug.Log($"[PhaseManager] {unit.SourceCard.CardName} is using Teleport — range check bypassed, moving {fromSlot} -> {toSlot}.");
                return true;
            }

            int distance = toSlot - fromSlot;
            int maxRange = unit.HasKeyword(Keyword.Agile, state) ? 2 : 1;

            if (System.Math.Abs(distance) > maxRange) return false;

            int step = distance > 0 ? 1 : -1;
            for (int slot = fromSlot + step; slot != toSlot; slot += step)
            {
                if (state.Board.GetUnit(side, slot) != null) return false;
            }

            return true;
        }

        public bool HasAvailableGrantedEnemyMove(PlayerSide controllingSide)
        {
            return FindAvailableEnemyMoveGranter(controllingSide) != null;
        }

        private BoardUnit FindAvailableEnemyMoveGranter(PlayerSide controllingSide)
        {
            for (int i = 0; i < Board.SlotsPerSide; i++)
            {
                BoardUnit unit = state.Board.GetUnit(controllingSide, i);

                if (unit == null) continue;
                if (!(unit.SourceCard is UnitCardData unitCard)) continue;
                if (!unitCard.GrantsEnemyUnitMove) continue;
                if (unit.HasUsedGrantedEnemyMoveThisTurn) continue;

                return unit;
            }

            return null;
        }

        public bool CanMoveEnemyUnitViaGrantedAbility(PlayerSide controllingSide, int fromSlot, int toSlot)
        {
            if (FindAvailableEnemyMoveGranter(controllingSide) == null) return false;

            PlayerSide enemySide = controllingSide.Opposite();
            BoardUnit unit = state.Board.GetUnit(enemySide, fromSlot);

            if (unit == null) return false;

            return IsMoveRangeLegal(enemySide, unit, fromSlot, toSlot);
        }

        public bool MoveEnemyUnitViaGrantedAbility(PlayerSide controllingSide, int fromSlot, int toSlot)
        {
            if (!CanMoveEnemyUnitViaGrantedAbility(controllingSide, fromSlot, toSlot)) return false;

            BoardUnit granter = FindAvailableEnemyMoveGranter(controllingSide);
            PlayerSide enemySide = controllingSide.Opposite();
            BoardUnit unit = state.Board.GetUnit(enemySide, fromSlot);

            state.Board.RemoveUnit(enemySide, fromSlot);
            state.Board.PlaceUnit(enemySide, toSlot, unit);

            granter.HasUsedGrantedEnemyMoveThisTurn = true;

            return true;
        }

        public void EnterTurnEndPhase()
        {
            state.CurrentPhase = TurnPhase.TurnEnd;
        }

        public void PassMoveToEndTurn()
        {
            EnterTurnEndPhase();
            EndTurn();
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
                    unit.HasUsedGrantedEnemyMoveThisTurn = false;

                    if (unit.IsSilenced)
                    {
                        unit.Statuses.RemoveAll(status => status.Type == StatusEffectType.Silenced);
                        Debug.Log($"[PhaseManager] {unit.SourceCard.CardName}'s Silence wore off at the end of {state.ActivePlayer}'s turn.");
                    }
                }
            }

            state.HasUsedMoveThisTurn = false;
            state.PlayerA.TriggeredOncePerTurnEffects.Clear();
            state.PlayerB.TriggeredOncePerTurnEffects.Clear();
            state.PlayerA.HasUsedFirstUnitDiscountThisTurn = false;
            state.PlayerB.HasUsedFirstUnitDiscountThisTurn = false;

            Player endingPlayer = state.GetPlayer(state.ActivePlayer);
            if (endingPlayer.HasNextItemDoubled)
            {
                Debug.Log("[PhaseManager] Unused item-doubling bonus expired at end of turn.");
            }
            endingPlayer.HasNextItemDoubled = false;

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

            CancelPendingTargetedEffectIfNonMandatory();

            Player active = state.GetActivePlayerData();

            active.CurrentMana -= card.ManaCost;
            active.Hand.Remove(card);

            bool isDoubled = active.HasNextItemDoubled;
            active.HasNextItemDoubled = false;

            CardEffect effect = card.PrimaryEffect;
            EffectContext context = new EffectContext(state, state.ActivePlayer, null, target);
            EffectExecutor.Execute(effect, context, this);

            if (isDoubled)
            {
                Debug.Log($"[PhaseManager] {card.CardName} played twice due to Bobby H. Chicago's bonus.");
                EffectExecutor.Execute(effect, context, this);
            }

            return true;
        }

        public bool CanPlayItem(ItemCardData card, EffectTarget target)
        {
            if (HasBlockingPendingTargetedEffect())
            {
                Debug.Log("[PhaseManager] CanPlayItem FAIL: a mandatory On-Play effect is still awaiting a target.");
                return false;
            }

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
            foreach (CardEffect effect in unit.SourceCard.Effects)
            {
                if (effect.trigger != EffectTriggerType.OnPlay)
                {
                    continue;
                }

                if (effect.action == EffectActionType.ChooseXCards)
                {
                    DeferCardPoolChoice(effect, unit);
                    continue;
                }

                if (effect.action == EffectActionType.ChooseFixedCard)
                {
                    DeferFixedCardChoice(effect, unit);
                    continue;
                }

                if (RequiresChosenTarget(effect.targetType))
                {
                    DeferTargetedEffect(effect, unit, EffectTriggerType.OnPlay);
                }
                else
                {
                    EffectTarget immediateTarget = EffectTargeting.ResolveImmediateTarget(effect.targetType, unit, unit.Owner, state);
                    Debug.Log($"[PhaseManager] {unit.SourceCard.CardName}'s OnPlay effect (targetType={effect.targetType}) resolved immediately, target.Kind={immediateTarget.Kind}, LeaderSide={immediateTarget.LeaderSide}.");
                    EffectContext immediateContext = new EffectContext(state, unit.Owner, unit, immediateTarget);
                    EffectExecutor.Execute(effect, immediateContext, this);
                }
            }

            TriggerLeaderEffects(EffectTriggerType.OnPlay, unit.Owner, unit);
        }

        private static bool RequiresChosenTarget(TargetType targetType)
        {
            return targetType != TargetType.None
                && targetType != TargetType.Board
                && targetType != TargetType.Self
                && targetType != TargetType.AllyLeader
                && targetType != TargetType.EnemyLeader
                && targetType != TargetType.OpposingEnemy;
        }

        private void DeferCardPoolChoice(CardEffect effect, BoardUnit sourceUnit)
        {
            Player owner = state.GetPlayer(sourceUnit.Owner);
            List<CardData> eligibleCards = owner.Deck.FindAll(card => MatchesCardCategory(card, effect.cardCategory));

            Debug.Log($"[PhaseManager] {sourceUnit.SourceCard.CardName}'s ChooseXCards effect (category={effect.cardCategory}) found {eligibleCards.Count} eligible card(s) in deck.");

            if (eligibleCards.Count == 0)
            {
                Debug.Log($"[PhaseManager] {sourceUnit.SourceCard.CardName}'s ChooseXCards effect has no eligible cards to offer — fizzling.");
                return;
            }

            List<CardData> shuffledCopy = new List<CardData>(eligibleCards);
            ListShuffler.Shuffle(shuffledCopy);

            int offerCount = Mathf.Min(effect.amount, shuffledCopy.Count);
            List<CardData> offeredCards = shuffledCopy.GetRange(0, offerCount);

            state.PendingCardChoiceOptions = offeredCards;
            state.PendingCardChoiceSource = sourceUnit;

            Debug.Log($"[PhaseManager] {sourceUnit.SourceCard.CardName} is now awaiting a card choice from {offerCount} option(s): {string.Join(", ", offeredCards.ConvertAll(c => c.CardName))}.");
        }

        private void DeferFixedCardChoice(CardEffect effect, BoardUnit sourceUnit)
        {
            if (effect.fixedChoiceOptions == null || effect.fixedChoiceOptions.Length == 0)
            {
                Debug.Log($"[PhaseManager] {sourceUnit.SourceCard.CardName}'s ChooseFixedCard effect has no fixedChoiceOptions configured on the asset — fizzling.");
                return;
            }

            List<CardData> offeredCards = new List<CardData>(effect.fixedChoiceOptions);

            state.PendingCardChoiceOptions = offeredCards;
            state.PendingCardChoiceSource = sourceUnit;

            Debug.Log($"[PhaseManager] {sourceUnit.SourceCard.CardName} is now awaiting a fixed card choice from {offeredCards.Count} option(s): {string.Join(", ", offeredCards.ConvertAll(c => c.CardName))}.");
        }

        private static bool MatchesCardCategory(CardData card, CardCategory category)
        {
            switch (category)
            {
                case CardCategory.Unit:
                    return card is UnitCardData;
                case CardCategory.Item:
                    return card is ItemCardData;
                default:
                    return true;
            }
        }

        public bool TryResolvePendingCardChoice(CardData chosenCard)
        {
            if (state.PendingCardChoiceOptions == null || state.PendingCardChoiceSource == null)
            {
                Debug.Log("[PhaseManager] TryResolvePendingCardChoice FAIL: no pending card choice.");
                return false;
            }

            if (!state.PendingCardChoiceOptions.Contains(chosenCard))
            {
                Debug.Log($"[PhaseManager] TryResolvePendingCardChoice FAIL: {chosenCard?.CardName} was not one of the offered options.");
                return false;
            }

            BoardUnit sourceUnit = state.PendingCardChoiceSource;
            Player owner = state.GetPlayer(sourceUnit.Owner);

            state.PendingCardChoiceOptions = null;
            state.PendingCardChoiceSource = null;

            bool cameFromDeck = owner.Deck.Contains(chosenCard);

            Debug.Log($"[PhaseManager] TryResolvePendingCardChoice: {chosenCard.CardName} cameFromDeck={cameFromDeck}.");

            if (cameFromDeck)
            {
                owner.Deck.Remove(chosenCard);
            }

            bool added = owner.TryAddCardToHand(chosenCard);

            if (!added)
            {
                if (cameFromDeck)
                {
                    owner.Deck.Add(chosenCard);
                }

                Debug.Log($"[PhaseManager] TryResolvePendingCardChoice FAIL: {owner.Side}'s hand is full, {chosenCard.CardName}{(cameFromDeck ? " returned to deck" : " discarded")}.");
                return false;
            }

            Debug.Log($"[PhaseManager] {sourceUnit.SourceCard.CardName}'s card choice resolved: {chosenCard.CardName} added to {owner.Side}'s hand.");

            return true;
        }

        private void DeferTargetedEffect(CardEffect effect, BoardUnit sourceUnit, EffectTriggerType trigger, bool excludeSource = true)
        {
            BoardUnit excludingUnit = excludeSource ? sourceUnit : null;
            bool hasValidTarget = BoardHasValidTarget(effect.targetType, sourceUnit.Owner, excludingUnit);

            if (!hasValidTarget)
            {
                Debug.Log($"[PhaseManager] {sourceUnit.SourceCard.CardName}'s {trigger} effect has no valid target on board — fizzling.");
                return;
            }

            state.PendingTargetedEffect = effect;
            state.PendingTargetedEffectSource = sourceUnit;
            state.PendingTargetedEffectTrigger = trigger;
            Debug.Log($"[PhaseManager] {sourceUnit.SourceCard.CardName}'s {trigger} effect is now awaiting a target click.");
        }

        private bool BoardHasValidTarget(TargetType targetType, PlayerSide sourceOwner, BoardUnit excludingUnit = null)
        {
            for (int i = 0; i < Board.SlotsPerSide; i++)
            {
                BoardUnit candidateA = state.Board.GetUnit(PlayerSide.PlayerA, i);
                if (candidateA != null && candidateA != excludingUnit && EffectTargeting.IsValidTarget(targetType, EffectTarget.ForUnit(candidateA), state))
                {
                    return true;
                }

                BoardUnit candidateB = state.Board.GetUnit(PlayerSide.PlayerB, i);
                if (candidateB != null && candidateB != excludingUnit && EffectTargeting.IsValidTarget(targetType, EffectTarget.ForUnit(candidateB), state))
                {
                    return true;
                }
            }

            if (EffectTargeting.IsValidTarget(targetType, EffectTarget.ForLeader(PlayerSide.PlayerA), state))
            {
                return true;
            }

            if (EffectTargeting.IsValidTarget(targetType, EffectTarget.ForLeader(PlayerSide.PlayerB), state))
            {
                return true;
            }

            return false;
        }

        public bool TryResolvePendingTargetedEffect(EffectTarget chosenTarget)
        {
            if (state.PendingTargetedEffect == null || state.PendingTargetedEffectSource == null)
            {
                Debug.Log("[PhaseManager] TryResolvePendingTargetedEffect FAIL: no pending effect.");
                return false;
            }

            bool cameFromTurnStart = state.PendingTargetedEffectTrigger == EffectTriggerType.OnTurnStart;

            if (state.IsExcludedAsSelfTarget(chosenTarget.Kind == EffectTargetKind.Unit ? chosenTarget.Unit : null))
            {
                Debug.Log("[PhaseManager] TryResolvePendingTargetedEffect FAIL: clicked the source unit itself.");
                return false;
            }

            if (!EffectTargeting.IsValidTarget(state.PendingTargetedEffect.targetType, chosenTarget, state))
            {
                Debug.Log($"[PhaseManager] TryResolvePendingTargetedEffect FAIL: target invalid for targetType={state.PendingTargetedEffect.targetType}, chosenTarget.Kind={chosenTarget.Kind}");
                return false;
            }

            CardEffect effect = state.PendingTargetedEffect;
            BoardUnit sourceUnit = state.PendingTargetedEffectSource;
            PlayerSide sourceOwner = sourceUnit.Owner;

            state.PendingTargetedEffect = null;
            state.PendingTargetedEffectSource = null;
            state.PendingTargetedEffectTrigger = null;

            EffectContext context = new EffectContext(state, sourceUnit.Owner, sourceUnit, chosenTarget);
            EffectExecutor.Execute(effect, context, this);

            Debug.Log($"[PhaseManager] TryResolvePendingTargetedEffect SUCCESS: {effect.action} resolved.");

            if (cameFromTurnStart && state.IsResolvingTurnStartEffects)
            {
                ContinueTurnStartScan(state.GetPlayer(sourceOwner), state.TurnStartScanSlot);
            }

            return true;
        }

        public bool HasBlockingPendingTargetedEffect()
        {
            bool hasBlockingTargetedEffect = state.PendingTargetedEffect != null && state.PendingTargetedEffect.mandatoryTarget;
            bool hasBlockingCardChoice = state.PendingCardChoiceOptions != null;

            return hasBlockingTargetedEffect || hasBlockingCardChoice;
        }

        public void CancelPendingTargetedEffectIfNonMandatory()
        {
            if (state.PendingTargetedEffect == null)
            {
                return;
            }

            if (state.PendingTargetedEffect.mandatoryTarget)
            {
                return;
            }

            Debug.Log($"[PhaseManager] Non-mandatory pending effect on {state.PendingTargetedEffectSource?.SourceCard?.CardName} was cancelled.");

            bool cameFromTurnStart = state.PendingTargetedEffectTrigger == EffectTriggerType.OnTurnStart;
            PlayerSide? sourceOwner = state.PendingTargetedEffectSource?.Owner;

            state.PendingTargetedEffect = null;
            state.PendingTargetedEffectSource = null;
            state.PendingTargetedEffectTrigger = null;

            if (cameFromTurnStart && state.IsResolvingTurnStartEffects && sourceOwner.HasValue)
            {
                ContinueTurnStartScan(state.GetPlayer(sourceOwner.Value), state.TurnStartScanSlot);
            }
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
            if (!(card is UnitCardData unitCard))
            {
                return;
            }

            CardEffect randomizeEffect = null;

            foreach (CardEffect effect in unitCard.Effects)
            {
                if (effect.trigger == EffectTriggerType.OnDraw && effect.action == EffectActionType.RandomizeStatsOnDraw)
                {
                    randomizeEffect = effect;
                    break;
                }
            }

            if (randomizeEffect == null)
            {
                return;
            }

            Player player = state.GetPlayer(side);
            int handIndex = player.Hand.IndexOf(card);

            if (handIndex < 0)
            {
                return;
            }

            System.Random rng = new System.Random();
            UnitCardData randomizedClone = unitCard.CreateRandomizedClone(1, 6, rng);

            player.Hand[handIndex] = randomizedClone;

            Debug.Log($"[PhaseManager] {unitCard.CardName} randomized on draw: Cost={randomizedClone.ManaCost}, Attack={randomizedClone.Attack}, Health={randomizedClone.Health}");
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