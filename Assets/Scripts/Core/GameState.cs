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
    }
}