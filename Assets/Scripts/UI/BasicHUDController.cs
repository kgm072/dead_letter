using DeadLetter.Interaction;
using DeadLetter.Mail;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeadLetter.UI
{
    public sealed class BasicHUDController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private FirstPersonInteractor interactor;
        [SerializeField] private LetterTutorialHUD tutorial;
        [SerializeField] private LetterTaskState taskState;
        [SerializeField] private DeadLetterUITheme theme;

        [Header("HUD Elements")]
        [SerializeField] private TextMeshProUGUI dayText;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private TextMeshProUGUI currentWorkText;
        [SerializeField] private TextMeshProUGUI nextActionText;
        [SerializeField] private Image crosshair;
        [SerializeField] private GameObject promptPanel;
        [SerializeField] private TextMeshProUGUI promptButtonText;
        [SerializeField] private TextMeshProUGUI promptActionText;
        [SerializeField] private GameObject taskPanel;

        private string displayedPrompt = string.Empty;
        private bool displayedInteractive;

        private void Awake()
        {
            ResolveReferences();
            ApplyTheme();
            RefreshTaskHUD();
            RefreshWorldHUD(true);
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (taskState != null)
                taskState.Changed += RefreshTaskHUD;
            RefreshTaskHUD();
        }

        private void OnDisable()
        {
            if (taskState != null)
                taskState.Changed -= RefreshTaskHUD;
        }

        private void Update()
        {
            RefreshWorldHUD(false);
        }

        private void ResolveReferences()
        {
            interactor ??= FindAnyObjectByType<FirstPersonInteractor>();
            tutorial ??= FindAnyObjectByType<LetterTutorialHUD>();
            taskState ??= tutorial != null ? tutorial.TaskState : FindAnyObjectByType<LetterTaskState>();
        }

        private void RefreshWorldHUD(bool force)
        {
            bool worldFocused = interactor != null && interactor.isActiveAndEnabled &&
                                Cursor.lockState == CursorLockMode.Locked &&
                                (taskState == null || !taskState.WorkstationViewOpen);
            bool interactive = worldFocused && interactor.HasInteractionTarget;
            string prompt = interactive ? interactor.CurrentPrompt : string.Empty;

            if (force || interactive != displayedInteractive)
            {
                displayedInteractive = interactive;
                if (crosshair != null)
                    crosshair.color = interactive && theme != null ? theme.Accent : GetPrimaryTextColor();
                if (promptPanel != null)
                    promptPanel.SetActive(interactive);
            }

            if (force || prompt != displayedPrompt)
            {
                displayedPrompt = prompt;
                if (promptActionText != null)
                    promptActionText.text = prompt;
            }

            if (taskPanel != null)
                taskPanel.SetActive(taskState == null || !taskState.WorkstationViewOpen);
        }

        private void RefreshTaskHUD()
        {
            if (taskState == null)
            {
                if (dayText != null) dayText.text = "DAY --";
                if (progressText != null) progressText.text = "업무 -- / --";
                if (currentWorkText != null) currentWorkText.text = "현재 업무: 정보 없음";
                if (nextActionText != null) nextActionText.text = "다음 행동: 업무 상태를 확인할 수 없음";
                return;
            }

            if (dayText != null)
                dayText.text = $"DAY {taskState.CurrentDay:00}";
            if (progressText != null)
                progressText.text = $"업무 {taskState.CompletedRequests:00} / {taskState.RequiredRequestsPerDay:00}";
            if (currentWorkText != null)
                currentWorkText.text = $"현재 업무: {(tutorial != null ? tutorial.GetCurrentWorkLabel() : "편지 처리")}";
            if (nextActionText != null)
                nextActionText.text = $"다음 행동: {(tutorial != null ? tutorial.GetNextActionLabel() : "업무 상태 확인")}";
        }

        private void ApplyTheme()
        {
            if (theme == null)
                return;

            ApplyTextStyle(dayText, theme.HeadingSize, theme.PrimaryText);
            ApplyTextStyle(progressText, theme.BodySize, theme.PrimaryText);
            ApplyTextStyle(currentWorkText, theme.CaptionSize, theme.PrimaryText);
            ApplyTextStyle(nextActionText, theme.CaptionSize, theme.MutedText);
            ApplyTextStyle(promptButtonText, theme.CaptionSize, theme.Accent);
            ApplyTextStyle(promptActionText, theme.CaptionSize, theme.PrimaryText);
            if (crosshair != null)
                crosshair.color = theme.PrimaryText;
        }

        private void ApplyTextStyle(TextMeshProUGUI target, int size, Color color)
        {
            if (target == null)
                return;
            target.font = theme.PrimaryFont;
            target.fontSize = size;
            target.color = color;
        }

        private Color GetPrimaryTextColor()
        {
            return theme != null ? theme.PrimaryText : new Color(0.95f, 0.91f, 0.82f, 1f);
        }
    }
}
