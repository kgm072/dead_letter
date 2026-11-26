using DeadLetter.Mail;
using DeadLetter.Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DeadLetter.UI
{
    public sealed class DayOneIdentityPanel : MonoBehaviour
    {
        [SerializeField] private DayCycleManager dayCycle;
        [SerializeField] private FirstPersonController playerController;
        [SerializeField] private TextMeshProUGUI customerNameText;
        [SerializeField] private TextMeshProUGUI dayText;
        [SerializeField] private TextMeshProUGUI workText;
        [SerializeField] private TextMeshProUGUI itemText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Button lookupButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button closeButton;

        private bool restorePlayerInput;
        private bool lookupInProgress;
        private string lookupError = string.Empty;

        public static bool IsAnyOpen { get; private set; }
        public bool IsOpen => gameObject.activeSelf;

        private void OnEnable()
        {
            IsAnyOpen = true;
            ResolveReferences();
            Refresh();
        }

        private void OnDisable()
        {
            IsAnyOpen = false;
            RestorePlayerInput();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                Close();
        }

        public void Open()
        {
            ResolveReferences();
            if (dayCycle == null || !dayCycle.CurrentIdentityRequired)
                return;

            restorePlayerInput = playerController == null || playerController.InputEnabled;
            if (playerController != null)
                playerController.SetInputEnabled(false);
            gameObject.SetActive(true);
            lookupError = string.Empty;
            lookupInProgress = false;
            Refresh();
        }

        public void Close()
        {
            RestorePlayerInput();
            gameObject.SetActive(false);
        }

        public void Refresh()
        {
            ResolveReferences();
            string displayName = dayCycle != null && !string.IsNullOrWhiteSpace(dayCycle.CurrentCustomerDisplayName)
                ? dayCycle.CurrentCustomerDisplayName
                : "조회 대상 없음";
            bool verified = dayCycle != null && dayCycle.CurrentIdentityVerified;
            bool matched = dayCycle != null && dayCycle.CurrentIdentityMatches;
            Day2RequestDefinition package = dayCycle != null && dayCycle.IsDayTwoActive
                ? dayCycle.CurrentDayTwoRequest?.Definition : null;
            if (package != null)
            {
                if (package.CustomerId == "drunk_driver")
                    displayName = "음주운전자";
                else if (CustomerDialogueCatalog.TryGet(2, package.CustomerId, out CustomerDialogueScenario scenario))
                    displayName = scenario.SpeakerName;
            }

            dayText ??= FindLabel("Day");
            workText ??= FindLabel("Work");
            itemText ??= FindLabel("Item");
            if (dayText != null)
                dayText.text = $"DAY {(dayCycle != null && dayCycle.IsDayTwoActive ? 2 : 1):00}";
            if (workText != null)
                workText.text = package == null ? "업무 유형: 편지 발송" :
                    $"업무 유형: 택배 {(package.RequestType == MailRequestType.Dispatch ? "발송" : "수령")}";
            if (itemText != null)
                itemText.text = package == null ? "접수 물품: 편지 1통" :
                    $"{(package.RequestType == MailRequestType.Dispatch ? "발송" : "수령")} 물품: {Day2Labels.GetItemList(package.RequestedItems)}  |  편지 처리: 필요";

            if (customerNameText != null)
                customerNameText.text = $"현재 민원인: {displayName}";
            if (statusText != null)
            {
                statusText.text = verified ? "신원 확인 완료" :
                    !string.IsNullOrEmpty(lookupError) ? $"조회 오류: {lookupError}" :
                    lookupInProgress ? "정보 조회 중" :
                    matched ? "정보 일치 — 접수 승인을 누르세요." : "확인 대기 — 정보를 조회하세요.";
                statusText.color = verified || matched
                    ? new Color(0.34f, 0.66f, 0.43f, 1f)
                    : new Color(0.95f, 0.91f, 0.82f, 1f);
            }
            if (lookupButton != null)
                lookupButton.interactable = dayCycle != null && dayCycle.CanUseCurrentIdentityMonitor && !verified;
            if (confirmButton != null)
                confirmButton.interactable = dayCycle != null && dayCycle.CurrentIdentityRequired && matched;
        }

        public void LookUpIdentity()
        {
            lookupError = string.Empty;
            lookupInProgress = true;
            Refresh();
            string message = "조회를 완료할 수 없습니다.";
            if (dayCycle == null || !dayCycle.LookUpCurrentIdentity(out message))
                lookupError = message;
            lookupInProgress = false;
            Refresh();
        }

        public void ConfirmIdentity()
        {
            if (dayCycle != null && dayCycle.ConfirmCurrentIdentity())
                Refresh();
        }

        private TextMeshProUGUI FindLabel(string labelName)
        {
            foreach (TextMeshProUGUI label in GetComponentsInChildren<TextMeshProUGUI>(true))
                if (label.name == labelName)
                    return label;
            return null;
        }

        private void ResolveReferences()
        {
            dayCycle ??= FindAnyObjectByType<DayCycleManager>();
            playerController ??= FindAnyObjectByType<FirstPersonController>();
            EnsureEventSystem();
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
                return;

            GameObject eventSystem = new("Identity Monitor EventSystem", typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            eventSystem.transform.SetParent(transform, false);
        }

        private void RestorePlayerInput()
        {
            if (playerController != null && restorePlayerInput)
                playerController.SetInputEnabled(true);
            restorePlayerInput = false;
        }
    }
}
