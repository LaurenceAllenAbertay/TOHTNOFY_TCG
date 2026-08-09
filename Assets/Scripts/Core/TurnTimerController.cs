using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using DDD.TNFY.TCG.Cards;
using DDD.TNFY.TCG.Effects;

namespace DDD.TNFY.TCG.Core
{
    [RequireComponent(typeof(GameManager))]
    public class TurnTimerController : MonoBehaviour
    {
        [SerializeField] private float timerDurationSeconds = 60f;
        [SerializeField] private float draftTimerDurationSeconds = 120f;
        [SerializeField] private int maxPendingSelectionIterations = 20;
        [SerializeField] private int maxDraftAutoFillIterations = 40;

        private GameManager gameManager;

        private float? playerADraftDeadline;
        private float? playerBDraftDeadline;
        private float? playerAMulliganDeadline;
        private float? playerBMulliganDeadline;
        private float? turnDeadline;

        private DraftStage? lastSeenPlayerADraftStage;
        private DraftStage? lastSeenPlayerBDraftStage;
        private bool lastSeenPlayerAMulliganActive;
        private bool lastSeenPlayerBMulliganActive;
        private TurnPhase? lastSeenTurnPhase;
        private PlayerSide? lastSeenTurnActivePlayer;

        private readonly Dictionary<PlayerSide, List<int>> liveMulliganSelection = new Dictionary<PlayerSide, List<int>>
        {
            { PlayerSide.PlayerA, new List<int>() },
            { PlayerSide.PlayerB, new List<int>() }
        };

        private void Awake()
        {
            gameManager = GetComponent<GameManager>();
        }

        private void Update()
        {
            if (gameManager == null || gameManager.State == null)
            {
                return;
            }

            bool isAuthoritative = !PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient;

            if (!isAuthoritative)
            {
                return;
            }

            RefreshDeadlines();

            if (gameManager.State.IsGameOver)
            {
                return;
            }

            CheckExpiry();
            RefreshDeadlines();
        }

        public void RefreshDeadlines()
        {
            if (gameManager == null || gameManager.State == null)
            {
                return;
            }

            if (gameManager.State.IsGameOver)
            {
                ClearAllDeadlines();
                return;
            }

            DetectDraftChanges();
            DetectMulliganChanges();
            DetectTurnChanges();
        }

        private void ClearAllDeadlines()
        {
            playerADraftDeadline = null;
            playerBDraftDeadline = null;
            playerAMulliganDeadline = null;
            playerBMulliganDeadline = null;
            turnDeadline = null;
        }

        private void DetectDraftChanges()
        {
            GameState state = gameManager.State;

            bool playerADraftStarting = state.PlayerA.CurrentDraftStage != null && lastSeenPlayerADraftStage == null;
            bool playerADraftEnding = state.PlayerA.CurrentDraftStage == null && lastSeenPlayerADraftStage != null;

            if (playerADraftStarting)
            {
                playerADraftDeadline = Time.time + draftTimerDurationSeconds;
                Debug.Log($"[TurnTimerController] PlayerA's draft has begun - starting a single {draftTimerDurationSeconds}s timer for the whole draft phase.");
            }
            else if (playerADraftEnding)
            {
                playerADraftDeadline = null;
            }

            lastSeenPlayerADraftStage = state.PlayerA.CurrentDraftStage;

            bool playerBDraftStarting = state.PlayerB.CurrentDraftStage != null && lastSeenPlayerBDraftStage == null;
            bool playerBDraftEnding = state.PlayerB.CurrentDraftStage == null && lastSeenPlayerBDraftStage != null;

            if (playerBDraftStarting)
            {
                playerBDraftDeadline = Time.time + draftTimerDurationSeconds;
                Debug.Log($"[TurnTimerController] PlayerB's draft has begun - starting a single {draftTimerDurationSeconds}s timer for the whole draft phase.");
            }
            else if (playerBDraftEnding)
            {
                playerBDraftDeadline = null;
            }

            lastSeenPlayerBDraftStage = state.PlayerB.CurrentDraftStage;
        }

        private void DetectMulliganChanges()
        {
            GameState state = gameManager.State;

            bool playerAMulliganActive = state.CurrentPhase == TurnPhase.Mulligan && !state.PlayerA.HasCompletedMulligan;
            bool playerAMulliganStarting = playerAMulliganActive && !lastSeenPlayerAMulliganActive;
            bool playerAMulliganEnding = !playerAMulliganActive && lastSeenPlayerAMulliganActive;

            if (playerAMulliganStarting)
            {
                playerAMulliganDeadline = Time.time + timerDurationSeconds;
                liveMulliganSelection[PlayerSide.PlayerA] = new List<int>();
                Debug.Log($"[TurnTimerController] PlayerA's mulligan has begun - starting a {timerDurationSeconds}s timer.");
            }
            else if (playerAMulliganEnding)
            {
                playerAMulliganDeadline = null;
            }

            lastSeenPlayerAMulliganActive = playerAMulliganActive;

            bool playerBMulliganActive = state.CurrentPhase == TurnPhase.Mulligan && !state.PlayerB.HasCompletedMulligan;
            bool playerBMulliganStarting = playerBMulliganActive && !lastSeenPlayerBMulliganActive;
            bool playerBMulliganEnding = !playerBMulliganActive && lastSeenPlayerBMulliganActive;

            if (playerBMulliganStarting)
            {
                playerBMulliganDeadline = Time.time + timerDurationSeconds;
                liveMulliganSelection[PlayerSide.PlayerB] = new List<int>();
                Debug.Log($"[TurnTimerController] PlayerB's mulligan has begun - starting a {timerDurationSeconds}s timer.");
            }
            else if (playerBMulliganEnding)
            {
                playerBMulliganDeadline = null;
            }

            lastSeenPlayerBMulliganActive = playerBMulliganActive;
        }

        private void DetectTurnChanges()
        {
            GameState state = gameManager.State;
            bool isTurnTimedPhase = state.CurrentPhase == TurnPhase.Action;

            bool changed = lastSeenTurnPhase != state.CurrentPhase || lastSeenTurnActivePlayer != state.ActivePlayer;

            if (!changed)
            {
                return;
            }

            lastSeenTurnPhase = state.CurrentPhase;
            lastSeenTurnActivePlayer = state.ActivePlayer;

            if (!isTurnTimedPhase)
            {
                turnDeadline = null;
                return;
            }

            turnDeadline = Time.time + timerDurationSeconds;
        }

        private void CheckExpiry()
        {
            if (playerADraftDeadline.HasValue && Time.time >= playerADraftDeadline.Value)
            {
                playerADraftDeadline = null;
                ResolveDraftTimeout(PlayerSide.PlayerA);
            }

            if (playerBDraftDeadline.HasValue && Time.time >= playerBDraftDeadline.Value)
            {
                playerBDraftDeadline = null;
                ResolveDraftTimeout(PlayerSide.PlayerB);
            }

            if (playerAMulliganDeadline.HasValue && Time.time >= playerAMulliganDeadline.Value)
            {
                playerAMulliganDeadline = null;
                ResolveMulliganTimeout(PlayerSide.PlayerA);
            }

            if (playerBMulliganDeadline.HasValue && Time.time >= playerBMulliganDeadline.Value)
            {
                playerBMulliganDeadline = null;
                ResolveMulliganTimeout(PlayerSide.PlayerB);
            }

            if (turnDeadline.HasValue && Time.time >= turnDeadline.Value)
            {
                turnDeadline = null;
                Debug.Log("[TurnTimerController] Turn timer expired - resolving timeout.");
                ResolveTurnTimeout();
                Debug.Log($"[TurnTimerController] After timeout resolution: CurrentPhase={gameManager.State.CurrentPhase}, ActivePlayer={gameManager.State.ActivePlayer}, turnDeadline={(turnDeadline.HasValue ? (turnDeadline.Value - Time.time).ToString("F1") : "null")}.");
            }
        }

        private void ResolveDraftTimeout(PlayerSide side)
        {
            Player player = gameManager.State.GetPlayer(side);
            int iterations = 0;

            Debug.Log($"[TurnTimerController] {side}'s draft pick timed out - auto-filling the rest of their deck with random picks.");

            while (player.PendingDraftOptions != null && player.PendingDraftOptions.Count > 0 && iterations < maxDraftAutoFillIterations)
            {
                iterations++;

                List<CardData> options = player.PendingDraftOptions;
                CardData randomCard = options[Random.Range(0, options.Count)];

                Debug.Log($"[TurnTimerController] Draft auto-fill: {side} picks {randomCard.CardName} ({player.CurrentDraftStage}).");

                gameManager.Phases.TryResolvePendingDraftChoice(side, randomCard);
            }

            if (iterations >= maxDraftAutoFillIterations)
            {
                Debug.LogWarning($"[TurnTimerController] Draft auto-fill for {side} hit max iterations ({maxDraftAutoFillIterations}) - stopping regardless of remaining picks.");
            }
        }

        private void ResolveTurnTimeout()
        {
            switch (gameManager.State.CurrentPhase)
            {
                case TurnPhase.Action:
                    ResolvePendingSelectionsThenAdvance();
                    break;
            }
        }

        private void ResolveMulliganTimeout(PlayerSide side)
        {
            List<int> selection = GetLiveMulliganSelection(side);
            Player player = gameManager.State.GetPlayer(side);
            List<CardData> cardsToMulligan = new List<CardData>();

            foreach (int index in selection)
            {
                if (index >= 0 && index < player.Hand.Count)
                {
                    cardsToMulligan.Add(player.Hand[index]);
                }
            }

            Debug.Log($"[TurnTimerController] {side}'s mulligan timed out - auto-confirming with {cardsToMulligan.Count} card(s) currently selected.");
            gameManager.Phases.ResolveMulliganAndAdvance(side, cardsToMulligan);
        }

        private void ResolvePendingSelectionsThenAdvance()
        {
            GameState state = gameManager.State;
            int iterations = 0;

            while (gameManager.Phases.HasBlockingPendingTargetedEffect() && iterations < maxPendingSelectionIterations)
            {
                iterations++;

                if (state.PendingCardChoiceOptions != null)
                {
                    List<CardData> options = state.PendingCardChoiceOptions;
                    CardData randomChoice = options[Random.Range(0, options.Count)];
                    Debug.Log($"[TurnTimerController] Timeout: auto-choosing {randomChoice.CardName} from pending card choice.");
                    gameManager.Phases.TryResolvePendingCardChoice(randomChoice);
                    continue;
                }

                if (state.PendingTargetedEffect != null)
                {
                    EffectTarget target = PickRandomValidTarget(state.PendingTargetedEffect.targetType, state);

                    if (target.Kind == EffectTargetKind.None)
                    {
                        Debug.LogWarning("[TurnTimerController] Timeout: no valid target found for mandatory pending effect - clearing it.");
                        state.PendingTargetedEffect = null;
                        state.PendingTargetedEffectSource = null;
                        state.PendingTargetedEffectTrigger = null;
                        continue;
                    }

                    Debug.Log($"[TurnTimerController] Timeout: auto-targeting {target.Kind} for pending mandatory effect.");
                    gameManager.Phases.TryResolvePendingTargetedEffect(target);
                }
            }

            if (iterations >= maxPendingSelectionIterations)
            {
                Debug.LogWarning("[TurnTimerController] Timeout: hit max iterations while resolving pending selections - forcing phase advance regardless.");
            }

            gameManager.Phases.CancelPendingTargetedEffectIfNonMandatory();

            if (state.CurrentPhase == TurnPhase.Action)
            {
                Debug.Log("[TurnTimerController] Timeout: forcing EndActionPhase.");
                gameManager.Phases.EndActionPhase();
            }
        }

        private EffectTarget PickRandomValidTarget(TargetType targetType, GameState state)
        {
            List<EffectTarget> candidates = new List<EffectTarget>();

            for (int slot = 0; slot < Board.SlotsPerSide; slot++)
            {
                TryAddCandidate(state.Board.GetUnit(PlayerSide.PlayerA, slot), targetType, state, candidates);
                TryAddCandidate(state.Board.GetUnit(PlayerSide.PlayerB, slot), targetType, state, candidates);
            }

            EffectTarget leaderA = EffectTarget.ForLeader(PlayerSide.PlayerA);
            if (EffectTargeting.IsValidTarget(targetType, leaderA, state))
            {
                candidates.Add(leaderA);
            }

            EffectTarget leaderB = EffectTarget.ForLeader(PlayerSide.PlayerB);
            if (EffectTargeting.IsValidTarget(targetType, leaderB, state))
            {
                candidates.Add(leaderB);
            }

            if (candidates.Count == 0)
            {
                return EffectTarget.None;
            }

            return candidates[Random.Range(0, candidates.Count)];
        }

        private void TryAddCandidate(BoardUnit unit, TargetType targetType, GameState state, List<EffectTarget> candidates)
        {
            if (unit == null || state.IsExcludedAsSelfTarget(unit))
            {
                return;
            }

            EffectTarget candidate = EffectTarget.ForUnit(unit);

            if (EffectTargeting.IsValidTarget(targetType, candidate, state))
            {
                candidates.Add(candidate);
            }
        }

        public float? GetDraftRemainingSeconds(PlayerSide side)
        {
            float? deadline = side == PlayerSide.PlayerA ? playerADraftDeadline : playerBDraftDeadline;
            return deadline.HasValue ? Mathf.Max(0f, deadline.Value - Time.time) : (float?)null;
        }

        public float? GetMulliganRemainingSeconds(PlayerSide side)
        {
            float? deadline = side == PlayerSide.PlayerA ? playerAMulliganDeadline : playerBMulliganDeadline;
            return deadline.HasValue ? Mathf.Max(0f, deadline.Value - Time.time) : (float?)null;
        }

        public float? GetTurnRemainingSeconds()
        {
            return turnDeadline.HasValue ? Mathf.Max(0f, turnDeadline.Value - Time.time) : (float?)null;
        }

        public void ApplySyncedRemaining(float playerADraftRemaining, float playerBDraftRemaining, float playerAMulliganRemaining, float playerBMulliganRemaining, float turnRemaining)
        {
            playerADraftDeadline = playerADraftRemaining >= 0f ? (float?)(Time.time + playerADraftRemaining) : null;
            playerBDraftDeadline = playerBDraftRemaining >= 0f ? (float?)(Time.time + playerBDraftRemaining) : null;
            playerAMulliganDeadline = playerAMulliganRemaining >= 0f ? (float?)(Time.time + playerAMulliganRemaining) : null;
            playerBMulliganDeadline = playerBMulliganRemaining >= 0f ? (float?)(Time.time + playerBMulliganRemaining) : null;
            turnDeadline = turnRemaining >= 0f ? (float?)(Time.time + turnRemaining) : null;
        }

        public void SetLiveMulliganSelection(PlayerSide side, List<int> handIndices)
        {
            liveMulliganSelection[side] = new List<int>(handIndices);
        }

        private List<int> GetLiveMulliganSelection(PlayerSide side)
        {
            return liveMulliganSelection.TryGetValue(side, out List<int> selection) ? selection : new List<int>();
        }
    }
}