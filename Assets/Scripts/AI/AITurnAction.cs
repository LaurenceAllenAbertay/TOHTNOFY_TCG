using DDD.TNFY.TCG.Cards;
using DDD.TNFY.TCG.Effects;

namespace DDD.TNFY.TCG.Core
{
    public readonly struct AITurnAction
    {
        public AITurnActionKind Kind { get; }
        public UnitCardData UnitCard { get; }
        public ItemCardData ItemCard { get; }
        public CardData ChosenCard { get; }
        public EffectTarget Target { get; }
        public int SlotIndex { get; }
        public int FromSlot { get; }
        public int ToSlot { get; }
        public AIAbstractUnitCategory AbstractCategory { get; }

        private readonly PlayerSide targetUnitOwner;
        private readonly int targetUnitSlot;

        private AITurnAction(AITurnActionKind kind, UnitCardData unitCard, ItemCardData itemCard, CardData chosenCard,
            EffectTarget target, int slotIndex, int fromSlot, int toSlot, AIAbstractUnitCategory abstractCategory = AIAbstractUnitCategory.Chump)
        {
            Kind = kind;
            UnitCard = unitCard;
            ItemCard = itemCard;
            ChosenCard = chosenCard;
            Target = target;
            SlotIndex = slotIndex;
            FromSlot = fromSlot;
            ToSlot = toSlot;
            AbstractCategory = abstractCategory;

            bool targetsUnit = target.Kind == EffectTargetKind.Unit && target.Unit != null;
            targetUnitOwner = targetsUnit ? target.Unit.Owner : default;
            targetUnitSlot = targetsUnit ? target.Unit.SlotIndex : -1;
        }

        public bool TargetsUnit => targetUnitSlot >= 0;

        public AITurnAction WithTargetRemappedTo(GameState state)
        {
            if (!TargetsUnit)
            {
                return this;
            }

            BoardUnit unitInTargetSlot = state.Board.GetUnit(targetUnitOwner, targetUnitSlot);

            if (unitInTargetSlot == null || unitInTargetSlot == Target.Unit)
            {
                return this;
            }

            return new AITurnAction(Kind, UnitCard, ItemCard, ChosenCard, EffectTarget.ForUnit(unitInTargetSlot), SlotIndex, FromSlot, ToSlot, AbstractCategory);
        }

        public static readonly AITurnAction EndPhaseAction =
            new AITurnAction(AITurnActionKind.EndPhase, null, null, null, EffectTarget.None, -1, -1, -1);

        public static AITurnAction PlayUnitAt(UnitCardData card, int slotIndex)
        {
            return new AITurnAction(AITurnActionKind.PlayUnit, card, null, null, EffectTarget.None, slotIndex, -1, -1);
        }

        public static AITurnAction PlayItemAt(ItemCardData card, EffectTarget target)
        {
            return new AITurnAction(AITurnActionKind.PlayItem, null, card, null, target, -1, -1, -1);
        }

        public static AITurnAction AttackFrom(int slotIndex)
        {
            return new AITurnAction(AITurnActionKind.Attack, null, null, null, EffectTarget.None, slotIndex, -1, -1);
        }

        public static AITurnAction MoveFromTo(int fromSlot, int toSlot)
        {
            return new AITurnAction(AITurnActionKind.Move, null, null, null, EffectTarget.None, -1, fromSlot, toSlot);
        }

        public static AITurnAction ResolveCardChoiceWith(CardData chosenCard)
        {
            return new AITurnAction(AITurnActionKind.ResolveCardChoice, null, null, chosenCard, EffectTarget.None, -1, -1, -1);
        }

        public static AITurnAction ResolveTargetedEffectWith(EffectTarget target)
        {
            return new AITurnAction(AITurnActionKind.ResolveTargetedEffect, null, null, null, target, -1, -1, -1);
        }

        public static AITurnAction PlaceAbstractUnitAt(int slotIndex, AIAbstractUnitCategory category)
        {
            return new AITurnAction(AITurnActionKind.PlaceAbstractUnit, null, null, null, EffectTarget.None, slotIndex, -1, -1, category);
        }

        public static AITurnAction AbstractRemovalAt(int targetSlotIndex)
        {
            return new AITurnAction(AITurnActionKind.AbstractRemoval, null, null, null, EffectTarget.None, targetSlotIndex, -1, -1);
        }

        public override string ToString()
        {
            switch (Kind)
            {
                case AITurnActionKind.PlayUnit:
                    return $"PlayUnit({UnitCard?.CardName} -> slot {SlotIndex})";
                case AITurnActionKind.PlayItem:
                    return $"PlayItem({ItemCard?.CardName} -> {DescribeTarget()})";
                case AITurnActionKind.Attack:
                    return $"Attack(slot {SlotIndex})";
                case AITurnActionKind.Move:
                    return $"Move({FromSlot} -> {ToSlot})";
                case AITurnActionKind.ResolveCardChoice:
                    return $"ResolveCardChoice({ChosenCard?.CardName})";
                case AITurnActionKind.ResolveTargetedEffect:
                    return $"ResolveTargetedEffect({DescribeTarget()})";
                case AITurnActionKind.PlaceAbstractUnit:
                    return $"PlaceAbstractUnit({AbstractCategory} -> slot {SlotIndex})";
                case AITurnActionKind.AbstractRemoval:
                    return $"AbstractRemoval(-> enemy slot {SlotIndex})";
                default:
                    return "EndPhase";
            }
        }

        private string DescribeTarget()
        {
            switch (Target.Kind)
            {
                case EffectTargetKind.Unit:
                    return TargetsUnit
                        ? $"{Target.Unit.SourceCard?.CardName} ({targetUnitOwner} slot {targetUnitSlot})"
                        : "Unit (missing)";
                case EffectTargetKind.Leader:
                    return $"{Target.LeaderSide} leader";
                case EffectTargetKind.Slot:
                    return $"{Target.SlotSide} slot {Target.SlotIndex}";
                default:
                    return "no target";
            }
        }
    }
}