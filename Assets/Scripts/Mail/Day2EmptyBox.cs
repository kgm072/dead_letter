using DeadLetter.Interaction;
using UnityEngine;

namespace DeadLetter.Mail
{
    public sealed class Day2EmptyBox : MonoBehaviour, IInteractable, IConditionalInteractable, IInteractionPromptProvider
    {
        [SerializeField] private DayCycleManager dayCycle;
        [SerializeField] private string boxId;

        private Renderer[] renderers;
        private Collider[] colliders;

        public string BoxId => boxId;
        public string GetInteractionPrompt(GameObject interactor) => "빈 박스 확보";

        private void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);
            ResolveManager();
        }

        private void OnEnable()
        {
            ResolveManager();
            if (dayCycle != null)
                dayCycle.DayTwoStateChanged += RefreshVisibility;
            RefreshVisibility();
        }

        private void OnDisable()
        {
            if (dayCycle != null)
                dayCycle.DayTwoStateChanged -= RefreshVisibility;
        }

        public bool CanInteract(GameObject interactor)
        {
            return IsVisible() && dayCycle != null && dayCycle.CanAcquireEmptyBox(boxId, out _);
        }

        public void Interact(GameObject interactor)
        {
            if (dayCycle != null && dayCycle.TryAcquireEmptyBox(boxId))
                RefreshVisibility();
        }

        public void Configure(string newBoxId)
        {
            boxId = newBoxId;
        }

        private void ResolveManager()
        {
            dayCycle ??= FindAnyObjectByType<DayCycleManager>();
        }

        private bool IsVisible()
        {
            return dayCycle == null || !dayCycle.IsBoxClaimed(boxId);
        }

        private void RefreshVisibility()
        {
            bool visible = IsVisible();
            renderers ??= GetComponentsInChildren<Renderer>(true);
            colliders ??= GetComponentsInChildren<Collider>(true);
            foreach (Renderer target in renderers)
                target.enabled = visible;
            foreach (Collider target in colliders)
                target.enabled = visible;
        }
    }
}
