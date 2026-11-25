using DeadLetter.Interaction;
using DeadLetter.UI;
using UnityEngine;

namespace DeadLetter.Mail
{
    public sealed class DayOneIdentityMonitor : MonoBehaviour, IInteractable,
        IConditionalInteractable, IInteractionPromptProvider
    {
        [SerializeField] private DayCycleManager dayCycle;
        [SerializeField] private TestCustomerNPC customer;
        [SerializeField] private LetterTaskState taskState;
        [SerializeField] private DayOneIdentityPanel panel;

        public string GetInteractionPrompt(GameObject interactor) => "신원 확인";

        public bool CanInteract(GameObject interactor)
        {
            ResolveReferences();
            return dayCycle != null && panel != null && dayCycle.CurrentDayOneIdentityRequired &&
                   (customer == null || !customer.DialogueActive) &&
                   (taskState == null || !taskState.WorkstationViewOpen) && !panel.IsOpen;
        }

        public void Interact(GameObject interactor)
        {
            if (CanInteract(interactor))
                panel.Open();
        }

        private void ResolveReferences()
        {
            dayCycle ??= FindAnyObjectByType<DayCycleManager>();
            customer ??= FindAnyObjectByType<TestCustomerNPC>();
            taskState ??= FindAnyObjectByType<LetterTaskState>();
            panel ??= FindAnyObjectByType<DayOneIdentityPanel>(FindObjectsInactive.Include);
        }
    }
}
