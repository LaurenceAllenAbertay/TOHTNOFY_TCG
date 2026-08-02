namespace DDD.TNFY.TCG.Core
{
    public enum PlayerSide
    {
        PlayerA,
        PlayerB
    }

    public static class PlayerSideExtensions
    {
        public static PlayerSide Opposite(this PlayerSide side)
        {
            return side == PlayerSide.PlayerA ? PlayerSide.PlayerB : PlayerSide.PlayerA;
        }

        public static PlayerSide ToActualSide(this PlayerSide seat, PlayerSide localSide)
        {
            return localSide == PlayerSide.PlayerA ? seat : seat.Opposite();
        }
    }
}