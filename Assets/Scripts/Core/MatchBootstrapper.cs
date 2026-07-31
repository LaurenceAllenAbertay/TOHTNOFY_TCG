using System.Collections.Generic;
using UnityEngine;
using DDD.TNFY.TCG.Cards;

namespace DDD.TNFY.TCG.Core
{
    [RequireComponent(typeof(GameManager))]
    public class MatchBootstrapper : MonoBehaviour
    {
        [Header("Card Pool Setup")]
        [SerializeField] private List<CardData> cardPool = new List<CardData>();
        [SerializeField] private DraftSettings draftSettings = new DraftSettings();

        [Header("Leader Setup")]
        [SerializeField] private List<LeaderData> leaderPool = new List<LeaderData>();

        private GameManager manager;

        private void Awake()
        {
            manager = GetComponent<GameManager>();
        }

        private void Start()
        {
            AssignRandomLeaders();
            manager.Phases.StartDraft(cardPool, draftSettings);
        }

        private void AssignRandomLeaders()
        {
            if (leaderPool.Count == 0)
            {
                return;
            }

            manager.State.PlayerA.Leader = leaderPool[Random.Range(0, leaderPool.Count)];
            manager.State.PlayerB.Leader = leaderPool[Random.Range(0, leaderPool.Count)];
        }
    }
}