using DeadLetter.Interaction;
using UnityEngine;

namespace DeadLetter.Mail
{
    public sealed class Day2WorldItem : MonoBehaviour, IInteractable, IConditionalInteractable, IInteractionPromptProvider
    {
        [SerializeField] private DayCycleManager dayCycle;
        [SerializeField] private string instanceId;
        [SerializeField] private string requestId;
        [SerializeField] private Day2ItemType itemType;
        [SerializeField] private Day2ItemSource source;

        private Renderer[] renderers;
        private Collider[] colliders;

        public string InstanceId => instanceId;
        public string RequestId => requestId;
        public Day2ItemType ItemType => itemType;
        public Day2ItemSource Source => source;

        public string GetInteractionPrompt(GameObject interactor) => $"{Day2Labels.GetItemLabel(itemType)} 획득";

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
            return IsVisible() && dayCycle != null &&
                   dayCycle.CanAcquireCurrentItem(instanceId, requestId, itemType, source, out _);
        }

        public void Interact(GameObject interactor)
        {
            if (dayCycle != null && dayCycle.TryAcquireCurrentItem(instanceId, requestId, itemType, source))
                RefreshVisibility();
        }

        public void Configure(string newInstanceId, string newRequestId, Day2ItemType newItemType, Day2ItemSource newSource)
        {
            instanceId = newInstanceId;
            requestId = newRequestId;
            itemType = newItemType;
            source = newSource;
        }

        private void ResolveManager()
        {
            dayCycle ??= FindAnyObjectByType<DayCycleManager>();
        }

        private bool IsVisible()
        {
            return dayCycle == null || !dayCycle.IsWorldItemClaimed(instanceId);
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
