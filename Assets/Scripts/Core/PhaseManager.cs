using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DDD.TNFY.TCG.Cards;
using DDD.TNFY.TCG.Effects;

namespace DDD.TNFY.TCG.Core
{
    public class PhaseManager
    {
        private const float PlayAnimationTimeout = 6f;
        private const float AttackHitLandedTimeout = 6f;
        private const float AttackAnimationFinishedTimeout = 3f;

        private static readonly PlayerSide[] Sides = { PlayerSide.PlayerA, PlayerSide.PlayerB };
        private static readonly System.Random rng = new System.Random();

        private readonly GameState state;
        private readonly MonoBehaviour coroutineRunner;

        private List<CardData> draftPool;
        private DraftSettings draftSettings;
        private int maxCopiesPerCard;
        private readonly Dictionary<PlayerSide, int> draftPickIndexInStage = new Dictionary<PlayerSide, int>();

        private int unresolvedActionCount;

        private BoardUnit lastPlayedUnit;
        private UnitCardData lastPlayedCard;
        private BoardUnit lastPlayedAbsorbedUnit;
        private int lastPlayedManaSpent;
        private int lastPlayedHealthPaid;

        public bool HasUnresolvedActions => unresolvedActionCount > 0;
        public bool IsEndTurnQueued { get; private set; }

        public PhaseManager(GameState state, MonoBehaviour coroutineRunner)
        {
            this.state = state;
            this.coroutineRunner = coroutineRunner;
            state.UnitMoved += TriggerOnMove;
        }

        public void StartDraft(List<CardData> pool, DraftSettings settings, int maxCopies)
        {
            draftPool = new List<CardData>(pool);
            draftSettings = settings;
            maxCopiesPerCard = maxCopies;

            state.CurrentPhase = TurnPhase.Draft;

            foreach (PlayerSide side in Sides)
            {
                state.GetPlayer(side).CurrentDraftStage = DraftStage.Common;
                draftPickIndexInStage[side] = 0;
                OfferNextDraftPick(side);
            }
        }

        private void OfferNextDraftPick(PlayerSide side)
        {
            Player player = state.GetPlayer(side);
            DraftStage stage = player.CurrentDraftStage.Value;
            List<CardData> eligible = draftPool.FindAll(card => MatchesDraftStage(card.Rarity, stage) && player.Deck.FindAll(c => c == card).Count < maxCopiesPerCard);

            if (eligible.Count == 0)
            {
                player.PendingDraftOptions = null;
                draftPickIndexInStage[side]++;
                AdvanceDraft(side);
                return;
            }

            ListShuffler.Shuffle(eligible);
            player.PendingDraftOptions = eligible.GetRange(0, Mathf.Min(draftSettings.optionsPerChoice, eligible.Count));
            state.RaiseDraftOptionsChanged();
        }

        public static bool MatchesDraftStage(CardRarity rarity, DraftStage stage) => stage switch
        {
            DraftStage.Common => rarity == CardRarity.Common,
            DraftStage.Uncommon => rarity == CardRarity.Uncommon,
            DraftStage.Rare => rarity == CardRarity.Rare,
            _ => rarity == CardRarity.Epic || rarity == CardRarity.Legendary
        };

        public bool TryResolvePendingDraftChoice(PlayerSide side, CardData chosenCard)
        {
            Player drafter = state.GetPlayer(side);

            if (drafter.PendingDraftOptions == null || drafter.CurrentDraftStage == null || !drafter.PendingDraftOptions.Contains(chosenCard))
            {
                return false;
            }

            int copies = drafter.CurrentDraftStage.Value switch
            {
                DraftStage.Common => draftSettings.copiesPerCommonPick,
                DraftStage.Uncommon => draftSettings.copiesPerUncommonPick,
                DraftStage.Rare => draftSettings.copiesPerRarePick,
                _ => draftSettings.copiesPerEpicOrLegendaryPick
            };

            for (int i = 0; i < copies; i++)
            {
                drafter.Deck.Add(chosenCard);
            }

            drafter.PendingDraftOptions = null;
            draftPickIndexInStage[side]++;
            AdvanceDraft(side);
            return true;
        }

        private void AdvanceDraft(PlayerSide side)
        {
            Player player = state.GetPlayer(side);
            DraftStage stage = player.CurrentDraftStage.Value;

            int picksForStage = stage switch
            {
                DraftStage.Common => draftSettings.commonPicks,
                DraftStage.Uncommon => draftSettings.uncommonPicks,
                DraftStage.Rare => draftSettings.rarePicks,
                _ => draftSettings.epicOrLegendaryPicks
            };

            if (draftPickIndexInStage[side] < picksForStage)
            {
                OfferNextDraftPick(side);
                return;
            }

            draftPickIndexInStage[side] = 0;

            if (stage != DraftStage.EpicOrLegendary)
            {
                player.CurrentDraftStage = stage + 1;
                OfferNextDraftPick(side);
                return;
            }

            player.CurrentDraftStage = null;

            if (state.PlayerA.CurrentDraftStage != null || state.PlayerB.CurrentDraftStage != null)
            {
                return;
            }

            ListShuffler.Shuffle(state.PlayerA.Deck);
            ListShuffler.Shuffle(state.PlayerB.Deck);
            state.ActivePlayer = state.FirstPlayer;
            StartMatch();
        }

        public void StartMatch()
        {
            foreach (PlayerSide side in Sides)
            {
                Player player = state.GetPlayer(side);

                if (player.Leader != null)
                {
                    player.MaxLeaderHealth = player.Leader.MaxHealth;
                    player.LeaderHealth = player.Leader.MaxHealth;
                }

                player.CurrentMana = 0;
                player.MaxManaThisGame = 0;
            }

            state.ActivePlayer = state.FirstPlayer;
            state.TurnNumber = 1;

            TriggerLeaderEffects(EffectTriggerType.OnGameStart, null);
            EnterMulliganPhase();
        }

        public void EnterMulliganPhase()
        {
            state.CurrentPhase = TurnPhase.Mulligan;
            state.PlayerA.HasCompletedMulligan = false;
            state.PlayerB.HasCompletedMulligan = false;

            DrawInitialHand(PlayerSide.PlayerA);
            DrawInitialHand(PlayerSide.PlayerB);
        }

        public void DrawInitialHand(PlayerSide side)
        {
            for (int i = 0; i < 4; i++)
            {
                DrawOne(state.GetPlayer(side));
            }
        }

        private void DrawOne(Player player)
        {
            CardData drawn = player.DrawCard(out bool addedToHand);

            if (drawn == null)
            {
                return;
            }

            if (addedToHand)
            {
                TriggerOnDraw(player, drawn);
            }
            else
            {
                state.RaiseCardBurnAnimationRequested(drawn, player.Side);
            }
        }

        public void ResolveMulligan(PlayerSide side, List<CardData> cardsToMulligan)
        {
            Player player = state.GetPlayer(side);
            List<CardData> returned = cardsToMulligan.FindAll(card => !player.GameStartBonusCards.Contains(card));

            foreach (CardData card in returned)
            {
                player.Hand.Remove(card);
            }

            for (int i = 0; i < returned.Count && player.Deck.Count > 0; i++)
            {
                DrawOne(player);
            }

            player.Deck.AddRange(returned);
            ListShuffler.Shuffle(player.Deck);
        }

        public void ResolveMulliganAndAdvance(PlayerSide side, List<CardData> cardsToMulligan)
        {
            Player player = state.GetPlayer(side);

            if (player.HasCompletedMulligan)
            {
                return;
            }

            ResolveMulligan(side, cardsToMulligan);
            player.HasCompletedMulligan = true;

            if (state.PlayerA.HasCompletedMulligan && state.PlayerB.HasCompletedMulligan)
            {
                EnterDrawPhase();
            }
        }

        public void EnterDrawPhase()
        {
            state.CurrentPhase = TurnPhase.Draw;
            Player active = state.GetActivePlayerData();

            ActiveStatusEffect manaReduction = active.Statuses.FindLast(status => status.Type == StatusEffectType.OpponentManaReduction);

            if (manaReduction != null)
            {
                active.PendingManaReduction = manaReduction.Magnitude;
                active.Statuses.Remove(manaReduction);
            }

            active.MaxManaThisGame = Mathf.Min(active.MaxManaThisGame + 1, Player.MaxMana);
            active.CurrentMana = Mathf.Max(0, active.MaxManaThisGame - active.PendingManaReduction);
            active.PendingManaReduction = 0;

            if (active.MaxManaThisGame >= Player.MaxMana && !active.HasReachedMaxMana)
            {
                active.HasReachedMaxMana = true;
                TopUpAllUnitsToEffectiveMaxHealth(active.Side);
            }

            active.OwnTurnCount++;
            TryTriggerPeriodicItemDraw(active);

            bool firstPlayerFirstTurn = state.TurnNumber == 1 && state.ActivePlayer == state.FirstPlayer;

            if (!firstPlayerFirstTurn && active.Deck.Count > 0)
            {
                DrawOne(active);
            }
            else if (!firstPlayerFirstTurn)
            {
                active.FatigueDamageTaken++;
                active.LeaderHealth -= active.FatigueDamageTaken;
                CheckWinCondition();

                if (state.IsGameOver)
                {
                    return;
                }
            }

            TickDelayedKills(active);
            TickDecay(active);

            state.IsResolvingTurnStartEffects = true;
            ContinueTurnStartScan(active, state.TurnStartScanSlot);
        }

        private void ContinueTurnStartScan(Player owner, int fromSlot)
        {
            for (int slot = fromSlot; slot < Board.SlotsPerSide; slot++)
            {
                if (state.IsGameOver)
                {
                    state.IsResolvingTurnStartEffects = false;
                    state.TurnStartScanSlot = 0;
                    return;
                }

                BoardUnit unit = state.Board.GetUnit(owner.Side, slot);

                if (unit == null || unit.IsSilenced)
                {
                    continue;
                }

                foreach (CardEffect effect in unit.SourceCard.Effects)
                {
                    if (effect.trigger != EffectTriggerType.OnTurnStart)
                    {
                        continue;
                    }

                    if (effect.action == EffectActionType.HealSelfByDamageDealt)
                    {
                        ResolveTurnStartDrain(unit, effect);
                    }
                    else if (EffectTargeting.RequiresClick(effect.targetType))
                    {
                        state.TurnStartScanSlot = slot + 1;
                        DeferTargetedEffect(effect, unit, EffectTriggerType.OnTurnStart, excludeSource: false);

                        if (state.PendingTargetedEffect != null)
                        {
                            EnterActionPhase();
                            return;
                        }
                    }
                    else
                    {
                        ResolveEffect(effect, unit, unit.Owner);
                    }
                }
            }

            state.IsResolvingTurnStartEffects = false;
            state.TurnStartScanSlot = 0;

            if (!state.IsGameOver && state.CurrentPhase != TurnPhase.Action)
            {
                EnterActionPhase();
            }
        }

        private void ResolveTurnStartDrain(BoardUnit source, CardEffect healEffect)
        {
            BoardUnit target = state.Board.GetUnits(source.Owner.Opposite()).FirstOrDefault();

            if (target == null)
            {
                return;
            }

            int damageDealt = Mathf.Min(healEffect.amount, target.CurrentHealth);
            DamageUnit(target, healEffect.amount, source.Owner);
            Execute(healEffect, source.Owner, source, EffectTarget.ForUnit(source), damageDealt);
        }

        public void EnterActionPhase()
        {
            state.CurrentPhase = TurnPhase.Action;
        }

        private void TryTriggerPeriodicItemDraw(Player player)
        {
            if (player.Leader == null || player.Leader.ItemDrawIntervalTurns <= 0 || player.OwnTurnCount % player.Leader.ItemDrawIntervalTurns != 0)
            {
                return;
            }

            CardData drawn = player.DrawRandomItemCard(out bool addedToHand);

            if (drawn != null && !addedToHand)
            {
                state.RaiseCardBurnAnimationRequested(drawn, player.Side);
            }
        }

        private System.Action BeginUnresolvedAction(System.Action onFullyResolved)
        {
            unresolvedActionCount++;

            return () =>
            {
                unresolvedActionCount--;
                onFullyResolved?.Invoke();
                TryRunQueuedEndTurn();
            };
        }

        private void TryRunQueuedEndTurn()
        {
            if (!IsEndTurnQueued || HasUnresolvedActions)
            {
                return;
            }

            if (state.IsGameOver || state.CurrentPhase != TurnPhase.Action)
            {
                IsEndTurnQueued = false;
                return;
            }

            EndActionPhase();
        }

        private static WaitUntil WaitOrTimeout(System.Func<bool> isDone, float timeoutSeconds, string description)
        {
            return new WaitUntil(isDone, System.TimeSpan.FromSeconds(timeoutSeconds), () => Debug.LogWarning($"[PhaseManager] Timed out waiting for {description} - continuing anyway."), WaitTimeoutMode.InGameTime);
        }

        private static IEnumerator RunThen(IEnumerator routine, System.Action onDone)
        {
            yield return routine;
            onDone();
        }

        private int PayCost(Player active, int cost)
        {
            int manaShort = cost - active.CurrentMana;
            int healthPaid = 0;

            if (manaShort > 0 && AuraCalculator.TryGetHealthCostForManaShortfall(active, manaShort, out int healthCost))
            {
                healthPaid = DamageLeader(active.Side, healthCost) ? healthCost : 0;
                active.CurrentMana += manaShort;
            }

            active.CurrentMana -= cost;
            return healthPaid;
        }

        private static bool CanPayCost(Player player, int cost)
        {
            int manaShort = cost - player.CurrentMana;
            return manaShort <= 0 || AuraCalculator.TryGetHealthCostForManaShortfall(player, manaShort, out _);
        }

        public bool TryPlayUnit(UnitCardData card, int slotIndex)
        {
            if (!IsUnitPlayLegal(card, slotIndex))
            {
                return false;
            }

            CancelPendingTargetedEffectIfNonMandatory();

            Player active = state.GetActivePlayerData();
            int manaBeforePayment = active.CurrentMana;
            int healthPaid = PayCost(active, card.ManaCost);
            active.Hand.Remove(card);

            BoardUnit occupyingUnit = state.Board.GetUnit(state.ActivePlayer, slotIndex);
            BoardUnit unit = occupyingUnit != null ? AbsorbUnit(occupyingUnit, card, slotIndex) : AddUnit(card, state.ActivePlayer, slotIndex);

            lastPlayedUnit = unit;
            lastPlayedCard = card;
            lastPlayedAbsorbedUnit = occupyingUnit;
            lastPlayedManaSpent = manaBeforePayment - active.CurrentMana;
            lastPlayedHealthPaid = healthPaid;

            TriggerOnPlay(unit);
            return true;
        }

        public bool CanPlayUnit(UnitCardData card, int slotIndex)
        {
            return !IsEndTurnQueued && IsUnitPlayLegal(card, slotIndex);
        }

        private bool IsUnitPlayLegal(UnitCardData card, int slotIndex)
        {
            if (HasBlockingPendingTargetedEffect() || state.CurrentPhase != TurnPhase.Action)
            {
                return false;
            }

            Player active = state.GetActivePlayerData();

            if (!CanPayCost(active, card.ManaCost) || !active.Hand.Contains(card))
            {
                return false;
            }

            BoardUnit occupyingUnit = state.Board.GetUnit(state.ActivePlayer, slotIndex);
            return occupyingUnit != null ? card.HasKeyword(Keyword.Absorb) : IsSlotLegalForPlacement(slotIndex);
        }

        public bool IsSlotLegalForPlacement(int slotIndex)
        {
            bool anyOpenTauntSlot = false;

            for (int i = 0; i < Board.SlotsPerSide; i++)
            {
                BoardUnit opposingUnit = state.Board.GetUnit(state.ActivePlayer.Opposite(), i);

                if (opposingUnit != null && opposingUnit.HasKeyword(Keyword.Taunt, state) && state.Board.GetUnit(state.ActivePlayer, i) == null)
                {
                    if (i == slotIndex)
                    {
                        return true;
                    }

                    anyOpenTauntSlot = true;
                }
            }

            return !anyOpenTauntSlot;
        }

        public bool TryPlayUnitAnimated(UnitCardData card, int slotIndex, System.Action onFullyResolved = null)
        {
            if (!CanPlayUnit(card, slotIndex))
            {
                return false;
            }

            PlayerSide side = state.ActivePlayer;
            int handIndex = state.GetPlayer(side).Hand.IndexOf(card);

            if (coroutineRunner != null)
            {
                state.RaiseUnitPlayAnimationRequested(card, side, handIndex, slotIndex);
            }

            ResolveUnitPlayAfterAnimation(card, side, handIndex, slotIndex, onFullyResolved);
            return true;
        }

        public void ResolveUnitPlayAfterAnimation(UnitCardData card, PlayerSide side, int handIndex, int slotIndex, System.Action onFullyResolved = null)
        {
            if (coroutineRunner == null)
            {
                TryPlayUnit(card, slotIndex);
                onFullyResolved?.Invoke();
                return;
            }

            coroutineRunner.StartCoroutine(RunThen(PlayUnitAfterAnimation(card, side, handIndex, slotIndex), BeginUnresolvedAction(onFullyResolved)));
        }

        private IEnumerator PlayUnitAfterAnimation(UnitCardData card, PlayerSide side, int handIndex, int slotIndex)
        {
            bool finished = false;

            void OnFinished(PlayerSide finishedSide, int finishedHandIndex, int finishedSlotIndex)
            {
                if (finishedSide == side && finishedHandIndex == handIndex && finishedSlotIndex == slotIndex)
                {
                    finished = true;
                }
            }

            state.UnitPlayAnimationFinished += OnFinished;
            yield return WaitOrTimeout(() => finished, PlayAnimationTimeout, $"{card.CardName}'s play animation");
            state.UnitPlayAnimationFinished -= OnFinished;

            TryPlayUnit(card, slotIndex);
        }

        public EffectTarget ResolveItemEffectTarget(CardEffect effect, EffectTarget target)
        {
            if (effect == null)
            {
                return target;
            }

            bool picksItsOwnTarget = effect.targetType == TargetType.AllyLeader
                || effect.targetType == TargetType.EnemyLeader
                || effect.targetType == TargetType.LowestHealthEnemy
                || effect.targetType == TargetType.RandomUnitEitherSide;

            if (!picksItsOwnTarget || (target.Kind != EffectTargetKind.None && EffectTargeting.IsValidTarget(effect.targetType, target, state)))
            {
                return target;
            }

            return EffectTargeting.ResolveImmediateTarget(effect.targetType, null, state.ActivePlayer, state);
        }

        public bool TryPlayItem(ItemCardData card, EffectTarget target)
        {
            CardEffect effect = card.PrimaryEffect;
            EffectTarget effectiveTarget = ResolveItemEffectTarget(effect, target);

            if (!IsItemPlayLegal(card, effectiveTarget))
            {
                return false;
            }

            CancelPendingTargetedEffectIfNonMandatory();

            Player active = state.GetActivePlayerData();
            PayCost(active, card.ManaCost);
            active.Hand.Remove(card);

            int passes = active.HasNextItemDoubled ? 2 : 1;
            active.HasNextItemDoubled = false;

            for (int pass = 0; pass < passes; pass++)
            {
                if (EffectTargeting.IsGroupTarget(effect.targetType) || EffectTargeting.IsGroupSlotTarget(effect.targetType))
                {
                    ResolveEffect(effect, null, state.ActivePlayer);
                }
                else
                {
                    Execute(effect, state.ActivePlayer, null, effectiveTarget);
                }
            }

            return true;
        }

        public bool CanPlayItem(ItemCardData card, EffectTarget target)
        {
            return !IsEndTurnQueued && IsItemPlayLegal(card, target);
        }

        private bool IsItemPlayLegal(ItemCardData card, EffectTarget target)
        {
            if (HasBlockingPendingTargetedEffect() || state.CurrentPhase != TurnPhase.Action || card.PrimaryEffect == null)
            {
                return false;
            }

            Player active = state.GetActivePlayerData();
            return CanPayCost(active, card.ManaCost) && active.Hand.Contains(card) && EffectTargeting.IsValidTarget(card.PrimaryEffect.targetType, target, state);
        }

        public bool TryPlayItemAnimated(ItemCardData card, EffectTarget target, System.Action onFullyResolved = null)
        {
            EffectTarget effectiveTarget = ResolveItemEffectTarget(card.PrimaryEffect, target);

            if (!CanPlayItem(card, effectiveTarget))
            {
                return false;
            }

            PlayerSide side = state.ActivePlayer;
            int handIndex = state.GetPlayer(side).Hand.IndexOf(card);

            if (coroutineRunner != null)
            {
                state.RaiseItemPlayAnimationRequested(card, side, handIndex, effectiveTarget);
            }

            ResolveItemPlayAfterAnimation(card, effectiveTarget, side, handIndex, onFullyResolved);
            return true;
        }

        public void ResolveItemPlayAfterAnimation(ItemCardData card, EffectTarget target, PlayerSide side, int handIndex, System.Action onFullyResolved = null)
        {
            if (coroutineRunner == null)
            {
                TryPlayItem(card, target);
                onFullyResolved?.Invoke();
                return;
            }

            coroutineRunner.StartCoroutine(RunThen(PlayItemAfterAnimation(card, target, side, handIndex), BeginUnresolvedAction(onFullyResolved)));
        }

        private IEnumerator PlayItemAfterAnimation(ItemCardData card, EffectTarget target, PlayerSide side, int handIndex)
        {
            bool finished = false;

            void OnFinished(PlayerSide finishedSide, int finishedHandIndex)
            {
                if (finishedSide == side && finishedHandIndex == handIndex)
                {
                    finished = true;
                }
            }

            state.ItemPlayAnimationFinished += OnFinished;
            yield return WaitOrTimeout(() => finished, PlayAnimationTimeout, $"{card.CardName}'s play animation");
            state.ItemPlayAnimationFinished -= OnFinished;

            TryPlayItem(card, target);
        }

        private BoardUnit AddUnit(UnitCardData card, PlayerSide side, int slotIndex)
        {
            BoardUnit unit = new BoardUnit(card, side, slotIndex);
            state.Board.PlaceUnit(side, slotIndex, unit);
            InitializeHealthToEffectiveMax(unit);

            if (card.HasPendingCurrentHealth)
            {
                unit.CurrentHealth = Mathf.Min(card.PendingCurrentHealth, unit.GetEffectiveMaxHealth(state));
            }

            SyncQualifyingEnemyAuraHealth();
            return unit;
        }

        private BoardUnit AbsorbUnit(BoardUnit absorbedUnit, UnitCardData card, int slotIndex)
        {
            int inheritedAttack = absorbedUnit.SourceCard.Attack + absorbedUnit.BonusAttack + AuraCalculator.GetAttackBonus(absorbedUnit, state);
            List<ActiveStatusEffect> temporaryAttacks = absorbedUnit.Statuses.FindAll(status => status.Type == StatusEffectType.TemporaryAttack);

            state.Board.RemoveUnit(absorbedUnit.Owner, slotIndex);

            BoardUnit unit = new BoardUnit(card, absorbedUnit.Owner, slotIndex);
            state.Board.PlaceUnit(absorbedUnit.Owner, slotIndex, unit);

            unit.BonusAttack = inheritedAttack;
            unit.MaxHealth = card.Health + absorbedUnit.MaxHealth;

            foreach (ActiveStatusEffect temporaryAttack in temporaryAttacks)
            {
                unit.Statuses.Add(temporaryAttack.Clone());
            }

            unit.CurrentHealth = Mathf.Min(absorbedUnit.CurrentHealth + card.Health, unit.GetEffectiveMaxHealth(state));
            unit.LastSyncedAuraHealthBonus = AuraCalculator.GetQualifyingEnemyAuraHealthBonus(unit, state);

            SyncQualifyingEnemyAuraHealth();
            return unit;
        }

        private void InitializeHealthToEffectiveMax(BoardUnit unit)
        {
            unit.CurrentHealth = unit.GetEffectiveMaxHealth(state);
            unit.LastSyncedAuraHealthBonus = AuraCalculator.GetQualifyingEnemyAuraHealthBonus(unit, state);
        }

        public void SyncQualifyingEnemyAuraHealth()
        {
            foreach (PlayerSide side in Sides)
            {
                foreach (BoardUnit unit in state.Board.GetUnits(side))
                {
                    int auraHealthBonus = AuraCalculator.GetQualifyingEnemyAuraHealthBonus(unit, state);

                    if (auraHealthBonus > unit.LastSyncedAuraHealthBonus)
                    {
                        unit.CurrentHealth += auraHealthBonus - unit.LastSyncedAuraHealthBonus;
                    }

                    unit.LastSyncedAuraHealthBonus = auraHealthBonus;
                }
            }
        }

        private void TopUpAllUnitsToEffectiveMaxHealth(PlayerSide side)
        {
            foreach (BoardUnit unit in state.Board.GetUnits(side))
            {
                int gainedMaxHealth = unit.GetEffectiveMaxHealth(state) - unit.MaxHealth;

                if (gainedMaxHealth > 0)
                {
                    unit.CurrentHealth += gainedMaxHealth;
                }
            }
        }

        public bool CanAnyUnitAttack()
        {
            return state.Board.GetUnits(state.ActivePlayer).Any(CanUnitAttack);
        }

        public bool CanUnitAttack(BoardUnit unit)
        {
            return unit != null
                && (!unit.PlacedThisTurn || unit.HasKeyword(Keyword.Rush, state))
                && !unit.IsStunned
                && unit.GetCurrentAttack(state) > 0;
        }

        public bool CanAttackWithUnit(int slotIndex)
        {
            if (IsEndTurnQueued || state.CurrentPhase != TurnPhase.Action || HasBlockingPendingTargetedEffect())
            {
                return false;
            }

            BoardUnit unit = state.Board.GetUnit(state.ActivePlayer, slotIndex);
            return unit != null && !unit.HasAttackedThisTurn && CanUnitAttack(unit);
        }

        public bool TryAttackWithUnit(int slotIndex, System.Action onAttackFullyResolved = null)
        {
            if (!CanAttackWithUnit(slotIndex))
            {
                return false;
            }

            BoardUnit attacker = state.Board.GetUnit(state.ActivePlayer, slotIndex);
            bool hadDoubleAttack = ConsumeStatus(attacker.Statuses, StatusEffectType.DoubleAttackNextAttack);
            attacker.HasAttackedThisTurn = true;

            if (coroutineRunner == null || attacker.GetCurrentAttack(state) <= 0)
            {
                IEnumerator attack = RunAttack(attacker, slotIndex, hadDoubleAttack, false);
                while (attack.MoveNext()) { }
                onAttackFullyResolved?.Invoke();
                return true;
            }

            coroutineRunner.StartCoroutine(RunThen(RunAttack(attacker, slotIndex, hadDoubleAttack, true), BeginUnresolvedAction(onAttackFullyResolved)));
            return true;
        }

        private IEnumerator RunAttack(BoardUnit attacker, int slotIndex, bool hadDoubleAttack, bool animate)
        {
            bool isBifurcated = attacker.HasKeyword(Keyword.BifurcatedAttack, state);
            bool shouldChainAttack = false;

            if (animate)
            {
                state.CurrentlyAttackingUnit = attacker;
            }

            for (int attackIndex = 0; attackIndex < (hadDoubleAttack ? 2 : 1); attackIndex++)
            {
                if (attackIndex > 0 && animate)
                {
                    yield return WaitForAttackAnimationFinished(attacker);

                    if (!IsOnBoard(attacker))
                    {
                        break;
                    }

                    state.CurrentlyAttackingUnit = null;
                    yield return null;

                    if (!IsOnBoard(attacker))
                    {
                        break;
                    }

                    state.CurrentlyAttackingUnit = attacker;
                }

                if (animate)
                {
                    yield return WaitForAttackHitLanded(0);
                }

                if (!IsOnBoard(attacker))
                {
                    break;
                }

                bool killedDefender = false;

                if (isBifurcated)
                {
                    if (slotIndex > 0)
                    {
                        killedDefender |= ResolveAttack(attacker, slotIndex - 1);
                    }

                    if (state.IsGameOver)
                    {
                        break;
                    }

                    if (animate)
                    {
                        yield return WaitForAttackHitLanded(1);
                    }

                    if (!IsOnBoard(attacker))
                    {
                        break;
                    }

                    if (slotIndex < Board.SlotsPerSide - 1)
                    {
                        killedDefender |= ResolveAttack(attacker, slotIndex + 1);
                    }
                }
                else
                {
                    killedDefender = ResolveAttack(attacker, slotIndex);
                }

                if (state.IsGameOver)
                {
                    break;
                }

                if (killedDefender && attacker.CurrentHealth > 0 && HasAttackAgainOnKill(attacker))
                {
                    shouldChainAttack = true;
                }
            }

            if (animate)
            {
                state.CurrentlyAttackingUnit = null;
            }

            CheckWinCondition();

            if (state.IsGameOver || !shouldChainAttack)
            {
                yield break;
            }

            if (animate)
            {
                yield return null;

                if (!IsOnBoard(attacker))
                {
                    yield break;
                }

                if (attacker.GetCurrentAttack(state) > 0)
                {
                    state.CurrentlyAttackingUnit = attacker;
                    yield return WaitForAttackHitLanded(0);

                    if (IsOnBoard(attacker))
                    {
                        ResolveAttack(attacker, slotIndex);
                        CheckWinCondition();
                    }

                    state.CurrentlyAttackingUnit = null;
                    yield break;
                }
            }

            ResolveAttack(attacker, slotIndex);
            CheckWinCondition();
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
            yield return WaitOrTimeout(() => hitLanded, AttackHitLandedTimeout, $"hit {expectedHitIndex} (does the attack animation have an Animation Event calling OnAttackHitLanded({expectedHitIndex})?)");
            state.AttackHitLanded -= OnHitLanded;
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
            yield return WaitOrTimeout(() => animationFinished, AttackAnimationFinishedTimeout, $"{attacker.SourceCard.CardName}'s attack animation");
            state.AttackAnimationFinished -= OnAnimationFinished;
        }

        private static bool HasAttackAgainOnKill(BoardUnit attacker)
        {
            return !attacker.IsSilenced && attacker.SourceCard.Effects.Any(effect => effect.trigger == EffectTriggerType.OnAttack && effect.action == EffectActionType.AttackAgainOnKill);
        }

        private bool ResolveAttack(BoardUnit attacker, int targetSlot)
        {
            int damage = attacker.GetCurrentAttack(state);
            PlayerSide enemySide = state.ActivePlayer.Opposite();
            BoardUnit defender = state.Board.GetUnit(enemySide, targetSlot);
            bool blockedByTaunt = defender != null && defender.HasKeyword(Keyword.Taunt, state);

            if (attacker.HasKeyword(Keyword.Piercing, state) && !blockedByTaunt)
            {
                DamageLeader(enemySide, damage);
                TriggerOnAttack(attacker, null, damage);
                return false;
            }

            int attackerSlotBeforeDodge = attacker.SlotIndex;

            if (defender != null && TrySlippyDodge(defender))
            {
                bool relentlessFollowed = attacker.SlotIndex != attackerSlotBeforeDodge && attacker.SlotIndex == defender.SlotIndex;

                if (!relentlessFollowed)
                {
                    defender = null;
                }
            }

            bool killedDefender = false;

            if (defender != null)
            {
                killedDefender = DamageUnit(defender, damage, state.ActivePlayer);

                if (!killedDefender && defender.HasKeyword(Keyword.Retaliate, state))
                {
                    DamageUnit(attacker, defender.GetCurrentAttack(state), defender.Owner);
                }
            }
            else
            {
                DamageLeader(enemySide, damage);
            }

            if (attacker.CurrentHealth > 0)
            {
                TriggerOnAttack(attacker, defender, damage);
            }

            return killedDefender;
        }

        private void TriggerOnAttack(BoardUnit attacker, BoardUnit defender, int damageDealt)
        {
            if (attacker.IsSilenced)
            {
                return;
            }

            foreach (CardEffect effect in attacker.SourceCard.Effects)
            {
                if (effect.trigger != EffectTriggerType.OnAttack)
                {
                    continue;
                }

                if (effect.action != EffectActionType.ApplyDecay)
                {
                    Execute(effect, attacker.Owner, attacker, EffectTarget.ForUnit(attacker), damageDealt);
                }
                else if (defender != null)
                {
                    Execute(effect, attacker.Owner, attacker, EffectTarget.ForUnit(defender));
                }
            }
        }

        public bool DamageLeader(PlayerSide side, int amount)
        {
            Player player = state.GetPlayer(side);
            bool damaged = !ConsumeStatus(player.Statuses, StatusEffectType.Shield);

            if (damaged)
            {
                player.LeaderHealth -= amount;
            }

            CheckWinCondition();
            return damaged;
        }

        public bool DamageUnit(BoardUnit target, int amount, PlayerSide source)
        {
            if (target == null || amount <= 0 || ConsumeStatus(target.Statuses, StatusEffectType.Shield))
            {
                return false;
            }

            target.CurrentHealth -= amount;
            TriggerUnitEffects(target, EffectTriggerType.OnDamaged);

            if (target.CurrentHealth > 0)
            {
                return false;
            }

            KillUnit(target, source);
            return true;
        }

        public void HealUnit(BoardUnit unit, int amount)
        {
            unit.CurrentHealth = Mathf.Min(unit.CurrentHealth + amount, unit.GetEffectiveMaxHealth(state));
        }

        public void HealLeader(PlayerSide side, int amount)
        {
            Player player = state.GetPlayer(side);
            player.LeaderHealth = Mathf.Min(player.LeaderHealth + amount, player.MaxLeaderHealth);
        }

        public bool TransformUnit(BoardUnit originalUnit, UnitCardData replacementCard)
        {
            if (originalUnit == null || replacementCard == null)
            {
                return false;
            }

            state.Board.RemoveUnit(originalUnit.Owner, originalUnit.SlotIndex);

            BoardUnit replacement = AddUnit(replacementCard, originalUnit.Owner, originalUnit.SlotIndex);
            replacement.PlacedThisTurn = originalUnit.PlacedThisTurn;
            replacement.HasMovedThisTurn = originalUnit.HasMovedThisTurn;
            replacement.HasUsedGrantedEnemyMoveThisTurn = originalUnit.HasUsedGrantedEnemyMoveThisTurn;
            return true;
        }

        public bool SpawnUnit(PlayerSide side, int slotIndex, UnitCardData card)
        {
            if (card == null || state.Board.GetUnit(side, slotIndex) != null)
            {
                return false;
            }

            AddUnit(card, side, slotIndex);
            return true;
        }

        public void KillUnit(BoardUnit unit, PlayerSide killer)
        {
            state.Board.RemoveUnit(unit.Owner, unit.SlotIndex);
            state.GetPlayer(unit.Owner).AlliedUnitsDied++;

            TriggerUnitEffects(unit, EffectTriggerType.OnDeath);

            if (unit.HasKeyword(Keyword.Unstable, state))
            {
                TriggerUnstable(unit);
            }

            foreach (BoardUnit ally in new List<BoardUnit>(state.Board.GetUnits(unit.Owner)))
            {
                TriggerUnitEffects(ally, EffectTriggerType.OnAllyDeath);
            }

            TriggerLeaderEffects(EffectTriggerType.UnitDied, unit);
            TriggerLeaderEffectsFor(state.GetPlayer(killer), EffectTriggerType.UnitKilled, unit);

            SyncQualifyingEnemyAuraHealth();
        }

        private void TriggerUnstable(BoardUnit unit)
        {
            List<BoardUnit> candidates = new List<BoardUnit>(state.Board.GetUnits(unit.Owner.Opposite()));

            if (candidates.Count > 0)
            {
                DamageUnit(candidates[rng.Next(candidates.Count)], unit.GetCurrentAttack(state), unit.Owner);
            }
        }

        public void BounceUnit(BoardUnit unit)
        {
            Player owner = state.GetPlayer(unit.Owner);
            int damageTaken = unit.GetEffectiveMaxHealth(state) - unit.CurrentHealth;

            state.Board.RemoveUnit(unit.Owner, unit.SlotIndex);

            bool statsChanged = unit.BonusAttack != 0 || unit.MaxHealth != unit.SourceCard.Health || damageTaken != 0;

            CardData cardForHand = statsChanged
                ? unit.SourceCard.CreateSyncedClone(unit.SourceCard.ManaCost, unit.SourceCard.Attack + unit.BonusAttack, unit.MaxHealth, Mathf.Max(1, unit.MaxHealth - damageTaken))
                : unit.SourceCard;

            if (!owner.TryAddCardToHand(cardForHand))
            {
                state.RaiseCardBurnAnimationRequested(cardForHand, owner.Side);
            }
        }

        public void SilenceUnit(BoardUnit unit, int duration)
        {
            if (unit != null && !unit.IsSilenced)
            {
                unit.Statuses.Add(new ActiveStatusEffect(StatusEffectType.Silenced, duration));
            }
        }

        public bool SwapAttackAndHealth(BoardUnit unit)
        {
            if (unit == null || unit.HasKeyword(Keyword.Unmoving, state))
            {
                return false;
            }

            int baseAttack = unit.GetCurrentAttack(state) - AuraCalculator.GetAttackBonus(unit, state);
            int oldCurrentHealth = unit.CurrentHealth;

            unit.BonusAttack = oldCurrentHealth - unit.SourceCard.Attack;
            unit.MaxHealth = baseAttack;
            unit.CurrentHealth = unit.GetEffectiveMaxHealth(state);
            unit.LastSyncedAuraHealthBonus = AuraCalculator.GetQualifyingEnemyAuraHealthBonus(unit, state);

            if (unit.CurrentHealth <= 0)
            {
                KillUnit(unit, unit.Owner);
                return true;
            }

            SyncQualifyingEnemyAuraHealth();
            return true;
        }

        private static bool ConsumeStatus(List<ActiveStatusEffect> statuses, StatusEffectType type)
        {
            int index = statuses.FindIndex(status => status.Type == type);

            if (index < 0)
            {
                return false;
            }

            statuses.RemoveAt(index);
            return true;
        }

        private void TickDelayedKills(Player owner)
        {
            for (int slot = 0; slot < Board.SlotsPerSide; slot++)
            {
                BoardUnit unit = state.Board.GetUnit(owner.Side, slot);

                if (unit == null)
                {
                    continue;
                }

                for (int i = unit.Statuses.Count - 1; i >= 0; i--)
                {
                    ActiveStatusEffect status = unit.Statuses[i];

                    if (status.Type == StatusEffectType.DelayedKill && --status.RemainingTriggers <= 0)
                    {
                        KillUnit(unit, status.SourceOwner ?? unit.Owner.Opposite());
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
                List<ActiveStatusEffect> decay = unit?.Statuses.FindAll(status => status.Type == StatusEffectType.Decaying);

                if (decay != null && decay.Count > 0)
                {
                    DamageUnit(unit, decay.Count, decay[0].SourceOwner ?? unit.Owner.Opposite());
                }
            }
        }

        private bool IsOnBoard(BoardUnit unit)
        {
            return state.Board.GetUnit(unit.Owner, unit.SlotIndex) == unit;
        }

        private static int MoveManaCost(Player player)
        {
            return player.Leader != null ? player.Leader.MoveManaCost : 0;
        }

        public bool CanAnyUnitMove()
        {
            for (int fromSlot = 0; fromSlot < Board.SlotsPerSide; fromSlot++)
            {
                for (int toSlot = 0; toSlot < Board.SlotsPerSide; toSlot++)
                {
                    if (IsMoveLegal(fromSlot, toSlot, false))
                    {
                        return true;
                    }
                }
            }

            return HasAvailableGrantedEnemyMove(state.ActivePlayer);
        }

        public bool CanMoveUnit(int fromSlot, int toSlot, bool ignoreMoveLimitAndCost = false)
        {
            return !IsEndTurnQueued && !HasBlockingPendingTargetedEffect() && IsMoveLegal(fromSlot, toSlot, ignoreMoveLimitAndCost);
        }

        private bool IsMoveLegal(int fromSlot, int toSlot, bool ignoreMoveLimitAndCost)
        {
            Player mover = state.GetActivePlayerData();
            BoardUnit unit = state.Board.GetUnit(mover.Side, fromSlot);

            if (unit == null || unit.HasKeyword(Keyword.Unmoving, state) || unit.IsStunned)
            {
                return false;
            }

            if (!ignoreMoveLimitAndCost)
            {
                bool alreadyMoved = unit.HasKeyword(Keyword.Nimble, state)
                    ? unit.HasMovedThisTurn
                    : unit.PlacedThisTurn || state.HasUsedMoveThisTurn;

                if (alreadyMoved || mover.CurrentMana < MoveManaCost(mover))
                {
                    return false;
                }
            }

            return IsMoveRangeLegal(unit, fromSlot, toSlot);
        }

        private bool IsMoveRangeLegal(BoardUnit unit, int fromSlot, int toSlot)
        {
            if (fromSlot == toSlot || state.Board.GetUnit(unit.Owner, toSlot) != null)
            {
                return false;
            }

            if (unit.HasKeyword(Keyword.Teleport, state))
            {
                return true;
            }

            int maxRange = unit.HasKeyword(Keyword.Agile, state) ? 2 : 1;
            return Mathf.Abs(toSlot - fromSlot) <= maxRange && IsPathClear(unit.Owner, fromSlot, toSlot);
        }

        private bool IsPathClear(PlayerSide side, int fromSlot, int toSlot)
        {
            if (fromSlot == toSlot)
            {
                return false;
            }

            int step = toSlot > fromSlot ? 1 : -1;

            for (int slot = fromSlot + step; slot != toSlot + step; slot += step)
            {
                if (state.Board.GetUnit(side, slot) != null)
                {
                    return false;
                }
            }

            return true;
        }

        public bool TryMoveUnit(int fromSlot, int toSlot)
        {
            if (!CanMoveUnit(fromSlot, toSlot))
            {
                return false;
            }

            Player mover = state.GetActivePlayerData();
            BoardUnit unit = state.Board.GetUnit(mover.Side, fromSlot);

            mover.CurrentMana -= MoveManaCost(mover);
            unit.HasMovedThisTurn = true;

            if (!unit.HasKeyword(Keyword.Nimble, state))
            {
                state.HasUsedMoveThisTurn = true;
            }

            MoveOnBoard(unit, toSlot);
            return true;
        }

        public bool MoveUnitFree(PlayerSide side, int fromSlot, int toSlot)
        {
            if (state.ActivePlayer != side || !IsMoveLegal(fromSlot, toSlot, true))
            {
                return false;
            }

            MoveOnBoard(state.Board.GetUnit(side, fromSlot), toSlot);
            return true;
        }

        public bool HasAnyLegalUnblockedSlot(PlayerSide side, int fromSlot)
        {
            for (int toSlot = 0; toSlot < Board.SlotsPerSide; toSlot++)
            {
                if (IsPathClear(side, fromSlot, toSlot))
                {
                    return true;
                }
            }

            return false;
        }

        public bool CanMoveGrantedEnemyUnitFree(BoardUnit unit, int toSlot)
        {
            return unit != null && IsPathClear(unit.Owner, unit.SlotIndex, toSlot);
        }

        public bool MoveGrantedEnemyUnitFree(BoardUnit unit, int toSlot)
        {
            if (!CanMoveGrantedEnemyUnitFree(unit, toSlot))
            {
                return false;
            }

            MoveOnBoard(unit, toSlot);
            return true;
        }

        private BoardUnit FindAvailableEnemyMoveGranter(PlayerSide controllingSide)
        {
            return state.Board.GetUnits(controllingSide).FirstOrDefault(unit => unit.SourceCard.GrantsEnemyUnitMove && !unit.HasUsedGrantedEnemyMoveThisTurn);
        }

        public bool HasAvailableGrantedEnemyMove(PlayerSide controllingSide)
        {
            return FindAvailableEnemyMoveGranter(controllingSide) != null;
        }

        public bool CanMoveEnemyUnitViaGrantedAbility(PlayerSide controllingSide, int fromSlot, int toSlot)
        {
            BoardUnit unit = state.Board.GetUnit(controllingSide.Opposite(), fromSlot);
            return HasAvailableGrantedEnemyMove(controllingSide) && unit != null && IsMoveRangeLegal(unit, fromSlot, toSlot);
        }

        public bool MoveEnemyUnitViaGrantedAbility(PlayerSide controllingSide, int fromSlot, int toSlot)
        {
            if (!CanMoveEnemyUnitViaGrantedAbility(controllingSide, fromSlot, toSlot))
            {
                return false;
            }

            FindAvailableEnemyMoveGranter(controllingSide).HasUsedGrantedEnemyMoveThisTurn = true;
            MoveOnBoard(state.Board.GetUnit(controllingSide.Opposite(), fromSlot), toSlot, false);
            return true;
        }

        public bool HookClosestAllyLeft(BoardUnit sourceUnit)
        {
            if (sourceUnit == null)
            {
                return false;
            }

            int destinationSlot = sourceUnit.SlotIndex - 1;
            BoardUnit closestAlly = null;

            for (int slot = destinationSlot; slot >= 0 && closestAlly == null; slot--)
            {
                closestAlly = state.Board.GetUnit(sourceUnit.Owner, slot);
            }

            if (closestAlly == null || closestAlly.SlotIndex == destinationSlot || closestAlly.HasKeyword(Keyword.Unmoving, state))
            {
                return false;
            }

            MoveOnBoard(closestAlly, destinationSlot);
            return true;
        }

        public void PushAlliesAwayFrom(BoardUnit sourceUnit)
        {
            if (sourceUnit == null)
            {
                return;
            }

            int sourceSlot = sourceUnit.SlotIndex;
            List<BoardUnit> allies = new List<BoardUnit>(state.Board.GetUnits(sourceUnit.Owner));
            List<BoardUnit> leftGroup = allies.FindAll(unit => unit.SlotIndex < sourceSlot);
            List<BoardUnit> rightGroup = allies.FindAll(unit => unit.SlotIndex > sourceSlot);
            rightGroup.Reverse();

            foreach (BoardUnit unit in leftGroup)
            {
                PushUnitAsFarAsPossible(unit, -1);
            }

            foreach (BoardUnit unit in rightGroup)
            {
                PushUnitAsFarAsPossible(unit, 1);
            }
        }

        private void PushUnitAsFarAsPossible(BoardUnit unit, int direction)
        {
            if (!IsOnBoard(unit) || unit.HasKeyword(Keyword.Unmoving, state))
            {
                return;
            }

            int toSlot = unit.SlotIndex;

            while (toSlot + direction >= 0 && toSlot + direction < Board.SlotsPerSide && state.Board.GetUnit(unit.Owner, toSlot + direction) == null)
            {
                toSlot += direction;
            }

            if (toSlot != unit.SlotIndex)
            {
                MoveOnBoard(unit, toSlot);
            }
        }

        public bool SwapUnitSlots(BoardUnit unitA, BoardUnit unitB)
        {
            if (unitA == null || unitB == null || unitA == unitB || unitA.HasKeyword(Keyword.Unmoving, state) || unitB.HasKeyword(Keyword.Unmoving, state))
            {
                return false;
            }

            int slotA = unitA.SlotIndex;
            int slotB = unitB.SlotIndex;

            state.Board.RemoveUnit(unitA.Owner, slotA);
            state.Board.RemoveUnit(unitB.Owner, slotB);
            state.Board.PlaceUnit(unitA.Owner, slotB, unitA);
            state.Board.PlaceUnit(unitB.Owner, slotA, unitB);

            GrantMoveAttackBonus(unitA);
            GrantMoveAttackBonus(unitB);
            OnUnitRelocated(unitA, slotA);
            OnUnitRelocated(unitB, slotB);
            return true;
        }

        public bool PullUnitOpposite(BoardUnit sourceUnit, BoardUnit targetUnit)
        {
            if (sourceUnit == null || targetUnit == null || sourceUnit.Owner == targetUnit.Owner || targetUnit.HasKeyword(Keyword.Unmoving, state))
            {
                return false;
            }

            if (state.Board.GetUnit(targetUnit.Owner, sourceUnit.SlotIndex) != null)
            {
                return false;
            }

            MoveOnBoard(targetUnit, sourceUnit.SlotIndex, false);
            return true;
        }

        private bool TrySlippyDodge(BoardUnit defender)
        {
            if (!defender.HasKeyword(Keyword.Slippy, state) || defender.HasKeyword(Keyword.Unmoving, state))
            {
                return false;
            }

            int fromSlot = defender.SlotIndex;

            if (fromSlot > 0 && state.Board.GetUnit(defender.Owner, fromSlot - 1) == null)
            {
                MoveOnBoard(defender, fromSlot - 1, false);
                return true;
            }

            if (fromSlot < Board.SlotsPerSide - 1 && state.Board.GetUnit(defender.Owner, fromSlot + 1) == null)
            {
                MoveOnBoard(defender, fromSlot + 1, false);
                return true;
            }

            return false;
        }

        private void MoveOnBoard(BoardUnit unit, int toSlot, bool grantsMoveAttackBonus = true)
        {
            int fromSlot = unit.SlotIndex;

            state.Board.RemoveUnit(unit.Owner, fromSlot);
            state.Board.PlaceUnit(unit.Owner, toSlot, unit);

            if (grantsMoveAttackBonus)
            {
                GrantMoveAttackBonus(unit);
            }

            OnUnitRelocated(unit, fromSlot);
        }

        private void GrantMoveAttackBonus(BoardUnit unit)
        {
            LeaderData leader = state.GetPlayer(unit.Owner).Leader;

            if (unit.Owner == state.ActivePlayer && leader != null && leader.MoveTemporaryAttackBonus > 0)
            {
                unit.Statuses.Add(new ActiveStatusEffect(StatusEffectType.TemporaryAttack, 1, leader.MoveTemporaryAttackBonus));
            }
        }

        private void OnUnitRelocated(BoardUnit unit, int originSlot)
        {
            state.RaiseUnitMoved(unit);

            if (unit.IsSilenced)
            {
                return;
            }

            BoardUnit follower = state.Board.GetUnit(unit.Owner.Opposite(), originSlot);

            if (follower == null || !follower.HasKeyword(Keyword.Relentless, state) || follower.HasKeyword(Keyword.Unmoving, state))
            {
                return;
            }

            if (originSlot != unit.SlotIndex && state.Board.GetUnit(follower.Owner, unit.SlotIndex) == null)
            {
                MoveOnBoard(follower, unit.SlotIndex, false);
            }
        }

        public void EnterTurnEndPhase()
        {
            state.CurrentPhase = TurnPhase.TurnEnd;
        }

        public void EndActionPhase()
        {
            if (HasUnresolvedActions)
            {
                IsEndTurnQueued = true;
                return;
            }

            CancelPendingTargetedEffectIfNonMandatory();

            if (HasBlockingPendingTargetedEffect())
            {
                return;
            }

            IsEndTurnQueued = false;
            lastPlayedUnit = null;

            state.HasPendingFreeMove = false;
            state.PendingFreeMoveExcludedUnit = null;
            state.HasPendingEnemyMoveGrantOnPlay = false;
            state.PendingEnemyMoveGrantTarget = null;

            EnterTurnEndPhase();
            EndTurn();
        }

        public void EndTurn()
        {
            foreach (BoardUnit unit in state.Board.GetUnits(state.ActivePlayer))
            {
                unit.PlacedThisTurn = false;
                unit.HasMovedThisTurn = false;
                unit.HasAttackedThisTurn = false;
                unit.HasUsedGrantedEnemyMoveThisTurn = false;

                ActiveStatusEffect silence = unit.Statuses.Find(status => status.Type == StatusEffectType.Silenced);

                if (silence != null && --silence.RemainingTriggers <= 0)
                {
                    unit.Statuses.Remove(silence);
                }

                unit.Statuses.RemoveAll(status => status.Type == StatusEffectType.Stunned);
            }

            foreach (PlayerSide side in Sides)
            {
                foreach (BoardUnit unit in state.Board.GetUnits(side))
                {
                    unit.Statuses.RemoveAll(status => status.Type == StatusEffectType.DoubleAttackNextAttack || status.Type == StatusEffectType.TemporaryAttack);
                }
            }

            state.HasUsedMoveThisTurn = false;
            state.PlayerA.TriggeredOncePerTurnEffects.Clear();
            state.PlayerB.TriggeredOncePerTurnEffects.Clear();
            state.GetActivePlayerData().HasNextItemDoubled = false;

            if (state.ActivePlayer != state.FirstPlayer)
            {
                state.TurnNumber++;
            }

            state.ActivePlayer = state.ActivePlayer.Opposite();
            EnterDrawPhase();
        }

        private void Execute(CardEffect effect, PlayerSide owner, BoardUnit sourceUnit, EffectTarget target, int? runtimeAmount = null)
        {
            EffectExecutor.Execute(effect, new EffectContext(state, owner, sourceUnit, target), this, runtimeAmount);
        }

        private void ResolveEffect(CardEffect effect, BoardUnit sourceUnit, PlayerSide owner)
        {
            if (EffectTargeting.IsGroupTarget(effect.targetType))
            {
                foreach (BoardUnit target in EffectTargeting.ResolveGroupTargets(effect.targetType, sourceUnit, owner, state))
                {
                    Execute(effect, owner, sourceUnit, EffectTarget.ForUnit(target));
                }
            }
            else if (EffectTargeting.IsGroupSlotTarget(effect.targetType))
            {
                foreach (EffectTarget target in EffectTargeting.ResolveGroupSlotTargets(effect.targetType, sourceUnit, state))
                {
                    Execute(effect, owner, sourceUnit, target);
                }
            }
            else
            {
                Execute(effect, owner, sourceUnit, EffectTargeting.ResolveImmediateTarget(effect.targetType, sourceUnit, owner, state));
            }
        }

        private void TriggerUnitEffects(BoardUnit unit, EffectTriggerType trigger)
        {
            if (unit.IsSilenced)
            {
                return;
            }

            foreach (CardEffect effect in unit.SourceCard.Effects)
            {
                if (effect.trigger == trigger && !EffectTargeting.RequiresClick(effect.targetType))
                {
                    ResolveEffect(effect, unit, unit.Owner);
                }
            }
        }

        private void TriggerOnMove(BoardUnit unit)
        {
            if (unit != null && IsOnBoard(unit))
            {
                TriggerUnitEffects(unit, EffectTriggerType.OnMove);
            }
        }

        private void TriggerOnPlay(BoardUnit unit)
        {
            foreach (CardEffect effect in unit.SourceCard.Effects)
            {
                if (effect.trigger != EffectTriggerType.OnPlay)
                {
                    continue;
                }

                if (effect.action == EffectActionType.ChooseXCards || effect.action == EffectActionType.ChooseFixedCard)
                {
                    DeferCardChoice(effect, unit);
                }
                else if (EffectTargeting.RequiresClick(effect.targetType))
                {
                    DeferTargetedEffect(effect, unit, EffectTriggerType.OnPlay);
                }
                else
                {
                    ResolveEffect(effect, unit, unit.Owner);
                }
            }

            TriggerLeaderEffects(EffectTriggerType.OnPlay, unit);
        }

        private void TriggerOnDraw(Player player, CardData card)
        {
            if (!(card is UnitCardData unitCard) || !unitCard.Effects.Any(effect => effect.trigger == EffectTriggerType.OnDraw && effect.action == EffectActionType.RandomizeStatsOnDraw))
            {
                return;
            }

            int handIndex = player.Hand.IndexOf(card);

            if (handIndex >= 0)
            {
                player.Hand[handIndex] = unitCard.CreateSyncedClone(rng.Next(1, 7), rng.Next(1, 7), rng.Next(1, 7));
            }
        }

        private void TriggerLeaderEffects(EffectTriggerType trigger, BoardUnit sourceUnit)
        {
            TriggerLeaderEffectsFor(state.PlayerA, trigger, sourceUnit);
            TriggerLeaderEffectsFor(state.PlayerB, trigger, sourceUnit);
        }

        private void TriggerLeaderEffectsFor(Player leaderOwner, EffectTriggerType trigger, BoardUnit sourceUnit)
        {
            if (leaderOwner.Leader == null)
            {
                return;
            }

            foreach (CardEffect effect in leaderOwner.Leader.Effects)
            {
                if (effect.trigger != trigger || (effect.oncePerTurn && leaderOwner.TriggeredOncePerTurnEffects.Contains(effect)))
                {
                    continue;
                }

                if (EffectTargeting.IsGroupTarget(effect.targetType) || EffectTargeting.IsGroupSlotTarget(effect.targetType))
                {
                    ResolveEffect(effect, sourceUnit, leaderOwner.Side);
                }
                else
                {
                    EffectTarget target = effect.targetType == TargetType.None || (effect.targetType == TargetType.Self && sourceUnit == null)
                        ? EffectTarget.ForLeader(leaderOwner.Side)
                        : EffectTargeting.ResolveImmediateTarget(effect.targetType, sourceUnit, leaderOwner.Side, state);

                    if (target.Kind == EffectTargetKind.None || (target.Kind == EffectTargetKind.Unit && target.Unit == null))
                    {
                        continue;
                    }

                    Execute(effect, leaderOwner.Side, sourceUnit, target);
                }

                if (effect.oncePerTurn)
                {
                    leaderOwner.TriggeredOncePerTurnEffects.Add(effect);
                }
            }
        }

        private void DeferCardChoice(CardEffect effect, BoardUnit sourceUnit)
        {
            List<CardData> options;

            if (effect.action == EffectActionType.ChooseFixedCard)
            {
                options = new List<CardData>(effect.fixedChoiceOptions ?? new CardData[0]);
            }
            else
            {
                options = state.GetPlayer(sourceUnit.Owner).Deck.FindAll(card => MatchesCardCategory(card, effect.cardCategory));
                ListShuffler.Shuffle(options);
                options = options.GetRange(0, Mathf.Min(effect.amount, options.Count));
            }

            if (options.Count > 0)
            {
                state.PendingCardChoiceOptions = options;
                state.PendingCardChoiceSource = sourceUnit;
            }
        }

        public static bool MatchesCardCategory(CardData card, CardCategory category) => category switch
        {
            CardCategory.Unit => card is UnitCardData,
            CardCategory.Item => card is ItemCardData,
            _ => true
        };

        public bool TryResolvePendingCardChoice(CardData chosenCard)
        {
            if (state.PendingCardChoiceOptions == null || state.PendingCardChoiceSource == null || !state.PendingCardChoiceOptions.Contains(chosenCard))
            {
                return false;
            }

            Player owner = state.GetPlayer(state.PendingCardChoiceSource.Owner);
            state.PendingCardChoiceOptions = null;
            state.PendingCardChoiceSource = null;

            owner.Deck.Remove(chosenCard);

            if (!owner.TryAddCardToHand(chosenCard))
            {
                state.RaiseCardBurnAnimationRequested(chosenCard, owner.Side);
            }

            TryRunQueuedEndTurn();
            return true;
        }

        private void DeferTargetedEffect(CardEffect effect, BoardUnit sourceUnit, EffectTriggerType trigger, bool excludeSource = true)
        {
            if (!BoardHasValidTarget(effect.targetType, excludeSource ? sourceUnit : null))
            {
                return;
            }

            state.PendingTargetedEffect = effect;
            state.PendingTargetedEffectSource = sourceUnit;
            state.PendingTargetedEffectTrigger = trigger;
        }

        private bool BoardHasValidTarget(TargetType targetType, BoardUnit excludingUnit)
        {
            foreach (PlayerSide side in Sides)
            {
                if (EffectTargeting.IsValidTarget(targetType, EffectTarget.ForLeader(side), state))
                {
                    return true;
                }

                foreach (BoardUnit unit in state.Board.GetUnits(side))
                {
                    if (unit != excludingUnit && EffectTargeting.IsValidTarget(targetType, EffectTarget.ForUnit(unit), state))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void ClearPendingTargetedEffect()
        {
            state.PendingTargetedEffect = null;
            state.PendingTargetedEffectSource = null;
            state.PendingTargetedEffectTrigger = null;
        }

        public bool TryResolvePendingTargetedEffect(EffectTarget chosenTarget)
        {
            CardEffect effect = state.PendingTargetedEffect;
            BoardUnit sourceUnit = state.PendingTargetedEffectSource;

            if (effect == null || sourceUnit == null)
            {
                return false;
            }

            if (state.IsExcludedAsSelfTarget(chosenTarget.Kind == EffectTargetKind.Unit ? chosenTarget.Unit : null) || !EffectTargeting.IsValidTarget(effect.targetType, chosenTarget, state))
            {
                return false;
            }

            bool cameFromTurnStart = state.PendingTargetedEffectTrigger == EffectTriggerType.OnTurnStart;
            ClearPendingTargetedEffect();
            Execute(effect, sourceUnit.Owner, sourceUnit, chosenTarget);

            if (cameFromTurnStart && state.IsResolvingTurnStartEffects)
            {
                ContinueTurnStartScan(state.GetPlayer(sourceUnit.Owner), state.TurnStartScanSlot);
            }

            TryRunQueuedEndTurn();
            return true;
        }

        public bool TryReturnPendingOnPlayCardToHand()
        {
            BoardUnit source = state.PendingTargetedEffectSource;

            if (state.PendingTargetedEffect == null || source == null || state.PendingTargetedEffectTrigger != EffectTriggerType.OnPlay || source != lastPlayedUnit || !IsOnBoard(source))
            {
                return false;
            }

            Player owner = state.GetPlayer(source.Owner);
            ClearPendingTargetedEffect();

            state.Board.RemoveUnit(source.Owner, source.SlotIndex);

            if (lastPlayedAbsorbedUnit != null)
            {
                state.Board.PlaceUnit(source.Owner, source.SlotIndex, lastPlayedAbsorbedUnit);
            }

            owner.CurrentMana += lastPlayedManaSpent;

            if (lastPlayedHealthPaid > 0)
            {
                HealLeader(source.Owner, lastPlayedHealthPaid);
            }

            if (!owner.TryAddCardToHand(lastPlayedCard))
            {
                state.RaiseCardBurnAnimationRequested(lastPlayedCard, source.Owner);
            }

            lastPlayedUnit = null;
            SyncQualifyingEnemyAuraHealth();
            TryRunQueuedEndTurn();
            return true;
        }

        public bool HasBlockingPendingTargetedEffect()
        {
            CardEffect pending = state.PendingTargetedEffect;
            bool blockingTarget = pending != null && (pending.mandatoryTarget || state.PendingTargetedEffectTrigger == EffectTriggerType.OnTurnStart);
            return blockingTarget || state.PendingCardChoiceOptions != null;
        }

        public void CancelPendingTargetedEffectIfNonMandatory()
        {
            if (state.PendingTargetedEffect == null || state.PendingTargetedEffect.mandatoryTarget)
            {
                return;
            }

            bool cameFromTurnStart = state.PendingTargetedEffectTrigger == EffectTriggerType.OnTurnStart;
            BoardUnit source = state.PendingTargetedEffectSource;
            ClearPendingTargetedEffect();

            if (cameFromTurnStart && state.IsResolvingTurnStartEffects && source != null)
            {
                ContinueTurnStartScan(state.GetPlayer(source.Owner), state.TurnStartScanSlot);
            }
        }

        public void DeclareSurrender(PlayerSide surrenderingSide)
        {
            if (!state.IsGameOver)
            {
                EndGame(surrenderingSide.Opposite());
            }
        }

        private void CheckWinCondition()
        {
            if (state.IsGameOver)
            {
                return;
            }

            if (state.PlayerA.LeaderHealth <= 0)
            {
                EndGame(PlayerSide.PlayerB);
            }
            else if (state.PlayerB.LeaderHealth <= 0)
            {
                EndGame(PlayerSide.PlayerA);
            }
        }

        private void EndGame(PlayerSide winner)
        {
            state.IsGameOver = true;
            state.Winner = winner;
            state.RaiseGameOver();
        }
    }
}