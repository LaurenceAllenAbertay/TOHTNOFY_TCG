#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using DDD.TNFY.TCG.Cards;
using DDD.TNFY.TCG.Effects;

namespace DDD.TNFY.TCG.EditorTools
{
    public static class TestCardGenerator
    {
        private const string OutputFolder = "Assets/TestCards";

        [MenuItem("TNFY TCG/Generate Test Cards")]
        public static void GenerateTestCards()
        {
            EnsureFolder();

            CreateVanillaUnit("Militia", cost: 1, attack: 1, health: 1);
            CreateVanillaUnit("Footman", cost: 3, attack: 3, health: 3);
            CreateVanillaUnit("Warbeast", cost: 5, attack: 6, health: 5);
            CreateDamageOnPlayUnit("Firestarter", cost: 2, attack: 2, health: 3, damage: 2);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void CreateVanillaUnit(string name, int cost, int attack, int health)
        {
            UnitCardData card = ScriptableObject.CreateInstance<UnitCardData>();
            ApplyBaseFields(card, name, cost);
            SetUnitStats(card, attack, health);

            AssetDatabase.CreateAsset(card, $"{OutputFolder}/{name}.asset");
        }

        private static void CreateDamageOnPlayUnit(string name, int cost, int attack, int health, int damage)
        {
            UnitCardData card = ScriptableObject.CreateInstance<UnitCardData>();
            ApplyBaseFields(card, name, cost);
            SetUnitStats(card, attack, health);

            CardEffect effect = new CardEffect
            {
                trigger = EffectTriggerType.OnPlay,
                action = EffectActionType.DrawCard,
                targetType = TargetType.None,
                amount = damage
            };

            SetEffects(card, new List<CardEffect> { effect });

            AssetDatabase.CreateAsset(card, $"{OutputFolder}/{name}.asset");
        }

        private static void ApplyBaseFields(CardData card, string name, int cost)
        {
            SerializedObject so = new SerializedObject(card);
            so.FindProperty("cardId").stringValue = name.ToLowerInvariant();
            so.FindProperty("cardName").stringValue = name;
            so.FindProperty("manaCost").intValue = cost;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetUnitStats(UnitCardData card, int attack, int health)
        {
            SerializedObject so = new SerializedObject(card);
            so.FindProperty("attack").intValue = attack;
            so.FindProperty("health").intValue = health;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEffects(CardData card, List<CardEffect> effects)
        {
            SerializedObject so = new SerializedObject(card);
            SerializedProperty effectsProp = so.FindProperty("effects");
            effectsProp.ClearArray();

            for (int i = 0; i < effects.Count; i++)
            {
                effectsProp.InsertArrayElementAtIndex(i);
                SerializedProperty element = effectsProp.GetArrayElementAtIndex(i);

                element.FindPropertyRelative("trigger").enumValueIndex = (int)effects[i].trigger;
                element.FindPropertyRelative("action").enumValueIndex = (int)effects[i].action;
                element.FindPropertyRelative("targetType").enumValueIndex = (int)effects[i].targetType;
                element.FindPropertyRelative("amount").intValue = effects[i].amount;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(OutputFolder))
            {
                AssetDatabase.CreateFolder("Assets", "TestCards");
            }
        }
    }
}
#endif