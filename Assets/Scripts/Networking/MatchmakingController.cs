using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
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

        [Header("Nickname Prompt")]
        [SerializeField] private GameObject nicknamePanel;
        [SerializeField, FormerlySerializedAs("nameInputField")] private TMP_InputField nicknameInputField;
        [SerializeField] private Button confirmNicknameButton;
        [SerializeField] private Button changeNicknameButton;

        [Header("Main Menu")]
        [SerializeField] private TextMeshProUGUI welcomeText;
        [SerializeField] private Button quitButton;

        [Header("Game Mode Panel")]
        [SerializeField] private GameObject gameModePanel;
        [SerializeField] private Button draftModeButton;
        [SerializeField] private Button randomDeckModeButton;
        [SerializeField] private Button constructedModeButton;

        [Header("Matchmaking")]
        [SerializeField, FormerlySerializedAs("findMatchButton")] private Button playButton;
        [SerializeField] private Button cancelMatchmakingButton;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Image connectionStatusIcon;
        [SerializeField] private Sprite connectingIconSprite;
        [SerializeField] private Sprite readyIconSprite;
        [SerializeField] private string gameSceneName = "Game";
        [SerializeField] private CardDatabase cardDatabase;

        private bool cancelRequested;
        private GameMode pendingGameMode;

        private void Awake()
        {
            PhotonNetwork.AutomaticallySyncScene = true;

            playButton.interactable = false;
            playButton.onClick.AddListener(HandlePlayClicked);

            if (cancelMatchmakingButton != null)
            {
                cancelMatchmakingButton.gameObject.SetActive(false);
                cancelMatchmakingButton.onClick.AddListener(HandleCancelMatchmakingClicked);
            }

            if (gameModePanel != null)
            {
                gameModePanel.SetActive(false);
            }

            draftModeButton.onClick.AddListener(() => HandleGameModeSelected(GameMode.Draft));
            randomDeckModeButton.onClick.AddListener(() => HandleGameModeSelected(GameMode.RandomDeck));
            constructedModeButton.onClick.AddListener(() => HandleGameModeSelected(GameMode.Constructed));

            confirmNicknameButton.onClick.AddListener(HandleConfirmNicknameClicked);
            changeNicknameButton.onClick.AddListener(HandleChangeNicknameClicked);
            quitButton.onClick.AddListener(HandleQuitClicked);

            if (HasSavedNickname())
            {
                nicknameInputField.text = PlayerPrefs.GetString(NicknamePrefsKey);
                PhotonNetwork.NickName = nicknameInputField.text;
                UpdateWelcomeText(nicknameInputField.text);
                nicknamePanel.SetActive(false);
            }
            else
            {
                Debug.Log("[MatchmakingController] No saved nickname found - showing the nickname prompt.");
                nicknamePanel.SetActive(true);
            }
        }

        private void Start()
        {
            SetConnectionStatusIcon(connectingIconSprite);

            if (PhotonNetwork.IsConnectedAndReady)
            {
                UpdatePlayButtonInteractable();
                SetConnectionStatusIcon(readyIconSprite);
            }
            else
            {
                PhotonNetwork.ConnectUsingSettings();
            }
        }

        public override void OnConnectedToMaster()
        {
            Debug.Log("[MatchmakingController] OnConnectedToMaster.");
            UpdatePlayButtonInteractable();
            SetConnectionStatusIcon(readyIconSprite);
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            Debug.LogWarning($"[MatchmakingController] OnDisconnected - cause: {cause}");
            playButton.interactable = false;
            SetStatus($"Disconnected ({cause}). Reconnecting...");
            PhotonNetwork.ConnectUsingSettings();
        }

        private void HandlePlayClicked()
        {
            Debug.Log("[MatchmakingController] HandlePlayClicked() - opening game mode panel.");
            gameModePanel.SetActive(true);
        }

        private void HandleGameModeSelected(GameMode selectedMode)
        {
            Debug.Log($"[MatchmakingController] HandleGameModeSelected() - selectedMode={selectedMode}.");

            if (selectedMode == GameMode.Constructed && !ActiveDeckIsQueueReady())
            {
                return;
            }

            gameModePanel.SetActive(false);

            string chosenName = PlayerPrefs.GetString(NicknamePrefsKey, string.Empty);

            if (string.IsNullOrWhiteSpace(chosenName))
            {
                chosenName = "Player" + Random.Range(1000, 9999);
                PlayerPrefs.SetString(NicknamePrefsKey, chosenName);
                PlayerPrefs.Save();
            }

            PhotonNetwork.NickName = chosenName;

            ConstructedMatchSync.PublishSelection(selectedMode, cardDatabase);

            cancelRequested = false;
            pendingGameMode = selectedMode;
            playButton.interactable = false;
            changeNicknameButton.interactable = false;
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

        private void HandleConfirmNicknameClicked()
        {
            string chosenName = string.IsNullOrWhiteSpace(nicknameInputField.text)
                ? "Player" + Random.Range(1000, 9999)
                : nicknameInputField.text.Trim();

            Debug.Log($"[MatchmakingController] HandleConfirmNicknameClicked() - saving nickname '{chosenName}'.");

            PlayerPrefs.SetString(NicknamePrefsKey, chosenName);
            PlayerPrefs.Save();
            PhotonNetwork.NickName = chosenName;
            nicknameInputField.text = chosenName;
            UpdateWelcomeText(chosenName);

            nicknamePanel.SetActive(false);
            UpdatePlayButtonInteractable();
        }

        private void HandleChangeNicknameClicked()
        {
            Debug.Log("[MatchmakingController] HandleChangeNicknameClicked() - reopening nickname panel.");
            nicknameInputField.text = PlayerPrefs.GetString(NicknamePrefsKey, string.Empty);
            nicknamePanel.SetActive(true);
        }

        private bool HasSavedNickname()
        {
            return PlayerPrefs.HasKey(NicknamePrefsKey) && !string.IsNullOrWhiteSpace(PlayerPrefs.GetString(NicknamePrefsKey));
        }

        private void UpdateWelcomeText(string nicknameToShow)
        {
            if (welcomeText != null)
            {
                welcomeText.text = $"Welcome, {nicknameToShow}!";
            }
        }

        private void HandleQuitClicked()
        {
            Debug.Log("[MatchmakingController] HandleQuitClicked().");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void UpdatePlayButtonInteractable()
        {
            playButton.interactable = PhotonNetwork.IsConnectedAndReady && HasSavedNickname();
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
            changeNicknameButton.interactable = true;
            UpdatePlayButtonInteractable();

            if (cancelMatchmakingButton != null)
            {
                cancelMatchmakingButton.gameObject.SetActive(false);
            }

            SetConnectionStatusIcon(readyIconSprite);
        }

        private void SetStatus(string message)
        {
            if (connectionStatusIcon != null)
            {
                connectionStatusIcon.gameObject.SetActive(false);
            }

            if (statusText != null)
            {
                statusText.gameObject.SetActive(true);
                statusText.text = message;
            }
        }

        private void SetConnectionStatusIcon(Sprite icon)
        {
            if (statusText != null)
            {
                statusText.gameObject.SetActive(false);
            }

            if (connectionStatusIcon != null)
            {
                connectionStatusIcon.sprite = icon;
                connectionStatusIcon.gameObject.SetActive(true);
            }
        }
    }
}