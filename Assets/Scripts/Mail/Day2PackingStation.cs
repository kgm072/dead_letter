using DeadLetter.Interaction;
using UnityEngine;

namespace DeadLetter.Mail
{
    public sealed class Day2PackingStation : MonoBehaviour, IInteractable, IConditionalInteractable, IInteractionPromptProvider
    {
        [SerializeField] private DayCycleManager dayCycle;

        public string GetInteractionPrompt(GameObject interactor) => "물품 포장";

        private void Awake()
        {
            dayCycle ??= FindAnyObjectByType<DayCycleManager>();
        }

        public bool CanInteract(GameObject interactor)
        {
            dayCycle ??= FindAnyObjectByType<DayCycleManager>();
            return dayCycle != null && dayCycle.CanPackCurrentRequest(out _);
        }

        public void Interact(GameObject interactor)
        {
            dayCycle?.TryPackCurrentRequest();
        }
    }
}
