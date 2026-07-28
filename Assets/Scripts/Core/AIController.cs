using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DDD.TNFY.TCG.Cards;
using DDD.TNFY.TCG.Effects;

namespace DDD.TNFY.TCG.Core
{
    [RequireComponent(typeof(GameManager))]
    public class AIController : MonoBehaviour
    {
        [SerializeField] private bool isPlayerBAI;
        [SerializeField] private PlayerSide aiSide = PlayerSide.PlayerB;
        [SerializeField] private float actionDelaySeconds = 0.4f;
        [SerializeField] private int mulliganMaxCostToKeep = 5;

        private GameManager gameManager;
        private GameState state;
        private PhaseManager phases;
        private Coroutine runningTurnRoutine;

        private void Awake()
        {
            gameManager = GetComponent<GameManager>();
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

        private void HandlePhaseChanged(TurnPhase newPhase)
        {
            if (!isPlayerBAI)
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
                case TurnPhase.Mulligan:
                    RunMulligan();
                    break;

                case TurnPhase.Play:
                    yield return RunPlayPhase();
                    break;

                case TurnPhase.Move:
                    yield return RunMovePhase();
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

        private IEnumerator RunPlayPhase()
        {
            while (state.CurrentPhase == TurnPhase.Play && state.ActivePlayer == aiSide)
            {
                if (phases.HasBlockingPendingTargetedEffect())
                {
                    ResolvePendingInteraction();
                    yield return new WaitForSeconds(actionDelaySeconds);
                    continue;
                }

                if (!TryPlayBestCard())
                {
                    break;
                }

                yield return new WaitForSeconds(actionDelaySeconds);
            }

            if (state.CurrentPhase == TurnPhase.Play && state.ActivePlayer == aiSide)
            {
                Debug.Log("[AIController] No more affordable/legal plays — entering Attack phase.");
                phases.EnterAttackPhase();
            }
        }

        private void ResolvePendingInteraction()
        {
            if (state.PendingCardChoiceOptions != null)
            {
                CardData chosen = ChooseBestCardChoiceOption(state.PendingCardChoiceOptions);
                Debug.Log($"[AIController] Resolving pending card choice with {chosen.CardName}.");
                phases.TryResolvePendingCardChoice(chosen);
                return;
            }

            if (state.PendingTargetedEffect != null)
            {
                EffectTarget target = ChooseBestTarget(state.PendingTargetedEffect);
                bool resolved = phases.TryResolvePendingTargetedEffect(target);
                Debug.Log($"[AIController] Resolving pending targeted effect (targetType={state.PendingTargetedEffect?.targetType}), success={resolved}.");

                if (!resolved)
                {
                    phases.CancelPendingTargetedEffectIfNonMandatory();
                }
            }
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

        private bool TryPlayBestCard()
        {
            Player active = state.GetPlayer(aiSide);
            BestUnitPlay bestUnitPlay = FindBestUnitPlay(active);
            BestItemPlay bestItemPlay = FindBestItemPlay(active);

            bool hasUnitPlay = bestUnitPlay.Card != null;
            bool hasItemPlay = bestItemPlay.Card != null;

            if (!hasUnitPlay && !hasItemPlay)
            {
                return false;
            }

            if (hasUnitPlay && (!hasItemPlay || bestUnitPlay.Score >= bestItemPlay.Score))
            {
                Debug.Log($"[AIController] Playing unit {bestUnitPlay.Card.CardName} into slot {bestUnitPlay.SlotIndex} (score={bestUnitPlay.Score}).");
                return phases.TryPlayUnit(bestUnitPlay.Card, bestUnitPlay.SlotIndex);
            }

            Debug.Log($"[AIController] Playing item {bestItemPlay.Card.CardName} at target kind={bestItemPlay.Target.Kind} (score={bestItemPlay.Score}).");
            return phases.TryPlayItem(bestItemPlay.Card, bestItemPlay.Target);
        }

        private struct BestUnitPlay
        {
            public UnitCardData Card;
            public int SlotIndex;
            public float Score;
        }

        private struct BestItemPlay
        {
            public ItemCardData Card;
            public EffectTarget Target;
            public float Score;
        }

        private BestUnitPlay FindBestUnitPlay(Player active)
        {
            BestUnitPlay best = new BestUnitPlay { Card = null, SlotIndex = -1, Score = float.NegativeInfinity };

            foreach (CardData card in active.Hand)
            {
                if (!(card is UnitCardData unitCard))
                {
                    continue;
                }

                for (int slot = 0; slot < Board.SlotsPerSide; slot++)
                {
                    if (state.Board.GetUnit(aiSide, slot) != null)
                    {
                        continue;
                    }

                    if (!phases.CanPlayUnit(unitCard, slot))
                    {
                        continue;
                    }

                    float score = ScoreUnitPlacement(unitCard, slot);

                    if (score > best.Score)
                    {
                        best = new BestUnitPlay { Card = unitCard, SlotIndex = slot, Score = score };
                    }
                }
            }

            return best;
        }

        private float ScoreUnitPlacement(UnitCardData unitCard, int slot)
        {
            int cost = AuraCalculator.GetUnitCost(unitCard, state.GetPlayer(aiSide));
            float score = cost * 10f;

            BoardUnit opposing = state.Board.GetOpponentUnit(aiSide, slot);

            if (opposing == null)
            {
                score += unitCard.Attack * 2f;
            }
            else
            {
                int opposingAttack = opposing.GetCurrentAttack(state);
                bool weKillThem = unitCard.Attack >= opposing.CurrentHealth;
                bool theyKillUs = opposingAttack >= unitCard.Health;

                if (weKillThem && !theyKillUs)
                {
                    score += 30f;
                }
                else if (weKillThem && theyKillUs)
                {
                    score += 8f;
                }
                else if (!weKillThem && theyKillUs)
                {
                    score -= 25f;
                }
                else
                {
                    score += unitCard.Attack - opposingAttack;
                }
            }

            if (unitCard.HasKeyword(Keyword.Taunt))
            {
                score += 4f;
            }

            return score;
        }

        private BestItemPlay FindBestItemPlay(Player active)
        {
            BestItemPlay best = new BestItemPlay { Card = null, Target = EffectTarget.None, Score = float.NegativeInfinity };

            foreach (CardData card in active.Hand)
            {
                if (!(card is ItemCardData itemCard))
                {
                    continue;
                }

                CardEffect effect = itemCard.PrimaryEffect;

                if (effect == null)
                {
                    continue;
                }

                foreach (EffectTarget candidateTarget in EnumerateCandidateTargets(effect.targetType))
                {
                    if (!phases.CanPlayItem(itemCard, candidateTarget))
                    {
                        continue;
                    }

                    float score = ScoreItemPlay(itemCard, effect, candidateTarget);

                    if (score > best.Score)
                    {
                        best = new BestItemPlay { Card = itemCard, Target = candidateTarget, Score = score };
                    }
                }
            }

            return best;
        }

        private IEnumerable<EffectTarget> EnumerateCandidateTargets(TargetType targetType)
        {
            if (targetType == TargetType.None || targetType == TargetType.Board || targetType == TargetType.Self)
            {
                yield return EffectTarget.None;
                yield break;
            }

            if (targetType == TargetType.AllyLeader)
            {
                yield return EffectTarget.ForLeader(aiSide);
                yield break;
            }

            if (targetType == TargetType.EnemyLeader)
            {
                yield return EffectTarget.ForLeader(aiSide.Opposite());
                yield break;
            }

            for (int slot = 0; slot < Board.SlotsPerSide; slot++)
            {
                BoardUnit allyUnit = state.Board.GetUnit(aiSide, slot);
                if (allyUnit != null)
                {
                    yield return EffectTarget.ForUnit(allyUnit);
                }

                BoardUnit enemyUnit = state.Board.GetUnit(aiSide.Opposite(), slot);
                if (enemyUnit != null)
                {
                    yield return EffectTarget.ForUnit(enemyUnit);
                }
            }

            if (targetType == TargetType.AnyUnitOrLeader || targetType == TargetType.EnemyUnitOrLeader)
            {
                yield return EffectTarget.ForLeader(aiSide.Opposite());
            }

            if (targetType == TargetType.AnyUnitOrLeader)
            {
                yield return EffectTarget.ForLeader(aiSide);
            }
        }

        private static readonly HashSet<EffectActionType> HarmfulActions = new HashSet<EffectActionType>
        {
            EffectActionType.StunUnit,
            EffectActionType.DealDamageToTarget,
            EffectActionType.BounceUnit,
            EffectActionType.BounceUnitOpposite,
            EffectActionType.ApplyDelayedKill,
            EffectActionType.SilenceUnit,
            EffectActionType.ApplyDecay,
            EffectActionType.PullUnitOpposite,
            EffectActionType.PushAlliesAway,
            EffectActionType.ReduceOpponentMana,
        };

        private float ScoreItemPlay(ItemCardData itemCard, CardEffect effect, EffectTarget target)
        {
            float score = itemCard.ManaCost * 10f;
            bool isHarmful = HarmfulActions.Contains(effect.action);

            bool targetIsEnemy = (target.Kind == EffectTargetKind.Unit && target.Unit.Owner != aiSide)
                || (target.Kind == EffectTargetKind.Leader && target.LeaderSide != aiSide);

            bool targetIsAlly = (target.Kind == EffectTargetKind.Unit && target.Unit.Owner == aiSide)
                || (target.Kind == EffectTargetKind.Leader && target.LeaderSide == aiSide);

            if (isHarmful && targetIsEnemy)
            {
                score += 15f + effect.amount;
            }
            else if (!isHarmful && targetIsAlly)
            {
                score += 15f + effect.amount;
            }
            else
            {
                score -= 20f;
            }

            return score;
        }

        private EffectTarget ChooseBestTarget(CardEffect effect)
        {
            EffectTarget best = EffectTarget.None;
            float bestScore = float.NegativeInfinity;

            foreach (EffectTarget candidate in EnumerateCandidateTargets(effect.targetType))
            {
                if (!EffectTargeting.IsValidTarget(effect.targetType, candidate, state))
                {
                    continue;
                }

                float score = ScoreItemPlay(null, effect, candidate);

                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        private IEnumerator RunMovePhase()
        {
            while (state.CurrentPhase == TurnPhase.Move && state.ActivePlayer == aiSide)
            {
                MoveOption bestMove = FindBestMove();

                if (bestMove.FromSlot < 0 || bestMove.Score <= 0f)
                {
                    Debug.Log("[AIController] No further beneficial move found.");
                    break;
                }

                Debug.Log($"[AIController] Moving unit from slot {bestMove.FromSlot} to {bestMove.ToSlot} (score={bestMove.Score}).");

                if (!phases.TryMoveUnit(bestMove.FromSlot, bestMove.ToSlot))
                {
                    Debug.LogWarning($"[AIController] TryMoveUnit unexpectedly failed for {bestMove.FromSlot} -> {bestMove.ToSlot} despite CanMoveUnit passing — stopping move phase to avoid an infinite loop.");
                    break;
                }

                yield return new WaitForSeconds(actionDelaySeconds);
            }

            phases.PassMoveToEndTurn();
        }

        private struct MoveOption
        {
            public int FromSlot;
            public int ToSlot;
            public float Score;
        }

        private MoveOption FindBestMove()
        {
            MoveOption best = new MoveOption { FromSlot = -1, ToSlot = -1, Score = float.NegativeInfinity };

            for (int fromSlot = 0; fromSlot < Board.SlotsPerSide; fromSlot++)
            {
                BoardUnit unit = state.Board.GetUnit(aiSide, fromSlot);

                if (unit == null)
                {
                    continue;
                }

                for (int toSlot = 0; toSlot < Board.SlotsPerSide; toSlot++)
                {
                    if (toSlot == fromSlot)
                    {
                        continue;
                    }

                    if (!phases.CanMoveUnit(fromSlot, toSlot))
                    {
                        continue;
                    }

                    float currentLaneScore = ScoreLaneForUnit(unit, fromSlot);
                    float destinationLaneScore = ScoreLaneForUnit(unit, toSlot);
                    float moveManaCost = state.GetPlayer(aiSide).Leader != null ? state.GetPlayer(aiSide).Leader.MoveManaCost : 0;

                    float score = (destinationLaneScore - currentLaneScore) - moveManaCost;

                    if (score > best.Score)
                    {
                        best = new MoveOption { FromSlot = fromSlot, ToSlot = toSlot, Score = score };
                    }
                }
            }

            return best;
        }

        private float ScoreLaneForUnit(BoardUnit unit, int slot)
        {
            BoardUnit opposing = state.Board.GetOpponentUnit(aiSide, slot);
            int ourAttack = unit.GetCurrentAttack(state);

            if (opposing == null)
            {
                return 20f + ourAttack;
            }

            int opposingAttack = opposing.GetCurrentAttack(state);
            bool weKillThem = ourAttack >= opposing.CurrentHealth;
            bool theyKillUs = opposingAttack >= unit.CurrentHealth;

            if (weKillThem && !theyKillUs)
            {
                return 30f;
            }

            if (theyKillUs && !weKillThem)
            {
                return -30f;
            }

            if (weKillThem && theyKillUs)
            {
                return 5f;
            }

            return ourAttack - opposingAttack;
        }
    }
}