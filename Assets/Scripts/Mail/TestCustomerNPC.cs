using System.Collections.Generic;
using DeadLetter.Characters;
using DeadLetter.Interaction;
using DeadLetter.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadLetter.Mail
{
    public sealed class TestCustomerNPC : MonoBehaviour, IInteractable, IConditionalInteractable,
        IInteractionPromptProvider, IInteractionRaycastPassthrough
    {
        private enum CustomerState
        {
            Inactive,
            Approaching,
            WaitingForLetter,
            WaitingForDispatch,
            Departing
        }

        private enum DialoguePhase
        {
            None,
            Opening,
            Completion,
            CompletionPreview
        }

        [SerializeField] private LetterTaskState taskState;
        [SerializeField] private HingedDoor entranceDoor;
        [SerializeField] private Transform[] waypoints;
        [Tooltip("The transform the customer should face after reaching the counter.")]
        [SerializeField] private Transform waitingLookTarget;
        [Tooltip("Yaw correction between the prefab/model forward axis and Unity's +Z forward axis.")]
        [SerializeField] private float modelForwardYawOffset = 180f;
        [SerializeField] private GameObject letterProp;
        [SerializeField, Min(0.1f)] private float moveSpeed = 1.45f;
        [SerializeField, Min(1f)] private float turnSpeed = 360f;
        [Tooltip("손님 ID별 캐릭터 모델. 비어 있으면 같은 오브젝트에서 찾는다.")]
        [SerializeField] private CustomerVisualSet visualSet;

        private CustomerState state = CustomerState.Inactive;
        private DayCycleManager dayCycle;
        private int waypointIndex;
        private bool openedEntrance;
        private string customerId;
        private string customerDisplayName;
        private string letterId;
        private LetterStamp requiredStamps;
        private string dialogue;
        private GUIStyle dialogueStyle;
        private GUIStyle speakerStyle;
        private GUIStyle choiceStyle;
        private CustomerDialogueScenario dialogueScenario;
        private readonly Queue<string> pendingDialogueLines = new();
        private CustomerDialogueChoice pendingChoice;
        private IReadOnlyList<string> pendingCommonLines;
        private DialoguePhase dialoguePhase;
        private bool waitingForChoice;
        private bool dialogueInputSuspended;
        private bool dialogueEventGateActive;
        private bool dialogueEntryPendingAfterGate;
        private string pendingSectionEventId = string.Empty;
        private bool pendingSectionEventAtStart;
        private FirstPersonController playerController;
        private bool playerInputBeforeDialogue;

        public bool DialogueActive => dialoguePhase != DialoguePhase.None;
        public bool WaitingForDialogueChoice => waitingForChoice;
        public bool DialogueEventGateActive => dialogueEventGateActive;
        public string DialoguePhaseLabel => dialoguePhase.ToString();
        public event System.Action<string> DialogueEventReached;

        public string GetInteractionPrompt(GameObject interactor)
        {
            if (state != CustomerState.WaitingForDispatch)
                return "편지 받기";

            return dayCycle?.CurrentPackageRequest?.Definition.RequestType == MailRequestType.Receive
                ? "포장 물품 전달"
                : "발송 결과 알리기";
        }

        public bool AllowsInteractionRaycastPassthrough(MonoBehaviour target)
        {
            return target is Day2WorldItem item && item.IsPresentedByCustomer;
        }

        private void Awake()
        {
            HideUntilNextCustomer();
        }

        private void Update()
        {
            if (DialogueActive)
            {
                HandleDialogueInput();
                return;
            }

            if (state != CustomerState.Approaching && state != CustomerState.Departing)
                return;

            MoveAlongRoute();
        }

        public void BeginCustomer(string id, string displayName, string newLetterId, LetterStamp stamps, DayCycleManager manager)
        {
            customerId = id;
            customerDisplayName = displayName;
            letterId = newLetterId;
            requiredStamps = stamps;
            dayCycle = manager;
            state = CustomerState.Approaching;
            waypointIndex = 0;
            openedEntrance = false;
            dialogue = string.Empty;
            dialogueScenario = CustomerDialogueCatalog.TryGet(taskState != null ? taskState.CurrentDay : 1, id, out CustomerDialogueScenario scenario)
                ? scenario
                : null;
            ResetDialogueRuntime();
            if (waypoints != null && waypoints.Length > 0)
            {
                transform.position = waypoints[0].position + new Vector3(0f, 0f, -1.5f);
                FaceDirection(waypoints[0].position - transform.position, true);
            }
            if (letterProp != null)
                letterProp.SetActive(true);
            gameObject.SetActive(true);

            if (visualSet == null)
                visualSet = GetComponent<CustomerVisualSet>();
            if (visualSet != null)
            {
                visualSet.SetTalkingQuery(() => DialogueActive);
                visualSet.Show(taskState != null ? taskState.CurrentDay : 1, id);
            }
        }

        public void HideUntilNextCustomer()
        {
            EndDialogueLock();
            state = CustomerState.Inactive;
            dialogue = string.Empty;
            ResetDialogueRuntime();
            if (letterProp != null)
                letterProp.SetActive(false);
            gameObject.SetActive(false);
        }

        public bool CanInteract(GameObject interactor)
        {
            if (taskState == null)
                return false;

            if (DialogueActive)
                return false;

            return state == CustomerState.WaitingForLetter && !taskState.HasLetter &&
                   (dayCycle == null || dayCycle.CanAcceptCurrentLetter(customerId)) ||
                   state == CustomerState.WaitingForDispatch &&
                   (dayCycle != null
                       ? dayCycle.CanFinishCurrentCustomer(customerId, letterId)
                       : taskState.LetterMailed);
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor))
                return;

            if (state == CustomerState.WaitingForLetter)
            {
                if (dayCycle != null && !dayCycle.CanAcceptCurrentLetter(customerId))
                    return;
                if (!taskState.TryReceiveLetter(letterId, customerId, customerDisplayName, requiredStamps))
                    return;

                state = CustomerState.WaitingForDispatch;
                dayCycle?.NotifyRequestAccepted(letterId);
                dialogue = string.Empty;
                if (letterProp != null)
                    letterProp.SetActive(false);
                return;
            }

            if (dayCycle != null && !dayCycle.TryFinishCurrentCustomer(customerId, letterId))
                return;

            BeginDialogue(DialoguePhase.Completion, dialogueScenario?.Completion);
        }

        private void MoveAlongRoute()
        {
            if (waypoints == null || waypoints.Length == 0)
                return;

            int clampedIndex = Mathf.Clamp(waypointIndex, 0, waypoints.Length - 1);
            Transform target = waypoints[clampedIndex];
            Vector3 targetPosition = target.position;
            targetPosition.y = transform.position.y;

            if (!openedEntrance && entranceDoor != null && Vector3.Distance(transform.position, entranceDoor.transform.position) < 2.4f)
            {
                entranceDoor.OpenFrom(gameObject);
                openedEntrance = true;
            }

            Vector3 direction = targetPosition - transform.position;
            if (direction.sqrMagnitude > 0.001f)
                FaceDirection(direction, false);

            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
            if (Vector3.Distance(transform.position, targetPosition) > 0.04f)
                return;

            if (state == CustomerState.Approaching)
            {
                waypointIndex++;
                if (waypointIndex < waypoints.Length)
                    return;

                state = CustomerState.WaitingForLetter;
                FaceWaitingTarget();
                if (entranceDoor != null)
                    entranceDoor.CloseDoor();
                BeginDialogue(DialoguePhase.Opening, dialogueScenario?.Opening);
                return;
            }

            waypointIndex--;
            if (waypointIndex >= 0)
                return;

            if (entranceDoor != null)
                entranceDoor.CloseDoor();
            DayCycleManager manager = dayCycle;
            HideUntilNextCustomer();
            manager?.NotifyCustomerDeparted();
        }

        private void FaceWaitingTarget()
        {
            if (waitingLookTarget == null)
                return;

            Vector3 direction = waitingLookTarget.position - transform.position;
            direction.y = 0f;
            FaceDirection(direction, true);
        }

        private void FaceDirection(Vector3 worldDirection, bool immediate)
        {
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude <= 0.0001f)
                return;

            Quaternion pathRotation = Quaternion.LookRotation(worldDirection.normalized, Vector3.up);
            Quaternion correctedRotation = pathRotation * Quaternion.Euler(0f, modelForwardYawOffset, 0f);
            transform.rotation = immediate
                ? correctedRotation
                : Quaternion.RotateTowards(transform.rotation, correctedRotation, turnSpeed * Time.deltaTime);
        }

        private void OnGUI()
        {
            if (!DialogueActive || (string.IsNullOrEmpty(dialogue) && !waitingForChoice))
                return;

            EnsureDialogueStyles();
            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1920f, Screen.height / 1080f), 0.75f, 1.15f);
            float width = 720f * scale;
            float height = (waitingForChoice ? 196f : 132f) * scale;
            Rect panel = new Rect((Screen.width - width) * 0.5f, Screen.height - height - 42f * scale, width, height);
            DrawRect(panel, new Color(0.025f, 0.02f, 0.03f, 0.93f));
            DrawRect(new Rect(panel.x, panel.y, 6f * scale, panel.height), new Color(0.92f, 0.28f, 0.08f, 1f));
            string speakerName = dialogueScenario?.SpeakerName ?? customerDisplayName;
            GUI.Label(new Rect(panel.x + 24f * scale, panel.y + 13f * scale, panel.width - 48f * scale, 30f * scale), $"{speakerName} 민원인", speakerStyle);
            if (waitingForChoice && pendingChoice != null)
            {
                CustomerDialogueChoice visibleChoice = pendingChoice;
                GUI.Label(new Rect(panel.x + 24f * scale, panel.y + 47f * scale, panel.width - 48f * scale, 26f * scale), "선택지를 고르세요.", dialogueStyle);
                if (GUI.Button(new Rect(panel.x + 24f * scale, panel.y + 80f * scale, panel.width - 48f * scale, 43f * scale),
                        $"[1] {visibleChoice.Options[0]}", choiceStyle))
                    SelectChoice(0);
                else if (GUI.Button(new Rect(panel.x + 24f * scale, panel.y + 130f * scale, panel.width - 48f * scale, 43f * scale),
                        $"[2] {visibleChoice.Options[1]}", choiceStyle))
                    SelectChoice(1);
            }
            else
            {
                GUI.Label(new Rect(panel.x + 24f * scale, panel.y + 47f * scale, panel.width - 48f * scale, 54f * scale), dialogue, dialogueStyle);
                GUI.Label(new Rect(panel.x + panel.width - 180f * scale, panel.y + panel.height - 27f * scale, 156f * scale, 20f * scale),
                    "[Space/Enter] 계속", choiceStyle);
            }
        }

        private void EnsureDialogueStyles()
        {
            if (dialogueStyle != null)
                return;

            Font koreanFont = KoreanGuiFont.Get();
            speakerStyle = new GUIStyle(GUI.skin.label)
            {
                font = koreanFont,
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.46f, 0.15f, 1f) }
            };
            dialogueStyle = new GUIStyle(GUI.skin.label)
            {
                font = koreanFont,
                fontSize = 19,
                wordWrap = true,
                normal = { textColor = new Color(0.96f, 0.92f, 0.84f, 1f) }
            };
            choiceStyle = new GUIStyle(GUI.skin.button)
            {
                font = koreanFont,
                fontSize = 15,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                normal = { textColor = new Color(0.96f, 0.92f, 0.84f, 1f) },
                hover = { textColor = new Color(1f, 0.58f, 0.24f, 1f) }
            };
        }

        private void BeginDialogue(DialoguePhase phase, CustomerDialogueSection section)
        {
            bool dialogueWasAlreadyActive = DialogueActive;
            dialoguePhase = phase;
            pendingDialogueLines.Clear();
            waitingForChoice = false;
            pendingChoice = section?.Choice;
            pendingCommonLines = section?.AfterChoice;
            pendingSectionEventId = section?.EventId ?? string.Empty;
            pendingSectionEventAtStart = section != null && section.EventAtStart;

            if (pendingSectionEventAtStart && !string.IsNullOrWhiteSpace(pendingSectionEventId))
                RaiseDialogueEvent(pendingSectionEventId);

            if (section != null)
            {
                foreach (string line in section.BeforeChoice)
                    EnqueueLine(line);
            }

            if (pendingChoice == null && pendingCommonLines != null)
            {
                foreach (string line in pendingCommonLines)
                    EnqueueLine(line);
                pendingCommonLines = null;
            }

            BeginDialogueLock(dialogueWasAlreadyActive);
            if (dialogueEventGateActive)
            {
                dialogue = string.Empty;
                dialogueEntryPendingAfterGate = true;
                return;
            }
            ShowNextDialogueEntry();
        }

        private void HandleDialogueInput()
        {
            if (dialogueInputSuspended || dialogueEventGateActive)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (waitingForChoice)
            {
                if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
                    SelectChoice(0);
                else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
                    SelectChoice(1);
                return;
            }

            if (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame ||
                keyboard.numpadEnterKey.wasPressedThisFrame)
                ShowNextDialogueEntry();
        }

        private void ShowNextDialogueEntry()
        {
            if (pendingDialogueLines.Count > 0)
            {
                dialogue = pendingDialogueLines.Dequeue();
                return;
            }

            if (pendingChoice != null)
            {
                dialogue = string.Empty;
                waitingForChoice = true;
                return;
            }

            FinishDialogue();
        }

        private void SelectChoice(int optionIndex)
        {
            if (!waitingForChoice || pendingChoice == null || optionIndex < 0 || optionIndex >= pendingChoice.Options.Count)
                return;

            IReadOnlyList<string> responses = pendingChoice.Responses[optionIndex];
            foreach (string line in responses)
                EnqueueLine(line);
            if (pendingCommonLines != null)
            {
                foreach (string line in pendingCommonLines)
                    EnqueueLine(line);
            }

            pendingChoice = null;
            pendingCommonLines = null;
            waitingForChoice = false;
            ShowNextDialogueEntry();
        }

        private void FinishDialogue()
        {
            DialoguePhase finishedPhase = dialoguePhase;
            if (!pendingSectionEventAtStart && !string.IsNullOrWhiteSpace(pendingSectionEventId))
                RaiseDialogueEvent(pendingSectionEventId);
            dialoguePhase = DialoguePhase.None;
            dialogue = string.Empty;
            waitingForChoice = false;
            pendingChoice = null;
            pendingCommonLines = null;
            pendingSectionEventId = string.Empty;
            pendingSectionEventAtStart = false;
            EndDialogueLock();

            if (finishedPhase == DialoguePhase.Opening)
                dayCycle?.NotifyDayTwoIdentityCheckRequested(customerId);

            if (finishedPhase == DialoguePhase.Completion)
                BeginDeparture();
        }

        private void BeginDeparture()
        {
            state = CustomerState.Departing;
            waypointIndex = waypoints == null ? -1 : waypoints.Length - 1;
            openedEntrance = false;
        }

        private void BeginDialogueLock(bool preservePreviousInputState)
        {
            playerController ??= FindAnyObjectByType<FirstPersonController>();
            if (!preservePreviousInputState)
                playerInputBeforeDialogue = playerController == null || playerController.InputEnabled;
            if (playerController != null)
                playerController.SetInputEnabled(false);
        }

        private void EndDialogueLock()
        {
            if (playerController != null && playerInputBeforeDialogue &&
                !dialogueInputSuspended && !dialogueEventGateActive)
                playerController.SetInputEnabled(true);
        }

        private void ResetDialogueRuntime()
        {
            pendingDialogueLines.Clear();
            pendingChoice = null;
            pendingCommonLines = null;
            dialoguePhase = DialoguePhase.None;
            waitingForChoice = false;
            dialogueInputSuspended = false;
            dialogueEventGateActive = false;
            dialogueEntryPendingAfterGate = false;
            pendingSectionEventId = string.Empty;
            pendingSectionEventAtStart = false;
        }

        private void EnqueueLine(string line)
        {
            if (!string.IsNullOrWhiteSpace(line))
                pendingDialogueLines.Enqueue(line);
        }

        private void RaiseDialogueEvent(string eventId)
        {
            DialogueEventReached?.Invoke(eventId);

            if (eventId == CustomerDialogueCatalog.DayOneIdentityCheckEvent)
                dayCycle?.NotifyDayOneIdentityCheckRequested(customerId);

            // The customer object is disabled between visitors. Depending on the
            // activation order, an event listener on that same object may not have
            // subscribed yet. Route the DAY 3 reveal directly as a safe fallback;
            // the sequence guards against duplicate starts.
            if (TryGetComponent(out Day3MascotBalloonSequence mascotSequence))
                mascotSequence.TryStartForDialogueEvent(eventId);
        }

        public void SetDialogueInputSuspended(bool suspended)
        {
            dialogueInputSuspended = suspended;
            if (!suspended && DialogueActive && playerController != null)
            {
                playerInputBeforeDialogue |= playerController.InputEnabled;
                playerController.SetInputEnabled(false);
            }
        }

        public void SetDialogueEventGate(bool active)
        {
            dialogueEventGateActive = active;
            if (active)
                return;

            if (DialogueActive && playerController != null)
            {
                playerInputBeforeDialogue |= playerController.InputEnabled;
                playerController.SetInputEnabled(false);
            }

            if (!dialogueEntryPendingAfterGate || !DialogueActive)
                return;

            dialogueEntryPendingAfterGate = false;
            ShowNextDialogueEntry();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool DebugSnapToWaitingPosition()
        {
            if (waypoints == null || waypoints.Length == 0)
                return false;

            transform.position = waypoints[^1].position;
            waypointIndex = waypoints.Length;
            FaceWaitingTarget();
            return true;
        }

        public bool DebugReplayOpeningDialogue()
        {
            if (dialogueScenario == null)
                return false;
            BeginDialogue(DialoguePhase.Opening, dialogueScenario.Opening);
            return true;
        }

        public bool DebugPreviewCompletionDialogue()
        {
            if (dialogueScenario == null)
                return false;
            BeginDialogue(DialoguePhase.CompletionPreview, dialogueScenario.Completion);
            return true;
        }

        public bool DebugAdvanceDialogue()
        {
            if (!DialogueActive || waitingForChoice || dialogueInputSuspended || dialogueEventGateActive)
                return false;
            ShowNextDialogueEntry();
            return true;
        }

        public bool DebugSelectDialogueChoice(int optionIndex)
        {
            if (!DialogueActive || !waitingForChoice)
                return false;
            SelectChoice(optionIndex);
            return true;
        }
#endif

        private static string GetStampList(LetterStamp stamps)
        {
            string result = string.Empty;
            AppendStamp(ref result, stamps, LetterStamp.Fire, "불");
            AppendStamp(ref result, stamps, LetterStamp.Water, "물");
            AppendStamp(ref result, stamps, LetterStamp.Electricity, "전기");
            AppendStamp(ref result, stamps, LetterStamp.Shock, "충격");
            AppendStamp(ref result, stamps, LetterStamp.Medicine, "약");
            AppendStamp(ref result, stamps, LetterStamp.Alcohol, "술");
            return result;
        }

        private static void AppendStamp(ref string value, LetterStamp stamps, LetterStamp target, string label)
        {
            if ((stamps & target) == 0)
                return;
            value = string.IsNullOrEmpty(value) ? label : value + " + " + label;
        }

        private static void DrawRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
