using DDD.TNFY.TCG.Core;

namespace DDD.TNFY.TCG.Effects
{
    public class EffectContext
    {
        public GameState GameState { get; }
        public Board Board { get; }
        public PlayerSide SourceOwner { get; }
        public BoardUnit SourceUnit { get; }
        public EffectTarget ChosenTarget { get; }

        public EffectContext(GameState gameState, PlayerSide sourceOwner, BoardUnit sourceUnit, EffectTarget chosenTarget = default)
        {
            GameState = gameState;
            Board = gameState.Board;
            SourceOwner = sourceOwner;
            SourceUnit = sourceUnit;
            ChosenTarget = chosenTarget;
        }
    }
}