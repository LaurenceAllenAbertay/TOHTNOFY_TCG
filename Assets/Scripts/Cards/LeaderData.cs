using System.Collections.Generic;
using UnityEngine;
using DDD.TNFY.TCG.Effects;

namespace DDD.TNFY.TCG.Cards
{
    [CreateAssetMenu(fileName = "NewLeader", menuName = "TNFY TCG/Leaders/Leader")]
    public class LeaderData : ScriptableObject
    {
        [SerializeField] private string leaderId;
        [SerializeField] private string leaderName;
        [SerializeField] private int maxHealth = 30;
        [SerializeField] private Sprite portrait;
        [TextArea(2, 4)]
        [SerializeField] private string abilityText;
        [Header( "Leader Effects" )]
        [SerializeField] private int firstUnitCostDiscount;
        [SerializeField] private int itemDrawIntervalTurns;
        [SerializeField] private int moveManaCost;
        [SerializeField] private int moveTemporaryAttackBonus;
        [SerializeField] private int healthToManaRatio;

        [SerializeField]
        private List<CardEffect> effects = new List<CardEffect>();

        [SerializeField]
        private List<LeaderAura> auras = new List<LeaderAura>();

        public string LeaderId => leaderId;
        public string LeaderName => leaderName;
        public int MaxHealth => maxHealth;
        public Sprite Portrait => portrait;
        public string AbilityText => abilityText;
        public int FirstUnitCostDiscount => firstUnitCostDiscount;
        public int ItemDrawIntervalTurns => itemDrawIntervalTurns;
        public int MoveManaCost => moveManaCost;
        public int MoveTemporaryAttackBonus => moveTemporaryAttackBonus;
        public int HealthToManaRatio => healthToManaRatio;
        public IReadOnlyList<CardEffect> Effects => effects;
        public IReadOnlyList<LeaderAura> Auras => auras;
    }
}