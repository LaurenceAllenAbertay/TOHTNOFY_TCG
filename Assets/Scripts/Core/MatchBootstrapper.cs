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
        private static readonly List<LeaderData> EmptyLeaderList = new List<LeaderData>();

        [Header("Card Pool Setup")]
        [SerializeField] private CardDatabase cardDatabase;
        [SerializeField] private DraftSettings draftSettings = new DraftSettings();

        [Header("Game Mode")]
        [SerializeField] private GameMode fallbackGameMode = GameMode.Draft;

        public IReadOnlyList<CardData> CardPool => cardDatabase != null ? cardDatabase.AllCards : EmptyCardList;
        public IReadOnlyList<LeaderData> LeaderPool => cardDatabase != null ? cardDatabase.AllLeaders : EmptyLeaderList;

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

            AssignRandomFirstPlayer();

            GameMode effectiveMode = ConstructedMatchSync.ReadSelectedMode(fallbackGameMode);
            Debug.Log($"[MatchBootstrapper] Starting match in {effectiveMode} mode.");

            if (effectiveMode == GameMode.Constructed)
            {
                StartConstructedMatch();
            }
            else if (effectiveMode == GameMode.RandomDeck)
            {
                AssignRandomLeaders();
                StartRandomDeckMatch();
            }
            else if (effectiveMode == GameMode.VsAI)
            {
                StartVsAIMatch();
            }
            else
            {
                AssignRandomLeaders();
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

                    if (ConstructedMatchSync.TryReadDeck(photonPlayer, cardDatabase, out List<CardData> syncedDeck, out LeaderData syncedLeader))
                    {
                        player.Deck.AddRange(syncedDeck);
                        player.Leader = syncedLeader != null ? syncedLeader : PickRandomLeader();
                        appliedAnyRemoteDeck = true;
                        Debug.Log($"[MatchBootstrapper] Loaded {syncedDeck.Count}-card Constructed deck and leader '{(player.Leader != null ? player.Leader.LeaderName : "none")}' for {side} from actor {photonPlayer.ActorNumber}.");
                    }
                    else
                    {
                        player.Leader = PickRandomLeader();
                        Debug.LogWarning($"[MatchBootstrapper] No Constructed deck found for actor {photonPlayer.ActorNumber} ({side}) - that player will have an empty deck and a random leader.");
                    }
                }
            }

            if (!appliedAnyRemoteDeck)
            {
                List<CardData> localDeck = DeckStorage.LoadActiveDeckCards(cardDatabase);
                Debug.LogWarning($"[MatchBootstrapper] Constructed mode outside a synced Photon room - applying the local active deck ({localDeck.Count} cards) to both sides for local testing.");

                manager.State.PlayerA.Deck.AddRange(localDeck);
                manager.State.PlayerB.Deck.AddRange(new List<CardData>(localDeck));

                Debug.Log($"[MatchBootstrapper] StartConstructedMatch: using cardDatabase={(cardDatabase != null ? cardDatabase.name : "NULL")}.");

                bool foundLeader = DeckStorage.TryLoadActiveDeckLeader(cardDatabase, out LeaderData localLeader);
                Debug.Log($"[MatchBootstrapper] StartConstructedMatch: TryLoadActiveDeckLeader returned foundLeader={foundLeader}, localLeader={(localLeader != null ? localLeader.LeaderName : "NULL")}.");

                manager.State.PlayerA.Leader = localLeader != null ? localLeader : PickRandomLeader();
                manager.State.PlayerB.Leader = PickRandomLeader();

                Debug.Log($"[MatchBootstrapper] Local testing leaders - PlayerA='{(manager.State.PlayerA.Leader != null ? manager.State.PlayerA.Leader.LeaderName : "none")}', PlayerB='{(manager.State.PlayerB.Leader != null ? manager.State.PlayerB.Leader.LeaderName : "none")}'.");
            }

            ListShuffler.Shuffle(manager.State.PlayerA.Deck);
            ListShuffler.Shuffle(manager.State.PlayerB.Deck);

            manager.State.ActivePlayer = manager.State.FirstPlayer;

            manager.Phases.StartMatch();
        }

        private void StartRandomDeckMatch()
        {
            manager.State.PlayerA.Deck.AddRange(BuildRandomDeck());
            manager.State.PlayerB.Deck.AddRange(BuildRandomDeck());

            ListShuffler.Shuffle(manager.State.PlayerA.Deck);
            ListShuffler.Shuffle(manager.State.PlayerB.Deck);

            Debug.Log($"[MatchBootstrapper] Random Deck mode - built PlayerA {manager.State.PlayerA.Deck.Count}-card deck and PlayerB {manager.State.PlayerB.Deck.Count}-card deck from the full card pool.");

            manager.State.ActivePlayer = manager.State.FirstPlayer;

            manager.Phases.StartMatch();
        }

        private List<CardData> BuildRandomDeck()
        {
            int maxCopies = cardDatabase != null ? cardDatabase.MaxCopiesPerCard : 0;
            int targetSize = cardDatabase != null ? cardDatabase.TargetDeckSize : 0;

            List<CardData> pool = new List<CardData>();

            foreach (CardData card in CardPool)
            {
                for (int i = 0; i < maxCopies; i++)
                {
                    pool.Add(card);
                }
            }

            ListShuffler.Shuffle(pool);

            if (pool.Count < targetSize)
            {
                Debug.LogWarning($"[MatchBootstrapper] BuildRandomDeck() - pool only produced {pool.Count} card(s) (target is {targetSize}). The Card Database may not have enough unique cards x Max Copies Per Card to fill a deck - using all {pool.Count} available.");
                return pool;
            }

            return pool.GetRange(0, targetSize);
        }

        private void StartVsAIMatch()
        {
            Debug.Log($"[MatchBootstrapper] StartVsAIMatch: using cardDatabase={(cardDatabase != null ? cardDatabase.name : "NULL")}.");

            List<CardData> humanDeck = DeckStorage.LoadActiveDeckCards(cardDatabase);
            manager.State.PlayerA.Deck.AddRange(humanDeck);

            bool foundLeader = DeckStorage.TryLoadActiveDeckLeader(cardDatabase, out LeaderData humanLeader);
            Debug.Log($"[MatchBootstrapper] StartVsAIMatch: TryLoadActiveDeckLeader returned foundLeader={foundLeader}, humanLeader={(humanLeader != null ? humanLeader.LeaderName : "NULL")}.");

            manager.State.PlayerA.Leader = humanLeader != null ? humanLeader : PickRandomLeader();

            List<CardData> aiDeck = BuildDraftLegalRandomDeck();
            manager.State.PlayerB.Deck.AddRange(aiDeck);
            manager.State.PlayerB.Leader = PickRandomLeader();

            Debug.Log($"[MatchBootstrapper] Vs AI match - human loaded their {humanDeck.Count}-card Constructed deck with leader '{(manager.State.PlayerA.Leader != null ? manager.State.PlayerA.Leader.LeaderName : "none")}'; AI given a random {aiDeck.Count}-card draft-legal deck with leader '{(manager.State.PlayerB.Leader != null ? manager.State.PlayerB.Leader.LeaderName : "none")}'.");

            ListShuffler.Shuffle(manager.State.PlayerA.Deck);
            ListShuffler.Shuffle(manager.State.PlayerB.Deck);

            manager.State.ActivePlayer = manager.State.FirstPlayer;

            AIController aiController = GetComponent<AIController>();

            if (aiController != null)
            {
                aiController.EnableAIControl(PlayerSide.PlayerB);
            }
            else
            {
                Debug.LogWarning("[MatchBootstrapper] Vs AI match started but no AIController component was found on this GameObject - PlayerB will not act.");
            }

            TurnTimerController turnTimer = GetComponent<TurnTimerController>();

            if (turnTimer != null)
            {
                turnTimer.enabled = false;
                Debug.Log($"[MatchBootstrapper] Vs AI match - disabled TurnTimerController on GameObject '{turnTimer.gameObject.name}' (instanceId={turnTimer.GetInstanceID()}, enabled now={turnTimer.enabled}). No draft/mulligan/turn timer against a solo AI opponent.");
            }
            else
            {
                Debug.LogWarning("[MatchBootstrapper] Vs AI match started but no TurnTimerController component was found on this GameObject.");
            }

            manager.Phases.StartMatch();
        }

        private List<CardData> BuildDraftLegalRandomDeck()
        {
            List<CardData> deck = new List<CardData>();

            AddRandomPicksForStage(deck, DraftStage.Common, draftSettings.commonPicks, draftSettings.copiesPerCommonPick);
            AddRandomPicksForStage(deck, DraftStage.Uncommon, draftSettings.uncommonPicks, draftSettings.copiesPerUncommonPick);
            AddRandomPicksForStage(deck, DraftStage.Rare, draftSettings.rarePicks, draftSettings.copiesPerRarePick);
            AddRandomPicksForStage(deck, DraftStage.EpicOrLegendary, draftSettings.epicOrLegendaryPicks, draftSettings.copiesPerEpicOrLegendaryPick);

            return deck;
        }

        private void AddRandomPicksForStage(List<CardData> deck, DraftStage stage, int pickCount, int copiesPerPick)
        {
            for (int pick = 0; pick < pickCount; pick++)
            {
                List<CardData> eligible = new List<CardData>();

                foreach (CardData card in CardPool)
                {
                    if (!PhaseManager.MatchesDraftStage(card.Rarity, stage))
                    {
                        continue;
                    }

                    int copiesAlready = deck.FindAll(c => c == card).Count;

                    if (copiesAlready >= draftSettings.maxCopiesPerCard)
                    {
                        continue;
                    }

                    eligible.Add(card);
                }

                if (eligible.Count == 0)
                {
                    Debug.LogWarning($"[MatchBootstrapper] BuildDraftLegalRandomDeck: no eligible {stage} cards left for pick {pick + 1}/{pickCount} - skipping this pick.");
                    continue;
                }

                CardData chosen = eligible[Random.Range(0, eligible.Count)];

                for (int copy = 0; copy < copiesPerPick; copy++)
                {
                    deck.Add(chosen);
                }
            }
        }

        private void AssignRandomFirstPlayer()
        {
            manager.State.FirstPlayer = Random.Range(0, 2) == 0 ? PlayerSide.PlayerA : PlayerSide.PlayerB;
            Debug.Log($"[MatchBootstrapper] Randomly assigned {manager.State.FirstPlayer} to go first this game.");
        }

        private void AssignRandomLeaders()
        {
            if (LeaderPool.Count == 0)
            {
                return;
            }

            manager.State.PlayerA.Leader = PickRandomLeader();
            manager.State.PlayerB.Leader = PickRandomLeader();
        }

        private LeaderData PickRandomLeader()
        {
            if (LeaderPool.Count == 0)
            {
                return null;
            }

            return LeaderPool[Random.Range(0, LeaderPool.Count)];
        }
    }
}