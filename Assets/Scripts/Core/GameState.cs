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

        public PlayerSide ActivePlayer { get; set; } = PlayerSide.PlayerA;
        public PlayerSide FirstPlayer { get; set; } = PlayerSide.PlayerA;
        public TurnPhase CurrentPhase { get; set; } = TurnPhase.Mulligan;
        public int TurnNumber { get; set; } = 1;
        public bool HasUsedMoveThisTurn { get; set; }
        public bool IsGameOver { get; set; }
        public PlayerSide? Winner { get; set; }

        public bool HasPendingFreeMove { get; set; }
        public BoardUnit PendingFreeMoveExcludedUnit { get; set; }

        public CardEffect PendingTargetedEffect { get; set; }
        public BoardUnit PendingTargetedEffectSource { get; set; }
        public EffectTriggerType? PendingTargetedEffectTrigger { get; set; }

        public List<CardData> PendingCardChoiceOptions { get; set; }
        public BoardUnit PendingCardChoiceSource { get; set; }

        public bool IsResolvingTurnStartEffects { get; set; }
        public int TurnStartScanSlot { get; set; }

        public BoardUnit CurrentlyAttackingUnit { get; set; }

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
    }
}