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
    }
}