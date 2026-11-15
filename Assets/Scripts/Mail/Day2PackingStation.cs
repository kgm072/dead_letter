using DeadLetter.Interaction;
using UnityEngine;

namespace DeadLetter.Mail
{
    public sealed class Day2PackingStation : MonoBehaviour, IInteractable, IConditionalInteractable, IInteractionPromptProvider
    {
        [SerializeField] private DayCycleManager dayCycle;
        private Day2ClassificationSlip classificationSlip;

        public string GetInteractionPrompt(GameObject interactor)
        {
            return dayCycle != null && dayCycle.CanPackCurrentRequest(out _)
                ? "물품 포장"
                : "우편물 분류 전표 확인";
        }

        private void Awake()
        {
            dayCycle ??= FindAnyObjectByType<DayCycleManager>();
            classificationSlip = GetComponent<Day2ClassificationSlip>();
            if (classificationSlip == null)
                classificationSlip = gameObject.AddComponent<Day2ClassificationSlip>();
        }

        public bool CanInteract(GameObject interactor)
        {
            dayCycle ??= FindAnyObjectByType<DayCycleManager>();
            return dayCycle != null &&
                   (dayCycle.CanPackCurrentRequest(out _) ||
                    classificationSlip != null && classificationSlip.CanInteract(interactor));
        }

        public void Interact(GameObject interactor)
        {
            if (dayCycle != null && dayCycle.CanPackCurrentRequest(out _))
                dayCycle.TryPackCurrentRequest();
            else
                classificationSlip?.Interact(interactor);
        }
    }
}
