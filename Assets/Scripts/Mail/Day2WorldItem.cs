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
        [SerializeField] private TestCustomerNPC customer;
        private DayCycleManager subscribedDayCycle;
        private Transform counterParent;
        private Vector3 counterLocalPosition;
        private Quaternion counterLocalRotation;
        private bool counterPoseCached;

        public string InstanceId => instanceId;
        public string RequestId => requestId;
        public Day2ItemType ItemType => itemType;
        public Day2ItemSource Source => source;
        public bool IsPresentedByCustomer => ShouldBeCarried();
        public Vector3 CounterWorldPosition => counterParent != null
            ? counterParent.TransformPoint(counterLocalPosition)
            : counterLocalPosition;

        public string GetInteractionPrompt(GameObject interactor) => $"{Day2Labels.GetItemLabel(itemType)} 획득";

        private void Awake()
        {
            CacheCounterPose();
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);
            ResolveReferences();
        }

        private void OnEnable()
        {
            CacheCounterPose();
            ResolveReferences();
            EnsureStateSubscription();
            RefreshVisibility();
        }

        private void LateUpdate()
        {
            if (ShouldBeCarried())
                FollowCustomerHands();
        }

        private void OnDisable()
        {
            UnsubscribeFromStateChanges();

            RestoreCounterPose();
        }

        private void OnDestroy()
        {
            UnsubscribeFromStateChanges();
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
            CacheCounterPose();
            ResolveReferences();
            EnsureStateSubscription();
            RefreshVisibility();
        }

        private void ResolveReferences()
        {
            dayCycle ??= FindAnyObjectByType<DayCycleManager>();
            customer ??= FindAnyObjectByType<TestCustomerNPC>(FindObjectsInactive.Include);
        }

        private void EnsureStateSubscription()
        {
            if (!Application.isPlaying)
                return;

            if (subscribedDayCycle == dayCycle)
                return;

            if (subscribedDayCycle != null)
                subscribedDayCycle.DayTwoStateChanged -= RefreshVisibility;
            subscribedDayCycle = dayCycle;
            if (subscribedDayCycle != null)
                subscribedDayCycle.DayTwoStateChanged += RefreshVisibility;
        }

        private void UnsubscribeFromStateChanges()
        {
            if (subscribedDayCycle != null)
                subscribedDayCycle.DayTwoStateChanged -= RefreshVisibility;
            subscribedDayCycle = null;
        }

        private bool IsVisible()
        {
            if (dayCycle == null)
                return true;
            if (dayCycle.IsWorldItemClaimed(instanceId))
                return false;

            // Storage items remain staged in the storage room. Dispatch items are props
            // brought in by the matching customer, so unrelated counter items stay hidden.
            if (source != Day2ItemSource.Counter)
                return dayCycle.IsDayTwoActive;

            return dayCycle.IsDayTwoActive &&
                   dayCycle.CurrentDayTwoRequest != null &&
                   string.Equals(
                       dayCycle.CurrentDayTwoRequest.Definition.RequestId,
                       requestId,
                       System.StringComparison.Ordinal);
        }

        private void RefreshVisibility()
        {
            bool visible = IsVisible();
            bool carried = visible && ShouldBeCarried();
            renderers ??= GetComponentsInChildren<Renderer>(true);
            colliders ??= GetComponentsInChildren<Collider>(true);
            foreach (Renderer target in renderers)
                if (target != null)
                    target.enabled = visible;
            foreach (Collider target in colliders)
                if (target != null)
                    target.enabled = visible && !carried;

            if (carried)
                FollowCustomerHands();
            else
                RestoreCounterPose();
        }

        private bool ShouldBeCarried()
        {
            ResolveReferences();
            return source == Day2ItemSource.Counter &&
                   customer != null && customer.gameObject.activeInHierarchy &&
                   dayCycle != null && dayCycle.CurrentDayTwoRequest != null &&
                   string.Equals(
                       dayCycle.CurrentDayTwoRequest.Definition.RequestId,
                       requestId,
                       System.StringComparison.Ordinal) &&
                   !dayCycle.CurrentDayTwoRequest.RequestAccepted &&
                   !dayCycle.IsWorldItemClaimed(instanceId);
        }

        private void CacheCounterPose()
        {
            if (counterPoseCached)
                return;

            counterParent = transform.parent;
            counterLocalPosition = transform.localPosition;
            counterLocalRotation = transform.localRotation;
            counterPoseCached = true;
        }

        private void RestoreCounterPose()
        {
            if (!counterPoseCached || transform.parent != counterParent)
                return;

            transform.localPosition = counterLocalPosition;
            transform.localRotation = counterLocalRotation;
        }

        private void FollowCustomerHands()
        {
            if (customer == null)
                return;

            // The current NPC mesh faces opposite its root +Z axis, hence the negative Z
            // offset. Separate offsets keep the mascot's two items readable in both hands.
            Vector3 carryOffset = itemType switch
            {
                Day2ItemType.Cigarettes => new Vector3(-0.17f, 1.05f, -0.24f),
                Day2ItemType.Lighter => new Vector3(0.17f, 1.08f, -0.24f),
                Day2ItemType.BulliedUniform => new Vector3(0f, 0.98f, -0.27f),
                _ => new Vector3(0f, 1.02f, -0.24f)
            };

            transform.SetPositionAndRotation(
                customer.transform.TransformPoint(carryOffset),
                customer.transform.rotation);
        }
    }
}
