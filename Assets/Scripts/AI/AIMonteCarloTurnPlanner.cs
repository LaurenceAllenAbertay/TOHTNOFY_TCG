using System.Collections;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace DDD.TNFY.TCG.Core
{
    public class AIMonteCarloTurnPlanner
    {
        private const float ExplorationConstant = 1.41421356f;
        private const int SearchHorizonTurnEnds = 2;
        private const int MaxPrincipalVariationSteps = 20;

        private static readonly ProfilerMarker IterationMarker = new ProfilerMarker("AI.MCTS.Iteration");
        private static readonly ProfilerMarker CloneMarker = new ProfilerMarker("AI.MCTS.CloneState");
        private static readonly ProfilerMarker SelectMarker = new ProfilerMarker("AI.MCTS.SelectAndExpand");
        private static readonly ProfilerMarker RolloutMarker = new ProfilerMarker("AI.MCTS.Rollout");

        private readonly PlayerSide aiSide;
        private readonly int maxIterations;
        private readonly int maxRolloutDepth;
        private readonly float frameBudgetMilliseconds;
        private readonly System.Random rng;

        private int iterationsReachingOpponentTurn;
        private int iterationsReachingHorizon;
        private int iterationsCutByRolloutDepth;

        private sealed class MCTSNode
        {
            public AITurnAction IncomingAction;
            public MCTSNode Parent;
            public readonly List<MCTSNode> Children = new List<MCTSNode>();
            public List<AITurnAction> UntriedActions;
            public PlayerSide SideToMove;
            public int TurnEndsFromRoot;
            public int VisitCount;
            public float TotalValue;
            public bool IsTerminal;

            public float AverageValue => VisitCount == 0 ? 0f : TotalValue / VisitCount;
        }

        public AIMonteCarloTurnPlanner(PlayerSide aiSide, int maxIterations, int maxRolloutDepth,
            float frameBudgetMilliseconds, System.Random rng)
        {
            this.aiSide = aiSide;
            this.maxIterations = maxIterations;
            this.maxRolloutDepth = maxRolloutDepth;
            this.frameBudgetMilliseconds = frameBudgetMilliseconds;
            this.rng = rng;
        }

        public IEnumerator FindBestActionCoroutine(GameState rootState, float maxSearchSeconds, System.Action<AITurnAction?> onComplete)
        {
            iterationsReachingOpponentTurn = 0;
            iterationsReachingHorizon = 0;
            iterationsCutByRolloutDepth = 0;

            GameState probeState = rootState.Clone();
            PhaseManager probePhases = new PhaseManager(probeState, null);

            MCTSNode root = new MCTSNode
            {
                SideToMove = probeState.ActivePlayer,
                TurnEndsFromRoot = 0,
                UntriedActions = EnumerateForSideToMove(probeState, probePhases)
            };

            Player rootAi = probeState.GetPlayer(aiSide);
            Debug.Log($"[AIMonteCarloTurnPlanner] Search start for {aiSide} (search budget {maxSearchSeconds:F2}s): phase={probeState.CurrentPhase}, active={probeState.ActivePlayer}, mana={rootAi.CurrentMana}/{rootAi.MaxManaThisGame}, hand=[{string.Join(", ", rootAi.Hand.ConvertAll(card => $"{card.CardName}({card.ManaCost})"))}], pendingTargetedEffect={(probeState.PendingTargetedEffect != null ? probeState.PendingTargetedEffect.action.ToString() : "none")}, pendingCardChoice={(probeState.PendingCardChoiceOptions != null)}, {root.UntriedActions.Count} legal root action(s): [{string.Join(", ", root.UntriedActions)}]");

            if (root.SideToMove != aiSide)
            {
                Debug.LogWarning($"[AIMonteCarloTurnPlanner] Search started while {root.SideToMove} is active, not the AI ({aiSide}) - the root would be planning the opponent's move.");
            }

            if (root.UntriedActions.Count == 0)
            {
                Debug.LogWarning($"[AIMonteCarloTurnPlanner] No legal actions at all for {aiSide} - nothing to plan.");
                onComplete?.Invoke(null);
                yield break;
            }

            if (root.UntriedActions.Count == 1)
            {
                Debug.Log($"[AIMonteCarloTurnPlanner] Only one legal action ({root.UntriedActions[0]}) - skipping search.");
                onComplete?.Invoke(root.UntriedActions[0]);
                yield break;
            }

            float searchStarted = Time.realtimeSinceStartup;
            float deadline = searchStarted + maxSearchSeconds;
            int iterations = 0;
            int framesUsed = 0;
            double slowestFrameMilliseconds = 0d;
            System.Diagnostics.Stopwatch frameTimer = new System.Diagnostics.Stopwatch();

            while (iterations < maxIterations && Time.realtimeSinceStartup < deadline)
            {
                framesUsed++;
                frameTimer.Restart();

                LogType previousFilter = Debug.unityLogger.filterLogType;
                Debug.unityLogger.filterLogType = LogType.Error;

                try
                {
                    do
                    {
                        iterations++;

                        using (IterationMarker.Auto())
                        {
                            RunIteration(root, rootState);
                        }
                    }
                    while (iterations < maxIterations && frameTimer.Elapsed.TotalMilliseconds < frameBudgetMilliseconds);
                }
                finally
                {
                    Debug.unityLogger.filterLogType = previousFilter;
                }

                slowestFrameMilliseconds = System.Math.Max(slowestFrameMilliseconds, frameTimer.Elapsed.TotalMilliseconds);

                yield return null;
            }

            float searchSeconds = Time.realtimeSinceStartup - searchStarted;
            string stopReason = iterations >= maxIterations ? $"hit the {maxIterations}-iteration cap" : $"hit the {maxSearchSeconds:F2}s time budget";

            Debug.Log($"[AIMonteCarloTurnPlanner] Frame cost for {aiSide}: {iterations} iteration(s) over {framesUsed} frame(s), avg {(framesUsed > 0 ? iterations / (float)framesUsed : 0f):F1} iteration(s)/frame, slowest frame spent {slowestFrameMilliseconds:F2}ms searching (budget {frameBudgetMilliseconds:F2}ms).");

            MCTSNode best = MostVisitedChild(root);

            if (best == null)
            {
                Debug.LogWarning($"[AIMonteCarloTurnPlanner] Ran {iterations} iteration(s) but never expanded a single child for {aiSide} - falling back to the highest-scoring untried action.");
                onComplete?.Invoke(root.UntriedActions[0]);
                yield break;
            }

            System.Text.StringBuilder optionsLog = new System.Text.StringBuilder();

            foreach (MCTSNode child in root.Children)
            {
                optionsLog.Append($"\n    {child.IncomingAction} -> visits={child.VisitCount}, avgValue={child.AverageValue:F1}");
            }

            Debug.Log($"[AIMonteCarloTurnPlanner] Ran {iterations} iteration(s) in {searchSeconds:F2}s for {aiSide} ({stopReason}). Chose {best.IncomingAction} (visits={best.VisitCount}, avgValue={best.AverageValue:F1}). All {root.Children.Count} explored option(s):{optionsLog}");

            Debug.Log($"[AIMonteCarloTurnPlanner] Lookahead for {aiSide}: {iterationsReachingOpponentTurn}/{iterations} iteration(s) reached the opponent's reply turn, {iterationsReachingHorizon}/{iterations} reached the full horizon (start of the AI's next turn), {iterationsCutByRolloutDepth}/{iterations} were cut short by maxRolloutDepth={maxRolloutDepth}.");

            Debug.Log($"[AIMonteCarloTurnPlanner] Expected line (most-visited path) for {aiSide}:{DescribePrincipalVariation(root)}");

            onComplete?.Invoke(best.IncomingAction);
        }

        private void RunIteration(MCTSNode root, GameState rootState)
        {
            GameState workingState;
            PhaseManager workingPhases;

            using (CloneMarker.Auto())
            {
                workingState = rootState.Clone();
                workingPhases = new PhaseManager(workingState, null);
            }

            MCTSNode leaf;

            using (SelectMarker.Auto())
            {
                leaf = SelectAndExpand(root, workingState, workingPhases);
            }

            float value;
            int finalTurnEnds;
            bool cutByDepth;

            using (RolloutMarker.Auto())
            {
                value = Rollout(workingState, workingPhases, leaf.TurnEndsFromRoot, out finalTurnEnds, out cutByDepth);
            }

            if (finalTurnEnds >= 1)
            {
                iterationsReachingOpponentTurn++;
            }

            if (finalTurnEnds >= SearchHorizonTurnEnds)
            {
                iterationsReachingHorizon++;
            }

            if (cutByDepth)
            {
                iterationsCutByRolloutDepth++;
            }

            Backpropagate(leaf, value);
        }

        private MCTSNode SelectAndExpand(MCTSNode node, GameState workingState, PhaseManager workingPhases)
        {
            while (true)
            {
                if (node.IsTerminal)
                {
                    return node;
                }

                if (node.UntriedActions == null)
                {
                    if (node.TurnEndsFromRoot >= SearchHorizonTurnEnds || workingState.IsGameOver)
                    {
                        node.IsTerminal = true;
                        return node;
                    }

                    node.SideToMove = workingState.ActivePlayer;
                    node.UntriedActions = EnumerateForSideToMove(workingState, workingPhases);

                    if (node.UntriedActions.Count == 0)
                    {
                        node.IsTerminal = true;
                        return node;
                    }
                }

                if (node.UntriedActions.Count > 0)
                {
                    AITurnAction action = node.UntriedActions[0];
                    node.UntriedActions.RemoveAt(0);

                    ApplyAction(action, workingState, workingPhases);

                    MCTSNode child = new MCTSNode
                    {
                        Parent = node,
                        IncomingAction = action,
                        TurnEndsFromRoot = node.TurnEndsFromRoot + (action.Kind == AITurnActionKind.EndPhase ? 1 : 0)
                    };

                    node.Children.Add(child);
                    return child;
                }

                MCTSNode selected = SelectChildByUCB(node);
                ApplyAction(selected.IncomingAction, workingState, workingPhases);
                node = selected;
            }
        }

        private MCTSNode SelectChildByUCB(MCTSNode node)
        {
            MCTSNode best = null;
            float bestScore = float.NegativeInfinity;
            float logParentVisits = Mathf.Log(Mathf.Max(1, node.VisitCount));
            float perspective = node.SideToMove == aiSide ? 1f : -1f;

            foreach (MCTSNode child in node.Children)
            {
                float exploit = perspective * child.AverageValue;
                float explore = ExplorationConstant * Mathf.Sqrt(logParentVisits / Mathf.Max(1, child.VisitCount));
                float ucb = exploit + explore;

                if (ucb > bestScore)
                {
                    bestScore = ucb;
                    best = child;
                }
            }

            return best;
        }

        private float Rollout(GameState workingState, PhaseManager workingPhases, int turnEnds, out int finalTurnEnds, out bool cutByDepth)
        {
            cutByDepth = false;

            for (int depth = 0; ; depth++)
            {
                if (turnEnds >= SearchHorizonTurnEnds || workingState.IsGameOver)
                {
                    break;
                }

                if (depth >= maxRolloutDepth)
                {
                    cutByDepth = true;
                    break;
                }

                List<AITurnAction> legalActions = EnumerateForSideToMove(workingState, workingPhases);

                if (legalActions.Count == 0)
                {
                    break;
                }

                AITurnAction chosen = ChooseRolloutAction(legalActions);
                ApplyAction(chosen, workingState, workingPhases);

                if (chosen.Kind == AITurnActionKind.EndPhase)
                {
                    turnEnds++;
                }
            }

            finalTurnEnds = turnEnds;
            return AIHeuristics.EvaluateState(workingState, aiSide);
        }

        private List<AITurnAction> EnumerateForSideToMove(GameState state, PhaseManager phases)
        {
            PlayerSide sideToMove = state.ActivePlayer;

            List<AITurnAction> actions = sideToMove == aiSide
                ? AITurnActionEnumerator.EnumerateLegalActions(state, phases, aiSide)
                : AIOpponentReplyModel.EnumerateActions(state, phases, sideToMove);

            RemoveEndPhaseWhileAttacksRemain(actions);

            return OrderByPromise(state, sideToMove, actions);
        }

        private static void RemoveEndPhaseWhileAttacksRemain(List<AITurnAction> actions)
        {
            bool hasAttack = actions.Exists(action => action.Kind == AITurnActionKind.Attack);

            if (hasAttack)
            {
                actions.RemoveAll(action => action.Kind == AITurnActionKind.EndPhase);
            }
        }

        private static void ApplyAction(AITurnAction action, GameState state, PhaseManager phases)
        {
            if (action.Kind == AITurnActionKind.PlaceAbstractUnit)
            {
                AIOpponentReplyModel.TryPlaceAbstractUnit(action, state, phases);
                return;
            }

            AITurnActionApplier.Apply(action, phases);
        }

        private AITurnAction ChooseRolloutAction(List<AITurnAction> ranked)
        {
            if (ranked.Count == 1)
            {
                return ranked[0];
            }

            float totalWeight = 0f;
            float[] weights = new float[ranked.Count];

            for (int i = 0; i < ranked.Count; i++)
            {
                weights[i] = 1f / (i + 1);
                totalWeight += weights[i];
            }

            float roll = (float)(rng.NextDouble() * totalWeight);
            float cumulative = 0f;

            for (int i = 0; i < ranked.Count; i++)
            {
                cumulative += weights[i];

                if (roll <= cumulative)
                {
                    return ranked[i];
                }
            }

            return ranked[ranked.Count - 1];
        }

        private static List<AITurnAction> OrderByPromise(GameState state, PlayerSide scoringSide, List<AITurnAction> actions)
        {
            actions.Sort((a, b) => AIHeuristics.ScoreAction(state, scoringSide, b).CompareTo(AIHeuristics.ScoreAction(state, scoringSide, a)));
            return actions;
        }

        private static MCTSNode MostVisitedChild(MCTSNode node)
        {
            MCTSNode best = null;

            foreach (MCTSNode child in node.Children)
            {
                if (best == null || child.VisitCount > best.VisitCount)
                {
                    best = child;
                }
            }

            return best;
        }

        private static string DescribePrincipalVariation(MCTSNode root)
        {
            System.Text.StringBuilder line = new System.Text.StringBuilder();
            MCTSNode node = root;

            for (int step = 0; step < MaxPrincipalVariationSteps; step++)
            {
                MCTSNode next = MostVisitedChild(node);

                if (next == null)
                {
                    break;
                }

                line.Append($"\n    [{node.SideToMove}] {next.IncomingAction} (visits={next.VisitCount}, avgValue={next.AverageValue:F1})");
                node = next;
            }

            return line.Length == 0 ? " (no expanded moves)" : line.ToString();
        }

        private static void Backpropagate(MCTSNode node, float value)
        {
            while (node != null)
            {
                node.VisitCount++;
                node.TotalValue += value;
                node = node.Parent;
            }
        }
    }
}