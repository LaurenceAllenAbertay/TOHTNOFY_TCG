using System;
using System.Collections.Generic;
using DDD.TNFY.TCG.Cards;
using DDD.TNFY.TCG.Effects;

namespace DDD.TNFY.TCG.Core
{
    public class GameState
    {
        public Board Board { get; } = new Board();
        public Player PlayerA { get; } = new Player(PlayerSide.PlayerA);
        public Player PlayerB { get; } = new Player(PlayerSide.PlayerB);

        public event Action<PlayerSide> ActivePlayerChanged;
        public event Action<TurnPhase> PhaseChanged;
        public event Action<TurnPhase, PlayerSide> PhaseAnnounced;
        public event Action BannerAnimationFinished;
        public event Action<int> AttackHitLanded;
        public event Action<BoardUnit> AttackAnimationFinished;
        public event Action<BoardUnit> UnitMoved;
        public event Action DraftOptionsChanged;
        public event Action GameOver;
        public event Action<CardData, PlayerSide, int, int> UnitPlayAnimationRequested;
        public event Action<PlayerSide, int, int> UnitPlayAnimationFinished;
        public event Action<CardData, PlayerSide, int, EffectTarget> ItemPlayAnimationRequested;
        public event Action<PlayerSide, int> ItemPlayAnimationFinished;
        public event Action<CardData, PlayerSide> CardBurnAnimationRequested;
        public event Action<BoardUnit> CurrentlyAttackingUnitChanged;

        public void RaisePhaseAnnounced(TurnPhase phase, PlayerSide activePlayerAtAnnouncement) => PhaseAnnounced?.Invoke(phase, activePlayerAtAnnouncement);
        public void RaiseBannerAnimationFinished() => BannerAnimationFinished?.Invoke();
        public void RaiseAttackHitLanded(int hitIndex) => AttackHitLanded?.Invoke(hitIndex);
        public void RaiseAttackAnimationFinished(BoardUnit unit) => AttackAnimationFinished?.Invoke(unit);
        public void RaiseUnitMoved(BoardUnit unit) => UnitMoved?.Invoke(unit);
        public void RaiseDraftOptionsChanged() => DraftOptionsChanged?.Invoke();
        public void RaiseGameOver() => GameOver?.Invoke();
        public void RaiseUnitPlayAnimationRequested(CardData card, PlayerSide playingSide, int handIndex, int slotIndex) => UnitPlayAnimationRequested?.Invoke(card, playingSide, handIndex, slotIndex);
        public void RaiseUnitPlayAnimationFinished(PlayerSide playingSide, int handIndex, int slotIndex) => UnitPlayAnimationFinished?.Invoke(playingSide, handIndex, slotIndex);
        public void RaiseItemPlayAnimationRequested(CardData card, PlayerSide playingSide, int handIndex, EffectTarget target) => ItemPlayAnimationRequested?.Invoke(card, playingSide, handIndex, target);
        public void RaiseItemPlayAnimationFinished(PlayerSide playingSide, int handIndex) => ItemPlayAnimationFinished?.Invoke(playingSide, handIndex);
        public void RaiseCardBurnAnimationRequested(CardData card, PlayerSide side) => CardBurnAnimationRequested?.Invoke(card, side);

        private PlayerSide activePlayer = PlayerSide.PlayerA;
        public PlayerSide ActivePlayer
        {
            get => activePlayer;
            set
            {
                activePlayer = value;
                ActivePlayerChanged?.Invoke(value);
            }
        }

        private TurnPhase currentPhase = TurnPhase.None;
        public TurnPhase CurrentPhase
        {
            get => currentPhase;
            set
            {
                if (currentPhase == value)
                {
                    return;
                }

                currentPhase = value;
                PhaseChanged?.Invoke(value);
            }
        }

        private BoardUnit currentlyAttackingUnit;
        public BoardUnit CurrentlyAttackingUnit
        {
            get => currentlyAttackingUnit;
            set
            {
                currentlyAttackingUnit = value;
                CurrentlyAttackingUnitChanged?.Invoke(value);
            }
        }

        public PlayerSide FirstPlayer { get; set; } = PlayerSide.PlayerA;
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

        public Player GetPlayer(PlayerSide side) => side == PlayerSide.PlayerA ? PlayerA : PlayerB;

        public Player GetActivePlayerData() => GetPlayer(ActivePlayer);

        public PlayerSide GetOpponent(PlayerSide side) => side.Opposite();

        public bool IsExcludedAsSelfTarget(BoardUnit candidate)
        {
            return candidate != null && candidate == PendingTargetedEffectSource && PendingTargetedEffectTrigger != EffectTriggerType.OnTurnStart;
        }

        public GameState Clone()
        {
            GameState clone = new GameState();
            Dictionary<BoardUnit, BoardUnit> unitMap = new Dictionary<BoardUnit, BoardUnit>();

            clone.Board.CopyFrom(Board, unitMap);
            clone.PlayerA.CopyFrom(PlayerA);
            clone.PlayerB.CopyFrom(PlayerB);

            BoardUnit Map(BoardUnit original) => original != null && unitMap.TryGetValue(original, out BoardUnit mapped) ? mapped : null;

            clone.activePlayer = activePlayer;
            clone.currentPhase = currentPhase;
            clone.currentlyAttackingUnit = Map(currentlyAttackingUnit);
            clone.FirstPlayer = FirstPlayer;
            clone.TurnNumber = TurnNumber;
            clone.HasUsedMoveThisTurn = HasUsedMoveThisTurn;
            clone.IsGameOver = IsGameOver;
            clone.Winner = Winner;
            clone.HasPendingFreeMove = HasPendingFreeMove;
            clone.PendingFreeMoveExcludedUnit = Map(PendingFreeMoveExcludedUnit);
            clone.HasPendingEnemyMoveGrantOnPlay = HasPendingEnemyMoveGrantOnPlay;
            clone.PendingEnemyMoveGrantTarget = Map(PendingEnemyMoveGrantTarget);
            clone.PendingTargetedEffect = PendingTargetedEffect;
            clone.PendingTargetedEffectSource = Map(PendingTargetedEffectSource);
            clone.PendingTargetedEffectTrigger = PendingTargetedEffectTrigger;
            clone.PendingCardChoiceOptions = PendingCardChoiceOptions != null ? new List<CardData>(PendingCardChoiceOptions) : null;
            clone.PendingCardChoiceSource = Map(PendingCardChoiceSource);
            clone.IsResolvingTurnStartEffects = IsResolvingTurnStartEffects;
            clone.TurnStartScanSlot = TurnStartScanSlot;

            return clone;
        }
    }
}