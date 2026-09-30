using System.Collections.Generic;
using UnityEngine;
using DDD.TNFY.TCG.Effects;

namespace DDD.TNFY.TCG.Cards
{
    [CreateAssetMenu(fileName = "NewUnitCard", menuName = "TNFY TCG/Cards/Unit Card")]
    public class UnitCardData : CardData
    {
        [SerializeField] private int attack;
        [SerializeField] private int health;
        [SerializeField] private Keyword keywords;
        [SerializeField] private bool grantsEnemyUnitMove;
        [SerializeField] private int pendingCurrentHealth;
        [SerializeField] private List<LeaderAura> auras = new List<LeaderAura>();

        public int Attack => attack;
        public int Health => health;
        public Keyword Keywords => keywords;
        public bool GrantsEnemyUnitMove => grantsEnemyUnitMove;
        public IReadOnlyList<LeaderAura> Auras => auras;
        public bool HasPendingCurrentHealth => pendingCurrentHealth != 0;
        public int PendingCurrentHealth => pendingCurrentHealth;

        public bool HasKeyword(Keyword keyword) => (keywords & keyword) != 0;

        public UnitCardData CreateSyncedClone(int newManaCost, int newAttack, int newHealth, int newPendingCurrentHealth = 0)
        {
            UnitCardData clone = Instantiate(this);
            clone.manaCost = newManaCost;
            clone.attack = newAttack;
            clone.health = newHealth;
            clone.pendingCurrentHealth = newPendingCurrentHealth;
            return clone;
        }
    }
}