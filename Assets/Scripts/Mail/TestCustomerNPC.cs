using DeadLetter.Interaction;
using UnityEngine;

namespace DeadLetter.Mail
{
    public sealed class TestCustomerNPC : MonoBehaviour, IInteractable, IConditionalInteractable
    {
        private enum CustomerState
        {
            Inactive,
            Approaching,
            WaitingForLetter,
            WaitingForDispatch,
            Departing
        }

        [SerializeField] private LetterTaskState taskState;
        [SerializeField] private HingedDoor entranceDoor;
        [SerializeField] private Transform[] waypoints;
        [SerializeField] private GameObject letterProp;
        [SerializeField, Min(0.1f)] private float moveSpeed = 1.45f;
        [SerializeField, Min(1f)] private float turnSpeed = 360f;

        private CustomerState state = CustomerState.Inactive;
        private DayCycleManager dayCycle;
        private int waypointIndex;
        private bool openedEntrance;
        private string customerId;
        private string customerDisplayName;
        private string letterId;
        private LetterStamp requiredStamps;
        private string dialogue;
        private float dialogueUntil;
        private GUIStyle dialogueStyle;
        private GUIStyle speakerStyle;

        private void Awake()
        {
            HideUntilNextCustomer();
        }

        private void Update()
        {
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
            dialogueUntil = 0f;
            if (waypoints != null && waypoints.Length > 0)
                transform.position = waypoints[0].position + new Vector3(0f, 0f, -1.5f);
            if (letterProp != null)
                letterProp.SetActive(true);
            gameObject.SetActive(true);
        }

        public void HideUntilNextCustomer()
        {
            state = CustomerState.Inactive;
            dialogue = string.Empty;
            if (letterProp != null)
                letterProp.SetActive(false);
            gameObject.SetActive(false);
        }

        public bool CanInteract(GameObject interactor)
        {
            if (taskState == null)
                return false;

            return state == CustomerState.WaitingForLetter && !taskState.HasLetter ||
                   state == CustomerState.WaitingForDispatch && taskState.LetterMailed;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor))
                return;

            if (state == CustomerState.WaitingForLetter)
            {
                if (!taskState.TryReceiveLetter(letterId, customerId, customerDisplayName, requiredStamps))
                    return;

                state = CustomerState.WaitingForDispatch;
                dialogue = $"필요한 우표는 {GetStampList(requiredStamps)}입니다. 처리 후 다시 알려 주세요.";
                dialogueUntil = Time.time + 6f;
                if (letterProp != null)
                    letterProp.SetActive(false);
                return;
            }

            dialogue = "확인했습니다. 수고하셨습니다.";
            dialogueUntil = Time.time + 2.5f;
            state = CustomerState.Departing;
            waypointIndex = waypoints == null ? -1 : waypoints.Length - 1;
            openedEntrance = false;
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
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
            }

            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
            if (Vector3.Distance(transform.position, targetPosition) > 0.04f)
                return;

            if (state == CustomerState.Approaching)
            {
                waypointIndex++;
                if (waypointIndex < waypoints.Length)
                    return;

                state = CustomerState.WaitingForLetter;
                dialogue = "안녕하세요. 이 편지를 접수해 주세요.";
                dialogueUntil = float.PositiveInfinity;
                if (entranceDoor != null)
                    entranceDoor.CloseDoor();
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

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(dialogue) || Time.time > dialogueUntil)
                return;

            EnsureDialogueStyles();
            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1920f, Screen.height / 1080f), 0.75f, 1.15f);
            float width = 720f * scale;
            float height = 112f * scale;
            Rect panel = new Rect((Screen.width - width) * 0.5f, Screen.height - height - 42f * scale, width, height);
            DrawRect(panel, new Color(0.025f, 0.02f, 0.03f, 0.93f));
            DrawRect(new Rect(panel.x, panel.y, 6f * scale, panel.height), new Color(0.92f, 0.28f, 0.08f, 1f));
            GUI.Label(new Rect(panel.x + 24f * scale, panel.y + 13f * scale, panel.width - 48f * scale, 30f * scale), $"{customerDisplayName} 민원인", speakerStyle);
            GUI.Label(new Rect(panel.x + 24f * scale, panel.y + 47f * scale, panel.width - 48f * scale, 50f * scale), dialogue, dialogueStyle);
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
        }

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
