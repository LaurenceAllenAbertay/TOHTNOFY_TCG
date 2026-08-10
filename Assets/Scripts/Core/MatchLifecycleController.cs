using System;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DDD.TNFY.TCG.Core
{
    [RequireComponent(typeof(GameManager))]
    [RequireComponent(typeof(PhotonView))]
    public class MatchLifecycleController : MonoBehaviourPunCallbacks
    {
        [SerializeField] private string mainMenuSceneName = "Matchmaking";
        [SerializeField] private string gameSceneName = "Game";

        private bool isReturningToMenu;
        private bool localWantsRematch;
        private bool remoteWantsRematch;

        public bool LocalWantsRematch => localWantsRematch;
        public bool RemoteWantsRematch => remoteWantsRematch;

        public event Action<bool, bool> RematchVoteChanged;

        private GameManager gameManager;

        private void Awake()
        {
            gameManager = GetComponent<GameManager>();
        }

        public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
        {
            if (gameManager != null && gameManager.State != null && gameManager.State.IsGameOver)
            {
                Debug.Log($"[MatchLifecycleController] {otherPlayer.NickName} left the room, but the match already ended (Winner={gameManager.State.Winner}) - leaving WinLoseScreen in control of navigation.");
                return;
            }

            Debug.Log($"[MatchLifecycleController] {otherPlayer.NickName} left the room - ending the match.");
            ReturnToMainMenu();
        }

        public override void OnDisconnected(Photon.Realtime.DisconnectCause cause)
        {
            Debug.Log($"[MatchLifecycleController] Disconnected from Photon (cause={cause}) - returning to the main menu.");
            ReturnToMainMenu();
        }

        public void ReturnToMainMenu()
        {
            if (isReturningToMenu)
            {
                return;
            }

            isReturningToMenu = true;

            Debug.Log($"[MatchLifecycleController] Returning to the main menu - loading scene '{mainMenuSceneName}'.");

            if (PhotonNetwork.InRoom)
            {
                PhotonNetwork.LeaveRoom();
            }

            SceneManager.LoadScene(mainMenuSceneName);
        }

        public void RequestRematch()
        {
            if (isReturningToMenu)
            {
                Debug.Log("[MatchLifecycleController] RequestRematch ignored - already returning to the main menu.");
                return;
            }

            if (!PhotonNetwork.InRoom)
            {
                Debug.Log("[MatchLifecycleController] Rematch (offline): reloading the match scene.");
                SceneManager.LoadScene(gameSceneName);
                return;
            }

            if (localWantsRematch)
            {
                Debug.Log("[MatchLifecycleController] RequestRematch ignored - local vote already cast.");
                return;
            }

            localWantsRematch = true;
            Debug.Log($"[MatchLifecycleController] Local player voted for rematch. remoteWantsRematch={remoteWantsRematch}.");
            RematchVoteChanged?.Invoke(localWantsRematch, remoteWantsRematch);

            photonView.RPC(nameof(ReceiveRematchVote), RpcTarget.Others);

            TryStartRematchIfBothReady();
        }

        [PunRPC]
        private void ReceiveRematchVote()
        {
            remoteWantsRematch = true;
            Debug.Log($"[MatchLifecycleController] Received rematch vote from the other client. localWantsRematch={localWantsRematch}.");
            RematchVoteChanged?.Invoke(localWantsRematch, remoteWantsRematch);

            TryStartRematchIfBothReady();
        }

        private void TryStartRematchIfBothReady()
        {
            if (!localWantsRematch || !remoteWantsRematch)
            {
                Debug.Log($"[MatchLifecycleController] Not starting rematch yet - localWantsRematch={localWantsRematch}, remoteWantsRematch={remoteWantsRematch}.");
                return;
            }

            if (!PhotonNetwork.IsMasterClient)
            {
                Debug.Log("[MatchLifecycleController] Both players want a rematch - waiting for the Master Client to reload the scene.");
                return;
            }

            Debug.Log("[MatchLifecycleController] Both players want a rematch - Master Client reloading the scene now.");
            PhotonNetwork.LoadLevel(gameSceneName);
        }
    }
}