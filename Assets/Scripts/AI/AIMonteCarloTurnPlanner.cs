using System.Collections;
using System.Collections.Generic;
using DDD.TNFY.TCG.Effects;
using Unity.Profiling;
using UnityEngine;

namespace DDD.TNFY.TCG.Core
{
    public class AIMonteCarloTurnPlanner
    {
        private const float HeuristicValueCeiling = 0.9f;
        private const int SearchHorizonTurnEnds = 2;
        private const int MaxPrincipalVariationSteps = 20;
        private const int MaxReplayFailureSamples = 5;

        private static readonly ProfilerMarker IterationMarker = new ProfilerMarker("AI.MCTS.Iteration");
        private static readonly ProfilerMarker CloneMarker = new ProfilerMarker("AI.MCTS.CloneState");
        private static readonly ProfilerMarker SelectMarker = new ProfilerMarker("AI.MCTS.SelectAndExpand");
        private static readonly ProfilerMarker RolloutMarker = new ProfilerMarker("AI.MCTS.Rollout");

        private readonly PlayerSide aiSide;
        private readonly int maxIterations;
        private readonly int maxRolloutDepth;
        private readonly float frameBudgetMilliseconds;
        private readonly float explorationConstant;
        private readonly float valueScale;
        private readonly System.Random rng;

        private int iterationsReachingOpponentTurn;
        private int iterationsReachingHorizon;
        private int iterationsCutByRolloutDepth;

        private int treeReplays;
        private int treeReplayFailures;
        private int treeReplayFailuresWithUnitTarget;
        private int unitTargetsRemapped;
        private int unitTargetsRemappedToDifferentCard;
        private int rolloutActions;
        private int rolloutActionFailures;
        private readonly List<string> replayFailureSamples = new List<string>();

        private MCTSNode retainedRoot;
        private MCTSNode lastChosenChild;

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
            public float TotalNormalizedValue;
            public bool IsTerminal;
            public bool IsSolved;
            public int ProvenWinDistance = -1;

            public float AverageValue => VisitCount == 0 ? 0f : TotalValue / VisitCount;
            public float NormalizedAverage => VisitCount == 0 ? 0f : TotalNormalizedValue / VisitCount;
        }

        public AIMonteCarloTurnPlanner(PlayerSide aiSide, int maxIterations, int maxRolloutDepth,
            float frameBudgetMilliseconds, float explorationConstant, float valueScale, System.Random rng)
        {
            this.aiSide = aiSide;
            this.maxIterations = maxIterations;
            this.maxRolloutDepth = maxRolloutDepth;
            this.frameBudgetMilliseconds = frameBudgetMilliseconds;
            this.explorationConstant = Mathf.Max(0f, explorationConstant);
            this.valueScale = Mathf.Max(1f, valueScale);
            this.rng = rng;
        }

        public void AdvanceTreeToLastChosenAction()
        {
            retainedRoot = lastChosenChild;
            lastChosenChild = null;

            if (retainedRoot != null)
            {
                retainedRoot.Parent = null;
            }

            Debug.Log($"[AIMonteCarloTurnPlanner] Keeping the subtree under the action {aiSide} just played: {(retainedRoot != null ? $"{retainedRoot.IncomingAction} with {retainedRoot.VisitCount} visit(s) and {retainedRoot.Children.Count} explored follow-up(s)" : "nothing to keep (no search ran for that action)")}.");
        }

        public void DiscardTree()
        {
            if (retainedRoot != null || lastChosenChild != null)
            {
                Debug.Log($"[AIMonteCarloTurnPlanner] Discarding the kept search tree for {aiSide} (retained={(retainedRoot != null ? $"{retainedRoot.VisitCount} visit(s)" : "none")}, lastChosen={(lastChosenChild != null ? $"{lastChosenChild.VisitCount} visit(s)" : "none")}).");
            }

            retainedRoot = null;
            lastChosenChild = null;
        }

        public IEnumerator FindBestActionCoroutine(GameState rootState, float maxSearchSeconds, System.Action<AITurnAction?> onComplete)
        {
            iterationsReachingOpponentTurn = 0;
            iterationsReachingHorizon = 0;
            iterationsCutByRolloutDepth = 0;
            treeReplays = 0;
            treeReplayFailures = 0;
            treeReplayFailuresWithUnitTarget = 0;
            unitTargetsRemapped = 0;
            unitTargetsRemappedToDifferentCard = 0;
            replayFailureSamples.Clear();
            rolloutActions = 0;
            rolloutActionFailures = 0;

            GameState probeState = rootState.Clone();
            PhaseManager probePhases = new PhaseManager(probeState, null);
            List<AITurnAction> liveActions = EnumerateForSideToMove(probeState, probePhases);

            MCTSNode root = TryReuseRetainedRoot(probeState, liveActions);
            retainedRoot = null;
            lastChosenChild = null;

            if (root == null)
            {
                root = new MCTSNode
                {
                    SideToMove = probeState.ActivePlayer,
                    TurnEndsFromRoot = 0,
                    UntriedActions = liveActions
                };
            }

            int inheritedVisits = root.VisitCount;

            Player rootAi = probeState.GetPlayer(aiSide);
            Debug.Log($"[AIMonteCarloTurnPlanner] Search start for {aiSide} (search budget {maxSearchSeconds:F2}s, {(inheritedVisits > 0 ? $"reused tree with {inheritedVisits} inherited visit(s)" : "fresh tree")}): phase={probeState.CurrentPhase}, active={probeState.ActivePlayer}, mana={rootAi.CurrentMana}/{rootAi.MaxManaThisGame}, hand=[{string.Join(", ", rootAi.Hand.ConvertAll(card => $"{card.CardName}({card.ManaCost})"))}], pendingTargetedEffect={(probeState.PendingTargetedEffect != null ? probeState.PendingTargetedEffect.action.ToString() : "none")}, pendingCardChoice={(probeState.PendingCardChoiceOptions != null)}, {liveActions.Count} legal root action(s): [{string.Join(", ", liveActions)}]");

            if (root.SideToMove != aiSide)
            {
                Debug.LogWarning($"[AIMonteCarloTurnPlanner] Search started while {root.SideToMove} is active, not the AI ({aiSide}) - the root would be planning the opponent's move.");
            }

            if (liveActions.Count == 0)
            {
                Debug.LogWarning($"[AIMonteCarloTurnPlanner] No legal actions at all for {aiSide} - nothing to plan.");
                onComplete?.Invoke(null);
                yield break;
            }

            if (liveActions.Count == 1)
            {
                lastChosenChild = root.Children.Count == 1 ? root.Children[0] : null;
                Debug.Log($"[AIMonteCarloTurnPlanner] Only one legal action ({liveActions[0]}) - skipping search (keeping {(lastChosenChild != null ? $"{lastChosenChild.VisitCount} visit(s)" : "no")} subtree for it).");
                onComplete?.Invoke(liveActions[0]);
                yield break;
            }

            float searchStarted = Time.realtimeSinceStartup;
            float deadline = searchStarted + maxSearchSeconds;
            int iterations = 0;
            int framesUsed = 0;
            double slowestFrameMilliseconds = 0d;
            System.Diagnostics.Stopwatch frameTimer = new System.Diagnostics.Stopwatch();

            while (iterations < maxIterations && Time.realtimeSinceStartup < deadline && !root.IsSolved && root.ProvenWinDistance < 0)
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
                    while (iterations < maxIterations && frameTimer.Elapsed.TotalMilliseconds < frameBudgetMilliseconds && !root.IsSolved && root.ProvenWinDistance < 0);
                }
                finally
                {
                    Debug.unityLogger.filterLogType = previousFilter;
                }

                slowestFrameMilliseconds = System.Math.Max(slowestFrameMilliseconds, frameTimer.Elapsed.TotalMilliseconds);

                yield return null;
            }

            float searchSeconds = Time.realtimeSinceStartup - searchStarted;
            bool solved = root.IsSolved;
            bool provenWin = root.ProvenWinDistance >= 0;
            string stopReason = provenWin
                ? $"PROVEN WIN found - a line wins this turn in {root.ProvenWinDistance} action(s), so it stopped early and took the quickest one"
                : solved
                    ? "tree SOLVED - every line to the horizon was explored, so it stopped early and chose by exact value"
                    : iterations >= maxIterations ? $"hit the {maxIterations}-iteration cap" : $"hit the {maxSearchSeconds:F2}s time budget";

            Debug.Log($"[AIMonteCarloTurnPlanner] Frame cost for {aiSide}: {iterations} iteration(s) over {framesUsed} frame(s), avg {(framesUsed > 0 ? iterations / (float)framesUsed : 0f):F1} iteration(s)/frame, slowest frame spent {slowestFrameMilliseconds:F2}ms searching (budget {frameBudgetMilliseconds:F2}ms).");

            MCTSNode best = provenWin ? QuickestProvenWinChild(root) : solved ? BestChildByExactValue(root) : MostVisitedChild(root);

            if (best == null)
            {
                Debug.LogWarning($"[AIMonteCarloTurnPlanner] Ran {iterations} iteration(s) but never expanded a single child for {aiSide} - falling back to the highest-scoring untried action.");
                onComplete?.Invoke(root.UntriedActions[0]);
                yield break;
            }

            lastChosenChild = best;

            System.Text.StringBuilder optionsLog = new System.Text.StringBuilder();
            int singleVisitOptions = 0;

            foreach (MCTSNode child in root.Children)
            {
                optionsLog.Append($"\n    {child.IncomingAction} -> visits={child.VisitCount}, avgValue={child.AverageValue:F1} (normalized {child.NormalizedAverage:F3}){(child.IsSolved ? $", solved exact value {ExactValue(child):F3}" : string.Empty)}{(child.ProvenWinDistance >= 0 ? $", PROVEN WIN in {child.ProvenWinDistance + 1} action(s)" : string.Empty)}");

                if (child.VisitCount <= 1)
                {
                    singleVisitOptions++;
                }
            }

            Debug.Log($"[AIMonteCarloTurnPlanner] Ran {iterations} iteration(s) in {searchSeconds:F2}s for {aiSide} ({stopReason}); root now has {root.VisitCount} visit(s) ({inheritedVisits} inherited + {root.VisitCount - inheritedVisits} from this search). Chose {best.IncomingAction} (visits={best.VisitCount}, avgValue={best.AverageValue:F1}, normalized {best.NormalizedAverage:F3}). Exploration: {singleVisitOptions}/{root.Children.Count} option(s) were only ever visited once (explorationConstant={explorationConstant:F2}, valueScale={valueScale:F1}). All {root.Children.Count} explored option(s):{optionsLog}");

            Debug.Log($"[AIMonteCarloTurnPlanner] Lookahead for {aiSide}: {iterationsReachingOpponentTurn}/{iterations} iteration(s) reached the opponent's reply turn, {iterationsReachingHorizon}/{iterations} reached the full horizon (start of the AI's next turn), {iterationsCutByRolloutDepth}/{iterations} were cut short by maxRolloutDepth={maxRolloutDepth}.");

            Debug.Log($"[AIMonteCarloTurnPlanner] Replay check for {aiSide}: {treeReplayFailures}/{treeReplays} stored tree action(s) were REJECTED when replayed ({treeReplayFailuresWithUnitTarget} of those had a unit target); {unitTargetsRemapped} unit target(s) were re-pointed to this iteration's board copy ({unitTargetsRemappedToDifferentCard} landed on a different card than planned); {rolloutActionFailures}/{rolloutActions} freshly enumerated rollout action(s) were rejected.");

            if (replayFailureSamples.Count > 0)
            {
                Debug.Log($"[AIMonteCarloTurnPlanner] First {replayFailureSamples.Count} rejected tree replay(s) for {aiSide}:\n    {string.Join("\n    ", replayFailureSamples)}");
            }

            Debug.Log($"[AIMonteCarloTurnPlanner] Expected line (most-visited path) for {aiSide}:{DescribePrincipalVariation(root)}");

            Debug.Log($"[AIMonteCarloTurnPlanner] Expected line for {aiSide} ends in: {DescribeExpectedLineOutcome(root, rootState)}");

            LogHealthPaymentDecision(root, best, probeState, rootState);

            LogEndTurnWithCardsInHand(root, best, probeState, rootState);

            onComplete?.Invoke(best.IncomingAction);
        }

        private MCTSNode TryReuseRetainedRoot(GameState probeState, List<AITurnAction> liveActions)
        {
            MCTSNode candidate = retainedRoot;

            if (candidate == null)
            {
                return null;
            }

            if (probeState.ActivePlayer != aiSide || candidate.IsTerminal || candidate.TurnEndsFromRoot != 0)
            {
                Debug.Log($"[AIMonteCarloTurnPlanner] Not reusing the kept subtree for {aiSide}: active={probeState.ActivePlayer}, isTerminal={candidate.IsTerminal}, turnEndsFromRoot={candidate.TurnEndsFromRoot} - starting a fresh tree.");
                return null;
            }

            List<AITurnAction> untried = new List<AITurnAction>(liveActions);
            List<string> prunedDescriptions = new List<string>();
            int keptChildren = 0;
            int keptVisits = 0;

            for (int i = candidate.Children.Count - 1; i >= 0; i--)
            {
                MCTSNode child = candidate.Children[i];
                AITurnAction childActionOnProbe = child.IncomingAction.WithTargetRemappedTo(probeState);
                int matchIndex = untried.FindIndex(action => IsSameAction(action, childActionOnProbe));

                if (matchIndex < 0)
                {
                    prunedDescriptions.Add($"{child.IncomingAction} (visits={child.VisitCount})");
                    candidate.Children.RemoveAt(i);
                    continue;
                }

                child.IncomingAction = untried[matchIndex];
                untried.RemoveAt(matchIndex);
                keptChildren++;
                keptVisits += child.VisitCount;
            }

            candidate.SideToMove = probeState.ActivePlayer;
            candidate.UntriedActions = untried;
            candidate.IsSolved = AreAllChildrenSolved(candidate);
            candidate.ProvenWinDistance = -1;

            foreach (MCTSNode keptChild in candidate.Children)
            {
                if (keptChild.ProvenWinDistance >= 0 && (candidate.ProvenWinDistance < 0 || keptChild.ProvenWinDistance + 1 < candidate.ProvenWinDistance))
                {
                    candidate.ProvenWinDistance = keptChild.ProvenWinDistance + 1;
                }
            }

            Debug.Log($"[AIMonteCarloTurnPlanner] Reusing kept subtree for {aiSide}: new root has {candidate.VisitCount} visit(s); kept {keptChildren} explored follow-up(s) holding {keptVisits} visit(s); pruned {prunedDescriptions.Count} no longer legal on the real board{(prunedDescriptions.Count > 0 ? $" [{string.Join(", ", prunedDescriptions)}]" : string.Empty)}; {untried.Count} legal action(s) not yet tried.");

            return candidate;
        }

        private static bool IsSameAction(AITurnAction a, AITurnAction b)
        {
            return a.Kind == b.Kind
                && a.UnitCard == b.UnitCard
                && a.ItemCard == b.ItemCard
                && a.ChosenCard == b.ChosenCard
                && a.SlotIndex == b.SlotIndex
                && a.FromSlot == b.FromSlot
                && a.ToSlot == b.ToSlot
                && a.AbstractCategory == b.AbstractCategory
                && IsSameTarget(a.Target, b.Target);
        }

        private static bool IsSameTarget(EffectTarget a, EffectTarget b)
        {
            if (a.Kind != b.Kind)
            {
                return false;
            }

            switch (a.Kind)
            {
                case EffectTargetKind.Unit:
                    return a.Unit != null && a.Unit == b.Unit;

                case EffectTargetKind.Leader:
                    return a.LeaderSide == b.LeaderSide;

                case EffectTargetKind.Slot:
                    return a.SlotSide == b.SlotSide && a.SlotIndex == b.SlotIndex;

                default:
                    return true;
            }
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

            Backpropagate(leaf, value, NormalizeValue(value));

            if (leaf.IsSolved)
            {
                PropagateSolved(leaf);
            }

            if (leaf.ProvenWinDistance == 0)
            {
                PropagateProvenWin(leaf);
            }
        }

        private void PropagateProvenWin(MCTSNode winningNode)
        {
            MCTSNode child = winningNode;
            MCTSNode parent = child.Parent;

            while (parent != null && parent.SideToMove == aiSide)
            {
                int distance = child.ProvenWinDistance + 1;

                if (parent.ProvenWinDistance >= 0 && parent.ProvenWinDistance <= distance)
                {
                    break;
                }

                parent.ProvenWinDistance = distance;
                child = parent;
                parent = parent.Parent;
            }
        }

        private static MCTSNode QuickestProvenWinChild(MCTSNode node)
        {
            MCTSNode best = null;

            foreach (MCTSNode child in node.Children)
            {
                if (child.ProvenWinDistance < 0)
                {
                    continue;
                }

                if (best == null || child.ProvenWinDistance < best.ProvenWinDistance)
                {
                    best = child;
                }
            }

            return best;
        }

        private static void MarkTerminal(MCTSNode node)
        {
            node.IsTerminal = true;
            node.IsSolved = true;
        }

        private static void PropagateSolved(MCTSNode solvedNode)
        {
            MCTSNode parent = solvedNode.Parent;

            while (parent != null && !parent.IsSolved && AreAllChildrenSolved(parent))
            {
                parent.IsSolved = true;
                parent = parent.Parent;
            }
        }

        private static bool AreAllChildrenSolved(MCTSNode node)
        {
            if (node.UntriedActions == null || node.UntriedActions.Count > 0 || node.Children.Count == 0)
            {
                return false;
            }

            foreach (MCTSNode child in node.Children)
            {
                if (!child.IsSolved)
                {
                    return false;
                }
            }

            return true;
        }

        private float ExactValue(MCTSNode node)
        {
            if (node.Children.Count == 0)
            {
                return node.NormalizedAverage;
            }

            bool aiToMove = node.SideToMove == aiSide;
            float best = aiToMove ? float.NegativeInfinity : float.PositiveInfinity;

            foreach (MCTSNode child in node.Children)
            {
                float childValue = ExactValue(child);
                best = aiToMove ? Mathf.Max(best, childValue) : Mathf.Min(best, childValue);
            }

            return best;
        }

        private MCTSNode BestChildByExactValue(MCTSNode node)
        {
            MCTSNode best = null;
            float bestScore = float.NegativeInfinity;
            float perspective = node.SideToMove == aiSide ? 1f : -1f;

            foreach (MCTSNode child in node.Children)
            {
                float score = perspective * ExactValue(child);

                if (best == null || score > bestScore || (score == bestScore && child.VisitCount > best.VisitCount))
                {
                    best = child;
                    bestScore = score;
                }
            }

            return best;
        }

        private MCTSNode NextOnExpectedLine(MCTSNode node)
        {
            if (node.ProvenWinDistance > 0 && node.SideToMove == aiSide)
            {
                return QuickestProvenWinChild(node);
            }

            return node.IsSolved ? BestChildByExactValue(node) : MostVisitedChild(node);
        }

        private float NormalizeValue(float rawValue)
        {
            if (rawValue >= AIHeuristics.WinScore)
            {
                return 1f;
            }

            if (rawValue <= AIHeuristics.LossScore)
            {
                return -1f;
            }

            return HeuristicValueCeiling * (float)System.Math.Tanh(rawValue / valueScale);
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
                        MarkTerminal(node);
                        return node;
                    }

                    node.SideToMove = workingState.ActivePlayer;
                    node.UntriedActions = EnumerateForSideToMove(workingState, workingPhases);

                    if (node.UntriedActions.Count == 0)
                    {
                        MarkTerminal(node);
                        return node;
                    }
                }

                if (node.UntriedActions.Count > 0)
                {
                    AITurnAction action = node.UntriedActions[0];
                    node.UntriedActions.RemoveAt(0);

                    ReplayTreeAction(action, workingState, workingPhases);

                    MCTSNode child = new MCTSNode
                    {
                        Parent = node,
                        IncomingAction = action,
                        TurnEndsFromRoot = node.TurnEndsFromRoot + (action.Kind == AITurnActionKind.EndPhase ? 1 : 0)
                    };

                    if (child.TurnEndsFromRoot >= SearchHorizonTurnEnds || workingState.IsGameOver)
                    {
                        MarkTerminal(child);

                        if (workingState.IsGameOver && workingState.Winner == aiSide)
                        {
                            child.ProvenWinDistance = 0;
                        }
                    }

                    node.Children.Add(child);
                    return child;
                }

                MCTSNode selected = SelectChildByUCB(node);
                ReplayTreeAction(selected.IncomingAction, workingState, workingPhases);
                node = selected;
            }
        }

        private void ReplayTreeAction(AITurnAction storedAction, GameState workingState, PhaseManager workingPhases)
        {
            treeReplays++;

            AITurnAction action = storedAction.WithTargetRemappedTo(workingState);

            if (storedAction.TargetsUnit && action.Target.Unit != storedAction.Target.Unit)
            {
                unitTargetsRemapped++;

                if (action.Target.Unit.SourceCard != storedAction.Target.Unit.SourceCard)
                {
                    unitTargetsRemappedToDifferentCard++;
                }
            }

            if (ApplyAction(action, workingState, workingPhases))
            {
                return;
            }

            treeReplayFailures++;

            if (action.Target.Kind == EffectTargetKind.Unit)
            {
                treeReplayFailuresWithUnitTarget++;
            }

            if (replayFailureSamples.Count < MaxReplayFailureSamples)
            {
                replayFailureSamples.Add(DescribeReplayFailure(action, workingState));
            }
        }

        private static string DescribeReplayFailure(AITurnAction action, GameState workingState)
        {
            Player active = workingState.GetActivePlayerData();
            string context = $"active={workingState.ActivePlayer}, phase={workingState.CurrentPhase}, mana={active.CurrentMana}, leaderHealth={active.LeaderHealth}, pendingTargetedEffect={(workingState.PendingTargetedEffect != null)}, pendingCardChoice={(workingState.PendingCardChoiceOptions != null)}";

            if (action.Target.Kind != EffectTargetKind.Unit || action.Target.Unit == null)
            {
                return $"{action} | no unit target | {context}";
            }

            BoardUnit planned = action.Target.Unit;
            BoardUnit onBoard = workingState.Board.GetUnit(planned.Owner, planned.SlotIndex);
            bool sameCard = onBoard != null && onBoard.SourceCard == planned.SourceCard;
            bool sameObject = onBoard == planned;

            return $"{action} | planned target {planned.SourceCard.CardName} ({planned.Owner} slot {planned.SlotIndex}) | working board there: {(onBoard != null ? onBoard.SourceCard.CardName : "EMPTY")} | sameCard={sameCard}, sameObject={sameObject} | {context}";
        }

        private MCTSNode SelectChildByUCB(MCTSNode node)
        {
            MCTSNode best = null;
            float bestScore = float.NegativeInfinity;
            float logParentVisits = Mathf.Log(Mathf.Max(1, node.VisitCount));
            float perspective = node.SideToMove == aiSide ? 1f : -1f;

            foreach (MCTSNode child in node.Children)
            {
                float exploit = perspective * child.NormalizedAverage;
                float explore = explorationConstant * Mathf.Sqrt(logParentVisits / Mathf.Max(1, child.VisitCount));
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
                rolloutActions++;

                if (!ApplyAction(chosen, workingState, workingPhases))
                {
                    rolloutActionFailures++;
                }

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

        private static bool ApplyAction(AITurnAction action, GameState state, PhaseManager phases)
        {
            switch (action.Kind)
            {
                case AITurnActionKind.PlaceAbstractUnit:
                    return AIOpponentReplyModel.TryPlaceAbstractUnit(action, state, phases);

                case AITurnActionKind.AbstractRemoval:
                    return AIOpponentReplyModel.TryApplyAbstractRemoval(action, state, phases);

                default:
                    return AITurnActionApplier.Apply(action, phases);
            }
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
            if (actions.Count < 2)
            {
                return actions;
            }

            AITurnAction[] ordered = actions.ToArray();
            float[] negatedScores = new float[ordered.Length];

            for (int i = 0; i < ordered.Length; i++)
            {
                negatedScores[i] = -AIHeuristics.ScoreAction(state, scoringSide, ordered[i]);
            }

            System.Array.Sort(negatedScores, ordered);

            actions.Clear();
            actions.AddRange(ordered);

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

        private string DescribePrincipalVariation(MCTSNode root)
        {
            System.Text.StringBuilder line = new System.Text.StringBuilder();
            MCTSNode node = root;

            for (int step = 0; step < MaxPrincipalVariationSteps; step++)
            {
                MCTSNode next = NextOnExpectedLine(node);

                if (next == null)
                {
                    break;
                }

                line.Append($"\n    [{node.SideToMove}] {next.IncomingAction} (visits={next.VisitCount}, avgValue={next.AverageValue:F1})");
                node = next;
            }

            return line.Length == 0 ? " (no expanded moves)" : line.ToString();
        }

        private void LogHealthPaymentDecision(MCTSNode root, MCTSNode chosen, GameState probeState, GameState rootState)
        {
            if (!AIHeuristics.RequiresHealthPayment(probeState, aiSide, chosen.IncomingAction, out int healthCost) || healthCost <= 0)
            {
                return;
            }

            MCTSNode bestAlternative = null;

            foreach (MCTSNode child in root.Children)
            {
                if (child == chosen)
                {
                    continue;
                }

                if (AIHeuristics.RequiresHealthPayment(probeState, aiSide, child.IncomingAction, out int alternativeCost) && alternativeCost > 0)
                {
                    continue;
                }

                if (bestAlternative == null || child.VisitCount > bestAlternative.VisitCount)
                {
                    bestAlternative = child;
                }
            }

            string alternativeDescription = bestAlternative != null
                ? $"{bestAlternative.IncomingAction} (visits={bestAlternative.VisitCount}, normalized {bestAlternative.NormalizedAverage:F3}) whose line ends in: {DescribeExpectedLineOutcome(root, rootState, bestAlternative)}"
                : "none";

            Debug.Log($"[AIMonteCarloTurnPlanner] HEALTH PAYMENT: {aiSide} chose {chosen.IncomingAction}, paying {healthCost} health with its leader on {probeState.GetPlayer(aiSide).LeaderHealth}. Chosen (visits={chosen.VisitCount}, normalized {chosen.NormalizedAverage:F3}) line ends in: {DescribeExpectedLineOutcome(root, rootState, chosen)} || Best option that pays no health: {alternativeDescription}");
        }

        private void LogEndTurnWithCardsInHand(MCTSNode root, MCTSNode chosen, GameState probeState, GameState rootState)
        {
            if (chosen.IncomingAction.Kind != AITurnActionKind.EndPhase)
            {
                return;
            }

            MCTSNode bestPlay = null;

            foreach (MCTSNode child in root.Children)
            {
                AITurnActionKind kind = child.IncomingAction.Kind;

                if (kind != AITurnActionKind.PlayUnit && kind != AITurnActionKind.PlayItem)
                {
                    continue;
                }

                if (bestPlay == null || child.VisitCount > bestPlay.VisitCount)
                {
                    bestPlay = child;
                }
            }

            if (bestPlay == null)
            {
                return;
            }

            Player ai = probeState.GetPlayer(aiSide);

            Debug.Log($"[AIMonteCarloTurnPlanner] HELD CARDS: {aiSide} ended its turn with {ai.CurrentMana}/{ai.MaxManaThisGame} mana unspent and hand [{string.Join(", ", ai.Hand.ConvertAll(card => $"{card.CardName}({card.ManaCost})"))}]. EndPhase (visits={chosen.VisitCount}, normalized {chosen.NormalizedAverage:F3}) ends in: {DescribeExpectedLineOutcome(root, rootState, chosen)} || Best card play {bestPlay.IncomingAction} (visits={bestPlay.VisitCount}, normalized {bestPlay.NormalizedAverage:F3}) expected reply:{DescribePrincipalVariation(bestPlay)} || ends in: {DescribeExpectedLineOutcome(root, rootState, bestPlay)}");
        }

        private string DescribeExpectedLineOutcome(MCTSNode root, GameState rootState, MCTSNode forcedFirstStep = null)
        {
            GameState lineState = rootState.Clone();
            PhaseManager linePhases = new PhaseManager(lineState, null);
            MCTSNode node = root;
            int stepsApplied = 0;
            int stepsRejected = 0;

            LogType previousFilter = Debug.unityLogger.filterLogType;
            Debug.unityLogger.filterLogType = LogType.Error;

            try
            {
                for (int step = 0; step < MaxPrincipalVariationSteps; step++)
                {
                    MCTSNode next = step == 0 && forcedFirstStep != null ? forcedFirstStep : NextOnExpectedLine(node);

                    if (next == null)
                    {
                        break;
                    }

                    if (ApplyAction(next.IncomingAction.WithTargetRemappedTo(lineState), lineState, linePhases))
                    {
                        stepsApplied++;
                    }
                    else
                    {
                        stepsRejected++;
                    }

                    node = next;
                }
            }
            finally
            {
                Debug.unityLogger.filterLogType = previousFilter;
            }

            return $"{AIHeuristics.DescribeEvaluation(lineState, aiSide)} (replayed {stepsApplied} step(s), {stepsRejected} rejected; enemy units on board: [{DescribeUnits(lineState, aiSide.Opposite())}]; AI units on board: [{DescribeUnits(lineState, aiSide)}])";
        }

        private static string DescribeUnits(GameState state, PlayerSide side)
        {
            List<string> descriptions = new List<string>();

            foreach (BoardUnit unit in state.Board.GetUnits(side))
            {
                descriptions.Add($"{unit.SourceCard.CardName} slot {unit.SlotIndex} {unit.GetCurrentAttack(state)}/{unit.CurrentHealth}");
            }

            return string.Join(", ", descriptions);
        }

        private static void Backpropagate(MCTSNode node, float value, float normalizedValue)
        {
            while (node != null)
            {
                node.VisitCount++;
                node.TotalValue += value;
                node.TotalNormalizedValue += normalizedValue;
                node = node.Parent;
            }
        }
    }
}