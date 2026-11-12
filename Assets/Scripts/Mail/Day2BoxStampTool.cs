using DeadLetter.Interaction;
using UnityEngine;

namespace DeadLetter.Mail
{
    public static class Day2BoxStampSelection
    {
        public static LetterStamp Selected { get; private set; }

        public static void Select(LetterStamp stamp)
        {
            Selected = stamp;
        }

        public static void Clear()
        {
            Selected = LetterStamp.None;
        }
    }

    public sealed class Day2BoxStampTool : MonoBehaviour, IInteractable, IConditionalInteractable, IInteractionPromptProvider
    {
        [SerializeField] private DayCycleManager dayCycle;
        [SerializeField] private LetterStamp pictogram;

        public LetterStamp Pictogram => pictogram;
        public string GetInteractionPrompt(GameObject interactor) => $"{Day2Labels.GetStampLabel(pictogram)} 박스 도장 선택";

        private void Awake()
        {
            dayCycle ??= FindAnyObjectByType<DayCycleManager>();
        }

        public bool CanInteract(GameObject interactor)
        {
            dayCycle ??= FindAnyObjectByType<DayCycleManager>();
            Day2RequestRuntimeState state = dayCycle != null ? dayCycle.CurrentDayTwoRequest : null;
            return state != null && state.RequestAccepted && state.Packed &&
                   !state.ConveyorDispatched && !state.DeliveredToCustomer && !state.Completed;
        }

        public void Interact(GameObject interactor)
        {
            if (CanInteract(interactor))
                Day2BoxStampSelection.Select(pictogram);
        }

        public void Configure(LetterStamp newPictogram)
        {
            pictogram = newPictogram;
        }
    }
}
