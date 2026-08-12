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

        private AITurnAction(AITurnActionKind kind, UnitCardData unitCard, ItemCardData itemCard, CardData chosenCard,
            EffectTarget target, int slotIndex, int fromSlot, int toSlot)
        {
            Kind = kind;
            UnitCard = unitCard;
            ItemCard = itemCard;
            ChosenCard = chosenCard;
            Target = target;
            SlotIndex = slotIndex;
            FromSlot = fromSlot;
            ToSlot = toSlot;
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

        public override string ToString()
        {
            switch (Kind)
            {
                case AITurnActionKind.PlayUnit:
                    return $"PlayUnit({UnitCard?.CardName} -> slot {SlotIndex})";
                case AITurnActionKind.PlayItem:
                    return $"PlayItem({ItemCard?.CardName} -> {Target.Kind})";
                case AITurnActionKind.Attack:
                    return $"Attack(slot {SlotIndex})";
                case AITurnActionKind.Move:
                    return $"Move({FromSlot} -> {ToSlot})";
                case AITurnActionKind.ResolveCardChoice:
                    return $"ResolveCardChoice({ChosenCard?.CardName})";
                case AITurnActionKind.ResolveTargetedEffect:
                    return $"ResolveTargetedEffect({Target.Kind})";
                default:
                    return "EndPhase";
            }
        }
    }
}