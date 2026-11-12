using System;
using System.Linq;
using System.Text;
using DeadLetter.Interaction;
using DeadLetter.Mail;
using DeadLetter.Player;
using DeadLetter.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DeadLetter.DebugTools
{
    public sealed class DeveloperTestMode : MonoBehaviour
    {
        [SerializeField] private DeadLetterUITheme theme;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private DayCycleManager dayCycle;
        private LetterTaskState taskState;
        private FirstPersonController playerController;
        private FirstPersonInteractor interactor;
        private GameObject canvasRoot;
        private GameObject panel;
        private TextMeshProUGUI statusText;
        private TextMeshProUGUI feedbackText;
        private bool panelOpen;
        private bool previousPlayerInput;
        private bool previousInteractorEnabled;
        private CursorLockMode previousCursorLockMode;
        private bool previousCursorVisible;
        private float nextRefreshTime;

        public static bool IsAvailableInCurrentBuild => true;

        private void Awake()
        {
            ResolveReferences();
            BuildInterface();
            panelOpen = false;
            panel.SetActive(false);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame)
            {
                if (panelOpen)
                    SetPanelOpen(false);
                else if (CanOpen())
                    SetPanelOpen(true);
            }

            if (panelOpen && Time.unscaledTime >= nextRefreshTime)
            {
                RefreshStatus();
                nextRefreshTime = Time.unscaledTime + 0.2f;
            }
        }

        private void OnDisable()
        {
            if (panelOpen)
                SetPanelOpen(false);
        }

        private bool CanOpen()
        {
            ResolveReferences();
            return dayCycle != null && taskState != null &&
                   !taskState.WorkstationViewOpen &&
                   (playerController == null || playerController.InputEnabled);
        }

        private void SetPanelOpen(bool open)
        {
            panelOpen = open;
            if (panel != null)
                panel.SetActive(open);

            ResolveReferences();
            if (open)
            {
                previousPlayerInput = playerController != null && playerController.InputEnabled;
                previousInteractorEnabled = interactor != null && interactor.enabled;
                previousCursorLockMode = Cursor.lockState;
                previousCursorVisible = Cursor.visible;
                if (playerController != null)
                    playerController.SetInputEnabled(false);
                if (interactor != null)
                    interactor.enabled = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                RefreshStatus();
            }
            else
            {
                if (interactor != null)
                    interactor.enabled = previousInteractorEnabled;
                if (playerController != null)
                    playerController.SetInputEnabled(previousPlayerInput);
                Cursor.lockState = previousCursorLockMode;
                Cursor.visible = previousCursorVisible;
            }
        }

        private void ResolveReferences()
        {
            dayCycle ??= FindAnyObjectByType<DayCycleManager>();
            taskState ??= FindAnyObjectByType<LetterTaskState>();
            playerController ??= FindAnyObjectByType<FirstPersonController>();
            interactor ??= FindAnyObjectByType<FirstPersonInteractor>();
        }

        private void BuildInterface()
        {
            EnsureEventSystem();
            canvasRoot = new GameObject("Developer Test Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasRoot.transform.SetParent(transform, false);
            Canvas canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            CanvasScaler scaler = canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            panel = CreateImage(canvasRoot.transform, "Developer Panel", new Color(0.025f, 0.055f, 0.05f, 0.97f)).gameObject;
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(18f, -18f);
            panelRect.sizeDelta = new Vector2(560f, 1020f);

            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 16, 16);
            layout.spacing = 7f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            AddLabel(panel.transform, "DEVELOPER TEST MODE  [F1]", 24, GetAccent(), 38f, TextAlignmentOptions.Left);
            statusText = AddLabel(panel.transform, "", 15, GetPrimary(), 310f, TextAlignmentOptions.TopLeft);
            feedbackText = AddLabel(panel.transform, "대기 중", 14, GetMuted(), 28f, TextAlignmentOptions.Left);

            AddLabel(panel.transform, "DAY 제어", 17, GetAccent(), 26f, TextAlignmentOptions.Left);
            Transform dayRow = CreateRow(panel.transform, "DAY Buttons");
            AddButton(dayRow, "DAY 1", () => Run("DAY 1 초기화", () => dayCycle.DebugStartDay(1)));
            AddButton(dayRow, "DAY 2", () => Run("DAY 2 초기화", () => dayCycle.DebugStartDay(2)));
            AddButton(dayRow, "현재 DAY 초기화", () => Run("현재 DAY 전체 초기화", dayCycle.DebugResetCurrentDay));
            Transform orderRow = CreateRow(panel.transform, "Order Buttons");
            AddButton(orderRow, "무작위 등장 순서 재생성", () => Run("등장 순서 재생성", dayCycle.DebugRegenerateCurrentDay));

            AddLabel(panel.transform, "DAY 2 민원 선택", 17, GetAccent(), 26f, TextAlignmentOptions.Left);
            Transform customerRow1 = CreateRow(panel.transform, "Customer Row 1");
            AddCustomerButton(customerRow1, "인형탈", Day2RequestCatalog.MascotRequestId);
            AddCustomerButton(customerRow1, "음주운전", Day2RequestCatalog.DrunkDriverRequestId);
            AddCustomerButton(customerRow1, "모범생", Day2RequestCatalog.ModelStudentRequestId);
            Transform customerRow2 = CreateRow(panel.transform, "Customer Row 2");
            AddCustomerButton(customerRow2, "부실공사", Day2RequestCatalog.BadConstructionRequestId);
            AddCustomerButton(customerRow2, "불량이", Day2RequestCatalog.TroublemakerRequestId);
            AddButton(customerRow2, "현재 민원 초기화", () => Run("현재 민원 초기화", dayCycle.DebugRestartCurrentRequest));

            AddLabel(panel.transform, "현재 민원 상태 진행", 17, GetAccent(), 26f, TextAlignmentOptions.Left);
            Transform progressRow1 = CreateRow(panel.transform, "Progress Row 1");
            AddButton(progressRow1, "1 접수", () => Run("민원 접수", dayCycle.DebugAcceptCurrentRequest));
            AddButton(progressRow1, "2 물품", () => Run("요청 물품 확보", dayCycle.DebugSecureAllCurrentItems));
            AddButton(progressRow1, "3 빈 박스", () => Run("빈 박스 확보", dayCycle.DebugSecureCurrentBox));
            Transform progressRow2 = CreateRow(panel.transform, "Progress Row 2");
            AddButton(progressRow2, "4 포장", () => Run("포장 완료", dayCycle.DebugPackCurrentRequest));
            AddButton(progressRow2, "5 편지 처리", () => Run("편지 우표·도장·발송", dayCycle.DebugProcessCurrentLetter));
            AddButton(progressRow2, "6 박스 도장", () => Run("박스 픽토그램", dayCycle.DebugApplyCurrentBoxPictograms));
            Transform progressRow3 = CreateRow(panel.transform, "Progress Row 3");
            AddButton(progressRow3, "7 컨베이어", () => Run("컨베이어 발송", dayCycle.DebugDispatchCurrentRequest));
            AddButton(progressRow3, "8 전달", () => Run("현재 민원인에게 전달", () =>
                dayCycle.DebugDeliverCurrentRequest(dayCycle.CurrentCustomerId)));
            AddButton(progressRow3, "9 완료 안내", () => Run("민원인 완료 안내", dayCycle.DebugRecordCurrentCompletionNotice));

            AddLabel(panel.transform, "※ 버튼은 실제 상태 전이 조건을 통과할 때만 성공합니다.", 13, GetMuted(), 24f, TextAlignmentOptions.Left);
        }

        private void AddCustomerButton(Transform parent, string label, string requestId)
        {
            AddButton(parent, label, () => Run($"{label} 민원 선택", () => dayCycle.DebugSelectDayTwoRequest(requestId)));
        }

        private void Run(string label, Func<bool> operation)
        {
            ResolveReferences();
            bool success = dayCycle != null && operation();
            feedbackText.text = success ? $"성공: {label}" : $"실패: {label} — 선행 조건 확인";
            feedbackText.color = success ? new Color(0.45f, 0.9f, 0.62f, 1f) : new Color(1f, 0.42f, 0.3f, 1f);
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            ResolveReferences();
            if (statusText == null)
                return;
            if (dayCycle == null || taskState == null)
            {
                statusText.text = "필수 게임 관리자를 찾을 수 없습니다.";
                return;
            }

            StringBuilder value = new();
            value.AppendLine($"현재 DAY: {taskState.CurrentDay:00}    완료: {taskState.CompletedRequests}/{taskState.RequiredRequestsPerDay}");
            value.AppendLine($"현재 민원인: {EmptyAsDash(dayCycle.CurrentCustomerDisplayName)}  ({EmptyAsDash(dayCycle.CurrentCustomerId)})");
            value.AppendLine($"등장 순서: {EmptyAsDash(dayCycle.GetCurrentOrderSummary())}");

            if (taskState.CurrentDay != 2 || dayCycle.CurrentDayTwoRequest == null)
            {
                value.AppendLine("DAY 2 민원 상태 없음");
                statusText.text = value.ToString();
                return;
            }

            Day2RequestRuntimeState state = dayCycle.CurrentDayTwoRequest;
            Day2RequestDefinition definition = state.Definition;
            int modelIndex = IndexOfRequest(dayCycle.DayTwoOrder, Day2RequestCatalog.ModelStudentRequestId);
            int troubleIndex = IndexOfRequest(dayCycle.DayTwoOrder, Day2RequestCatalog.TroublemakerRequestId);
            value.AppendLine($"선후 조건: {(modelIndex >= 0 && modelIndex < troubleIndex ? "정상" : "오류")}  모범생 {modelIndex + 1} / 불량이 {troubleIndex + 1}");
            value.AppendLine($"민원: {definition.RequestId}  유형: {(definition.RequestType == MailRequestType.Dispatch ? "발송" : "수령")}");
            value.AppendLine($"물품: {string.Join(", ", definition.RequestedItems.Select(GetItemLabel))}");
            value.AppendLine($"확보 물품: {(state.SecuredItemInstanceIds.Count == 0 ? "-" : string.Join(", ", state.SecuredItemInstanceIds.Select(pair => $"{GetItemLabel(pair.Key)}={pair.Value}")))}");
            value.AppendLine($"박스 ID: {EmptyAsDash(state.BoxId)}");
            value.AppendLine($"진행: {state.ProgressStage}");
            value.AppendLine($"{Flag(state.RequestAccepted)} 접수   {Flag(state.ItemsSecured)} 물품   {Flag(state.EmptyBoxSecured)} 빈 박스   {Flag(state.Packed)} 포장");
            value.AppendLine($"{Flag(state.HasOnlyRequiredLetterStamps)} 편지 우표   {Flag(state.LetterPostmarkApplied)} 편지 도장   {Flag(state.HasOnlyRequiredBoxPictograms)} 박스 도장");
            value.AppendLine($"{Flag(state.ConveyorDispatched)} 발송   {Flag(state.DeliveredToCustomer)} 전달   {Flag(state.CompletionNoticeGiven)} 안내   {Flag(state.Completed)} 최종 완료");
            value.AppendLine($"편지 우표: {state.AttachedLetterStamps} / 박스 도장: {state.AppliedBoxPictograms}");
            value.AppendLine($"연계 민원: {EmptyAsDash(definition.RelatedRequestId)} / 전달 ID: {EmptyAsDash(definition.TransferLinkId)}");
            if (definition.TransferLinkId == Day2RequestCatalog.StudentUniformTransferLinkId)
            {
                Day2RequestRuntimeState modelStudent = dayCycle.DayTwoRequests.FirstOrDefault(
                    request => request.Definition.RequestId == Day2RequestCatalog.ModelStudentRequestId);
                value.AppendLine($"교복 연계 발송 완료: {Flag(modelStudent != null && modelStudent.Completed)}");
            }
            statusText.text = value.ToString();
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
                return;
            GameObject eventSystem = new("Developer EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.transform.SetParent(transform, false);
        }

        private Transform CreateRow(Transform parent, string name)
        {
            GameObject row = new(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            row.GetComponent<LayoutElement>().preferredHeight = 36f;
            return row.transform;
        }

        private void AddButton(Transform parent, string label, Action action)
        {
            Image image = CreateImage(parent, label, new Color(0.08f, 0.16f, 0.145f, 1f));
            Button button = image.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.16f, 0.34f, 0.28f, 1f);
            colors.pressedColor = GetAccent();
            button.colors = colors;
            button.onClick.AddListener(() => action());
            TextMeshProUGUI text = AddLabel(image.transform, "Label", label, 13, GetPrimary(), 0f, TextAlignmentOptions.Center);
            Stretch(text.rectTransform, 4f);
        }

        private TextMeshProUGUI AddLabel(Transform parent, string value, int size, Color color, float height, TextAlignmentOptions alignment)
        {
            return AddLabel(parent, value, value, size, color, height, alignment);
        }

        private TextMeshProUGUI AddLabel(Transform parent, string name, string value, int size, Color color, float height, TextAlignmentOptions alignment)
        {
            GameObject gameObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
            gameObject.transform.SetParent(parent, false);
            TextMeshProUGUI text = gameObject.GetComponent<TextMeshProUGUI>();
            text.font = theme != null ? theme.PrimaryFont : null;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            if (height > 0f)
                gameObject.GetComponent<LayoutElement>().preferredHeight = height;
            return text;
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            GameObject gameObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private Color GetPrimary() => theme != null ? theme.PrimaryText : Color.white;
        private Color GetMuted() => theme != null ? theme.MutedText : Color.gray;
        private Color GetAccent() => theme != null ? theme.Monitor : new Color(0.2f, 0.7f, 0.5f, 1f);
        private static string Flag(bool value) => value ? "[✓]" : "[ ]";
        private static string EmptyAsDash(string value) => string.IsNullOrWhiteSpace(value) ? "-" : value;

        private static int IndexOfRequest(System.Collections.Generic.IReadOnlyList<Day2RequestRuntimeState> requests, string requestId)
        {
            for (int index = 0; index < requests.Count; index++)
            {
                if (requests[index].Definition.RequestId == requestId)
                    return index;
            }
            return -1;
        }

        private static string GetItemLabel(Day2ItemType item)
        {
            return item switch
            {
                Day2ItemType.Cigarettes => "담배",
                Day2ItemType.Lighter => "라이터",
                Day2ItemType.HangoverRemedy => "숙취해소제",
                Day2ItemType.BulliedUniform => "괴롭힘 흔적 교복",
                Day2ItemType.SafetyHelmet => "안전모",
                Day2ItemType.InsulatedGloves => "절연장갑",
                _ => item.ToString()
            };
        }
#else
        public static bool IsAvailableInCurrentBuild => false;

        private void Awake()
        {
            gameObject.SetActive(false);
        }
#endif
    }
}
