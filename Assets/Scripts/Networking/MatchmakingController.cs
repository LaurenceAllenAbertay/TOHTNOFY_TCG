using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DDD.TNFY.TCG.Networking
{
    public class MatchmakingController : MonoBehaviourPunCallbacks
    {
        private const string NicknamePrefsKey = "SavedPlayerNickname";
        private const byte MaxPlayersPerRoom = 2;

        [SerializeField] private TMP_InputField nameInputField;
        [SerializeField] private Button findMatchButton;
        [SerializeField] private Button cancelMatchmakingButton;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private string gameSceneName = "Game";

        private bool cancelRequested;

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
            string chosenName = string.IsNullOrWhiteSpace(nameInputField.text)
                ? "Player" + Random.Range(1000, 9999)
                : nameInputField.text.Trim();

            PlayerPrefs.SetString(NicknamePrefsKey, chosenName);
            PhotonNetwork.NickName = chosenName;

            cancelRequested = false;
            findMatchButton.interactable = false;
            nameInputField.interactable = false;
            SetStatus("Searching for an opponent...");

            if (cancelMatchmakingButton != null)
            {
                cancelMatchmakingButton.gameObject.SetActive(true);
                cancelMatchmakingButton.interactable = true;
            }

            Debug.Log($"[MatchmakingController] Attempting JoinRandomOrCreateRoom as '{chosenName}'.");
            PhotonNetwork.JoinRandomOrCreateRoom();
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

            Debug.Log($"[MatchmakingController] OnJoinRandomFailed (no open room found) - creating one. message={message}");

            RoomOptions roomOptions = new RoomOptions { MaxPlayers = MaxPlayersPerRoom };
            PhotonNetwork.CreateRoom(null, roomOptions);
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