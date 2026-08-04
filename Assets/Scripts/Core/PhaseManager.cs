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
        private readonly MovementResolver movement;
        private readonly UnitLifecycleService lifecycle;

        private List<CardData> draftPool;
        private DraftSettings draftSettings;
        private readonly Dictionary<PlayerSide, int> draftPickIndexInStage = new Dictionary<PlayerSide, int>();

        public PhaseManager(GameState state, MonoBehaviour coroutineRunner)
        {
            this.state = state;
            this.coroutineRunner = coroutineRunner;
            this.movement = new MovementResolver(state);
            this.lifecycle = new UnitLifecycleService(state);

            this.state.UnitMoved += TriggerOnMove;
        }

        public void StartDraft(List<CardData> pool, DraftSettings settings)
        {
            draftPool = new List<CardData>(pool);
            draftSettings = settings;

            state.CurrentPhase = TurnPhase.Draft;

            Debug.Log("[PhaseManager] Draft started. Both players are drafting simultaneously.");

            StartDraftForSide(PlayerSide.PlayerA);
            StartDraftForSide(PlayerSide.PlayerB);
        }

        private void StartDraftForSide(PlayerSide side)
        {
            Player player = state.GetPlayer(side);
            player.CurrentDraftStage = DraftStage.Common;
            draftPickIndexInStage[side] = 0;

            OfferNextDraftPick(side);
        }

        private void OfferNextDraftPick(PlayerSide side)
        {
            Player player = state.GetPlayer(side);
            DraftStage stage = player.CurrentDraftStage.Value;
            List<CardData> eligible = GetEligibleDraftCards(stage, side);

            if (eligible.Count == 0)
            {
                Debug.LogWarning($"[PhaseManager] No eligible {stage} cards left for {side} — skipping this pick.");
                player.PendingDraftOptions = null;
                draftPickIndexInStage[side]++;
                AdvanceDraft(side);
                return;
            }

            ListShuffler.Shuffle(eligible);
            int optionCount = Mathf.Min(draftSettings.optionsPerChoice, eligible.Count);
            player.PendingDraftOptions = eligible.GetRange(0, optionCount);

            Debug.Log($"[PhaseManager] Offering {side} {optionCount} {stage} option(s): {string.Join(", ", player.PendingDraftOptions.ConvertAll(c => c.CardName))}");

            state.RaiseDraftOptionsChanged();
        }

        private List<CardData> GetEligibleDraftCards(DraftStage stage, PlayerSide side)
        {
            Player drafter = state.GetPlayer(side);
            List<CardData> eligible = new List<CardData>();

            foreach (CardData card in draftPool)
            {
                if (!MatchesDraftStage(card.Rarity, stage))
                {
                    continue;
                }

                int copiesInDeck = CountCopiesInDeck(drafter.Deck, card);

                if (copiesInDeck >= draftSettings.maxCopiesPerCard)
                {
                    Debug.Log($"[PhaseManager] GetEligibleDraftCards: {card.CardName} already has {copiesInDeck}/{draftSettings.maxCopiesPerCard} copies in {side}'s deck — excluding from {stage} offers.");
                    continue;
                }

                eligible.Add(card);
            }

            return eligible;
        }

        private static int CountCopiesInDeck(List<CardData> deck, CardData card)
        {
            int count = 0;

            foreach (CardData deckCard in deck)
            {
                if (deckCard == card)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool MatchesDraftStage(CardRarity rarity, DraftStage stage)
        {
            switch (stage)
            {
                case DraftStage.Common:
                    return rarity == CardRarity.Common;
                case DraftStage.Uncommon:
                    return rarity == CardRarity.Uncommon;
                case DraftStage.Rare:
                    return rarity == CardRarity.Rare;
                case DraftStage.EpicOrLegendary:
                    return rarity == CardRarity.Epic || rarity == CardRarity.Legendary;
                default:
                    return false;
            }
        }

        public bool TryResolvePendingDraftChoice(PlayerSide side, CardData chosenCard)
        {
            Player drafter = state.GetPlayer(side);

            if (drafter.PendingDraftOptions == null || drafter.CurrentDraftStage == null)
            {
                Debug.Log($"[PhaseManager] TryResolvePendingDraftChoice FAIL: no pending draft choice for {side}.");
                return false;
            }

            if (!drafter.PendingDraftOptions.Contains(chosenCard))
            {
                Debug.Log($"[PhaseManager] TryResolvePendingDraftChoice FAIL: {chosenCard?.CardName} was not one of {side}'s offered options.");
                return false;
            }

            DraftStage stage = drafter.CurrentDraftStage.Value;
            int copies = GetCopiesForStage(stage);

            for (int i = 0; i < copies; i++)
            {
                drafter.Deck.Add(chosenCard);
            }

            Debug.Log($"[PhaseManager] {side} drafted {chosenCard.CardName} x{copies} ({stage}).");

            drafter.PendingDraftOptions = null;
            draftPickIndexInStage[side]++;

            AdvanceDraft(side);

            return true;
        }

        private int GetPickCountForStage(DraftStage stage)
        {
            switch (stage)
            {
                case DraftStage.Common:
                    return draftSettings.commonPicks;
                case DraftStage.Uncommon:
                    return draftSettings.uncommonPicks;
                case DraftStage.Rare:
                    return draftSettings.rarePicks;
                case DraftStage.EpicOrLegendary:
                    return draftSettings.epicOrLegendaryPicks;
                default:
                    return 0;
            }
        }

        private int GetCopiesForStage(DraftStage stage)
        {
            switch (stage)
            {
                case DraftStage.Common:
                    return draftSettings.copiesPerCommonPick;
                case DraftStage.Uncommon:
                    return draftSettings.copiesPerUncommonPick;
                case DraftStage.Rare:
                    return draftSettings.copiesPerRarePick;
                case DraftStage.EpicOrLegendary:
                    return draftSettings.copiesPerEpicOrLegendaryPick;
                default:
                    return 1;
            }
        }

        private static DraftStage? GetNextDraftStage(DraftStage stage)
        {
            switch (stage)
            {
                case DraftStage.Common:
                    return DraftStage.Uncommon;
                case DraftStage.Uncommon:
                    return DraftStage.Rare;
                case DraftStage.Rare:
                    return DraftStage.EpicOrLegendary;
                default:
                    return null;
            }
        }

        private void AdvanceDraft(PlayerSide side)
        {
            Player player = state.GetPlayer(side);
            DraftStage stage = player.CurrentDraftStage.Value;
            int picksForStage = GetPickCountForStage(stage);

            if (draftPickIndexInStage[side] < picksForStage)
            {
                OfferNextDraftPick(side);
                return;
            }

            draftPickIndexInStage[side] = 0;
            DraftStage? nextStage = GetNextDraftStage(stage);

            if (nextStage != null)
            {
                player.CurrentDraftStage = nextStage;
                OfferNextDraftPick(side);
                return;
            }

            player.CurrentDraftStage = null;
            Debug.Log($"[PhaseManager] {side}'s draft is complete.");

            if (state.PlayerA.CurrentDraftStage != null || state.PlayerB.CurrentDraftStage != null)
            {
                Debug.Log("[PhaseManager] Waiting on the other player to finish drafting.");
                return;
            }

            Debug.Log("[PhaseManager] Draft complete for both players.");

            ListShuffler.Shuffle(state.PlayerA.Deck);
            ListShuffler.Shuffle(state.PlayerB.Deck);

            state.ActivePlayer = state.FirstPlayer;

            StartMatch();
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

            TriggerLeaderEffects(EffectTriggerType.OnGameStart, state.FirstPlayer, null);

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

            List<CardData> validCardsToMulligan = new List<CardData>();

            foreach (CardData card in cardsToMulligan)
            {
                if (player.GameStartBonusCards.Contains(card))
                {
                    Debug.LogWarning($"[PhaseManager] {card.CardName} is a GameStartBonusCard for {side} - ignoring attempt to mulligan it.");
                    continue;
                }

                validCardsToMulligan.Add(card);
            }

            foreach (CardData card in validCardsToMulligan)
            {
                player.Hand.Remove(card);
            }

            for (int i = 0; i < validCardsToMulligan.Count; i++)
            {
                if (player.Deck.Count == 0) break;

                CardData drawn = player.DrawCard();
                if (drawn != null)
                {
                    TriggerOnDraw(side, drawn);
                }
            }

            foreach (CardData card in validCardsToMulligan)
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
            EnterActionPhase();
        }

        private void ResolveTurnStartDrain(BoardUnit source, CardEffect healEffect)
        {
            BoardUnit target = FindFirstEnemyUnit(source.Owner);

            if (target == null)
            {
                return;
            }

            int damageDealt = System.Math.Min(healEffect.amount, target.CurrentHealth);
            DamageUnit(target, healEffect.amount, source.Owner, DamageSourceType.Effect);

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

        public void EnterActionPhase()
        {
            state.CurrentPhase = TurnPhase.Action;
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
            int manaShort = effectiveCost - active.CurrentMana;

            if (manaShort > 0 && AuraCalculator.TryGetHealthCostForManaShortfall(active, manaShort, out int healthCost))
            {
                Debug.Log($"[PhaseManager] {active.Side} converting {healthCost} health into {manaShort} mana to afford {card.CardName}.");
                DamageLeader(active.Side, healthCost);
                active.CurrentMana += manaShort;
            }

            active.CurrentMana -= effectiveCost;

            active.Hand.Remove(card);

            BoardUnit occupyingUnit = state.Board.GetUnit(state.ActivePlayer, slotIndex);

            BoardUnit unit = occupyingUnit != null
                ? AbsorbUnit(occupyingUnit, card, slotIndex)
                : PlaceNewUnit(card, slotIndex);

            TriggerOnPlay(unit);

            return true;
        }

        private BoardUnit PlaceNewUnit(UnitCardData card, int slotIndex)
        {
            return lifecycle.PlaceNewUnit(card, slotIndex);
        }

        private BoardUnit AbsorbUnit(BoardUnit absorbedUnit, UnitCardData card, int slotIndex)
        {
            return lifecycle.AbsorbUnit(absorbedUnit, card, slotIndex);
        }

        private void InitializeHealthToEffectiveMax(BoardUnit unit)
        {
            lifecycle.InitializeHealthToEffectiveMax(unit);
        }

        public void SyncQualifyingEnemyAuraHealth()
        {
            lifecycle.SyncQualifyingEnemyAuraHealth();
        }

        private void TopUpAllUnitsToEffectiveMaxHealth(PlayerSide side)
        {
            lifecycle.TopUpAllUnitsToEffectiveMaxHealth(side);
        }

        public bool CanPlayUnit(UnitCardData card, int slotIndex)
        {
            if (HasBlockingPendingTargetedEffect()) return false;
            if (state.CurrentPhase != TurnPhase.Action) return false;

            Player active = state.GetActivePlayerData();
            int effectiveCost = AuraCalculator.GetUnitCost(card, active);
            int manaShort = effectiveCost - active.CurrentMana;

            if (manaShort > 0 && !AuraCalculator.TryGetHealthCostForManaShortfall(active, manaShort, out _))
            {
                return false;
            }

            if (!active.Hand.Contains(card)) return false;

            BoardUnit occupyingUnit = state.Board.GetUnit(state.ActivePlayer, slotIndex);

            if (occupyingUnit != null)
            {
                return card.HasKeyword(Keyword.Absorb);
            }

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
                if (!CanUnitAttack(unit)) continue;

                Debug.Log($"[PhaseManager] CanAnyUnitAttack: {unit.SourceCard.CardName} (Slot={i}) can attack this turn.");
                return true;
            }

            Debug.Log("[PhaseManager] CanAnyUnitAttack: no eligible attackers found.");
            return false;
        }

        public bool CanUnitAttack(BoardUnit unit)
        {
            if (unit == null) return false;
            if (unit.PlacedThisTurn && !unit.HasKeyword(Keyword.Rush, state)) return false;
            if (unit.IsStunned) return false;
            if (unit.GetCurrentAttack(state) <= 0) return false;

            return true;
        }

        private const float BannerWaitTimeout = 3f;
        private const float AttackHitLandedWarningTime = 3f;
        private const float AttackHitLandedTimeout = 6f;
        private const float AttackAnimationFinishedTimeout = 3f;

        public bool CanAnyUnitMove()
        {
            return movement.CanAnyUnitMove();
        }


        public bool CanAttackWithUnit(int slotIndex)
        {
            if (state.CurrentPhase != TurnPhase.Action) return false;

            BoardUnit unit = state.Board.GetUnit(state.ActivePlayer, slotIndex);

            if (unit == null) return false;
            if (unit.HasAttackedThisTurn) return false;

            return CanUnitAttack(unit);
        }

        public bool TryAttackWithUnit(int slotIndex, System.Action onAttackFullyResolved = null)
        {
            if (!CanAttackWithUnit(slotIndex)) return false;

            BoardUnit attacker = state.Board.GetUnit(state.ActivePlayer, slotIndex);

            bool hadDoubleAttack = ConsumeStatus(attacker, StatusEffectType.DoubleAttackNextAttack);
            int temporaryAttackBonus = ConsumeStatusMagnitude(attacker, StatusEffectType.TemporaryAttackNextAttack);

            attacker.HasAttackedThisTurn = true;

            int effectiveAttack = attacker.GetCurrentAttack(state) + temporaryAttackBonus;

            if (effectiveAttack <= 0 || coroutineRunner == null)
            {
                Debug.Log($"[PhaseManager] {attacker.SourceCard.CardName} (Slot={slotIndex}) attacking without animation (effectiveAttack={effectiveAttack}, coroutineRunner={(coroutineRunner != null ? "set" : "null")}).");

                bool chainAttack = ResolveAttackerCombat(attacker, slotIndex, hadDoubleAttack, temporaryAttackBonus);

                CheckWinCondition();

                if (!state.IsGameOver && chainAttack)
                {
                    ResolveChainAttack(attacker, slotIndex, temporaryAttackBonus);
                    CheckWinCondition();
                }

                onAttackFullyResolved?.Invoke();

                return true;
            }

            coroutineRunner.StartCoroutine(RunSingleAttackAnimated(attacker, slotIndex, hadDoubleAttack, temporaryAttackBonus, onAttackFullyResolved));
            return true;
        }

        private IEnumerator RunSingleAttackAnimated(BoardUnit attacker, int slotIndex, bool hadDoubleAttack, int temporaryAttackBonus, System.Action onAttackFullyResolved)
        {
            state.CurrentlyAttackingUnit = attacker;
            Debug.Log($"[PhaseManager] CurrentlyAttackingUnit = {attacker.SourceCard.CardName} (this field is NOT networked - only visible on this machine). Coroutine now waiting on the animation before damage is applied.");

            bool shouldChainAttack = false;
            yield return ResolveAttackerCombatAnimated(attacker, slotIndex, hadDoubleAttack, temporaryAttackBonus, result => shouldChainAttack = result);

            state.CurrentlyAttackingUnit = null;
            Debug.Log($"[PhaseManager] {attacker.SourceCard.CardName}'s attack coroutine finished - invoking onAttackFullyResolved now so the network layer can broadcast.");

            CheckWinCondition();

            if (state.IsGameOver || !shouldChainAttack)
            {
                onAttackFullyResolved?.Invoke();
                yield break;
            }

            yield return null;

            if (state.Board.GetUnit(attacker.Owner, attacker.SlotIndex) != attacker)
            {
                onAttackFullyResolved?.Invoke();
                yield break;
            }

            int chainEffectiveAttack = attacker.GetCurrentAttack(state) + temporaryAttackBonus;

            if (chainEffectiveAttack <= 0)
            {
                Debug.Log($"[PhaseManager] {attacker.SourceCard.CardName}'s chained attack has 0 effective attack — skipping animation, resolving immediately.");
                ResolveChainAttack(attacker, slotIndex, temporaryAttackBonus);
                CheckWinCondition();
                onAttackFullyResolved?.Invoke();
                yield break;
            }

            state.CurrentlyAttackingUnit = attacker;
            yield return WaitForAttackHitLanded(0);

            if (state.Board.GetUnit(attacker.Owner, attacker.SlotIndex) == attacker)
            {
                ResolveChainAttack(attacker, slotIndex, temporaryAttackBonus);
                CheckWinCondition();
            }

            state.CurrentlyAttackingUnit = null;
            onAttackFullyResolved?.Invoke();
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

        private IEnumerator WaitForAttackHitLanded(int expectedHitIndex)
        {
            bool hitLanded = false;

            void OnHitLanded(int hitIndex)
            {
                if (hitIndex == expectedHitIndex)
                {
                    hitLanded = true;
                }
            }

            state.AttackHitLanded += OnHitLanded;

            float elapsed = 0f;
            bool hasWarned = false;

            while (!hitLanded && elapsed < AttackHitLandedTimeout)
            {
                elapsed += Time.deltaTime;

                if (!hasWarned && elapsed >= AttackHitLandedWarningTime)
                {
                    hasWarned = true;
                    Debug.LogWarning($"[PhaseManager] WaitForAttackHitLanded: still waiting for hitIndex={expectedHitIndex} after {elapsed:F2}s. Check the attacker's Animator has an Animation Event calling OnAttackHitLanded({expectedHitIndex}).");
                }

                yield return null;
            }

            state.AttackHitLanded -= OnHitLanded;

            if (!hitLanded)
            {
                Debug.LogWarning($"[PhaseManager] WaitForAttackHitLanded: timed out after {elapsed:F2}s waiting for hitIndex={expectedHitIndex} — proceeding anyway so the attack phase doesn't soft lock.");
            }
        }

        private IEnumerator WaitForAttackAnimationFinished(BoardUnit attacker)
        {
            bool animationFinished = false;

            void OnAnimationFinished(BoardUnit unit)
            {
                if (unit == attacker)
                {
                    animationFinished = true;
                }
            }

            state.AttackAnimationFinished += OnAnimationFinished;

            float elapsed = 0f;

            while (!animationFinished && elapsed < AttackAnimationFinishedTimeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            state.AttackAnimationFinished -= OnAnimationFinished;

            if (!animationFinished)
            {
                Debug.LogWarning($"[PhaseManager] WaitForAttackAnimationFinished: timed out after {elapsed:F2}s waiting for {attacker.SourceCard.CardName}'s attack animation to finish — proceeding anyway.");
            }
        }

        private IEnumerator ResolveAttackerCombatAnimated(BoardUnit attacker, int slotIndex, bool hadDoubleAttack, int temporaryAttackBonus, System.Action<bool> onComplete)
        {
            int attackCount = hadDoubleAttack ? 2 : 1;
            bool shouldChainAttack = false;
            bool isBifurcated = attacker.HasKeyword(Keyword.BifurcatedAttack, state);

            for (int attackIndex = 0; attackIndex < attackCount; attackIndex++)
            {
                bool killedDefender;

                if (attackIndex > 0)
                {
                    Debug.Log($"[PhaseManager] {attacker.SourceCard.CardName} waiting for attack {attackIndex - 1}'s animation to finish before repeat attack {attackIndex}.");

                    yield return WaitForAttackAnimationFinished(attacker);

                    if (state.Board.GetUnit(attacker.Owner, attacker.SlotIndex) != attacker)
                    {
                        break;
                    }

                    Debug.Log($"[PhaseManager] {attacker.SourceCard.CardName} starting repeat attack {attackIndex} (DoubleAttack) — retriggering attack animation.");

                    state.CurrentlyAttackingUnit = null;
                    yield return null;

                    if (state.Board.GetUnit(attacker.Owner, attacker.SlotIndex) != attacker)
                    {
                        break;
                    }

                    state.CurrentlyAttackingUnit = attacker;
                }

                if (isBifurcated)
                {
                    int beforeSlot = slotIndex - 1;
                    int afterSlot = slotIndex + 1;
                    killedDefender = false;

                    yield return WaitForAttackHitLanded(0);

                    if (state.Board.GetUnit(attacker.Owner, attacker.SlotIndex) != attacker)
                    {
                        break;
                    }

                    if (beforeSlot >= 0)
                    {
                        killedDefender |= ResolveAttack(attacker, beforeSlot, temporaryAttackBonus);
                    }

                    if (state.IsGameOver) break;

                    yield return WaitForAttackHitLanded(1);

                    if (state.Board.GetUnit(attacker.Owner, attacker.SlotIndex) != attacker)
                    {
                        break;
                    }

                    if (afterSlot < Board.SlotsPerSide)
                    {
                        killedDefender |= ResolveAttack(attacker, afterSlot, temporaryAttackBonus);
                    }
                }
                else
                {
                    yield return WaitForAttackHitLanded(0);

                    if (state.Board.GetUnit(attacker.Owner, attacker.SlotIndex) != attacker)
                    {
                        break;
                    }

                    killedDefender = ResolveAttack(attacker, slotIndex, temporaryAttackBonus);
                }

                if (state.IsGameOver) break;

                Debug.Log($"[PhaseManager] {attacker.SourceCard.CardName} ResolveAttackerCombatAnimated: attackIndex={attackIndex}, killedDefender={killedDefender}, HasAttackAgainOnKill={HasAttackAgainOnKill(attacker)}.");

                if (killedDefender && attacker.CurrentHealth > 0 && HasAttackAgainOnKill(attacker))
                {
                    shouldChainAttack = true;
                }
            }

            onComplete(shouldChainAttack);
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

            if (defender != null && movement.TrySlippyDodge(defender))
            {
                Debug.Log($"[PhaseManager] {defender.SourceCard.CardName} dodged out of slot {targetSlot} via Slippy — attack now resolves against an empty slot.");
                defender = null;
            }

            bool killedDefender = false;

            if (defender != null)
            {
                killedDefender = DamageUnit(defender, attackerCurrentAttack, state.ActivePlayer, DamageSourceType.Combat);

                if (!killedDefender && defender.HasKeyword(Keyword.Retaliate, state))
                {
                    int retaliateDamage = defender.GetCurrentAttack(state);
                    Debug.Log($"[PhaseManager] {defender.SourceCard.CardName} (Retaliate) survived and deals {retaliateDamage} back to {attacker.SourceCard.CardName}.");
                    DamageUnit(attacker, retaliateDamage, defender.Owner, DamageSourceType.Combat);
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

        public bool DamageLeader(PlayerSide side, int amount)
        {
            return lifecycle.DamageLeader(side, amount);
        }

        public void HealUnit(BoardUnit unit, int amount)
        {
            lifecycle.HealUnit(unit, amount);
        }

        public void HealLeader(PlayerSide side, int amount)
        {
            lifecycle.HealLeader(side, amount);
        }

        public bool TransformUnit(BoardUnit originalUnit, UnitCardData replacementCard)
        {
            return lifecycle.TransformUnit(originalUnit, replacementCard);
        }

        public bool SpawnUnit(PlayerSide side, int slotIndex, UnitCardData card)
        {
            return lifecycle.SpawnUnit(side, slotIndex, card);
        }

        public void KillUnit(BoardUnit unit, PlayerSide killer)
        {
            state.Board.RemoveUnit(unit.Owner, unit.SlotIndex);

            Player deadUnitOwner = state.GetPlayer(unit.Owner);
            deadUnitOwner.AlliedUnitsDied++;

            Debug.Log($"[PhaseManager] {unit.SourceCard.CardName} died. {unit.Owner}'s AlliedUnitsDied is now {deadUnitOwner.AlliedUnitsDied}.");

            TriggerOnDeath(unit);
            TriggerOnAllyDeath(unit);
            TriggerLeaderEffects(EffectTriggerType.UnitDied, killer, unit);
            TriggerLeaderEffectsFor(state.GetPlayer(killer), EffectTriggerType.UnitKilled, killer, unit);

            SyncQualifyingEnemyAuraHealth();
        }

        public bool DamageUnit(BoardUnit target, int amount, PlayerSide source, DamageSourceType sourceType)
        {
            if (target == null || amount <= 0)
            {
                Debug.Log($"[PhaseManager] DamageUnit skipped: target={(target == null ? "null" : target.SourceCard.CardName)}, amount={amount}.");
                return false;
            }

            if (UnitLifecycleService.ConsumeShieldIfPresent(target.Statuses))
            {
                Debug.Log($"[PhaseManager] {target.SourceCard.CardName} had Shield — {amount} {sourceType} damage from {source} blocked and Shield consumed.");
                return false;
            }

            target.CurrentHealth -= amount;

            Debug.Log($"[PhaseManager] {target.SourceCard.CardName} took {amount} {sourceType} damage from {source}, CurrentHealth={target.CurrentHealth}.");

            TriggerOnDamaged(target, source, sourceType);

            if (target.CurrentHealth <= 0)
            {
                KillUnit(target, source);
                return true;
            }

            return false;
        }

        private void TriggerOnDamaged(BoardUnit unit, PlayerSide source, DamageSourceType sourceType)
        {
            if (unit.IsSilenced)
            {
                Debug.Log($"[PhaseManager] {unit.SourceCard.CardName} is Silenced — skipping OnDamaged effects.");
                return;
            }

            foreach (CardEffect effect in unit.SourceCard.Effects)
            {
                if (effect.trigger != EffectTriggerType.OnDamaged)
                {
                    continue;
                }

                if (RequiresChosenTarget(effect.targetType))
                {
                    Debug.LogWarning($"[PhaseManager] {unit.SourceCard.CardName}'s OnDamaged effect requires a chosen target, which isn't supported yet — skipping.");
                    continue;
                }

                EffectTarget immediateTarget = EffectTargeting.ResolveImmediateTarget(effect.targetType, unit, unit.Owner, state);
                Debug.Log($"[PhaseManager] {unit.SourceCard.CardName}'s OnDamaged effect (targetType={effect.targetType}, sourceType={sourceType}) resolved immediately, target.Kind={immediateTarget.Kind}.");
                EffectContext context = new EffectContext(state, unit.Owner, unit, immediateTarget);
                EffectExecutor.Execute(effect, context, this);
            }
        }

        private void TriggerOnMove(BoardUnit unit)
        {
            if (unit == null)
            {
                return;
            }

            if (state.Board.GetUnit(unit.Owner, unit.SlotIndex) != unit)
            {
                Debug.Log($"[PhaseManager] TriggerOnMove skipped for {unit.SourceCard.CardName} — unit is no longer on the board at slot {unit.SlotIndex}.");
                return;
            }

            if (unit.IsSilenced)
            {
                Debug.Log($"[PhaseManager] {unit.SourceCard.CardName} is Silenced — skipping OnMove effects.");
                return;
            }

            foreach (CardEffect effect in unit.SourceCard.Effects)
            {
                if (effect.trigger != EffectTriggerType.OnMove)
                {
                    continue;
                }

                if (RequiresChosenTarget(effect.targetType))
                {
                    Debug.LogWarning($"[PhaseManager] {unit.SourceCard.CardName}'s OnMove effect requires a chosen target, which isn't supported yet — skipping.");
                    continue;
                }

                EffectTarget immediateTarget = EffectTargeting.ResolveImmediateTarget(effect.targetType, unit, unit.Owner, state);
                Debug.Log($"[PhaseManager] {unit.SourceCard.CardName}'s OnMove effect (targetType={effect.targetType}) resolved immediately, target.Kind={immediateTarget.Kind}.");
                EffectContext context = new EffectContext(state, unit.Owner, unit, immediateTarget);
                EffectExecutor.Execute(effect, context, this);
            }
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

            int unstableDamage = unit.GetCurrentAttack(state);

            Debug.Log($"[PhaseManager] {unit.SourceCard.CardName}'s Unstable dealing {unstableDamage} (its Attack) damage to {target.SourceCard.CardName} in slot {target.SlotIndex}.");

            DamageUnit(target, unstableDamage, unit.Owner, DamageSourceType.Effect);
        }

        public void BounceUnit(BoardUnit unit)
        {
            lifecycle.BounceUnit(unit);
        }

        public void SilenceUnit(BoardUnit unit)
        {
            lifecycle.SilenceUnit(unit);
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
            int oldBaseAttack = oldCurrentAttack - auraAttackBonus;

            Debug.Log($"[PhaseManager] {unit.SourceCard.CardName} SwapAttackAndHealth: oldCurrentAttack={oldCurrentAttack}, auraAttackBonus={auraAttackBonus}, oldBaseAttack={oldBaseAttack}, oldCurrentHealth={oldCurrentHealth}.");

            unit.BonusAttack = oldCurrentHealth - unit.SourceCard.Attack;
            unit.MaxHealth = oldBaseAttack;
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

        private static bool ConsumeStatus(BoardUnit unit, StatusEffectType type)
        {
            return UnitLifecycleService.ConsumeStatus(unit, type);
        }

        private static int ConsumeStatusMagnitude(BoardUnit unit, StatusEffectType type)
        {
            return UnitLifecycleService.ConsumeStatusMagnitude(unit, type);
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

                DamageUnit(unit, decayStacks, killer ?? unit.Owner.Opposite(), DamageSourceType.Effect);
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

        public bool TryMoveUnit(int fromSlot, int toSlot)
        {
            return movement.TryMoveUnit(fromSlot, toSlot);
        }

        public bool MoveUnitFree(PlayerSide side, int fromSlot, int toSlot)
        {
            return movement.MoveUnitFree(side, fromSlot, toSlot);
        }

        public bool HookClosestAllyLeft(BoardUnit sourceUnit)
        {
            return movement.HookClosestAllyLeft(sourceUnit);
        }

        public void PushAlliesAwayFrom(BoardUnit sourceUnit)
        {
            movement.PushAlliesAwayFrom(sourceUnit);
        }

        public bool SwapUnitSlots(BoardUnit unitA, BoardUnit unitB)
        {
            return movement.SwapUnitSlots(unitA, unitB);
        }

        public bool BounceUnitOpposite(BoardUnit sourceUnit)
        {
            return lifecycle.BounceUnitOpposite(sourceUnit);
        }

        public bool PullUnitOpposite(BoardUnit sourceUnit, BoardUnit targetUnit)
        {
            return movement.PullUnitOpposite(sourceUnit, targetUnit);
        }

        public bool CanMoveUnit(int fromSlot, int toSlot, bool ignoreMoveLimitAndCost = false)
        {
            return movement.CanMoveUnit(fromSlot, toSlot, ignoreMoveLimitAndCost);
        }

        public bool HasAvailableGrantedEnemyMove(PlayerSide controllingSide)
        {
            return movement.HasAvailableGrantedEnemyMove(controllingSide);
        }

        public bool CanMoveEnemyUnitViaGrantedAbility(PlayerSide controllingSide, int fromSlot, int toSlot)
        {
            return movement.CanMoveEnemyUnitViaGrantedAbility(controllingSide, fromSlot, toSlot);
        }

        public bool MoveEnemyUnitViaGrantedAbility(PlayerSide controllingSide, int fromSlot, int toSlot)
        {
            return movement.MoveEnemyUnitViaGrantedAbility(controllingSide, fromSlot, toSlot);
        }

        public bool HasAnyLegalUnblockedSlot(PlayerSide side, int fromSlot)
        {
            return movement.HasAnyLegalUnblockedSlot(side, fromSlot);
        }

        public bool CanMoveGrantedEnemyUnitFree(BoardUnit unit, int toSlot)
        {
            return movement.CanMoveGrantedEnemyUnitFree(unit, toSlot);
        }

        public bool MoveGrantedEnemyUnitFree(BoardUnit unit, int toSlot)
        {
            return movement.MoveGrantedEnemyUnitFree(unit, toSlot);
        }

        public void EnterTurnEndPhase()
        {
            state.CurrentPhase = TurnPhase.TurnEnd;
        }

        public void EndActionPhase()
        {
            CancelPendingTargetedEffectIfNonMandatory();

            if (HasBlockingPendingTargetedEffect())
            {
                Debug.LogWarning("[PhaseManager] EndActionPhase blocked: an On-Play effect is still awaiting a target.");
                return;
            }

            if (state.HasPendingFreeMove)
            {
                Debug.Log("[PhaseManager] Unused pending free move expired at end of turn.");
            }

            state.HasPendingFreeMove = false;
            state.PendingFreeMoveExcludedUnit = null;

            if (state.HasPendingEnemyMoveGrantOnPlay)
            {
                Debug.Log($"[PhaseManager] Unused pending On-Play enemy move for {state.PendingEnemyMoveGrantTarget?.SourceCard?.CardName} expired at end of turn.");
            }

            state.HasPendingEnemyMoveGrantOnPlay = false;
            state.PendingEnemyMoveGrantTarget = null;

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
                    unit.HasAttackedThisTurn = false;
                    unit.HasUsedGrantedEnemyMoveThisTurn = false;

                    if (unit.IsSilenced)
                    {
                        unit.Statuses.RemoveAll(status => status.Type == StatusEffectType.Silenced);
                        Debug.Log($"[PhaseManager] {unit.SourceCard.CardName}'s Silence wore off at the end of {state.ActivePlayer}'s turn.");
                    }

                    if (unit.IsStunned)
                    {
                        unit.Statuses.RemoveAll(status => status.Type == StatusEffectType.Stunned);
                        Debug.Log($"[PhaseManager] {unit.SourceCard.CardName}'s Stun wore off at the end of {state.ActivePlayer}'s turn.");
                    }
                }
            }

            state.HasUsedMoveThisTurn = false;
            state.PlayerA.TriggeredOncePerTurnEffects.Clear();
            state.PlayerB.TriggeredOncePerTurnEffects.Clear();

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
            int manaShort = card.ManaCost - active.CurrentMana;

            if (manaShort > 0 && AuraCalculator.TryGetHealthCostForManaShortfall(active, manaShort, out int healthCost))
            {
                Debug.Log($"[PhaseManager] {active.Side} converting {healthCost} health into {manaShort} mana to afford {card.CardName}.");
                DamageLeader(active.Side, healthCost);
                active.CurrentMana += manaShort;
            }

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
                return false;
            }

            Player active = state.GetActivePlayerData();
            int manaShort = card.ManaCost - active.CurrentMana;

            if (manaShort > 0 && !AuraCalculator.TryGetHealthCostForManaShortfall(active, manaShort, out _))
            {
                return false;
            }
            if (!active.Hand.Contains(card))
            {
                return false;
            }
            if (state.CurrentPhase != TurnPhase.Action)
            {
                return false;
            }

            CardEffect effect = card.PrimaryEffect;
            if (effect == null)
            {
                return false;
            }

            bool valid = EffectTargeting.IsValidTarget(effect.targetType, target, state);
            
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
                && targetType != TargetType.OpposingEnemy
                && targetType != TargetType.LowestHealthEnemy
                && targetType != TargetType.RandomUnitEitherSide;
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

                EffectTarget resolvedTarget = (effect.targetType == TargetType.None || (effect.targetType == TargetType.Self && sourceUnit == null))
                    ? EffectTarget.ForLeader(leaderOwner.Side)
                    : EffectTargeting.ResolveImmediateTarget(effect.targetType, sourceUnit, leaderOwner.Side, state);

                if (resolvedTarget.Kind == EffectTargetKind.None || (resolvedTarget.Kind == EffectTargetKind.Unit && resolvedTarget.Unit == null))
                {
                    Debug.LogWarning($"[PhaseManager] {leaderOwner.Side}'s leader effect (trigger={trigger}, action={effect.action}, targetType={effect.targetType}) could not resolve a target — skipped.");
                    continue;
                }

                EffectContext context = new EffectContext(state, leaderOwner.Side, sourceUnit, resolvedTarget, triggeringPlayer);

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
            if (state.IsGameOver)
            {
                return;
            }

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

            if (state.IsGameOver)
            {
                Debug.Log($"[PhaseManager] Game over. Winner: {state.Winner}.");
                state.RaiseGameOver();
            }
        }
    }
}