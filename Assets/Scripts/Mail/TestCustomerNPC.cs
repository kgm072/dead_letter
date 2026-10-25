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
            if (letterProp != null)
                letterProp.SetActive(false);
            Debug.Log($"Accepted {letterId} from {customerId}.", this);
        }
    }
}
