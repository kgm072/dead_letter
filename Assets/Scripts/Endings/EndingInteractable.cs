using DeadLetter.Interaction;
using UnityEngine;

namespace DeadLetter.Endings
{
    public sealed class EndingInteractable : MonoBehaviour, IInteractable, IConditionalInteractable, IInteractionPromptProvider
    {
        [SerializeField] private EndingFlowController flow;
        [SerializeField] private bool whiteLetter;
        public void Configure(EndingFlowController owner, bool letter) { flow = owner; whiteLetter = letter; }
        public bool CanInteract(GameObject interactor) => flow != null && (whiteLetter ? flow.CanOpenLetter : flow.CanLowerFrame);
        public string GetInteractionPrompt(GameObject interactor) => whiteLetter ? "흰 편지 개봉" : "액자 내려놓기";
        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;
            if (whiteLetter) flow.TryStartTrue(); else flow.TryLowerFrame();
        }
    }
}
