using DeadLetter.Interaction;
using UnityEngine;

namespace DeadLetter.Mail
{
    public sealed class Day2ConveyorTerminal : MonoBehaviour, IInteractable, IConditionalInteractable, IInteractionPromptProvider
    {
        [SerializeField] private DayCycleManager dayCycle;

        public string GetInteractionPrompt(GameObject interactor) => "포장 박스 컨베이어 발송";

        private void Awake()
        {
            dayCycle ??= FindAnyObjectByType<DayCycleManager>();
        }

        public bool CanInteract(GameObject interactor)
        {
            dayCycle ??= FindAnyObjectByType<DayCycleManager>();
            return dayCycle != null && dayCycle.CanDispatchCurrentPackage(out _);
        }

        public void Interact(GameObject interactor)
        {
            dayCycle?.TryDispatchCurrentPackage();
        }
    }
}
