using DDD.TNFY.TCG.Core;

namespace DDD.TNFY.TCG.Effects
{
    public readonly struct EffectTarget
    {
        public EffectTargetKind Kind { get; }
        public BoardUnit Unit { get; }
        public PlayerSide LeaderSide { get; }
        public PlayerSide SlotSide { get; }
        public int SlotIndex { get; }

        private EffectTarget(EffectTargetKind kind, BoardUnit unit, PlayerSide leaderSide, PlayerSide slotSide, int slotIndex)
        {
            Kind = kind;
            Unit = unit;
            LeaderSide = leaderSide;
            SlotSide = slotSide;
            SlotIndex = slotIndex;
        }

        public static readonly EffectTarget None = new EffectTarget(EffectTargetKind.None, null, default, default, -1);

        public static EffectTarget ForUnit(BoardUnit unit)
        {
            return new EffectTarget(EffectTargetKind.Unit, unit, default, default, -1);
        }

        public static EffectTarget ForLeader(PlayerSide side)
        {
            return new EffectTarget(EffectTargetKind.Leader, null, side, default, -1);
        }

        public static EffectTarget ForSlot(PlayerSide side, int slotIndex)
        {
            return new EffectTarget(EffectTargetKind.Slot, null, default, side, slotIndex);
        }
    }
}