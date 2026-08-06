using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DDD.TNFY.TCG.Cards;
using DDD.TNFY.TCG.DeckBuilding;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using GameMode = DDD.TNFY.TCG.Core.GameMode;

namespace DDD.TNFY.TCG.Networking
{
    public class MatchmakingController : MonoBehaviourPunCallbacks
    {
        private const string NicknamePrefsKey = "SavedPlayerNickname";
        private const byte MaxPlayersPerRoom = 2;
        private const string GameModeRoomPropertyKey = "gm";

        private static readonly GameMode[] DropdownModeOrder =
        {
            GameMode.Draft,
            GameMode.RandomDeck,
            GameMode.Constructed
        };

        private static readonly List<string> DropdownModeLabels = new List<string>
        {
            "Draft",
            "Random Deck",
            "Constructed"
        };

        [SerializeField] private TMP_InputField nameInputField;
        [SerializeField] private Button findMatchButton;
        [SerializeField] private Button cancelMatchmakingButton;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private string gameSceneName = "Game";
        [SerializeField] private CardDatabase cardDatabase;
        [SerializeField] private TMP_Dropdown gameModeDropdown;

        private bool cancelRequested;
        private GameMode pendingGameMode;

        private void Awake()
        {
            PhotonNetwork.AutomaticallySyncScene = true;
            findMatchButton.interactable = false;
            findMatchButton.onClick.AddListener(HandleFindMatchClicked);

            if (cancelMatchmakingButton != null)
            {
                cancelMatchmakingButton.gameObject.SetActive(false);
                cancelMatchmakingButton.onClick.AddListener(HandleCancelMatchmakingClicked);
            }

            if (PlayerPrefs.HasKey(NicknamePrefsKey))
            {
                nameInputField.text = PlayerPrefs.GetString(NicknamePrefsKey);
            }

            if (gameModeDropdown != null)
            {
                gameModeDropdown.ClearOptions();
                gameModeDropdown.AddOptions(DropdownModeLabels);
            }
        }

        private void Start()
        {
            SetStatus("Connecting...");

            if (PhotonNetwork.IsConnectedAndReady)
            {
                findMatchButton.interactable = true;
                SetStatus("Ready.");
            }
            else
            {
                PhotonNetwork.ConnectUsingSettings();
            }
        }

        public override void OnConnectedToMaster()
        {
            Debug.Log("[MatchmakingController] OnConnectedToMaster.");
            findMatchButton.interactable = true;
            SetStatus("Ready.");
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            Debug.LogWarning($"[MatchmakingController] OnDisconnected - cause: {cause}");
            findMatchButton.interactable = false;
            SetStatus($"Disconnected ({cause}). Reconnecting...");
            PhotonNetwork.ConnectUsingSettings();
        }

        private void HandleFindMatchClicked()
        {
            GameMode selectedMode = ReadSelectedModeFromDropdown();

            Debug.Log($"[MatchmakingController] HandleFindMatchClicked() - selectedMode={selectedMode}, gameModeDropdown.value={(gameModeDropdown != null ? gameModeDropdown.value.ToString() : "null dropdown ref")}.");

            if (selectedMode == GameMode.Constructed && !ActiveDeckIsQueueReady())
            {
                return;
            }

            string chosenName = string.IsNullOrWhiteSpace(nameInputField.text)
                ? "Player" + Random.Range(1000, 9999)
                : nameInputField.text.Trim();

            PlayerPrefs.SetString(NicknamePrefsKey, chosenName);
            PhotonNetwork.NickName = chosenName;

            ConstructedMatchSync.PublishSelection(selectedMode, cardDatabase);

            cancelRequested = false;
            pendingGameMode = selectedMode;
            findMatchButton.interactable = false;
            nameInputField.interactable = false;
            SetStatus("Searching for an opponent...");

            if (cancelMatchmakingButton != null)
            {
                cancelMatchmakingButton.gameObject.SetActive(true);
                cancelMatchmakingButton.interactable = true;
            }

            RoomOptions roomOptions = BuildRoomOptions(selectedMode);

            Debug.Log($"[MatchmakingController] Attempting JoinRandomOrCreateRoom as '{chosenName}' - filtering by {GameModeRoomPropertyKey}={selectedMode}.");
            PhotonNetwork.JoinRandomOrCreateRoom(
                expectedCustomRoomProperties: roomOptions.CustomRoomProperties,
                expectedMaxPlayers: MaxPlayersPerRoom,
                roomOptions: roomOptions);
        }

        private GameMode ReadSelectedModeFromDropdown()
        {
            if (gameModeDropdown == null)
            {
                return GameMode.Draft;
            }

            int index = gameModeDropdown.value;

            if (index < 0 || index >= DropdownModeOrder.Length)
            {
                Debug.LogWarning($"[MatchmakingController] gameModeDropdown.value ({index}) is out of range for DropdownModeOrder - defaulting to Draft.");
                return GameMode.Draft;
            }

            return DropdownModeOrder[index];
        }

        private RoomOptions BuildRoomOptions(GameMode mode)
        {
            Hashtable modeProperties = new Hashtable { { GameModeRoomPropertyKey, mode.ToString() } };

            return new RoomOptions
            {
                MaxPlayers = MaxPlayersPerRoom,
                CustomRoomProperties = modeProperties,
                CustomRoomPropertiesForLobby = new[] { GameModeRoomPropertyKey }
            };
        }

        private bool ActiveDeckIsQueueReady()
        {
            int targetSize = cardDatabase != null ? cardDatabase.TargetDeckSize : 0;
            int activeSize = DeckStorage.LoadActiveDeckCards(cardDatabase).Count;

            if (activeSize == targetSize)
            {
                Debug.Log($"[MatchmakingController] ActiveDeckIsQueueReady() passed - active deck has {activeSize} card(s).");
                return true;
            }

            Debug.LogWarning($"[MatchmakingController] Blocked matchmaking - active Constructed deck has {activeSize} card(s), needs exactly {targetSize}.");
            SetStatus($"Your deck needs exactly {targetSize} cards to queue (currently {activeSize}).");
            return false;
        }

        private void HandleCancelMatchmakingClicked()
        {
            Debug.Log($"[MatchmakingController] Cancel requested. InRoom={PhotonNetwork.InRoom}.");

            cancelRequested = true;

            if (cancelMatchmakingButton != null)
            {
                cancelMatchmakingButton.interactable = false;
            }

            if (PhotonNetwork.InRoom)
            {
                SetStatus("Cancelling...");
                PhotonNetwork.LeaveRoom();
            }
            else
            {
                SetStatus("Cancelling...");
            }
        }

        public override void OnJoinRandomFailed(short returnCode, string message)
        {
            if (cancelRequested)
            {
                Debug.Log("[MatchmakingController] OnJoinRandomFailed - cancel was requested, not creating a room.");
                ResetToReadyState();
                return;
            }

            Debug.Log($"[MatchmakingController] OnJoinRandomFailed (no open room found) - creating one for mode={pendingGameMode}. message={message}");

            PhotonNetwork.CreateRoom(null, BuildRoomOptions(pendingGameMode));
        }

        public override void OnJoinedRoom()
        {
            Debug.Log($"[MatchmakingController] OnJoinedRoom - '{PhotonNetwork.CurrentRoom.Name}', PlayerCount={PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers}.");

            if (cancelRequested)
            {
                Debug.Log("[MatchmakingController] OnJoinedRoom - cancel was requested, leaving immediately.");
                PhotonNetwork.LeaveRoom();
                return;
            }

            if (PhotonNetwork.CurrentRoom.PlayerCount >= MaxPlayersPerRoom)
            {
                TryStartMatch();
            }
            else
            {
                SetStatus("Waiting for an opponent...");
            }
        }

        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            Debug.Log($"[MatchmakingController] OnPlayerEnteredRoom - '{newPlayer.NickName}' joined. PlayerCount={PhotonNetwork.CurrentRoom.PlayerCount}.");

            if (PhotonNetwork.CurrentRoom.PlayerCount >= MaxPlayersPerRoom)
            {
                TryStartMatch();
            }
        }

        public override void OnLeftRoom()
        {
            Debug.Log("[MatchmakingController] OnLeftRoom.");

            if (cancelRequested)
            {
                ResetToReadyState();
            }
        }

        private void TryStartMatch()
        {
            SetStatus("Opponent found! Loading match...");

            if (cancelMatchmakingButton != null)
            {
                cancelMatchmakingButton.interactable = false;
            }

            if (!PhotonNetwork.IsMasterClient)
            {
                return;
            }

            Debug.Log($"[MatchmakingController] MasterClient loading '{gameSceneName}' for the room.");
            PhotonNetwork.LoadLevel(gameSceneName);
        }

        private void ResetToReadyState()
        {
            cancelRequested = false;
            nameInputField.interactable = true;
            findMatchButton.interactable = true;

            if (cancelMatchmakingButton != null)
            {
                cancelMatchmakingButton.gameObject.SetActive(false);
            }

            SetStatus("Ready.");
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }
    }
}