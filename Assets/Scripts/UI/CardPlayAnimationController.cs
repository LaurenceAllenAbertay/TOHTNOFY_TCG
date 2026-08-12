using System.Collections;
using UnityEngine;
using DDD.TNFY.TCG.Cards;
using DDD.TNFY.TCG.Core;

namespace DDD.TNFY.TCG.UI
{
    public class CardPlayAnimationController : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private BoardView boardView;
        [SerializeField] private RectTransform animationLayer;
        [SerializeField] private HandCardView clonePrefab;
        [SerializeField] private RectTransform rightAnchor;
        [SerializeField] private RectTransform leftAnchor;
        [SerializeField] private float growDuration = 1f;
        [SerializeField] private float lingerDuration = 3f;
        [SerializeField] private float resolveDuration = 0.4f;
        [SerializeField] private float maxScale = 1.5f;

        private NetworkedMatchSync networkSync;

        public static bool IsLocalPlayAnimationActive { get; private set; }

        private PlayerSide LocalSide => networkSync != null ? networkSync.LocalSide : PlayerSide.PlayerA;

        private void OnEnable()
        {
            if (gameManager == null)
            {
                Debug.LogWarning("[CardPlayAnimationController] gameManager reference not set.");
                return;
            }

            networkSync = gameManager.GetComponent<NetworkedMatchSync>();
            gameManager.State.UnitPlayAnimationRequested += HandleUnitPlayAnimationRequested;
            gameManager.State.ItemPlayAnimationRequested += HandleItemPlayAnimationRequested;
        }

        private void OnDisable()
        {
            if (gameManager == null || gameManager.State == null)
            {
                return;
            }

            gameManager.State.UnitPlayAnimationRequested -= HandleUnitPlayAnimationRequested;
            gameManager.State.ItemPlayAnimationRequested -= HandleItemPlayAnimationRequested;
        }

        private void HandleUnitPlayAnimationRequested(CardData card, PlayerSide playingSide, int handIndex, int slotIndex)
        {
            Debug.Log($"[CardPlayAnimationController] UnitPlayAnimationRequested: {card.CardName} ({playingSide}) -> slot {slotIndex}. LocalSide={LocalSide}.");
            StartCoroutine(RunUnitAnimation(card, playingSide, handIndex, slotIndex));
        }

        private void HandleItemPlayAnimationRequested(CardData card, PlayerSide playingSide, int handIndex)
        {
            Debug.Log($"[CardPlayAnimationController] ItemPlayAnimationRequested: {card.CardName} ({playingSide}). LocalSide={LocalSide}.");
            StartCoroutine(RunItemAnimation(card, playingSide, handIndex));
        }

        private IEnumerator RunUnitAnimation(CardData card, PlayerSide playingSide, int handIndex, int slotIndex)
        {
            bool isLocalCard = playingSide == LocalSide;
            RectTransform anchor = isLocalCard ? rightAnchor : leftAnchor;

            if (clonePrefab == null || anchor == null || animationLayer == null)
            {
                Debug.LogWarning("[CardPlayAnimationController] Missing clonePrefab/anchor/animationLayer reference - skipping visual and resolving immediately.");
                gameManager.State.RaiseUnitPlayAnimationFinished(playingSide, handIndex, slotIndex);
                yield break;
            }

            if (isLocalCard)
            {
                IsLocalPlayAnimationActive = true;
            }

            HandCardView clone = SpawnClone(card, playingSide, anchor);
            RectTransform cloneRect = clone.transform as RectTransform;

            yield return ScaleOver(cloneRect, Vector3.zero, Vector3.one * maxScale, growDuration);
            yield return new WaitForSeconds(lingerDuration);

            Transform destination = boardView != null ? boardView.GetSlotContainer(playingSide, slotIndex) : null;

            if (destination == null)
            {
                Debug.LogWarning($"[CardPlayAnimationController] No slot container found for {playingSide} slot {slotIndex} - shrinking in place.");
            }

            Vector3 destinationPosition = destination != null ? destination.position : cloneRect.position;

            yield return ScaleAndMoveOver(cloneRect, Vector3.zero, destinationPosition, resolveDuration);

            Destroy(clone.gameObject);

            if (isLocalCard)
            {
                IsLocalPlayAnimationActive = false;
            }

            Debug.Log($"[CardPlayAnimationController] Unit animation finished for {card.CardName} ({playingSide}) -> slot {slotIndex}. Raising UnitPlayAnimationFinished.");
            gameManager.State.RaiseUnitPlayAnimationFinished(playingSide, handIndex, slotIndex);
        }

        private IEnumerator RunItemAnimation(CardData card, PlayerSide playingSide, int handIndex)
        {
            bool isLocalCard = playingSide == LocalSide;
            RectTransform anchor = isLocalCard ? rightAnchor : leftAnchor;

            if (clonePrefab == null || anchor == null || animationLayer == null)
            {
                Debug.LogWarning("[CardPlayAnimationController] Missing clonePrefab/anchor/animationLayer reference - skipping visual and resolving immediately.");
                gameManager.State.RaiseItemPlayAnimationFinished(playingSide, handIndex);
                yield break;
            }

            if (isLocalCard)
            {
                IsLocalPlayAnimationActive = true;
            }

            HandCardView clone = SpawnClone(card, playingSide, anchor);
            RectTransform cloneRect = clone.transform as RectTransform;

            yield return ScaleOver(cloneRect, Vector3.zero, Vector3.one * maxScale, growDuration);
            yield return new WaitForSeconds(lingerDuration);
            yield return ScaleOver(cloneRect, cloneRect.localScale, Vector3.zero, resolveDuration);

            Destroy(clone.gameObject);

            if (isLocalCard)
            {
                IsLocalPlayAnimationActive = false;
            }

            Debug.Log($"[CardPlayAnimationController] Item animation finished for {card.CardName} ({playingSide}). Raising ItemPlayAnimationFinished.");
            gameManager.State.RaiseItemPlayAnimationFinished(playingSide, handIndex);
        }

        private HandCardView SpawnClone(CardData card, PlayerSide side, RectTransform anchor)
        {
            HandCardView clone = Instantiate(clonePrefab, animationLayer);

            HandCardDrag dragComponent = clone.GetComponent<HandCardDrag>();
            if (dragComponent != null)
            {
                Destroy(dragComponent);
            }

            CanvasGroup canvasGroup = clone.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = clone.gameObject.AddComponent<CanvasGroup>();
            }
            canvasGroup.blocksRaycasts = false;

            RectTransform cloneRect = clone.transform as RectTransform;
            cloneRect.position = anchor.position;
            cloneRect.localScale = Vector3.zero;

            clone.Bind(card, side, gameManager.State);

            return clone;
        }

        private static IEnumerator ScaleOver(RectTransform rect, Vector3 from, Vector3 to, float duration)
        {
            rect.localScale = from;

            if (duration <= 0f)
            {
                rect.localScale = to;
                yield break;
            }

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                rect.localScale = Vector3.LerpUnclamped(from, to, t);
                yield return null;
            }

            rect.localScale = to;
        }

        private static IEnumerator ScaleAndMoveOver(RectTransform rect, Vector3 toScale, Vector3 toPosition, float duration)
        {
            Vector3 fromScale = rect.localScale;
            Vector3 fromPosition = rect.position;

            if (duration <= 0f)
            {
                rect.localScale = toScale;
                rect.position = toPosition;
                yield break;
            }

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                rect.localScale = Vector3.LerpUnclamped(fromScale, toScale, t);
                rect.position = Vector3.LerpUnclamped(fromPosition, toPosition, t);
                yield return null;
            }

            rect.localScale = toScale;
            rect.position = toPosition;
        }
    }
}