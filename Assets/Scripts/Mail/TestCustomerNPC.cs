using DeadLetter.Interaction;
using UnityEngine;

namespace DeadLetter.Mail
{
    public sealed class TestCustomerNPC : MonoBehaviour, IInteractable, IConditionalInteractable
    {
        private enum CustomerState
        {
            Approaching,
            Waiting,
            LetterHandedOver
        }

        [SerializeField] private LetterTaskState taskState;
        [SerializeField] private HingedDoor entranceDoor;
        [SerializeField] private Transform[] waypoints;
        [SerializeField] private GameObject letterProp;
        [SerializeField] private string customerId = "customer_mascot_test";
        [SerializeField] private string letterId = "letter_mascot_001";
        [SerializeField] private LetterStamp requiredStamps = LetterStamp.Fire | LetterStamp.Shock;
        [SerializeField, Min(0.1f)] private float moveSpeed = 1.45f;
        [SerializeField, Min(1f)] private float turnSpeed = 360f;

        private CustomerState state = CustomerState.Approaching;
        private int waypointIndex;
        private bool openedEntrance;
        private string dialogue;
        private float dialogueUntil;
        private GUIStyle dialogueStyle;
        private GUIStyle speakerStyle;

        public bool IsWaiting => state == CustomerState.Waiting;

        private void Update()
        {
            if (state != CustomerState.Approaching || waypoints == null || waypoints.Length == 0)
                return;

            if (!openedEntrance && entranceDoor != null && Vector3.Distance(transform.position, entranceDoor.transform.position) < 2.4f)
            {
                entranceDoor.OpenFrom(gameObject);
                openedEntrance = true;
            }

            Transform target = waypoints[Mathf.Clamp(waypointIndex, 0, waypoints.Length - 1)];
            Vector3 targetPosition = target.position;
            targetPosition.y = transform.position.y;
            Vector3 direction = targetPosition - transform.position;
            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
            }

            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
            if (Vector3.Distance(transform.position, targetPosition) > 0.04f)
                return;

            waypointIndex++;
            if (waypointIndex < waypoints.Length)
                return;

            state = CustomerState.Waiting;
            dialogue = "안녕하세요. 이 편지를 접수해 주세요.";
            dialogueUntil = float.PositiveInfinity;
            if (entranceDoor != null)
                entranceDoor.CloseDoor();
        }

        public bool CanInteract(GameObject interactor)
        {
            return state == CustomerState.Waiting && taskState != null && !taskState.HasLetter;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor))
                return;

            if (!taskState.TryReceiveLetter(letterId, customerId, requiredStamps))
                return;

            state = CustomerState.LetterHandedOver;
            dialogue = "감사합니다. 불과 충격 우표를 모두 붙이고 도장을 찍어 주세요.";
            dialogueUntil = Time.time + 7f;
            if (letterProp != null)
                letterProp.SetActive(false);
            Debug.Log($"Accepted {letterId} from {customerId}.", this);
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(dialogue) || Time.time > dialogueUntil)
                return;

            EnsureDialogueStyles();
            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1920f, Screen.height / 1080f), 0.75f, 1.15f);
            float width = 650f * scale;
            float height = 105f * scale;
            Rect panel = new Rect((Screen.width - width) * 0.5f, Screen.height - height - 42f * scale, width, height);
            DrawRect(panel, new Color(0.025f, 0.02f, 0.03f, 0.93f));
            DrawRect(new Rect(panel.x, panel.y, 6f * scale, panel.height), new Color(0.92f, 0.28f, 0.08f, 1f));
            GUI.Label(new Rect(panel.x + 24f * scale, panel.y + 13f * scale, panel.width - 48f * scale, 30f * scale), "인형탈 민원인", speakerStyle);
            GUI.Label(new Rect(panel.x + 24f * scale, panel.y + 47f * scale, panel.width - 48f * scale, 42f * scale), dialogue, dialogueStyle);
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
                fontSize = 20,
                normal = { textColor = new Color(0.96f, 0.92f, 0.84f, 1f) }
            };
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
