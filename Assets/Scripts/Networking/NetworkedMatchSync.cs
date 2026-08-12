using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using DDD.TNFY.TCG.Cards;
using DDD.TNFY.TCG.Effects;

namespace DDD.TNFY.TCG.Core
{
    [RequireComponent(typeof(GameManager))]
    [RequireComponent(typeof(MatchBootstrapper))]
    [RequireComponent(typeof(PhotonView))]
    public class NetworkedMatchSync : MonoBehaviourPun
    {
        [Serializable]
        private struct CardRefDto
        {
            public string cardId;
            public bool isUnitCard;
            public int manaCost;
            public int attack;
            public int health;
        }

        [Serializable]
        private struct StatusEffectDto
        {
            public int type;
            public int remainingTriggers;
            public int magnitude;
            public bool hasSourceOwner;
            public int sourceOwner;
        }

        [Serializable]
        private class BoardUnitDto
        {
            public int slotIndex;
            public CardRefDto sourceCard;
            public int bonusAttack;
            public int maxHealth;
            public int currentHealth;
            public int lastSyncedAuraHealthBonus;
            public bool placedThisTurn;
            public bool hasMovedThisTurn;
            public bool hasAttackedThisTurn;
            public bool hasUsedGrantedEnemyMoveThisTurn;
            public int grantedKeywords;
            public StatusEffectDto[] statuses;
        }

        [Serializable]
        private class PlayerStateDto
        {
            public string leaderId;
            public int leaderHealth;
            public int maxLeaderHealth;
            public int currentMana;
            public int maxManaThisGame;
            public int pendingManaReduction;
            public bool hasReachedMaxMana;
            public bool hasNextItemDoubled;
            public int ownTurnCount;
            public int fatigueDamageTaken;
            public int alliedUnitsDied;
            public StatusEffectDto[] statuses;
            public bool hasDraftStage;
            public int draftStage;
            public CardRefDto[] pendingDraftOptions;
            public bool hasCompletedMulligan;
            public CardRefDto[] deck;
            public CardRefDto[] hand;
            public CardRefDto[] gameStartBonusCards;
        }

        [Serializable]
        private class MatchStateDto
        {
            public int currentPhase;
            public int activePlayer;
            public int firstPlayer;
            public int turnNumber;
            public bool isGameOver;
            public bool hasWinner;
            public int winner;
            public bool hasUsedMoveThisTurn;
            public bool hasPendingFreeMove;
            public int[] pendingFreeMoveExcludedUnitRef;
            public bool hasPendingEnemyMoveGrantOnPlay;
            public int[] pendingEnemyMoveGrantTargetRef;
            public bool hasPendingTargetedEffect;
            public int[] pendingTargetedEffectSourceRef;
            public int pendingTargetedEffectIndex;
            public int pendingTargetedEffectTrigger;
            public CardRefDto[] pendingCardChoiceOptions;
            public int[] pendingCardChoiceSourceRef;
            public int[] currentlyAttackingUnitRef;
            public float playerADraftRemaining;
            public float playerBDraftRemaining;
            public float playerAMulliganRemaining;
            public float playerBMulliganRemaining;
            public float turnRemaining;
            public BoardUnitDto[] boardA;
            public BoardUnitDto[] boardB;
            public PlayerStateDto playerA;
            public PlayerStateDto playerB;
        }

        public PlayerSide LocalSide { get; private set; } = PlayerSide.PlayerA;

        private GameManager gameManager;
        private MatchBootstrapper bootstrapper;
        private TurnTimerController turnTimer;

        private Dictionary<string, CardData> cardLookup;
        private Dictionary<string, LeaderData> leaderLookup;

        private void Awake()
        {
            gameManager = GetComponent<GameManager>();
            bootstrapper = GetComponent<MatchBootstrapper>();
            turnTimer = GetComponent<TurnTimerController>();
        }

        private void Start()
        {
            LocalSide = PhotonNetwork.InRoom ? SideForActorNumber(PhotonNetwork.LocalPlayer.ActorNumber) : PlayerSide.PlayerA;
            Debug.Log($"[NetworkedMatchSync] LocalSide resolved to {LocalSide} (actorNumber={(PhotonNetwork.InRoom ? PhotonNetwork.LocalPlayer.ActorNumber : -1)}).");

            gameManager.State.DraftOptionsChanged += HandleStateChangedForBroadcast;
            gameManager.State.PhaseChanged += HandlePhaseChangedForBroadcast;
            gameManager.State.GameOver += HandleStateChangedForBroadcast;
            gameManager.State.ActivePlayerChanged += HandleActivePlayerChangedForBroadcast;
            gameManager.State.CurrentlyAttackingUnitChanged += HandleCurrentlyAttackingUnitChangedForBroadcast;
        }

        private void OnDestroy()
        {
            if (gameManager != null && gameManager.State != null)
            {
                gameManager.State.DraftOptionsChanged -= HandleStateChangedForBroadcast;
                gameManager.State.PhaseChanged -= HandlePhaseChangedForBroadcast;
                gameManager.State.GameOver -= HandleStateChangedForBroadcast;
                gameManager.State.ActivePlayerChanged -= HandleActivePlayerChangedForBroadcast;
                gameManager.State.CurrentlyAttackingUnitChanged -= HandleCurrentlyAttackingUnitChangedForBroadcast;
            }
        }

        private void HandleActivePlayerChangedForBroadcast(PlayerSide newActivePlayer)
        {
            Debug.Log($"[NetworkedMatchSync] ActivePlayerChanged fired (newActivePlayer={newActivePlayer}) - queuing broadcast.");
            BroadcastStateIfMaster();
        }

        private void HandleCurrentlyAttackingUnitChangedForBroadcast(BoardUnit newAttacker)
        {
            Debug.Log($"[NetworkedMatchSync] CurrentlyAttackingUnitChanged fired (newAttacker={newAttacker?.SourceCard?.CardName}) - queuing broadcast.");
            BroadcastStateIfMaster();
        }

        public static PlayerSide SideForActorNumber(int actorNumber)
        {
            return actorNumber == 1 ? PlayerSide.PlayerA : PlayerSide.PlayerB;
        }

        private void HandleStateChangedForBroadcast()
        {
            Debug.Log("[NetworkedMatchSync] DraftOptionsChanged/GameOver fired - queuing broadcast.");
            BroadcastStateIfMaster();
        }

        private void HandlePhaseChangedForBroadcast(TurnPhase newPhase)
        {
            Debug.Log($"[NetworkedMatchSync] PhaseChanged fired (newPhase={newPhase}) - queuing broadcast.");
            BroadcastStateIfMaster();

            bool isAuthoritative = !PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient;

            if (!isAuthoritative)
            {
                return;
            }

            PlayerSide activePlayerAtAnnouncement = gameManager.State.ActivePlayer;
            Debug.Log($"[NetworkedMatchSync] Announcing phase locally: newPhase={newPhase}, activePlayer={activePlayerAtAnnouncement}.");
            gameManager.State.RaisePhaseAnnounced(newPhase, activePlayerAtAnnouncement);

            if (PhotonNetwork.InRoom)
            {
                photonView.RPC(nameof(ReceivePhaseAnnouncement), RpcTarget.Others, (int)newPhase, (int)activePlayerAtAnnouncement);
            }
        }

        [PunRPC]
        private void ReceivePhaseAnnouncement(int newPhaseRaw, int activePlayerRaw)
        {
            TurnPhase newPhase = (TurnPhase)newPhaseRaw;
            PlayerSide activePlayerAtAnnouncement = (PlayerSide)activePlayerRaw;
            Debug.Log($"[NetworkedMatchSync] Received phase announcement: newPhase={newPhase}, activePlayer={activePlayerAtAnnouncement}.");
            gameManager.State.RaisePhaseAnnounced(newPhase, activePlayerAtAnnouncement);
        }

        private bool broadcastPending;

        private void LateUpdate()
        {
            if (!broadcastPending)
            {
                return;
            }

            broadcastPending = false;

            if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
            {
                return;
            }

            BroadcastState();
        }

        private void BroadcastStateIfMaster()
        {
            if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
            {
                return;
            }

            broadcastPending = true;
        }

        public void BroadcastState()
        {
            GameState state = gameManager.State;

            turnTimer?.RefreshDeadlines();

            MatchStateDto dto = new MatchStateDto
            {
                currentPhase = (int)state.CurrentPhase,
                activePlayer = (int)state.ActivePlayer,
                firstPlayer = (int)state.FirstPlayer,
                turnNumber = state.TurnNumber,
                isGameOver = state.IsGameOver,
                hasWinner = state.Winner.HasValue,
                winner = state.Winner.HasValue ? (int)state.Winner.Value : -1,
                hasUsedMoveThisTurn = state.HasUsedMoveThisTurn,
                hasPendingFreeMove = state.HasPendingFreeMove,
                pendingFreeMoveExcludedUnitRef = EncodeUnitRef(state.PendingFreeMoveExcludedUnit),
                hasPendingEnemyMoveGrantOnPlay = state.HasPendingEnemyMoveGrantOnPlay,
                pendingEnemyMoveGrantTargetRef = EncodeUnitRef(state.PendingEnemyMoveGrantTarget),
                hasPendingTargetedEffect = state.PendingTargetedEffect != null,
                pendingTargetedEffectSourceRef = EncodeUnitRef(state.PendingTargetedEffectSource),
                pendingTargetedEffectIndex = EncodeEffectIndex(state.PendingTargetedEffectSource, state.PendingTargetedEffect),
                pendingTargetedEffectTrigger = (int)(state.PendingTargetedEffectTrigger ?? default),
                pendingCardChoiceOptions = state.PendingCardChoiceOptions != null ? BuildCardRefs(state.PendingCardChoiceOptions) : new CardRefDto[0],
                pendingCardChoiceSourceRef = EncodeUnitRef(state.PendingCardChoiceSource),
                currentlyAttackingUnitRef = EncodeUnitRef(state.CurrentlyAttackingUnit),
                playerADraftRemaining = turnTimer != null ? (turnTimer.GetDraftRemainingSeconds(PlayerSide.PlayerA) ?? -1f) : -1f,
                playerBDraftRemaining = turnTimer != null ? (turnTimer.GetDraftRemainingSeconds(PlayerSide.PlayerB) ?? -1f) : -1f,
                playerAMulliganRemaining = turnTimer != null ? (turnTimer.GetMulliganRemainingSeconds(PlayerSide.PlayerA) ?? -1f) : -1f,
                playerBMulliganRemaining = turnTimer != null ? (turnTimer.GetMulliganRemainingSeconds(PlayerSide.PlayerB) ?? -1f) : -1f,
                turnRemaining = turnTimer != null ? (turnTimer.GetTurnRemainingSeconds() ?? -1f) : -1f,
                boardA = BuildBoardUnits(state, PlayerSide.PlayerA),
                boardB = BuildBoardUnits(state, PlayerSide.PlayerB),
                playerA = BuildPlayerDto(state.PlayerA),
                playerB = BuildPlayerDto(state.PlayerB)
            };

            string json = JsonUtility.ToJson(dto);
            Debug.Log($"[NetworkedMatchSync] Broadcasting state - phase={state.CurrentPhase}, PlayerA draftStage={state.PlayerA.CurrentDraftStage}, PlayerB draftStage={state.PlayerB.CurrentDraftStage}.");
            Debug.Log($"[NetworkedMatchSync] Broadcasting turnRemaining={dto.turnRemaining}, activePlayer={state.ActivePlayer}.");
            photonView.RPC(nameof(ReceiveState), RpcTarget.Others, json);
        }

        [PunRPC]
        private void ReceiveState(string json)
        {
            EnsureLookupsBuilt();

            MatchStateDto dto = JsonUtility.FromJson<MatchStateDto>(json);
            GameState state = gameManager.State;

            ApplyPlayerState(state.PlayerA, dto.playerA);
            ApplyPlayerState(state.PlayerB, dto.playerB);
            ApplyBoard(state, PlayerSide.PlayerA, dto.boardA);
            ApplyBoard(state, PlayerSide.PlayerB, dto.boardB);

            state.HasUsedMoveThisTurn = dto.hasUsedMoveThisTurn;
            state.HasPendingFreeMove = dto.hasPendingFreeMove;
            state.PendingFreeMoveExcludedUnit = DecodeUnitRef(dto.pendingFreeMoveExcludedUnitRef, state);
            state.HasPendingEnemyMoveGrantOnPlay = dto.hasPendingEnemyMoveGrantOnPlay;
            state.PendingEnemyMoveGrantTarget = DecodeUnitRef(dto.pendingEnemyMoveGrantTargetRef, state);

            BoardUnit pendingTargetedEffectSource = dto.hasPendingTargetedEffect
                ? DecodeUnitRef(dto.pendingTargetedEffectSourceRef, state)
                : null;
            state.PendingTargetedEffectSource = pendingTargetedEffectSource;
            state.PendingTargetedEffect = dto.hasPendingTargetedEffect
                ? DecodeEffect(pendingTargetedEffectSource, dto.pendingTargetedEffectIndex)
                : null;
            state.PendingTargetedEffectTrigger = dto.hasPendingTargetedEffect
                ? (EffectTriggerType?)dto.pendingTargetedEffectTrigger
                : null;

            Debug.Log($"[NetworkedMatchSync] Synced pending targeted effect: hasPendingTargetedEffect={dto.hasPendingTargetedEffect}, source={pendingTargetedEffectSource?.SourceCard?.CardName}, effect={state.PendingTargetedEffect?.action}.");

            state.PendingCardChoiceSource = DecodeUnitRef(dto.pendingCardChoiceSourceRef, state);
            state.PendingCardChoiceOptions = (dto.pendingCardChoiceOptions != null && dto.pendingCardChoiceOptions.Length > 0)
                ? new List<CardData>(Array.ConvertAll(dto.pendingCardChoiceOptions, ResolveCard))
                : null;

            Debug.Log($"[NetworkedMatchSync] Synced pending card choice: hasPendingCardChoice={state.PendingCardChoiceOptions != null}, source={state.PendingCardChoiceSource?.SourceCard?.CardName}, optionCount={state.PendingCardChoiceOptions?.Count ?? 0}.");

            BoardUnit syncedAttacker = DecodeUnitRef(dto.currentlyAttackingUnitRef, state);
            state.CurrentlyAttackingUnit = syncedAttacker;

            Debug.Log($"[NetworkedMatchSync] Synced CurrentlyAttackingUnit: attacker={syncedAttacker?.SourceCard?.CardName}.");

            if (turnTimer != null && !PhotonNetwork.IsMasterClient)
            {
                Debug.Log($"[NetworkedMatchSync] Applying synced turnRemaining={dto.turnRemaining} on non-master client.");
                turnTimer.ApplySyncedRemaining(dto.playerADraftRemaining, dto.playerBDraftRemaining, dto.playerAMulliganRemaining, dto.playerBMulliganRemaining, dto.turnRemaining);
            }

            state.FirstPlayer = (PlayerSide)dto.firstPlayer;
            state.ActivePlayer = (PlayerSide)dto.activePlayer;
            state.TurnNumber = dto.turnNumber;
            state.CurrentPhase = (TurnPhase)dto.currentPhase;
            state.IsGameOver = dto.isGameOver;
            state.Winner = dto.hasWinner ? (PlayerSide?)dto.winner : null;

            Debug.Log($"[NetworkedMatchSync] Applied synced state - phase={state.CurrentPhase}, PlayerA draftStage={state.PlayerA.CurrentDraftStage}, PlayerB draftStage={state.PlayerB.CurrentDraftStage}.");
        }

        public void RequestDraftChoice(PlayerSide side, CardData chosenCard)
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient)
            {
                ResolveDraftChoice(side, chosenCard);
                return;
            }

            Debug.Log($"[NetworkedMatchSync] Requesting draft choice for {side}: '{chosenCard.CardId}'.");
            photonView.RPC(nameof(ReceiveDraftChoiceRequest), RpcTarget.MasterClient, (int)side, chosenCard.CardId);
        }

        [PunRPC]
        private void ReceiveDraftChoiceRequest(int sideRaw, string cardId, PhotonMessageInfo info)
        {
            PlayerSide requestedSide = (PlayerSide)sideRaw;
            PlayerSide senderSide = SideForActorNumber(info.Sender.ActorNumber);

            if (requestedSide != senderSide)
            {
                Debug.LogWarning($"[NetworkedMatchSync] ReceiveDraftChoiceRequest REJECTED: actor {info.Sender.ActorNumber} ({senderSide}) tried to act as {requestedSide}.");
                return;
            }

            Player player = gameManager.State.GetPlayer(requestedSide);
            CardData match = player.PendingDraftOptions?.Find(c => c.CardId == cardId);

            if (match == null)
            {
                Debug.LogWarning($"[NetworkedMatchSync] ReceiveDraftChoiceRequest: no pending draft option for {requestedSide} matches cardId '{cardId}'.");
                return;
            }

            ResolveDraftChoice(requestedSide, match);
        }

        private void ResolveDraftChoice(PlayerSide side, CardData chosenCard)
        {
            bool resolved = gameManager.Phases.TryResolvePendingDraftChoice(side, chosenCard);
            Debug.Log($"[NetworkedMatchSync] ResolveDraftChoice resolved={resolved} for {side}, '{chosenCard.CardId}' - broadcasting (in case only one side has finished picking so far).");
            BroadcastStateIfMaster();
        }

        public void RequestMulliganChoice(PlayerSide side, List<int> handIndicesToMulligan)
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient)
            {
                ResolveMulliganByIndices(side, handIndicesToMulligan);
                return;
            }

            Debug.Log($"[NetworkedMatchSync] Requesting mulligan choice for {side}: {handIndicesToMulligan.Count} card(s).");
            photonView.RPC(nameof(ReceiveMulliganChoiceRequest), RpcTarget.MasterClient, (int)side, handIndicesToMulligan.ToArray());
        }

        [PunRPC]
        private void ReceiveMulliganChoiceRequest(int sideRaw, int[] handIndices, PhotonMessageInfo info)
        {
            PlayerSide requestedSide = (PlayerSide)sideRaw;
            PlayerSide senderSide = SideForActorNumber(info.Sender.ActorNumber);

            if (requestedSide != senderSide)
            {
                Debug.LogWarning($"[NetworkedMatchSync] ReceiveMulliganChoiceRequest REJECTED: actor {info.Sender.ActorNumber} ({senderSide}) tried to act as {requestedSide}.");
                return;
            }

            if (gameManager.State.CurrentPhase != TurnPhase.Mulligan || gameManager.State.GetPlayer(requestedSide).HasCompletedMulligan)
            {
                Debug.LogWarning($"[NetworkedMatchSync] ReceiveMulliganChoiceRequest REJECTED: not currently accepting a mulligan for {requestedSide} (phase={gameManager.State.CurrentPhase}, alreadyCompleted={gameManager.State.GetPlayer(requestedSide).HasCompletedMulligan}).");
                return;
            }

            ResolveMulliganByIndices(requestedSide, new List<int>(handIndices));
            Debug.Log($"[NetworkedMatchSync] ReceiveMulliganChoiceRequest resolved for {requestedSide}: {handIndices.Length} card(s).");
        }

        private bool ValidateSenderIsActivePlayer(PhotonMessageInfo info, out PlayerSide senderSide)
        {
            senderSide = SideForActorNumber(info.Sender.ActorNumber);

            if (senderSide != gameManager.State.ActivePlayer)
            {
                Debug.LogWarning($"[NetworkedMatchSync] REJECTED: actor {info.Sender.ActorNumber} ({senderSide}) is not the active player ({gameManager.State.ActivePlayer}).");
                return false;
            }

            return true;
        }

        private bool ValidateSenderOwnsPendingTargetedEffect(PhotonMessageInfo info)
        {
            if (gameManager.State.PendingTargetedEffectSource == null)
            {
                Debug.LogWarning("[NetworkedMatchSync] REJECTED: no pending targeted effect.");
                return false;
            }

            PlayerSide senderSide = SideForActorNumber(info.Sender.ActorNumber);
            PlayerSide ownerSide = gameManager.State.PendingTargetedEffectSource.Owner;

            if (senderSide != ownerSide)
            {
                Debug.LogWarning($"[NetworkedMatchSync] REJECTED: actor {info.Sender.ActorNumber} ({senderSide}) does not own the pending targeted effect ({ownerSide}).");
                return false;
            }

            return true;
        }

        private bool ValidateSenderOwnsPendingCardChoice(PhotonMessageInfo info)
        {
            if (gameManager.State.PendingCardChoiceSource == null)
            {
                Debug.LogWarning("[NetworkedMatchSync] REJECTED: no pending card choice.");
                return false;
            }

            PlayerSide senderSide = SideForActorNumber(info.Sender.ActorNumber);
            PlayerSide ownerSide = gameManager.State.PendingCardChoiceSource.Owner;

            if (senderSide != ownerSide)
            {
                Debug.LogWarning($"[NetworkedMatchSync] REJECTED: actor {info.Sender.ActorNumber} ({senderSide}) does not own the pending card choice ({ownerSide}).");
                return false;
            }

            return true;
        }

        private int[] EncodeTarget(EffectTarget target)
        {
            switch (target.Kind)
            {
                case EffectTargetKind.Unit:
                    return new[] { (int)EffectTargetKind.Unit, (int)target.Unit.Owner, target.Unit.SlotIndex };
                case EffectTargetKind.Leader:
                    return new[] { (int)EffectTargetKind.Leader, (int)target.LeaderSide };
                case EffectTargetKind.Slot:
                    return new[] { (int)EffectTargetKind.Slot, (int)target.SlotSide, target.SlotIndex };
                default:
                    return new[] { (int)EffectTargetKind.None };
            }
        }

        private EffectTarget DecodeTarget(int[] data, GameState state)
        {
            EffectTargetKind kind = (EffectTargetKind)data[0];

            switch (kind)
            {
                case EffectTargetKind.Unit:
                    BoardUnit unit = state.Board.GetUnit((PlayerSide)data[1], data[2]);
                    return unit != null ? EffectTarget.ForUnit(unit) : EffectTarget.None;
                case EffectTargetKind.Leader:
                    return EffectTarget.ForLeader((PlayerSide)data[1]);
                case EffectTargetKind.Slot:
                    return EffectTarget.ForSlot((PlayerSide)data[1], data[2]);
                default:
                    return EffectTarget.None;
            }
        }

        public bool RequestPlayUnit(int handIndex, int slotIndex)
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient)
            {
                return BeginAnimatedPlayUnit(gameManager.State.ActivePlayer, handIndex, slotIndex);
            }

            Debug.Log($"[NetworkedMatchSync] Requesting PlayUnit: handIndex={handIndex}, slot={slotIndex}.");
            photonView.RPC(nameof(ReceivePlayUnitRequest), RpcTarget.MasterClient, handIndex, slotIndex);
            return true;
        }

        [PunRPC]
        private void ReceivePlayUnitRequest(int handIndex, int slotIndex, PhotonMessageInfo info)
        {
            if (!ValidateSenderIsActivePlayer(info, out PlayerSide senderSide))
            {
                return;
            }

            BeginAnimatedPlayUnit(senderSide, handIndex, slotIndex);
        }

        private bool BeginAnimatedPlayUnit(PlayerSide side, int handIndex, int slotIndex)
        {
            Player player = gameManager.State.GetPlayer(side);

            if (handIndex < 0 || handIndex >= player.Hand.Count || !(player.Hand[handIndex] is UnitCardData unitCard))
            {
                Debug.LogWarning($"[NetworkedMatchSync] BeginAnimatedPlayUnit: invalid handIndex {handIndex}.");
                return false;
            }

            if (!gameManager.Phases.CanPlayUnit(unitCard, slotIndex))
            {
                Debug.LogWarning($"[NetworkedMatchSync] BeginAnimatedPlayUnit: CanPlayUnit rejected {unitCard.CardName} -> slot {slotIndex} for {side}.");
                return false;
            }

            Debug.Log($"[NetworkedMatchSync] BeginAnimatedPlayUnit accepted: {unitCard.CardName} handIndex={handIndex} -> slot {slotIndex} for {side}. Broadcasting animation cue.");

            if (PhotonNetwork.InRoom)
            {
                photonView.RPC(nameof(ReceivePlayUnitAnimation), RpcTarget.All, (int)side, handIndex, slotIndex);
            }
            else
            {
                ReceivePlayUnitAnimation((int)side, handIndex, slotIndex);
            }

            gameManager.Phases.ResolveUnitPlayAfterAnimation(unitCard, side, handIndex, slotIndex, BroadcastStateIfMaster);

            return true;
        }

        [PunRPC]
        private void ReceivePlayUnitAnimation(int sideRaw, int handIndex, int slotIndex)
        {
            PlayerSide side = (PlayerSide)sideRaw;
            Player player = gameManager.State.GetPlayer(side);

            if (handIndex < 0 || handIndex >= player.Hand.Count)
            {
                Debug.LogWarning($"[NetworkedMatchSync] ReceivePlayUnitAnimation: invalid handIndex {handIndex} for {side}.");
                return;
            }

            Debug.Log($"[NetworkedMatchSync] ReceivePlayUnitAnimation: raising UnitPlayAnimationRequested for {player.Hand[handIndex].CardName} ({side}) -> slot {slotIndex}.");
            gameManager.State.RaiseUnitPlayAnimationRequested(player.Hand[handIndex], side, handIndex, slotIndex);
        }

        public bool RequestPlayItem(int handIndex, EffectTarget target)
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient)
            {
                return BeginAnimatedPlayItem(gameManager.State.ActivePlayer, handIndex, target);
            }

            Debug.Log($"[NetworkedMatchSync] Requesting PlayItem: handIndex={handIndex}, target.Kind={target.Kind}.");
            photonView.RPC(nameof(ReceivePlayItemRequest), RpcTarget.MasterClient, handIndex, EncodeTarget(target));
            return true;
        }

        [PunRPC]
        private void ReceivePlayItemRequest(int handIndex, int[] targetData, PhotonMessageInfo info)
        {
            if (!ValidateSenderIsActivePlayer(info, out PlayerSide senderSide))
            {
                return;
            }

            EffectTarget target = DecodeTarget(targetData, gameManager.State);
            BeginAnimatedPlayItem(senderSide, handIndex, target);
        }

        private bool BeginAnimatedPlayItem(PlayerSide side, int handIndex, EffectTarget target)
        {
            Player player = gameManager.State.GetPlayer(side);

            if (handIndex < 0 || handIndex >= player.Hand.Count || !(player.Hand[handIndex] is ItemCardData itemCard))
            {
                Debug.LogWarning($"[NetworkedMatchSync] BeginAnimatedPlayItem: invalid handIndex {handIndex}.");
                return false;
            }

            EffectTarget effectiveTarget = gameManager.Phases.ResolveItemEffectTarget(itemCard.PrimaryEffect, target);

            if (!gameManager.Phases.CanPlayItem(itemCard, effectiveTarget))
            {
                Debug.LogWarning($"[NetworkedMatchSync] BeginAnimatedPlayItem: CanPlayItem rejected {itemCard.CardName} for {side}, target.Kind={effectiveTarget.Kind}.");
                return false;
            }

            Debug.Log($"[NetworkedMatchSync] BeginAnimatedPlayItem accepted: {itemCard.CardName} handIndex={handIndex} for {side}. Broadcasting animation cue.");

            if (PhotonNetwork.InRoom)
            {
                photonView.RPC(nameof(ReceivePlayItemAnimation), RpcTarget.All, (int)side, handIndex);
            }
            else
            {
                ReceivePlayItemAnimation((int)side, handIndex);
            }

            gameManager.Phases.ResolveItemPlayAfterAnimation(itemCard, target, side, handIndex, BroadcastStateIfMaster);

            return true;
        }

        [PunRPC]
        private void ReceivePlayItemAnimation(int sideRaw, int handIndex)
        {
            PlayerSide side = (PlayerSide)sideRaw;
            Player player = gameManager.State.GetPlayer(side);

            if (handIndex < 0 || handIndex >= player.Hand.Count)
            {
                Debug.LogWarning($"[NetworkedMatchSync] ReceivePlayItemAnimation: invalid handIndex {handIndex} for {side}.");
                return;
            }

            Debug.Log($"[NetworkedMatchSync] ReceivePlayItemAnimation: raising ItemPlayAnimationRequested for {player.Hand[handIndex].CardName} ({side}).");
            gameManager.State.RaiseItemPlayAnimationRequested(player.Hand[handIndex], side, handIndex);
        }

        public void RequestResolveTargetedEffect(EffectTarget target)
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient)
            {
                bool resolved = gameManager.Phases.TryResolvePendingTargetedEffect(target);
                Debug.Log($"[NetworkedMatchSync] TryResolvePendingTargetedEffect resolved={resolved} for target.Kind={target.Kind}.");
                BroadcastStateIfMaster();
                return;
            }

            Debug.Log($"[NetworkedMatchSync] Requesting targeted effect resolution: target.Kind={target.Kind}.");
            photonView.RPC(nameof(ReceiveResolveTargetedEffectRequest), RpcTarget.MasterClient, EncodeTarget(target));
        }

        [PunRPC]
        private void ReceiveResolveTargetedEffectRequest(int[] targetData, PhotonMessageInfo info)
        {
            if (!ValidateSenderOwnsPendingTargetedEffect(info))
            {
                return;
            }

            EffectTarget target = DecodeTarget(targetData, gameManager.State);
            bool resolved = gameManager.Phases.TryResolvePendingTargetedEffect(target);
            Debug.Log($"[NetworkedMatchSync] ReceiveResolveTargetedEffectRequest resolved={resolved} for target.Kind={target.Kind}.");
            BroadcastStateIfMaster();
        }

        public void RequestResolveCardChoice(int optionIndex)
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient)
            {
                ResolveCardChoiceByIndex(optionIndex);
                return;
            }

            Debug.Log($"[NetworkedMatchSync] Requesting card choice resolution: optionIndex={optionIndex}.");
            photonView.RPC(nameof(ReceiveResolveCardChoiceRequest), RpcTarget.MasterClient, optionIndex);
        }

        private void ResolveCardChoiceByIndex(int optionIndex)
        {
            List<CardData> options = gameManager.State.PendingCardChoiceOptions;

            if (options == null || optionIndex < 0 || optionIndex >= options.Count)
            {
                Debug.LogWarning($"[NetworkedMatchSync] ResolveCardChoiceByIndex: invalid optionIndex {optionIndex}.");
                return;
            }

            bool resolved = gameManager.Phases.TryResolvePendingCardChoice(options[optionIndex]);
            Debug.Log($"[NetworkedMatchSync] TryResolvePendingCardChoice resolved={resolved} for optionIndex={optionIndex}.");
            BroadcastStateIfMaster();
        }

        [PunRPC]
        private void ReceiveResolveCardChoiceRequest(int optionIndex, PhotonMessageInfo info)
        {
            if (!ValidateSenderOwnsPendingCardChoice(info))
            {
                return;
            }

            ResolveCardChoiceByIndex(optionIndex);
        }

        private int[] EncodeUnitRef(BoardUnit unit)
        {
            return unit != null ? new[] { 1, (int)unit.Owner, unit.SlotIndex } : new[] { 0 };
        }

        private BoardUnit DecodeUnitRef(int[] data, GameState state)
        {
            if (data == null || data.Length == 0 || data[0] == 0)
            {
                return null;
            }

            return state.Board.GetUnit((PlayerSide)data[1], data[2]);
        }

        private int EncodeEffectIndex(BoardUnit sourceUnit, CardEffect effect)
        {
            if (sourceUnit == null || effect == null)
            {
                return -1;
            }

            IReadOnlyList<CardEffect> effects = sourceUnit.SourceCard.Effects;

            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i] == effect)
                {
                    return i;
                }
            }

            Debug.LogWarning($"[NetworkedMatchSync] EncodeEffectIndex: could not find pending effect on {sourceUnit.SourceCard.CardName}'s effect list. Target prompt will not sync correctly.");
            return -1;
        }

        private CardEffect DecodeEffect(BoardUnit sourceUnit, int index)
        {
            if (sourceUnit == null || index < 0)
            {
                return null;
            }

            IReadOnlyList<CardEffect> effects = sourceUnit.SourceCard.Effects;

            if (index >= effects.Count)
            {
                Debug.LogWarning($"[NetworkedMatchSync] DecodeEffect: index {index} out of range for {sourceUnit.SourceCard.CardName} ({effects.Count} effects).");
                return null;
            }

            return effects[index];
        }

        public bool RequestMoveUnit(int fromSlot, int toSlot)
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient)
            {
                bool resolved = gameManager.Phases.TryMoveUnit(fromSlot, toSlot);
                Debug.Log($"[NetworkedMatchSync] TryMoveUnit resolved={resolved} for {fromSlot} -> {toSlot}.");
                BroadcastStateIfMaster();
                return resolved;
            }

            Debug.Log($"[NetworkedMatchSync] Requesting MoveUnit: {fromSlot} -> {toSlot}.");
            photonView.RPC(nameof(ReceiveMoveUnitRequest), RpcTarget.MasterClient, fromSlot, toSlot);
            return true;
        }

        [PunRPC]
        private void ReceiveMoveUnitRequest(int fromSlot, int toSlot, PhotonMessageInfo info)
        {
            if (!ValidateSenderIsActivePlayer(info, out _))
            {
                return;
            }

            bool resolved = gameManager.Phases.TryMoveUnit(fromSlot, toSlot);
            Debug.Log($"[NetworkedMatchSync] ReceiveMoveUnitRequest resolved={resolved} for {fromSlot} -> {toSlot}.");
            BroadcastStateIfMaster();
        }

        public bool RequestMoveUnitFree(int fromSlot, int toSlot)
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient)
            {
                return ResolveMoveUnitFree(fromSlot, toSlot);
            }

            Debug.Log($"[NetworkedMatchSync] Requesting MoveUnitFree: {fromSlot} -> {toSlot}.");
            photonView.RPC(nameof(ReceiveMoveUnitFreeRequest), RpcTarget.MasterClient, fromSlot, toSlot);
            return true;
        }

        private bool ResolveMoveUnitFree(int fromSlot, int toSlot)
        {
            PlayerSide side = gameManager.State.ActivePlayer;
            bool resolved = gameManager.Phases.MoveUnitFree(side, fromSlot, toSlot);

            if (resolved)
            {
                gameManager.State.HasPendingFreeMove = false;
                gameManager.State.PendingFreeMoveExcludedUnit = null;
            }

            Debug.Log($"[NetworkedMatchSync] MoveUnitFree resolved={resolved} for {fromSlot} -> {toSlot}.");
            BroadcastStateIfMaster();
            return resolved;
        }

        [PunRPC]
        private void ReceiveMoveUnitFreeRequest(int fromSlot, int toSlot, PhotonMessageInfo info)
        {
            if (!ValidateSenderIsActivePlayer(info, out _))
            {
                return;
            }

            ResolveMoveUnitFree(fromSlot, toSlot);
        }

        public bool RequestMoveGrantedEnemyUnitFree(int toSlot)
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient)
            {
                return ResolveMoveGrantedEnemyUnitFree(toSlot);
            }

            Debug.Log($"[NetworkedMatchSync] Requesting MoveGrantedEnemyUnitFree: toSlot={toSlot}.");
            photonView.RPC(nameof(ReceiveMoveGrantedEnemyUnitFreeRequest), RpcTarget.MasterClient, toSlot);
            return true;
        }

        private bool ResolveMoveGrantedEnemyUnitFree(int toSlot)
        {
            BoardUnit target = gameManager.State.PendingEnemyMoveGrantTarget;

            if (target == null)
            {
                Debug.LogWarning("[NetworkedMatchSync] ResolveMoveGrantedEnemyUnitFree: no PendingEnemyMoveGrantTarget.");
                return false;
            }

            bool resolved = gameManager.Phases.MoveGrantedEnemyUnitFree(target, toSlot);

            if (resolved)
            {
                gameManager.State.HasPendingEnemyMoveGrantOnPlay = false;
                gameManager.State.PendingEnemyMoveGrantTarget = null;
            }

            Debug.Log($"[NetworkedMatchSync] MoveGrantedEnemyUnitFree resolved={resolved} for toSlot={toSlot}.");
            BroadcastStateIfMaster();
            return resolved;
        }

        [PunRPC]
        private void ReceiveMoveGrantedEnemyUnitFreeRequest(int toSlot, PhotonMessageInfo info)
        {
            if (!ValidateSenderIsActivePlayer(info, out _))
            {
                return;
            }

            ResolveMoveGrantedEnemyUnitFree(toSlot);
        }

        public bool RequestMoveEnemyUnitViaGrantedAbility(int fromSlot, int toSlot)
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient)
            {
                bool resolved = gameManager.Phases.MoveEnemyUnitViaGrantedAbility(gameManager.State.ActivePlayer, fromSlot, toSlot);
                Debug.Log($"[NetworkedMatchSync] MoveEnemyUnitViaGrantedAbility resolved={resolved} for {fromSlot} -> {toSlot}.");
                BroadcastStateIfMaster();
                return resolved;
            }

            Debug.Log($"[NetworkedMatchSync] Requesting MoveEnemyUnitViaGrantedAbility: {fromSlot} -> {toSlot}.");
            photonView.RPC(nameof(ReceiveMoveEnemyUnitViaGrantedAbilityRequest), RpcTarget.MasterClient, fromSlot, toSlot);
            return true;
        }

        [PunRPC]
        private void ReceiveMoveEnemyUnitViaGrantedAbilityRequest(int fromSlot, int toSlot, PhotonMessageInfo info)
        {
            if (!ValidateSenderIsActivePlayer(info, out PlayerSide senderSide))
            {
                return;
            }

            bool resolved = gameManager.Phases.MoveEnemyUnitViaGrantedAbility(senderSide, fromSlot, toSlot);
            Debug.Log($"[NetworkedMatchSync] ReceiveMoveEnemyUnitViaGrantedAbilityRequest resolved={resolved} for {fromSlot} -> {toSlot}.");
            BroadcastStateIfMaster();
        }

        public void RequestEndActionPhase()
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient)
            {
                gameManager.Phases.EndActionPhase();
                return;
            }

            Debug.Log("[NetworkedMatchSync] Requesting EndActionPhase.");
            photonView.RPC(nameof(ReceiveEndActionPhaseRequest), RpcTarget.MasterClient);
        }

        [PunRPC]
        private void ReceiveEndActionPhaseRequest(PhotonMessageInfo info)
        {
            if (!ValidateSenderIsActivePlayer(info, out _))
            {
                return;
            }

            if (gameManager.State.CurrentPhase != TurnPhase.Action)
            {
                Debug.LogWarning($"[NetworkedMatchSync] ReceiveEndActionPhaseRequest REJECTED: not currently Action phase (phase={gameManager.State.CurrentPhase}).");
                return;
            }

            gameManager.Phases.EndActionPhase();
            Debug.Log("[NetworkedMatchSync] EndActionPhase triggered by remote request.");
        }

        public bool RequestAttackWithUnit(int slotIndex)
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient)
            {
                bool resolved = gameManager.Phases.TryAttackWithUnit(slotIndex, OnAttackFullyResolved);
                Debug.Log($"[NetworkedMatchSync] TryAttackWithUnit resolved={resolved} for slot={slotIndex}.");
                BroadcastStateIfMaster();
                return resolved;
            }

            Debug.Log($"[NetworkedMatchSync] Requesting AttackWithUnit: slot={slotIndex}.");
            photonView.RPC(nameof(ReceiveAttackWithUnitRequest), RpcTarget.MasterClient, slotIndex);
            return true;
        }

        public void RequestSurrender()
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient)
            {
                gameManager.Phases.DeclareSurrender(LocalSide);
                Debug.Log($"[NetworkedMatchSync] Surrender resolved locally for {LocalSide}.");
                BroadcastStateIfMaster();
                return;
            }

            Debug.Log($"[NetworkedMatchSync] Requesting Surrender for {LocalSide}.");
            photonView.RPC(nameof(ReceiveSurrenderRequest), RpcTarget.MasterClient);
        }

        [PunRPC]
        private void ReceiveSurrenderRequest(PhotonMessageInfo info)
        {
            PlayerSide surrenderingSide = SideForActorNumber(info.Sender.ActorNumber);
            gameManager.Phases.DeclareSurrender(surrenderingSide);
            Debug.Log($"[NetworkedMatchSync] ReceiveSurrenderRequest resolved for {surrenderingSide}.");
            BroadcastStateIfMaster();
        }

        [PunRPC]
        private void ReceiveAttackWithUnitRequest(int slotIndex, PhotonMessageInfo info)
        {
            if (!ValidateSenderIsActivePlayer(info, out _))
            {
                return;
            }

            bool resolved = gameManager.Phases.TryAttackWithUnit(slotIndex, OnAttackFullyResolved);
            Debug.Log($"[NetworkedMatchSync] ReceiveAttackWithUnitRequest resolved={resolved} for slot={slotIndex}.");
            BroadcastStateIfMaster();
        }

        private void OnAttackFullyResolved()
        {
            Debug.Log("[NetworkedMatchSync] Attack sequence fully resolved (damage/chain-attacks applied) - queuing a follow-up broadcast.");
            BroadcastStateIfMaster();
        }

        private void ResolveMulliganByIndices(PlayerSide side, List<int> handIndices)
        {
            Player player = gameManager.State.GetPlayer(side);
            List<CardData> cardsToMulligan = new List<CardData>();

            foreach (int index in handIndices)
            {
                if (index >= 0 && index < player.Hand.Count)
                {
                    cardsToMulligan.Add(player.Hand[index]);
                }
            }

            gameManager.Phases.ResolveMulliganAndAdvance(side, cardsToMulligan);

            Debug.Log($"[NetworkedMatchSync] ResolveMulliganByIndices: broadcasting after resolving {side}'s mulligan (in case only one side has finished so far).");
            BroadcastStateIfMaster();
        }

        public void RequestUpdateMulliganSelection(PlayerSide side, List<int> handIndices)
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient)
            {
                turnTimer?.SetLiveMulliganSelection(side, handIndices);
                return;
            }

            photonView.RPC(nameof(ReceiveUpdateMulliganSelectionRequest), RpcTarget.MasterClient, (int)side, handIndices.ToArray());
        }

        [PunRPC]
        private void ReceiveUpdateMulliganSelectionRequest(int sideRaw, int[] handIndices, PhotonMessageInfo info)
        {
            PlayerSide requestedSide = (PlayerSide)sideRaw;
            PlayerSide senderSide = SideForActorNumber(info.Sender.ActorNumber);

            if (requestedSide != senderSide)
            {
                Debug.LogWarning($"[NetworkedMatchSync] ReceiveUpdateMulliganSelectionRequest REJECTED: actor {info.Sender.ActorNumber} ({senderSide}) tried to act as {requestedSide}.");
                return;
            }

            turnTimer?.SetLiveMulliganSelection(requestedSide, new List<int>(handIndices));
        }

        private PlayerStateDto BuildPlayerDto(Player player)
        {
            Debug.Log($"[NetworkedMatchSync] BuildPlayerDto: side={player.Side}, leaderHealth={player.LeaderHealth}, maxLeaderHealth={player.MaxLeaderHealth}.");

            return new PlayerStateDto
            {
                leaderId = player.Leader != null ? player.Leader.LeaderId : string.Empty,
                leaderHealth = player.LeaderHealth,
                maxLeaderHealth = player.MaxLeaderHealth,
                currentMana = player.CurrentMana,
                maxManaThisGame = player.MaxManaThisGame,
                pendingManaReduction = player.PendingManaReduction,
                hasReachedMaxMana = player.HasReachedMaxMana,
                hasNextItemDoubled = player.HasNextItemDoubled,
                ownTurnCount = player.OwnTurnCount,
                fatigueDamageTaken = player.FatigueDamageTaken,
                alliedUnitsDied = player.AlliedUnitsDied,
                statuses = BuildStatusEffects(player.Statuses),
                hasDraftStage = player.CurrentDraftStage.HasValue,
                draftStage = player.CurrentDraftStage.HasValue ? (int)player.CurrentDraftStage.Value : -1,
                pendingDraftOptions = player.PendingDraftOptions != null ? BuildCardRefs(player.PendingDraftOptions) : new CardRefDto[0],
                hasCompletedMulligan = player.HasCompletedMulligan,
                deck = BuildCardRefs(player.Deck),
                hand = BuildCardRefs(player.Hand),
                gameStartBonusCards = BuildCardRefs(player.GameStartBonusCards)
            };
        }

        private BoardUnitDto[] BuildBoardUnits(GameState state, PlayerSide side)
        {
            List<BoardUnitDto> result = new List<BoardUnitDto>();

            foreach (BoardUnit unit in state.Board.GetUnits(side))
            {
                result.Add(new BoardUnitDto
                {
                    slotIndex = unit.SlotIndex,
                    sourceCard = BuildCardRef(unit.SourceCard),
                    bonusAttack = unit.BonusAttack,
                    maxHealth = unit.MaxHealth,
                    currentHealth = unit.CurrentHealth,
                    lastSyncedAuraHealthBonus = unit.LastSyncedAuraHealthBonus,
                    placedThisTurn = unit.PlacedThisTurn,
                    hasMovedThisTurn = unit.HasMovedThisTurn,
                    hasAttackedThisTurn = unit.HasAttackedThisTurn,
                    hasUsedGrantedEnemyMoveThisTurn = unit.HasUsedGrantedEnemyMoveThisTurn,
                    grantedKeywords = (int)unit.GrantedKeywords,
                    statuses = BuildStatusEffects(unit.Statuses)
                });
            }

            return result.ToArray();
        }

        private StatusEffectDto[] BuildStatusEffects(List<ActiveStatusEffect> statuses)
        {
            StatusEffectDto[] result = new StatusEffectDto[statuses.Count];

            for (int i = 0; i < statuses.Count; i++)
            {
                ActiveStatusEffect status = statuses[i];

                result[i] = new StatusEffectDto
                {
                    type = (int)status.Type,
                    remainingTriggers = status.RemainingTriggers,
                    magnitude = status.Magnitude,
                    hasSourceOwner = status.SourceOwner.HasValue,
                    sourceOwner = status.SourceOwner.HasValue ? (int)status.SourceOwner.Value : -1
                };
            }

            return result;
        }

        private CardRefDto[] BuildCardRefs(List<CardData> cards)
        {
            CardRefDto[] result = new CardRefDto[cards.Count];

            for (int i = 0; i < cards.Count; i++)
            {
                result[i] = BuildCardRef(cards[i]);
            }

            return result;
        }

        private CardRefDto BuildCardRef(CardData card)
        {
            if (card is UnitCardData unitCard)
            {
                return new CardRefDto
                {
                    cardId = unitCard.CardId,
                    isUnitCard = true,
                    manaCost = unitCard.ManaCost,
                    attack = unitCard.Attack,
                    health = unitCard.Health
                };
            }

            return new CardRefDto
            {
                cardId = card.CardId,
                isUnitCard = false,
                manaCost = card.ManaCost
            };
        }

        private void ApplyPlayerState(Player player, PlayerStateDto dto)
        {
            Debug.Log($"[NetworkedMatchSync] ApplyPlayerState: side={player.Side}, beforeLeaderHealth={player.LeaderHealth}, dto.leaderHealth={dto.leaderHealth}, dto.maxLeaderHealth={dto.maxLeaderHealth}.");

            player.Leader = ResolveLeader(dto.leaderId);
            player.LeaderHealth = dto.leaderHealth;
            player.MaxLeaderHealth = dto.maxLeaderHealth;
            player.CurrentMana = dto.currentMana;
            player.MaxManaThisGame = dto.maxManaThisGame;
            player.PendingManaReduction = dto.pendingManaReduction;
            player.HasReachedMaxMana = dto.hasReachedMaxMana;
            player.HasNextItemDoubled = dto.hasNextItemDoubled;
            player.OwnTurnCount = dto.ownTurnCount;
            player.FatigueDamageTaken = dto.fatigueDamageTaken;
            player.AlliedUnitsDied = dto.alliedUnitsDied;
            ApplyStatusEffects(player.Statuses, dto.statuses);
            player.CurrentDraftStage = dto.hasDraftStage ? (DraftStage?)dto.draftStage : null;
            player.PendingDraftOptions = (dto.pendingDraftOptions != null && dto.pendingDraftOptions.Length > 0)
                ? new List<CardData>(Array.ConvertAll(dto.pendingDraftOptions, ResolveCard))
                : null;
            player.HasCompletedMulligan = dto.hasCompletedMulligan;

            player.Deck.Clear();
            foreach (CardRefDto cardDto in dto.deck)
            {
                CardData resolvedCard = ResolveCard(cardDto);
                if (resolvedCard == null)
                {
                    Debug.LogError($"[NetworkedMatchSync] Dropped unresolved card '{cardDto.cardId}' from {player.Side}'s synced Deck instead of adding null.");
                    continue;
                }

                player.Deck.Add(resolvedCard);
            }

            player.Hand.Clear();
            foreach (CardRefDto cardDto in dto.hand)
            {
                CardData resolvedCard = ResolveCard(cardDto);
                if (resolvedCard == null)
                {
                    Debug.LogError($"[NetworkedMatchSync] Dropped unresolved card '{cardDto.cardId}' from {player.Side}'s synced Hand instead of adding null.");
                    continue;
                }

                player.Hand.Add(resolvedCard);
            }

            player.GameStartBonusCards.Clear();
            if (dto.gameStartBonusCards != null)
            {
                foreach (CardRefDto cardDto in dto.gameStartBonusCards)
                {
                    CardData resolvedCard = ResolveCard(cardDto);
                    if (resolvedCard == null)
                    {
                        Debug.LogError($"[NetworkedMatchSync] Dropped unresolved card '{cardDto.cardId}' from {player.Side}'s synced GameStartBonusCards instead of adding null.");
                        continue;
                    }

                    player.GameStartBonusCards.Add(resolvedCard);
                }
            }

            Debug.Log($"[NetworkedMatchSync] Applied player state for {player.Side}: hand={player.Hand.Count}, gameStartBonusCards={player.GameStartBonusCards.Count}.");
        }

        private void ApplyBoard(GameState state, PlayerSide side, BoardUnitDto[] unitDtos)
        {
            BoardUnit[] existingUnits = new BoardUnit[Board.SlotsPerSide];
            for (int slot = 0; slot < Board.SlotsPerSide; slot++)
            {
                existingUnits[slot] = state.Board.GetUnit(side, slot);
            }

            bool[] slotOccupiedByIncoming = new bool[Board.SlotsPerSide];

            if (unitDtos != null)
            {
                foreach (BoardUnitDto unitDto in unitDtos)
                {
                    CardData sourceCard = ResolveCard(unitDto.sourceCard);

                    if (!(sourceCard is UnitCardData sourceUnitCard))
                    {
                        Debug.LogWarning($"[NetworkedMatchSync] ApplyBoard: resolved card for {side} slot {unitDto.slotIndex} was not a UnitCardData - skipping.");
                        continue;
                    }

                    slotOccupiedByIncoming[unitDto.slotIndex] = true;

                    BoardUnit existing = existingUnits[unitDto.slotIndex];
                    bool canReuse = existing != null
                        && existing.SourceCard.CardId == sourceUnitCard.CardId
                        && !(unitDto.placedThisTurn && !existing.PlacedThisTurn);

                    BoardUnit unit = canReuse ? existing : new BoardUnit(sourceUnitCard, side, unitDto.slotIndex);

                    unit.BonusAttack = unitDto.bonusAttack;
                    unit.MaxHealth = unitDto.maxHealth;
                    unit.CurrentHealth = unitDto.currentHealth;
                    unit.LastSyncedAuraHealthBonus = unitDto.lastSyncedAuraHealthBonus;
                    unit.PlacedThisTurn = unitDto.placedThisTurn;
                    unit.HasMovedThisTurn = unitDto.hasMovedThisTurn;
                    unit.HasAttackedThisTurn = unitDto.hasAttackedThisTurn;
                    unit.HasUsedGrantedEnemyMoveThisTurn = unitDto.hasUsedGrantedEnemyMoveThisTurn;
                    unit.GrantedKeywords = (Keyword)unitDto.grantedKeywords;

                    ApplyStatusEffects(unit.Statuses, unitDto.statuses);

                    if (!canReuse)
                    {
                        state.Board.PlaceUnit(side, unitDto.slotIndex, unit);
                    }
                }
            }

            for (int slot = 0; slot < Board.SlotsPerSide; slot++)
            {
                if (!slotOccupiedByIncoming[slot] && existingUnits[slot] != null)
                {
                    state.Board.RemoveUnit(side, slot);
                }
            }
        }

        private void ApplyStatusEffects(List<ActiveStatusEffect> target, StatusEffectDto[] dtos)
        {
            target.Clear();

            if (dtos == null)
            {
                return;
            }

            foreach (StatusEffectDto dto in dtos)
            {
                target.Add(new ActiveStatusEffect(
                    (StatusEffectType)dto.type,
                    dto.remainingTriggers,
                    dto.magnitude,
                    dto.hasSourceOwner ? (PlayerSide?)dto.sourceOwner : null));
            }
        }

        private CardData ResolveCard(CardRefDto dto)
        {
            if (!cardLookup.TryGetValue(dto.cardId, out CardData baseCard))
            {
                Debug.LogWarning($"[NetworkedMatchSync] Unknown cardId '{dto.cardId}' received - no matching asset in the local card pool. Check that this card is included in MatchBootstrapper's Card Pool list, even if it's a reward-only card that's never drafted.");
                return null;
            }

            if (!dto.isUnitCard || !(baseCard is UnitCardData baseUnitCard))
            {
                return baseCard;
            }

            if (dto.manaCost == baseUnitCard.ManaCost && dto.attack == baseUnitCard.Attack && dto.health == baseUnitCard.Health)
            {
                return baseUnitCard;
            }

            return baseUnitCard.CreateSyncedClone(dto.manaCost, dto.attack, dto.health);
        }

        private LeaderData ResolveLeader(string leaderId)
        {
            if (string.IsNullOrEmpty(leaderId))
            {
                return null;
            }

            if (!leaderLookup.TryGetValue(leaderId, out LeaderData leader))
            {
                Debug.LogWarning($"[NetworkedMatchSync] Unknown leaderId '{leaderId}' received - no matching asset in the local leader pool.");
                return null;
            }

            return leader;
        }

        private void EnsureLookupsBuilt()
        {
            if (cardLookup != null)
            {
                return;
            }

            cardLookup = new Dictionary<string, CardData>();
            Queue<CardData> cardsToScanForReferences = new Queue<CardData>();

            foreach (CardData card in bootstrapper.CardPool)
            {
                if (TryRegisterCard(card))
                {
                    cardsToScanForReferences.Enqueue(card);
                }
            }

            while (cardsToScanForReferences.Count > 0)
            {
                CardData card = cardsToScanForReferences.Dequeue();

                foreach (CardEffect effect in card.Effects)
                {
                    if (TryRegisterCard(effect.relevantCard))
                    {
                        cardsToScanForReferences.Enqueue(effect.relevantCard);
                    }

                    if (effect.fixedChoiceOptions == null)
                    {
                        continue;
                    }

                    foreach (CardData option in effect.fixedChoiceOptions)
                    {
                        if (TryRegisterCard(option))
                        {
                            cardsToScanForReferences.Enqueue(option);
                        }
                    }
                }
            }

            Debug.Log($"[NetworkedMatchSync] Built card lookup with {cardLookup.Count} card(s) ({bootstrapper.CardPool.Count} from Card Pool, {cardLookup.Count - bootstrapper.CardPool.Count} discovered via relevantCard/fixedChoiceOptions references).");

            leaderLookup = new Dictionary<string, LeaderData>();
            foreach (LeaderData leader in bootstrapper.LeaderPool)
            {
                leaderLookup[leader.LeaderId] = leader;
            }
        }

        private bool TryRegisterCard(CardData card)
        {
            if (card == null || cardLookup.ContainsKey(card.CardId))
            {
                return false;
            }

            cardLookup[card.CardId] = card;
            return true;
        }
    }
}