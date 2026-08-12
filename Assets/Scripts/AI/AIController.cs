using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using DDD.TNFY.TCG.Cards;

namespace DDD.TNFY.TCG.Core
{
    [RequireComponent(typeof(GameManager))]
    public class AIController : MonoBehaviour
    {
        [SerializeField] private bool aiControlEnabled;
        [SerializeField] private PlayerSide aiSide = PlayerSide.PlayerB;
        [SerializeField] private float actionDelaySeconds = 3f;
        [SerializeField] private int mulliganMaxCostToKeep = 5;

        [Header("Monte Carlo Tree Search")]
        [SerializeField] private int mctsMaxIterations = 400;
        [SerializeField] private float mctsMaxSearchSeconds = 0.35f;
        [SerializeField] private int mctsMaxRolloutDepth = 12;
        [SerializeField] private int mctsIterationsPerFrame = 25;

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
            planner = new AIMonteCarloTurnPlanner(aiSide, mctsMaxIterations, mctsMaxSearchSeconds, mctsMaxRolloutDepth,
                mctsIterationsPerFrame, new System.Random());
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
            yield return new WaitForSeconds(actionDelaySeconds);

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
            yield return new WaitForSeconds(actionDelaySeconds);

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
            yield return new WaitForSeconds(actionDelaySeconds);

            Debug.Log($"[AIController] Acting in phase {phase} for {aiSide}.");

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

        private IEnumerator RunActionPhase()
        {
            const int maxSafetyIterations = 60;
            int safetyIterations = 0;

            while (state.CurrentPhase == TurnPhase.Action && state.ActivePlayer == aiSide && !state.IsGameOver)
            {
                safetyIterations++;

                if (safetyIterations > maxSafetyIterations)
                {
                    Debug.LogWarning($"[AIController] RunActionPhase hit the safety cap of {maxSafetyIterations} iterations - forcing EndActionPhase to avoid a stuck turn.");
                    phases.EndActionPhase();
                    break;
                }

                AITurnAction? bestAction = null;
                yield return planner.FindBestActionCoroutine(state, result => bestAction = result);

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

                yield return new WaitForSeconds(actionDelaySeconds);
            }
        }
    }
}