using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
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

        public IReadOnlyList<CardData> CardPool => cardPool;
        public IReadOnlyList<LeaderData> LeaderPool => leaderPool;

        private GameManager manager;

        private void Awake()
        {
            manager = GetComponent<GameManager>();
        }

        private void Start()
        {
            if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient)
            {
                Debug.Log("[MatchBootstrapper] Not the Master Client - waiting for synced draft/match state instead of starting the draft locally.");
                return;
            }

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