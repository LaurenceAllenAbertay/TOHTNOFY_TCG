using System.Collections.Generic;

namespace DDD.TNFY.TCG.Core
{
    public class Board
    {
        public const int SlotsPerSide = 7;

        public void CopyFrom(Board source, Dictionary<BoardUnit, BoardUnit> unitMap)
        {
            CopySlots(source.playerASlots, playerASlots, unitMap);
            CopySlots(source.playerBSlots, playerBSlots, unitMap);
        }

        private static void CopySlots(BoardUnit[] sourceSlots, BoardUnit[] destinationSlots, Dictionary<BoardUnit, BoardUnit> unitMap)
        {
            for (int i = 0; i < sourceSlots.Length; i++)
            {
                BoardUnit sourceUnit = sourceSlots[i];

                if (sourceUnit == null)
                {
                    destinationSlots[i] = null;
                    continue;
                }

                BoardUnit clonedUnit = sourceUnit.Clone();
                unitMap[sourceUnit] = clonedUnit;
                destinationSlots[i] = clonedUnit;
            }
        }

        private readonly BoardUnit[] playerASlots = new BoardUnit[SlotsPerSide];
        private readonly BoardUnit[] playerBSlots = new BoardUnit[SlotsPerSide];

        public BoardUnit GetUnit(PlayerSide side, int slotIndex)
        {
            return GetSlots(side)[slotIndex];
        }

        public void PlaceUnit(PlayerSide side, int slotIndex, BoardUnit unit)
        {
            unit.SlotIndex = slotIndex;
            GetSlots(side)[slotIndex] = unit;
        }

        public void RemoveUnit(PlayerSide side, int slotIndex)
        {
            GetSlots(side)[slotIndex] = null;
        }

        public BoardUnit GetOpponentUnit(PlayerSide side, int slotIndex)
        {
            return GetUnit(side.Opposite(), slotIndex);
        }

        public IEnumerable<BoardUnit> GetUnits(PlayerSide side)
        {
            BoardUnit[] slots = GetSlots(side);

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null)
                {
                    yield return slots[i];
                }
            }
        }

        private BoardUnit[] GetSlots(PlayerSide side)
        {
            return side == PlayerSide.PlayerA ? playerASlots : playerBSlots;
        }
    }
}