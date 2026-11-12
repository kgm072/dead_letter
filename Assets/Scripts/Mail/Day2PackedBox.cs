using DeadLetter.Interaction;
using UnityEngine;

namespace DeadLetter.Mail
{
    public sealed class Day2PackedBox : MonoBehaviour, IInteractable, IConditionalInteractable, IInteractionPromptProvider
    {
        [SerializeField] private DayCycleManager dayCycle;
        [SerializeField] private GameObject fireMark;
        [SerializeField] private GameObject waterMark;
        [SerializeField] private GameObject electricityMark;
        [SerializeField] private GameObject shockMark;
        [SerializeField] private GameObject medicineMark;
        [SerializeField] private GameObject alcoholMark;

        private Renderer[] renderers;
        private Collider[] colliders;

        public string GetInteractionPrompt(GameObject interactor)
        {
            return Day2BoxStampSelection.Selected == LetterStamp.None
                ? "박스 도장 선택 필요"
                : $"{Day2Labels.GetStampLabel(Day2BoxStampSelection.Selected)} 도장 날인";
        }

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
                dayCycle.DayTwoStateChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (dayCycle != null)
                dayCycle.DayTwoStateChanged -= Refresh;
        }

        public bool CanInteract(GameObject interactor)
        {
            LetterStamp selected = Day2BoxStampSelection.Selected;
            return IsPackageVisible() && selected != LetterStamp.None && dayCycle != null &&
                   dayCycle.CanApplyBoxPictogram(selected, out _);
        }

        public void Interact(GameObject interactor)
        {
            LetterStamp selected = Day2BoxStampSelection.Selected;
            if (selected != LetterStamp.None && dayCycle != null && dayCycle.TryApplyBoxPictogram(selected))
                Refresh();
        }

        public void ConfigureMarks(
            GameObject fire,
            GameObject water,
            GameObject electricity,
            GameObject shock,
            GameObject medicine,
            GameObject alcohol)
        {
            fireMark = fire;
            waterMark = water;
            electricityMark = electricity;
            shockMark = shock;
            medicineMark = medicine;
            alcoholMark = alcohol;
        }

        private void ResolveManager()
        {
            dayCycle ??= FindAnyObjectByType<DayCycleManager>();
        }

        private bool IsPackageVisible()
        {
            Day2RequestRuntimeState state = dayCycle != null ? dayCycle.CurrentDayTwoRequest : null;
            return state != null && state.Packed && !state.ConveyorDispatched &&
                   !state.DeliveredToCustomer && !state.Completed;
        }

        private void Refresh()
        {
            bool visible = IsPackageVisible();
            renderers ??= GetComponentsInChildren<Renderer>(true);
            colliders ??= GetComponentsInChildren<Collider>(true);
            foreach (Renderer target in renderers)
                target.enabled = visible;
            foreach (Collider target in colliders)
                target.enabled = visible;

            LetterStamp marks = dayCycle?.CurrentDayTwoRequest?.AppliedBoxPictograms ?? LetterStamp.None;
            SetMark(fireMark, visible && Has(marks, LetterStamp.Fire));
            SetMark(waterMark, visible && Has(marks, LetterStamp.Water));
            SetMark(electricityMark, visible && Has(marks, LetterStamp.Electricity));
            SetMark(shockMark, visible && Has(marks, LetterStamp.Shock));
            SetMark(medicineMark, visible && Has(marks, LetterStamp.Medicine));
            SetMark(alcoholMark, visible && Has(marks, LetterStamp.Alcohol));
        }

        private static bool Has(LetterStamp value, LetterStamp target) => (value & target) == target;

        private static void SetMark(GameObject target, bool active)
        {
            if (target != null)
                target.SetActive(active);
        }
    }
}
