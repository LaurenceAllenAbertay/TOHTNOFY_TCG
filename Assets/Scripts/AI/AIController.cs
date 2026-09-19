using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.Serialization;
using DDD.TNFY.TCG.Cards;
using DDD.TNFY.TCG.Effects;

namespace DDD.TNFY.TCG.Core
{
    [RequireComponent(typeof(GameManager))]
    public class AIController : MonoBehaviour
    {
        [SerializeField] private bool aiControlEnabled;
        [SerializeField] private PlayerSide aiSide = PlayerSide.PlayerB;
        [FormerlySerializedAs("actionDelaySeconds")]
        [SerializeField] private float thinkTimeSeconds = 4f;
        [SerializeField] private float delayBetweenActionsSeconds = 0.2f;
        [SerializeField] private int mulliganMaxCostToKeep = 5;

        [Header("Monte Carlo Tree Search")]
        [SerializeField] private int mctsMaxIterations = 20000;
        [SerializeField] private int mctsMaxRolloutDepth = 30;
        [SerializeField] private float mctsFrameBudgetMilliseconds = 6f;

        private GameManager gameManager;
        private GameState state;
        private PhaseManager phases;
        private AIMonteCarloTurnPlanner planner;
        private Coroutine runningTurnRoutine;

        private void Awake()
        {
            gameManager = GetComponent<GameManager>();
            RebuildPlanner();
        }

        private void OnEnable()
        {
            StartCoroutine(SubscribeWhenReady());
        }

        private void OnDisable()
        {
            if (state != null)
            {
                state.PhaseChanged -= HandlePhaseChanged;
            }

            if (runningTurnRoutine != null)
            {
                StopCoroutine(runningTurnRoutine);
                runningTurnRoutine = null;
            }
        }

        private IEnumerator SubscribeWhenReady()
        {
            while (gameManager.State == null)
            {
                yield return null;
            }

            state = gameManager.State;
            phases = gameManager.Phases;
            state.PhaseChanged += HandlePhaseChanged;
        }

        public void EnableAIControl(PlayerSide side)
        {
            aiControlEnabled = true;
            aiSide = side;
            RebuildPlanner();

            Debug.Log($"[AIController] AI control enabled for {aiSide}.");
        }

        private void RebuildPlanner()
        {
            planner = new AIMonteCarloTurnPlanner(aiSide, mctsMaxIterations, mctsMaxRolloutDepth,
                mctsFrameBudgetMilliseconds, new System.Random());

            Debug.Log($"[AIController] Planner built for {aiSide}: thinks for {thinkTimeSeconds:F2}s before its first action each turn, then {delayBetweenActionsSeconds:F2}s between actions, maxIterations={mctsMaxIterations}, maxRolloutDepth={mctsMaxRolloutDepth}, frameBudget={mctsFrameBudgetMilliseconds:F2}ms.");
        }

        private void Update()
        {
            if (!aiControlEnabled || state == null || PhotonNetwork.InRoom)
            {
                return;
            }

            if (state.CurrentPhase == TurnPhase.Draft && state.GetPlayer(aiSide).PendingDraftOptions != null)
            {
                if (runningTurnRoutine == null)
                {
                    runningTurnRoutine = StartCoroutine(RunDraftPick());
                }

                return;
            }

            if (state.CurrentPhase == TurnPhase.Mulligan && !state.GetPlayer(aiSide).HasCompletedMulligan)
            {
                if (runningTurnRoutine == null)
                {
                    runningTurnRoutine = StartCoroutine(RunMulliganRoutine());
                }
            }
        }

        private IEnumerator RunDraftPick()
        {
            yield return new WaitForSeconds(thinkTimeSeconds);

            Player aiPlayer = state.GetPlayer(aiSide);

            if (state.CurrentPhase == TurnPhase.Draft && aiPlayer.PendingDraftOptions != null)
            {
                CardData chosen = ChooseBestCardChoiceOption(aiPlayer.PendingDraftOptions);
                bool resolved = phases.TryResolvePendingDraftChoice(aiSide, chosen);
                Debug.Log($"[AIController] Draft pick resolved={resolved} for {chosen?.CardName} (stage={aiPlayer.CurrentDraftStage}).");
            }

            runningTurnRoutine = null;
        }

        private IEnumerator RunMulliganRoutine()
        {
            yield return new WaitForSeconds(thinkTimeSeconds);

            if (state.CurrentPhase == TurnPhase.Mulligan && !state.GetPlayer(aiSide).HasCompletedMulligan)
            {
                RunMulligan();
            }

            runningTurnRoutine = null;
        }

        private void HandlePhaseChanged(TurnPhase newPhase)
        {
            if (!aiControlEnabled || PhotonNetwork.InRoom)
            {
                return;
            }

            if (state.ActivePlayer != aiSide)
            {
                return;
            }

            if (runningTurnRoutine != null)
            {
                StopCoroutine(runningTurnRoutine);
            }

            runningTurnRoutine = StartCoroutine(HandlePhaseRoutine(newPhase));
        }

        private IEnumerator HandlePhaseRoutine(TurnPhase phase)
        {
            switch (phase)
            {
                case TurnPhase.Action:
                    yield return RunActionPhase();
                    break;
            }

            runningTurnRoutine = null;
        }

        private void RunMulligan()
        {
            Player player = state.GetPlayer(aiSide);
            List<CardData> toMulligan = new List<CardData>();

            foreach (CardData card in player.Hand)
            {
                if (card.ManaCost > mulliganMaxCostToKeep)
                {
                    toMulligan.Add(card);
                }
            }

            Debug.Log($"[AIController] Mulligan: keeping {player.Hand.Count - toMulligan.Count}, mulliganing {toMulligan.Count}.");

            phases.ResolveMulliganAndAdvance(aiSide, toMulligan);
        }

        private CardData ChooseBestCardChoiceOption(List<CardData> options)
        {
            CardData best = options[0];
            int bestCost = best.ManaCost;

            foreach (CardData option in options)
            {
                if (option.ManaCost > bestCost)
                {
                    best = option;
                    bestCost = option.ManaCost;
                }
            }

            return best;
        }

        private AITurnAction RemapTargetToLiveBoard(AITurnAction action)
        {
            EffectTarget plannedTarget = action.Target;

            if (plannedTarget.Kind != EffectTargetKind.Unit || plannedTarget.Unit == null)
            {
                return action;
            }

            BoardUnit plannedUnit = plannedTarget.Unit;
            BoardUnit liveUnit = state.Board.GetUnit(plannedUnit.Owner, plannedUnit.SlotIndex);
            bool wasSimulationCopy = liveUnit != plannedUnit;

            Debug.Log($"[AIController] Remapping {action.Kind} target: planned {plannedUnit.SourceCard.CardName} ({plannedUnit.Owner} slot {plannedUnit.SlotIndex}), wasSimulationCopy={wasSimulationCopy} -> live unit {(liveUnit != null ? liveUnit.SourceCard.CardName : "NONE")}.");

            if (liveUnit == null)
            {
                Debug.LogWarning($"[AIController] No live unit at {plannedUnit.Owner} slot {plannedUnit.SlotIndex} to match the planned target - leaving the action as-is so PhaseManager rejects it.");
                return action;
            }

            if (liveUnit.SourceCard.CardId != plannedUnit.SourceCard.CardId)
            {
                Debug.LogWarning($"[AIController] Live unit at {plannedUnit.Owner} slot {plannedUnit.SlotIndex} is {liveUnit.SourceCard.CardName}, but the AI planned against {plannedUnit.SourceCard.CardName} - targeting the live unit anyway.");
            }

            EffectTarget liveTarget = EffectTarget.ForUnit(liveUnit);

            switch (action.Kind)
            {
                case AITurnActionKind.PlayItem:
                    return AITurnAction.PlayItemAt(action.ItemCard, liveTarget);

                case AITurnActionKind.ResolveTargetedEffect:
                    return AITurnAction.ResolveTargetedEffectWith(liveTarget);

                default:
                    Debug.LogWarning($"[AIController] {action.Kind} carries a unit target but has no remap case - applying unchanged.");
                    return action;
            }
        }

        private IEnumerator RunActionPhase()
        {
            const int maxSafetyIterations = 60;
            int safetyIterations = 0;
            bool isFirstActionThisTurn = true;

            while (state.CurrentPhase == TurnPhase.Action && state.ActivePlayer == aiSide && !state.IsGameOver)
            {
                safetyIterations++;

                if (safetyIterations > maxSafetyIterations)
                {
                    Debug.LogWarning($"[AIController] RunActionPhase hit the safety cap of {maxSafetyIterations} iterations - forcing EndActionPhase to avoid a stuck turn.");
                    phases.EndActionPhase();
                    break;
                }

                float pauseSeconds = Mathf.Max(0f, isFirstActionThisTurn ? thinkTimeSeconds : delayBetweenActionsSeconds);
                float thinkStarted = Time.realtimeSinceStartup;

                AITurnAction? bestAction = null;
                yield return planner.FindBestActionCoroutine(state, pauseSeconds, result => bestAction = result);

                isFirstActionThisTurn = false;

                float thinkSeconds = Time.realtimeSinceStartup - thinkStarted;
                float remainingPause = pauseSeconds - thinkSeconds;

                Debug.Log($"[AIController] Thought for {thinkSeconds:F2}s of the {pauseSeconds:F2}s {(safetyIterations == 1 ? "turn-start think time" : "between-actions delay")} - waiting the remaining {Mathf.Max(0f, remainingPause):F2}s before acting.");

                if (remainingPause > 0f)
                {
                    yield return new WaitForSecondsRealtime(remainingPause);
                }

                if (state.CurrentPhase != TurnPhase.Action || state.ActivePlayer != aiSide || state.IsGameOver)
                {
                    Debug.LogWarning("[AIController] The live state left the AI's action phase while it was thinking - discarding the planned action.");
                    break;
                }

                if (bestAction == null)
                {
                    Debug.LogWarning("[AIController] MCTS planner returned no legal action at all - forcing EndActionPhase to avoid a stuck turn.");
                    phases.EndActionPhase();
                    break;
                }

                if (bestAction.Value.Kind == AITurnActionKind.EndPhase)
                {
                    Debug.Log("[AIController] MCTS planner chose to end the action phase.");
                    phases.EndActionPhase();
                    break;
                }

                Debug.Log($"[AIController] MCTS planner chose {bestAction.Value}.");

                bestAction = RemapTargetToLiveBoard(bestAction.Value);

                bool applied;

                if (bestAction.Value.Kind == AITurnActionKind.Attack)
                {
                    bool attackResolved = false;
                    applied = phases.TryAttackWithUnit(bestAction.Value.SlotIndex, () => attackResolved = true);

                    if (applied)
                    {
                        yield return new WaitUntil(() => attackResolved);
                    }
                }
                else if (bestAction.Value.Kind == AITurnActionKind.PlayUnit)
                {
                    bool playResolved = false;
                    applied = phases.TryPlayUnitAnimated(bestAction.Value.UnitCard, bestAction.Value.SlotIndex, () => playResolved = true);

                    if (applied)
                    {
                        yield return new WaitUntil(() => playResolved);
                    }
                }
                else if (bestAction.Value.Kind == AITurnActionKind.PlayItem)
                {
                    bool playResolved = false;
                    applied = phases.TryPlayItemAnimated(bestAction.Value.ItemCard, bestAction.Value.Target, () => playResolved = true);

                    if (applied)
                    {
                        yield return new WaitUntil(() => playResolved);
                    }
                }
                else
                {
                    applied = AITurnActionApplier.Apply(bestAction.Value, phases);
                }

                if (!applied)
                {
                    Debug.LogWarning($"[AIController] MCTS chose {bestAction.Value} but PhaseManager rejected it against the live state - stopping to avoid a stuck turn.");
                    break;
                }
            }
        }
    }
}