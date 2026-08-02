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

        public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
        {
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

            Debug.Log("[MatchLifecycleController] Returning to the main menu.");

            if (PhotonNetwork.InRoom)
            {
                PhotonNetwork.LeaveRoom();
            }

            SceneManager.LoadScene(mainMenuSceneName);
        }

        public void RequestRematch()
        {
            if (!PhotonNetwork.InRoom)
            {
                Debug.Log("[MatchLifecycleController] Rematch (offline): reloading the match scene.");
                SceneManager.LoadScene(gameSceneName);
                return;
            }

            if (PhotonNetwork.IsMasterClient)
            {
                Debug.Log("[MatchLifecycleController] Rematch: reloading the match scene for the room.");
                PhotonNetwork.LoadLevel(gameSceneName);
                return;
            }

            Debug.Log("[MatchLifecycleController] Requesting rematch from the Master Client.");
            photonView.RPC(nameof(ReceiveRematchRequest), RpcTarget.MasterClient);
        }

        [PunRPC]
        private void ReceiveRematchRequest()
        {
            Debug.Log("[MatchLifecycleController] Rematch requested by the other client - reloading the match scene.");
            PhotonNetwork.LoadLevel(gameSceneName);
        }
    }
}