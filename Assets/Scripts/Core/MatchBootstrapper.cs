using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using DDD.TNFY.TCG.Cards;
using DDD.TNFY.TCG.DeckBuilding;

namespace DDD.TNFY.TCG.Core
{
    [RequireComponent(typeof(GameManager))]
    public class MatchBootstrapper : MonoBehaviour
    {
        private static readonly List<CardData> EmptyCardList = new List<CardData>();

        [Header("Card Pool Setup")]
        [SerializeField] private CardDatabase cardDatabase;
        [SerializeField] private DraftSettings draftSettings = new DraftSettings();

        [Header("Leader Setup")]
        [SerializeField] private List<LeaderData> leaderPool = new List<LeaderData>();

        [Header("Game Mode")]
        [SerializeField] private GameMode fallbackGameMode = GameMode.Draft;

        public IReadOnlyList<CardData> CardPool => cardDatabase != null ? cardDatabase.AllCards : EmptyCardList;
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
            AssignRandomFirstPlayer();

            GameMode effectiveMode = ConstructedMatchSync.ReadSelectedMode(fallbackGameMode);
            Debug.Log($"[MatchBootstrapper] Starting match in {effectiveMode} mode.");

            if (effectiveMode == GameMode.Constructed)
            {
                StartConstructedMatch();
            }
            else
            {
                manager.Phases.StartDraft(new List<CardData>(CardPool), draftSettings);
            }
        }

        private void StartConstructedMatch()
        {
            bool appliedAnyRemoteDeck = false;

            if (PhotonNetwork.InRoom)
            {
                foreach (Photon.Realtime.Player photonPlayer in PhotonNetwork.PlayerList)
                {
                    PlayerSide side = NetworkedMatchSync.SideForActorNumber(photonPlayer.ActorNumber);
                    Player player = manager.State.GetPlayer(side);

                    if (ConstructedMatchSync.TryReadDeck(photonPlayer, cardDatabase, out List<CardData> syncedDeck))
                    {
                        player.Deck.AddRange(syncedDeck);
                        appliedAnyRemoteDeck = true;
                        Debug.Log($"[MatchBootstrapper] Loaded {syncedDeck.Count}-card Constructed deck for {side} from actor {photonPlayer.ActorNumber}.");
                    }
                    else
                    {
                        Debug.LogWarning($"[MatchBootstrapper] No Constructed deck found for actor {photonPlayer.ActorNumber} ({side}) - that player will have an empty deck.");
                    }
                }
            }

            if (!appliedAnyRemoteDeck)
            {
                List<CardData> localDeck = DeckStorage.LoadActiveDeckCards(cardDatabase);
                Debug.LogWarning($"[MatchBootstrapper] Constructed mode outside a synced Photon room - applying the local active deck ({localDeck.Count} cards) to both sides for local testing.");

                manager.State.PlayerA.Deck.AddRange(localDeck);
                manager.State.PlayerB.Deck.AddRange(new List<CardData>(localDeck));
            }

            ListShuffler.Shuffle(manager.State.PlayerA.Deck);
            ListShuffler.Shuffle(manager.State.PlayerB.Deck);

            manager.State.ActivePlayer = manager.State.FirstPlayer;

            manager.Phases.StartMatch();
        }

        private void AssignRandomFirstPlayer()
        {
            manager.State.FirstPlayer = Random.Range(0, 2) == 0 ? PlayerSide.PlayerA : PlayerSide.PlayerB;
            Debug.Log($"[MatchBootstrapper] Randomly assigned {manager.State.FirstPlayer} to go first this game.");
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