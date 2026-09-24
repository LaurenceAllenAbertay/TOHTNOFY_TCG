using System;
using System.Collections.Generic;
using UnityEngine;
using DDD.TNFY.TCG.Cards;
using DDD.TNFY.TCG.Effects;

namespace DDD.TNFY.TCG.Core
{
    public class GameState
    {
        public Board Board { get; } = new Board();
        public Player PlayerA { get; } = new Player(PlayerSide.PlayerA);
        public Player PlayerB { get; } = new Player(PlayerSide.PlayerB);

        private PlayerSide activePlayer = PlayerSide.PlayerA;
        public PlayerSide ActivePlayer
        {
            get => activePlayer;
            set
            {
                activePlayer = value;
                ActivePlayerChanged?.Invoke(activePlayer);
            }
        }

        public event Action<PlayerSide> ActivePlayerChanged;

        public PlayerSide FirstPlayer { get; set; } = PlayerSide.PlayerA;

        private TurnPhase currentPhase = TurnPhase.None;
        public TurnPhase CurrentPhase
        {
            get => currentPhase;
            set
            {
                if (currentPhase == value)
                {
                    Debug.Log($"[GameState] CurrentPhase set to {value} but it was already {currentPhase} - PhaseChanged will NOT fire.");
                    return;
                }

                currentPhase = value;
                PhaseChanged?.Invoke(currentPhase);
            }
        }

        public event Action<TurnPhase> PhaseChanged;

        public event Action<TurnPhase, PlayerSide> PhaseAnnounced;

        public void RaisePhaseAnnounced(TurnPhase phase, PlayerSide activePlayerAtAnnouncement)
        {
            PhaseAnnounced?.Invoke(phase, activePlayerAtAnnouncement);
        }

        public event Action BannerAnimationFinished;

        public void RaiseBannerAnimationFinished()
        {
            BannerAnimationFinished?.Invoke();
        }

        public event Action<int> AttackHitLanded;

        public void RaiseAttackHitLanded(int hitIndex)
        {
            AttackHitLanded?.Invoke(hitIndex);
        }

        public event Action<BoardUnit> AttackAnimationFinished;

        public void RaiseAttackAnimationFinished(BoardUnit unit)
        {
            AttackAnimationFinished?.Invoke(unit);
        }

        public event Action<BoardUnit> UnitMoved;

        public void RaiseUnitMoved(BoardUnit unit)
        {
            UnitMoved?.Invoke(unit);
        }

        public event Action DraftOptionsChanged;

        public void RaiseDraftOptionsChanged()
        {
            DraftOptionsChanged?.Invoke();
        }

        public event Action GameOver;

        public void RaiseGameOver()
        {
            GameOver?.Invoke();
        }

        public event Action<CardData, PlayerSide, int, int> UnitPlayAnimationRequested;

        public void RaiseUnitPlayAnimationRequested(CardData card, PlayerSide playingSide, int handIndex, int slotIndex)
        {
            UnitPlayAnimationRequested?.Invoke(card, playingSide, handIndex, slotIndex);
        }

        public event Action<PlayerSide, int, int> UnitPlayAnimationFinished;

        public void RaiseUnitPlayAnimationFinished(PlayerSide playingSide, int handIndex, int slotIndex)
        {
            UnitPlayAnimationFinished?.Invoke(playingSide, handIndex, slotIndex);
        }

        public event Action<CardData, PlayerSide, int, EffectTarget> ItemPlayAnimationRequested;

        public void RaiseItemPlayAnimationRequested(CardData card, PlayerSide playingSide, int handIndex, EffectTarget target)
        {
            ItemPlayAnimationRequested?.Invoke(card, playingSide, handIndex, target);
        }

        public event Action<PlayerSide, int> ItemPlayAnimationFinished;

        public void RaiseItemPlayAnimationFinished(PlayerSide playingSide, int handIndex)
        {
            ItemPlayAnimationFinished?.Invoke(playingSide, handIndex);
        }

        public event Action<CardData, PlayerSide> CardBurnAnimationRequested;

        public void RaiseCardBurnAnimationRequested(CardData card, PlayerSide side)
        {
            CardBurnAnimationRequested?.Invoke(card, side);
        }

        public int TurnNumber { get; set; } = 1;
        public bool HasUsedMoveThisTurn { get; set; }
        public bool IsGameOver { get; set; }
        public PlayerSide? Winner { get; set; }

        public bool HasPendingFreeMove { get; set; }
        public BoardUnit PendingFreeMoveExcludedUnit { get; set; }

        public bool HasPendingEnemyMoveGrantOnPlay { get; set; }
        public BoardUnit PendingEnemyMoveGrantTarget { get; set; }

        public CardEffect PendingTargetedEffect { get; set; }
        public BoardUnit PendingTargetedEffectSource { get; set; }
        public EffectTriggerType? PendingTargetedEffectTrigger { get; set; }

        public List<CardData> PendingCardChoiceOptions { get; set; }
        public BoardUnit PendingCardChoiceSource { get; set; }

        public bool IsResolvingTurnStartEffects { get; set; }
        public int TurnStartScanSlot { get; set; }

        private BoardUnit currentlyAttackingUnit;
        public BoardUnit CurrentlyAttackingUnit
        {
            get => currentlyAttackingUnit;
            set
            {
                currentlyAttackingUnit = value;
                CurrentlyAttackingUnitChanged?.Invoke(currentlyAttackingUnit);
            }
        }

        public event Action<BoardUnit> CurrentlyAttackingUnitChanged;

        public Player GetPlayer(PlayerSide side)
        {
            return side == PlayerSide.PlayerA ? PlayerA : PlayerB;
        }

        public Player GetActivePlayerData()
        {
            return GetPlayer(ActivePlayer);
        }

        public PlayerSide GetOpponent(PlayerSide side)
        {
            return side.Opposite();
        }

        public bool IsExcludedAsSelfTarget(BoardUnit candidate)
        {
            if (PendingTargetedEffectSource == null || candidate != PendingTargetedEffectSource)
            {
                return false;
            }

            bool cameFromTurnStart = PendingTargetedEffectTrigger == EffectTriggerType.OnTurnStart;
            return !cameFromTurnStart;
        }

        public GameState Clone()
        {
            GameState clone = new GameState();
            Dictionary<BoardUnit, BoardUnit> unitMap = new Dictionary<BoardUnit, BoardUnit>();

            clone.Board.CopyFrom(Board, unitMap);
            clone.PlayerA.CopyFrom(PlayerA);
            clone.PlayerB.CopyFrom(PlayerB);

            clone.activePlayer = activePlayer;
            clone.currentPhase = currentPhase;

            clone.FirstPlayer = FirstPlayer;
            clone.TurnNumber = TurnNumber;
            clone.HasUsedMoveThisTurn = HasUsedMoveThisTurn;
            clone.IsGameOver = IsGameOver;
            clone.Winner = Winner;

            clone.HasPendingFreeMove = HasPendingFreeMove;
            clone.PendingFreeMoveExcludedUnit = MapUnit(PendingFreeMoveExcludedUnit, unitMap);

            clone.HasPendingEnemyMoveGrantOnPlay = HasPendingEnemyMoveGrantOnPlay;
            clone.PendingEnemyMoveGrantTarget = MapUnit(PendingEnemyMoveGrantTarget, unitMap);

            clone.PendingTargetedEffect = PendingTargetedEffect;
            clone.PendingTargetedEffectSource = MapUnit(PendingTargetedEffectSource, unitMap);
            clone.PendingTargetedEffectTrigger = PendingTargetedEffectTrigger;

            clone.PendingCardChoiceOptions = PendingCardChoiceOptions != null ? new List<CardData>(PendingCardChoiceOptions) : null;
            clone.PendingCardChoiceSource = MapUnit(PendingCardChoiceSource, unitMap);

            clone.IsResolvingTurnStartEffects = IsResolvingTurnStartEffects;
            clone.TurnStartScanSlot = TurnStartScanSlot;

            clone.currentlyAttackingUnit = MapUnit(currentlyAttackingUnit, unitMap);

            return clone;
        }

        private static BoardUnit MapUnit(BoardUnit original, Dictionary<BoardUnit, BoardUnit> unitMap)
        {
            if (original == null)
            {
                return null;
            }

            return unitMap.TryGetValue(original, out BoardUnit mapped) ? mapped : null;
        }
    }
}