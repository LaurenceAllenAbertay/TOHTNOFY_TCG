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
    }
}