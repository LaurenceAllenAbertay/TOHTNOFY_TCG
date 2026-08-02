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
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private string gameSceneName = "Game";

        private void Awake()
        {
            PhotonNetwork.AutomaticallySyncScene = true;
            findMatchButton.interactable = false;
            findMatchButton.onClick.AddListener(HandleFindMatchClicked);

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

            findMatchButton.interactable = false;
            nameInputField.interactable = false;
            SetStatus("Searching for an opponent...");

            Debug.Log($"[MatchmakingController] Attempting JoinRandomOrCreateRoom as '{chosenName}'.");
            PhotonNetwork.JoinRandomOrCreateRoom();
        }

        public override void OnJoinRandomFailed(short returnCode, string message)
        {
            Debug.Log($"[MatchmakingController] OnJoinRandomFailed (no open room found) - creating one. message={message}");

            RoomOptions roomOptions = new RoomOptions { MaxPlayers = MaxPlayersPerRoom };
            PhotonNetwork.CreateRoom(null, roomOptions);
        }

        public override void OnJoinedRoom()
        {
            Debug.Log($"[MatchmakingController] OnJoinedRoom - '{PhotonNetwork.CurrentRoom.Name}', PlayerCount={PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers}.");

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

        private void TryStartMatch()
        {
            SetStatus("Opponent found! Loading match...");

            if (!PhotonNetwork.IsMasterClient)
            {
                return;
            }

            Debug.Log($"[MatchmakingController] MasterClient loading '{gameSceneName}' for the room.");
            PhotonNetwork.LoadLevel(gameSceneName);
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