using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DDD.TNFY.TCG.Core
{
    public class AIMonteCarloTurnPlanner
    {
        private const float ExplorationConstant = 1.41421356f;

        private readonly PlayerSide aiSide;
        private readonly int maxIterations;
        private readonly float maxSearchSeconds;
        private readonly int maxRolloutDepth;
        private readonly int iterationsPerFrame;
        private readonly System.Random rng;

        private sealed class MCTSNode
        {
            public AITurnAction IncomingAction;
            public MCTSNode Parent;
            public readonly List<MCTSNode> Children = new List<MCTSNode>();
            public List<AITurnAction> UntriedActions;
            public int VisitCount;
            public float TotalValue;
            public bool IsTerminal;

            public float AverageValue => VisitCount == 0 ? 0f : TotalValue / VisitCount;
        }

        public AIMonteCarloTurnPlanner(PlayerSide aiSide, int maxIterations, float maxSearchSeconds, int maxRolloutDepth,
            int iterationsPerFrame, System.Random rng)
        {
            this.aiSide = aiSide;
            this.maxIterations = maxIterations;
            this.maxSearchSeconds = maxSearchSeconds;
            this.maxRolloutDepth = maxRolloutDepth;
            this.iterationsPerFrame = iterationsPerFrame;
            this.rng = rng;
        }

        public IEnumerator FindBestActionCoroutine(GameState rootState, System.Action<AITurnAction?> onComplete)
        {
            GameState probeState = rootState.Clone();
            PhaseManager probePhases = new PhaseManager(probeState, null);
            MCTSNode root = new MCTSNode { UntriedActions = OrderByPromise(probeState, AITurnActionEnumerator.EnumerateLegalActions(probeState, probePhases, aiSide)) };

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

            float deadline = Time.realtimeSinceStartup + maxSearchSeconds;
            int iterations = 0;
            int iterationsThisFrame = 0;

            while (iterations < maxIterations && Time.realtimeSinceStartup < deadline)
            {
                iterations++;
                iterationsThisFrame++;

                RunIteration(root, rootState);

                if (iterationsThisFrame >= iterationsPerFrame)
                {
                    iterationsThisFrame = 0;
                    yield return null;
                }
            }

            MCTSNode best = null;

            foreach (MCTSNode child in root.Children)
            {
                if (best == null || child.VisitCount > best.VisitCount)
                {
                    best = child;
                }
            }

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

            Debug.Log($"[AIMonteCarloTurnPlanner] Ran {iterations} iteration(s) for {aiSide}. Chose {best.IncomingAction} (visits={best.VisitCount}, avgValue={best.AverageValue:F1}). All {root.Children.Count} explored option(s):{optionsLog}");

            onComplete?.Invoke(best.IncomingAction);
        }

        private void RunIteration(MCTSNode root, GameState rootState)
        {
            GameState workingState = rootState.Clone();
            PhaseManager workingPhases = new PhaseManager(workingState, null);

            MCTSNode leaf = SelectAndExpand(root, workingState, workingPhases);
            float value = Rollout(workingState, workingPhases);

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
                    node.UntriedActions = OrderByPromise(workingState, AITurnActionEnumerator.EnumerateLegalActions(workingState, workingPhases, aiSide));

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

                    AITurnActionApplier.Apply(action, workingPhases);

                    MCTSNode child = new MCTSNode { Parent = node, IncomingAction = action };
                    node.Children.Add(child);
                    return child;
                }

                MCTSNode selected = SelectChildByUCB(node);
                AITurnActionApplier.Apply(selected.IncomingAction, workingPhases);
                node = selected;
            }
        }

        private MCTSNode SelectChildByUCB(MCTSNode node)
        {
            MCTSNode best = null;
            float bestScore = float.NegativeInfinity;
            float logParentVisits = Mathf.Log(Mathf.Max(1, node.VisitCount));

            foreach (MCTSNode child in node.Children)
            {
                float exploit = child.AverageValue;
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

        private float Rollout(GameState workingState, PhaseManager workingPhases)
        {
            for (int depth = 0; depth < maxRolloutDepth; depth++)
            {
                List<AITurnAction> legalActions = AITurnActionEnumerator.EnumerateLegalActions(workingState, workingPhases, aiSide);

                if (legalActions.Count == 0)
                {
                    break;
                }

                AITurnAction chosen = ChooseRolloutAction(workingState, legalActions);
                AITurnActionApplier.Apply(chosen, workingPhases);

                if (chosen.Kind == AITurnActionKind.EndPhase || workingState.IsGameOver)
                {
                    break;
                }
            }

            return AIHeuristics.EvaluateState(workingState, aiSide);
        }

        private AITurnAction ChooseRolloutAction(GameState state, List<AITurnAction> legalActions)
        {
            if (legalActions.Count == 1)
            {
                return legalActions[0];
            }

            List<AITurnAction> ranked = OrderByPromise(state, legalActions);

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

        private List<AITurnAction> OrderByPromise(GameState state, List<AITurnAction> actions)
        {
            actions.Sort((a, b) => AIHeuristics.ScoreAction(state, aiSide, b).CompareTo(AIHeuristics.ScoreAction(state, aiSide, a)));
            return actions;
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