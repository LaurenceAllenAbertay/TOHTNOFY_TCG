using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DDD.TNFY.TCG.Cards;
using DDD.TNFY.TCG.Core;
using DDD.TNFY.TCG.UI;

namespace DDD.TNFY.TCG.DeckBuilding
{
    public class DeckBuilderPanel : MonoBehaviour
    {
        [System.Serializable]
        private struct RarityFilterButton
        {
            public CardRarity rarity;
            public Button button;
            public Image selectedIndicator;
        }

        [System.Serializable]
        private struct LeaderSelectButton
        {
            public LeaderData leader;
            public Button button;
            public Image selectedIndicator;
            public LeaderPreviewHover previewHover;
        }

        [Header("Data")]
        [SerializeField] private CardDatabase cardDatabase;

        [Header("Panel")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button backButton;

        [Header("Collection Grid")]
        [SerializeField] private Transform collectionContainer;
        [SerializeField] private DeckBuilderCardEntry collectionCardPrefab;
        [SerializeField] private TMP_InputField searchInput;
        [SerializeField] private TMP_Dropdown sortByDropdown;

        [Header("Leader Selection")]
        [SerializeField] private List<LeaderSelectButton> leaderSelectButtons = new List<LeaderSelectButton>();

        [Header("Type Filter")]
        [SerializeField] private Button typeFilterAnyButton;
        [SerializeField] private Image typeFilterAnySelectedIndicator;
        [SerializeField] private Button typeFilterUnitButton;
        [SerializeField] private Image typeFilterUnitSelectedIndicator;
        [SerializeField] private Button typeFilterItemButton;
        [SerializeField] private Image typeFilterItemSelectedIndicator;

        [Header("Rarity Filter")]
        [SerializeField] private List<RarityFilterButton> rarityFilterButtons = new List<RarityFilterButton>();

        [Header("Current Deck")]
        [SerializeField] private TMP_Dropdown deckSlotDropdown;
        [SerializeField] private TMP_InputField deckNameInput;
        [SerializeField] private TextMeshProUGUI cardCounterText;
        [SerializeField] private Transform deckListContainer;
        [SerializeField] private DeckListEntryView deckListEntryPrefab;
        [SerializeField] private Button saveDeckButton;
        [SerializeField] private Button clearDeckButton;

        private readonly List<SavedDeck> savedDecks = new List<SavedDeck>();
        private readonly HashSet<CardRarity> activeRarityFilters = new HashSet<CardRarity>();
        private readonly List<DeckBuilderCardEntry> spawnedCollectionEntries = new List<DeckBuilderCardEntry>();
        private readonly List<DeckListEntryView> spawnedDeckEntries = new List<DeckListEntryView>();

        private int activeDeckIndex;
        private CardCategory activeTypeFilter = CardCategory.Any;
        private DeckBuilderSortMode sortMode = DeckBuilderSortMode.ManaCost;

        private SavedDeck ActiveDeck => savedDecks[activeDeckIndex];

        private void Awake()
        {
            if (backButton != null)
            {
                backButton.onClick.AddListener(Close);
            }

            if (searchInput != null)
            {
                searchInput.onValueChanged.AddListener(_ => RefreshCollectionGrid());
            }

            if (sortByDropdown != null)
            {
                sortByDropdown.ClearOptions();
                sortByDropdown.AddOptions(new List<string> { "Mana Cost", "Name", "Rarity" });
                sortByDropdown.onValueChanged.AddListener(HandleSortChanged);
            }

            for (int i = 0; i < leaderSelectButtons.Count; i++)
            {
                LeaderSelectButton entry = leaderSelectButtons[i];

                if (entry.button == null || entry.leader == null)
                {
                    continue;
                }

                LeaderData capturedLeader = entry.leader;
                entry.button.onClick.AddListener(() => HandleLeaderClicked(capturedLeader));

                if (entry.previewHover != null)
                {
                    entry.previewHover.SetLeader(entry.leader);
                }
            }

            if (typeFilterAnyButton != null)
            {
                typeFilterAnyButton.onClick.AddListener(() => HandleTypeFilterClicked(CardCategory.Any));
            }

            if (typeFilterUnitButton != null)
            {
                typeFilterUnitButton.onClick.AddListener(() => HandleTypeFilterClicked(CardCategory.Unit));
            }

            if (typeFilterItemButton != null)
            {
                typeFilterItemButton.onClick.AddListener(() => HandleTypeFilterClicked(CardCategory.Item));
            }

            for (int i = 0; i < rarityFilterButtons.Count; i++)
            {
                RarityFilterButton entry = rarityFilterButtons[i];

                if (entry.button == null)
                {
                    continue;
                }

                CardRarity capturedRarity = entry.rarity;
                entry.button.onClick.AddListener(() => HandleRarityFilterClicked(capturedRarity));
            }

            if (deckSlotDropdown != null)
            {
                deckSlotDropdown.onValueChanged.AddListener(HandleDeckSlotChanged);
            }

            if (deckNameInput != null)
            {
                deckNameInput.onEndEdit.AddListener(HandleDeckNameChanged);
            }

            if (saveDeckButton != null)
            {
                saveDeckButton.onClick.AddListener(HandleSaveDeck);
            }

            if (clearDeckButton != null)
            {
                clearDeckButton.onClick.AddListener(HandleClearDeck);
            }

            UpdateTypeFilterVisuals();
            UpdateRarityFilterVisuals();
        }

        public void Open()
        {
            Debug.Log("[DeckBuilderPanel] Open() start.");
            System.Diagnostics.Stopwatch openStopwatch = System.Diagnostics.Stopwatch.StartNew();

            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }

            Debug.Log("[DeckBuilderPanel] Calling LoadDecks().");
            LoadDecks();
            Debug.Log($"[DeckBuilderPanel] LoadDecks() done. savedDecks.Count={savedDecks.Count}, activeDeckIndex={activeDeckIndex}.");

            Debug.Log("[DeckBuilderPanel] Calling PopulateDeckSlotDropdown().");
            PopulateDeckSlotDropdown();
            Debug.Log("[DeckBuilderPanel] PopulateDeckSlotDropdown() done.");

            Debug.Log("[DeckBuilderPanel] Calling RefreshAll().");
            RefreshAll();

            openStopwatch.Stop();
            Debug.Log($"[DeckBuilderPanel] Open() complete. Total time {openStopwatch.ElapsedMilliseconds}ms.");
        }

        public void Close()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        private void LoadDecks()
        {
            savedDecks.Clear();

            Debug.Log("[DeckBuilderPanel] Calling DeckStorage.LoadAll().");
            List<SavedDeck> loaded = DeckStorage.LoadAll(cardDatabase);
            Debug.Log($"[DeckBuilderPanel] DeckStorage.LoadAll() returned {loaded.Count} deck(s).");
            savedDecks.AddRange(loaded);

            int fillIterations = 0;
            while (savedDecks.Count < DeckStorage.MaxDeckSlots)
            {
                fillIterations++;
                if (fillIterations > DeckStorage.MaxDeckSlots * 2)
                {
                    Debug.LogError("[DeckBuilderPanel] LoadDecks() fill loop exceeded expected iterations - breaking to avoid a hang.");
                    break;
                }

                savedDecks.Add(new SavedDeck { deckName = $"Deck {savedDecks.Count + 1}" });
            }

            activeDeckIndex = Mathf.Clamp(DeckStorage.GetActiveDeckIndex(), 0, savedDecks.Count - 1);

            for (int i = 0; i < savedDecks.Count; i++)
            {
                Debug.Log($"[DeckBuilderPanel] LoadDecks: slot {i} '{savedDecks[i].deckName}' leaderId='{savedDecks[i].leaderId}' (cardDatabase={(cardDatabase != null ? cardDatabase.name : "NULL")}).");
            }

            EnsureAllDecksHaveALeader();

            Debug.Log("[DeckBuilderPanel] LoadDecks() finished filling slots.");
        }

        private void EnsureAllDecksHaveALeader()
        {
            string defaultLeaderId = GetDefaultLeaderId();

            if (string.IsNullOrEmpty(defaultLeaderId))
            {
                Debug.LogWarning("[DeckBuilderPanel] EnsureAllDecksHaveALeader() - no default leader available from the CardDatabase or leader buttons, can't assign one.");
                return;
            }

            bool anyChanged = false;

            foreach (SavedDeck deck in savedDecks)
            {
                if (string.IsNullOrEmpty(deck.leaderId))
                {
                    Debug.Log($"[DeckBuilderPanel] '{deck.deckName}' had no leader - defaulting to leaderId='{defaultLeaderId}'.");
                    deck.leaderId = defaultLeaderId;
                    anyChanged = true;
                }
            }

            if (anyChanged)
            {
                Debug.Log("[DeckBuilderPanel] EnsureAllDecksHaveALeader() patched one or more decks - saving immediately so the fix isn't lost if the player never opens this panel again.");
                DeckStorage.SaveAll(savedDecks);
            }
        }

        private string GetDefaultLeaderId()
        {
            if (cardDatabase != null && cardDatabase.AllLeaders.Count > 0 && cardDatabase.AllLeaders[0] != null)
            {
                return cardDatabase.AllLeaders[0].LeaderId;
            }

            for (int i = 0; i < leaderSelectButtons.Count; i++)
            {
                if (leaderSelectButtons[i].leader != null)
                {
                    return leaderSelectButtons[i].leader.LeaderId;
                }
            }

            return string.Empty;
        }

        private void PopulateDeckSlotDropdown()
        {
            if (deckSlotDropdown == null)
            {
                return;
            }

            deckSlotDropdown.ClearOptions();
            deckSlotDropdown.AddOptions(savedDecks.Select(GetDeckDisplayName).ToList());
            deckSlotDropdown.SetValueWithoutNotify(activeDeckIndex);
        }

        private string GetDeckDisplayName(SavedDeck deck)
        {
            if (deck == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(deck.leaderId) && cardDatabase != null && cardDatabase.TryGetLeader(deck.leaderId, out LeaderData leader) && leader != null)
            {
                return $"{leader.LeaderName}: {deck.deckName}";
            }

            return deck.deckName;
        }

        private void RefreshDeckSlotDropdownOptionText()
        {
            if (deckSlotDropdown == null || activeDeckIndex < 0 || activeDeckIndex >= deckSlotDropdown.options.Count)
            {
                return;
            }

            deckSlotDropdown.options[activeDeckIndex].text = GetDeckDisplayName(ActiveDeck);
            deckSlotDropdown.RefreshShownValue();
        }

        private void RefreshAll()
        {
            if (deckNameInput != null)
            {
                deckNameInput.text = ActiveDeck.deckName;
            }

            RefreshCollectionGrid();
            RefreshLeaderSelection();
            RefreshDeckList();
        }

        private void HandleSortChanged(int index)
        {
            sortMode = (DeckBuilderSortMode)index;
            RefreshCollectionGrid();
        }

        private void HandleTypeFilterClicked(CardCategory category)
        {
            activeTypeFilter = category;
            UpdateTypeFilterVisuals();
            RefreshCollectionGrid();
        }

        private void HandleRarityFilterClicked(CardRarity rarity)
        {
            if (!activeRarityFilters.Remove(rarity))
            {
                activeRarityFilters.Add(rarity);
            }

            UpdateRarityFilterVisuals();
            RefreshCollectionGrid();
        }

        private void UpdateTypeFilterVisuals()
        {
            if (typeFilterAnySelectedIndicator != null)
            {
                typeFilterAnySelectedIndicator.enabled = activeTypeFilter == CardCategory.Any;
            }

            if (typeFilterUnitSelectedIndicator != null)
            {
                typeFilterUnitSelectedIndicator.enabled = activeTypeFilter == CardCategory.Unit;
            }

            if (typeFilterItemSelectedIndicator != null)
            {
                typeFilterItemSelectedIndicator.enabled = activeTypeFilter == CardCategory.Item;
            }
        }

        private void UpdateRarityFilterVisuals()
        {
            for (int i = 0; i < rarityFilterButtons.Count; i++)
            {
                RarityFilterButton entry = rarityFilterButtons[i];

                if (entry.selectedIndicator != null)
                {
                    entry.selectedIndicator.enabled = activeRarityFilters.Contains(entry.rarity);
                }
            }
        }

        private void HandleDeckSlotChanged(int index)
        {
            activeDeckIndex = index;
            DeckStorage.SetActiveDeckIndex(activeDeckIndex);

            if (deckNameInput != null)
            {
                deckNameInput.text = ActiveDeck.deckName;
            }

            RefreshCollectionGrid();
            RefreshLeaderSelection();
            RefreshDeckList();
        }

        private void HandleDeckNameChanged(string newName)
        {
            ActiveDeck.deckName = string.IsNullOrWhiteSpace(newName) ? ActiveDeck.deckName : newName.Trim();
            RefreshDeckSlotDropdownOptionText();
        }

        private void HandleSaveDeck()
        {
            DeckStorage.SaveAll(savedDecks);
            DeckStorage.SetActiveDeckIndex(activeDeckIndex);

            Debug.Log($"[DeckBuilderPanel] Saved '{ActiveDeck.deckName}' with {GetTotalCardCount()} card(s) as the active deck (slot {activeDeckIndex}).");
        }

        private void HandleClearDeck()
        {
            ActiveDeck.cards.Clear();

            Debug.Log($"[DeckBuilderPanel] Cleared '{ActiveDeck.deckName}'.");

            RefreshCollectionGrid();
            RefreshDeckList();
        }

        private void HandleAddCard(DeckBuilderCardEntry entry)
        {
            if (entry.Card == null)
            {
                return;
            }

            int currentCount = GetCountInActiveDeck(entry.Card.CardId);

            if (currentCount >= cardDatabase.MaxCopiesPerCard)
            {
                return;
            }

            SavedDeckCardEntry existing = ActiveDeck.cards.Find(c => c.cardId == entry.Card.CardId);

            if (existing != null)
            {
                existing.count++;
            }
            else
            {
                ActiveDeck.cards.Add(new SavedDeckCardEntry { cardId = entry.Card.CardId, count = 1 });
            }

            RefreshCollectionGrid();
            RefreshDeckList();
        }

        private void HandleRemoveCard(DeckListEntryView row)
        {
            if (row.Card == null)
            {
                return;
            }

            SavedDeckCardEntry existing = ActiveDeck.cards.Find(c => c.cardId == row.Card.CardId);

            if (existing == null)
            {
                return;
            }

            existing.count--;

            if (existing.count <= 0)
            {
                ActiveDeck.cards.Remove(existing);
            }

            RefreshCollectionGrid();
            RefreshDeckList();
        }

        private int GetCountInActiveDeck(string cardId)
        {
            SavedDeckCardEntry existing = ActiveDeck.cards.Find(c => c.cardId == cardId);
            return existing != null ? existing.count : 0;
        }

        private int GetTotalCardCount()
        {
            int total = 0;

            foreach (SavedDeckCardEntry entry in ActiveDeck.cards)
            {
                total += entry.count;
            }

            return total;
        }

        private void RefreshCollectionGrid()
        {
            Debug.Log("[DeckBuilderPanel] RefreshCollectionGrid() start.");
            ClearSpawnedCollectionEntries();
            Debug.Log("[DeckBuilderPanel] ClearSpawnedCollectionEntries() done.");

            if (cardDatabase == null || collectionContainer == null || collectionCardPrefab == null)
            {
                Debug.LogWarning($"[DeckBuilderPanel] RefreshCollectionGrid() aborting - missing reference. cardDatabase={cardDatabase}, collectionContainer={collectionContainer}, collectionCardPrefab={collectionCardPrefab}.");
                return;
            }

            Debug.Log($"[DeckBuilderPanel] cardDatabase.AllCards.Count={cardDatabase.AllCards.Count} before filtering.");

            List<CardData> filtered = cardDatabase.AllCards
                .Where(card => card != null)
                .Where(card => PhaseManager.MatchesCardCategory(card, activeTypeFilter))
                .Where(card => activeRarityFilters.Count == 0 || activeRarityFilters.Contains(card.Rarity))
                .Where(MatchesSearch)
                .ToList();

            Debug.Log($"[DeckBuilderPanel] filtered.Count={filtered.Count} after filtering. Sorting now.");

            SortCards(filtered);

            Debug.Log("[DeckBuilderPanel] Sorting done. Starting instantiate loop.");

            System.Diagnostics.Stopwatch spawnStopwatch = System.Diagnostics.Stopwatch.StartNew();

            int spawnedCount = 0;
            foreach (CardData card in filtered)
            {
                DeckBuilderCardEntry entry = Instantiate(collectionCardPrefab, collectionContainer);
                entry.Bind(card);
                entry.SetOwnedCount(GetCountInActiveDeck(card.CardId), cardDatabase.MaxCopiesPerCard);
                entry.AddClicked += HandleAddCard;
                spawnedCollectionEntries.Add(entry);
                spawnedCount++;
            }

            spawnStopwatch.Stop();

            Debug.Log($"[DeckBuilderPanel] RefreshCollectionGrid() complete. Spawned {spawnedCount} entries in {spawnStopwatch.ElapsedMilliseconds}ms.");
        }

        private bool MatchesSearch(CardData card)
        {
            if (searchInput == null || string.IsNullOrWhiteSpace(searchInput.text))
            {
                return true;
            }

            return card.CardName.IndexOf(searchInput.text, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void SortCards(List<CardData> cards)
        {
            switch (sortMode)
            {
                case DeckBuilderSortMode.Name:
                    cards.Sort((a, b) => string.CompareOrdinal(a.CardName, b.CardName));
                    break;

                case DeckBuilderSortMode.Rarity:
                    cards.Sort((a, b) =>
                    {
                        int rarityCompare = ((int)a.Rarity).CompareTo((int)b.Rarity);
                        return rarityCompare != 0 ? rarityCompare : string.CompareOrdinal(a.CardName, b.CardName);
                    });
                    break;

                default:
                    cards.Sort((a, b) =>
                    {
                        int costCompare = a.ManaCost.CompareTo(b.ManaCost);
                        return costCompare != 0 ? costCompare : string.CompareOrdinal(a.CardName, b.CardName);
                    });
                    break;
            }
        }

        private void RefreshDeckList()
        {
            Debug.Log("[DeckBuilderPanel] RefreshDeckList() start.");
            ClearSpawnedDeckEntries();
            Debug.Log("[DeckBuilderPanel] ClearSpawnedDeckEntries() done.");

            if (deckListContainer == null || deckListEntryPrefab == null || cardDatabase == null)
            {
                Debug.LogWarning($"[DeckBuilderPanel] RefreshDeckList() aborting - missing reference. deckListContainer={deckListContainer}, deckListEntryPrefab={deckListEntryPrefab}, cardDatabase={cardDatabase}.");
                UpdateCardCounter();
                return;
            }

            Debug.Log($"[DeckBuilderPanel] ActiveDeck.cards.Count={ActiveDeck.cards.Count} before sort.");

            List<SavedDeckCardEntry> sortedEntries = new List<SavedDeckCardEntry>(ActiveDeck.cards);
            sortedEntries.Sort((a, b) =>
            {
                cardDatabase.TryGetCard(a.cardId, out CardData cardA);
                cardDatabase.TryGetCard(b.cardId, out CardData cardB);

                if (cardA == null || cardB == null)
                {
                    return 0;
                }

                int costCompare = cardA.ManaCost.CompareTo(cardB.ManaCost);
                return costCompare != 0 ? costCompare : string.CompareOrdinal(cardA.CardName, cardB.CardName);
            });

            Debug.Log("[DeckBuilderPanel] Sort done. Starting instantiate loop.");

            int spawnedCount = 0;
            foreach (SavedDeckCardEntry deckEntry in sortedEntries)
            {
                if (!cardDatabase.TryGetCard(deckEntry.cardId, out CardData card))
                {
                    Debug.LogWarning($"[DeckBuilderPanel] '{ActiveDeck.deckName}' references unknown cardId '{deckEntry.cardId}' - skipping in the list display.");
                    continue;
                }

                DeckListEntryView row = Instantiate(deckListEntryPrefab, deckListContainer);
                row.Bind(card, deckEntry.count);
                row.RemoveClicked += HandleRemoveCard;
                spawnedDeckEntries.Add(row);
                spawnedCount++;
            }

            Debug.Log($"[DeckBuilderPanel] RefreshDeckList() complete. Spawned {spawnedCount} entries.");

            UpdateCardCounter();
        }

        private void UpdateCardCounter()
        {
            if (cardCounterText == null)
            {
                return;
            }

            int target = cardDatabase != null ? cardDatabase.TargetDeckSize : 0;
            cardCounterText.text = $"{GetTotalCardCount()} / {target} CARDS";
        }

        private void ClearSpawnedCollectionEntries()
        {
            foreach (DeckBuilderCardEntry entry in spawnedCollectionEntries)
            {
                if (entry != null)
                {
                    entry.AddClicked -= HandleAddCard;
                    Destroy(entry.gameObject);
                }
            }

            spawnedCollectionEntries.Clear();
        }

        private void ClearSpawnedDeckEntries()
        {
            foreach (DeckListEntryView entry in spawnedDeckEntries)
            {
                if (entry != null)
                {
                    entry.RemoveClicked -= HandleRemoveCard;
                    Destroy(entry.gameObject);
                }
            }

            spawnedDeckEntries.Clear();
        }

        private void RefreshLeaderSelection()
        {
            UpdateLeaderSelectionVisuals();
        }

        private void UpdateLeaderSelectionVisuals()
        {
            for (int i = 0; i < leaderSelectButtons.Count; i++)
            {
                LeaderSelectButton entry = leaderSelectButtons[i];

                if (entry.selectedIndicator != null)
                {
                    entry.selectedIndicator.enabled = entry.leader != null && entry.leader.LeaderId == ActiveDeck.leaderId;
                }
            }
        }

        private void HandleLeaderClicked(LeaderData leader)
        {
            if (leader == null)
            {
                return;
            }

            ActiveDeck.leaderId = leader.LeaderId;
            UpdateLeaderSelectionVisuals();
            RefreshDeckSlotDropdownOptionText();

            Debug.Log($"[DeckBuilderPanel] Selected leader '{leader.LeaderName}' for '{ActiveDeck.deckName}'.");
        }
    }
}