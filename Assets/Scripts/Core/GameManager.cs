using UnityEngine;

namespace DDD.TNFY.TCG.Core
{
    public class GameManager : MonoBehaviour
    {
        public GameState State { get; private set; }
        public PhaseManager Phases { get; private set; }

        private void Awake()
        {
            State = new GameState();
            Phases = new PhaseManager(State);
        }
    }
}