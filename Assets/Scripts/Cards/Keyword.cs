using System;

namespace DDD.TNFY.TCG.Cards
{
    [Flags]
    public enum Keyword
    {
        None = 0,
        Taunt = 1 << 0,
        Rush = 1 << 1,
        BifurcatedAttack = 1 << 2,
        Agile = 1 << 3,
        Nimble = 1 << 4,
        Piercing = 1 << 5,
        Unmoving = 1 << 6,
        Unstable = 1 << 7,
        Retaliate = 1 << 8,
        Teleport = 1 << 9
    }
}