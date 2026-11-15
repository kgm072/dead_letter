using DeadLetter.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeadLetter.Mail
{
    public sealed class Day2ClassificationSlip : MonoBehaviour, IInteractable, IConditionalInteractable, IInteractionPromptProvider
    {
        [SerializeField] private DayCycleManager dayCycle;
        [SerializeField, Min(1f)] private float displaySeconds = 8f;

        private GameObject panel;
        private TextMeshProUGUI bodyText;
        private float hideAt;

        public bool IsOpen => panel != null && panel.activeSelf;
        public string CurrentText => BuildSlipText();
        public string GetInteractionPrompt(GameObject interactor) => IsOpen ? "우편물 분류 전표 닫기" : "우편물 분류 전표 확인";

        private void Awake()
        {
            dayCycle ??= FindAnyObjectByType<DayCycleManager>();
            CreateOverlay();
        }

        private void OnEnable()
        {
            dayCycle ??= FindAnyObjectByType<DayCycleManager>();
            if (dayCycle != null)
                dayCycle.DayTwoStateChanged += Refresh;
        }

        private void OnDisable()
        {
            if (dayCycle != null)
                dayCycle.DayTwoStateChanged -= Refresh;
            SetOpen(false);
        }

        private void Update()
        {
            if (IsOpen && Time.unscaledTime >= hideAt)
                SetOpen(false);
        }

        public bool CanInteract(GameObject interactor)
        {
            dayCycle ??= FindAnyObjectByType<DayCycleManager>();
            Day2RequestRuntimeState state = dayCycle != null ? dayCycle.CurrentDayTwoRequest : null;
            return dayCycle != null && dayCycle.IsDayTwoActive && state != null && state.RequestAccepted;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor))
                return;
            SetOpen(!IsOpen);
        }

        private void SetOpen(bool open)
        {
            if (panel == null)
                return;
            panel.SetActive(open);
            if (open)
            {
                Refresh();
                hideAt = Time.unscaledTime + displaySeconds;
            }
        }

        private void Refresh()
        {
            if (bodyText != null)
                bodyText.text = BuildSlipText();
        }

        private string BuildSlipText()
        {
            Day2RequestRuntimeState state = dayCycle != null ? dayCycle.CurrentDayTwoRequest : null;
            if (state == null)
                return "현재 처리 중인 DAY 2 민원이 없습니다.";

            Day2RequestDefinition definition = state.Definition;
            LetterStamp required = definition.RequiredPictograms;
            LetterStamp applied = state.AppliedBoxPictograms;
            LetterStamp missing = required & ~applied;
            LetterStamp incorrect = applied & ~required;
            string status = state.HasOnlyRequiredBoxPictograms
                ? "완료"
                : $"미완료  |  남은 도장: {Day2Labels.GetStampList(missing)}";
            if (incorrect != LetterStamp.None)
                status += $"\n잘못 찍은 도장: {Day2Labels.GetStampList(incorrect)} (같은 도장을 다시 찍어 제거)";

            return
                $"우편물 분류 전표\n\n" +
                $"민원인  {definition.CustomerDisplayName}\n" +
                $"구분    {(definition.RequestType == MailRequestType.Dispatch ? "발송" : "수령")}\n" +
                $"물품    {Day2Labels.GetItemList(definition.RequestedItems)}\n" +
                $"필수 박스 도장  {Day2Labels.GetStampList(required)}\n" +
                $"현재 박스 도장  {Day2Labels.GetStampList(applied)}\n" +
                $"상태    {status}\n\n" +
                "박스 도장은 우편물 분류·인계 확인용입니다.\n편지 우표와는 별도로 기록됩니다.";
        }

        private void CreateOverlay()
        {
            GameObject canvasObject = new("DAY 2 Classification Slip Canvas");
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 65;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            panel = new GameObject("Slip Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasObject.transform, false);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(660f, 500f);
            panel.GetComponent<Image>().color = new Color(0.035f, 0.028f, 0.024f, 0.96f);

            GameObject textObject = new("Slip Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(panel.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(34f, 28f);
            textRect.offsetMax = new Vector2(-34f, -28f);
            bodyText = textObject.GetComponent<TextMeshProUGUI>();
            bodyText.font = Resources.Load<TMP_FontAsset>("UI/Fonts/GalmuriMono11/GalmuriMono11 TMP");
            bodyText.fontSize = 24f;
            bodyText.color = new Color(0.95f, 0.91f, 0.82f, 1f);
            bodyText.alignment = TextAlignmentOptions.TopLeft;
            bodyText.textWrappingMode = TextWrappingModes.Normal;
            bodyText.raycastTarget = false;
            panel.SetActive(false);
        }
    }
}
