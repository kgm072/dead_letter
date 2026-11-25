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
            if (dayCycle == null || !dayCycle.CurrentDayOneIdentityRequired)
                return;

            restorePlayerInput = playerController == null || playerController.InputEnabled;
            if (playerController != null)
                playerController.SetInputEnabled(false);
            gameObject.SetActive(true);
            lookupError = string.Empty;
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
            bool verified = dayCycle != null && dayCycle.CurrentDayOneIdentityVerified;
            bool matched = dayCycle != null && dayCycle.CurrentDayOneIdentityMatches;

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
                lookupButton.interactable = dayCycle != null && dayCycle.CanUseDayOneIdentityMonitor && !verified;
            if (confirmButton != null)
                confirmButton.interactable = dayCycle != null && dayCycle.CurrentDayOneIdentityRequired && matched;
        }

        public void LookUpIdentity()
        {
            lookupError = string.Empty;
            lookupInProgress = true;
            Refresh();
            string message = "조회를 완료할 수 없습니다.";
            if (dayCycle == null || !dayCycle.LookUpCurrentDayOneIdentity(out message))
                lookupError = message;
            lookupInProgress = false;
            Refresh();
        }

        public void ConfirmIdentity()
        {
            if (dayCycle != null && dayCycle.ConfirmCurrentDayOneIdentity())
                Refresh();
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
