using UnityEngine;

namespace DeadLetter.Interaction
{
    public sealed class HingedDoor : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform hinge;
        [SerializeField] private float openAngle = 90f;
        [SerializeField, Min(1f)] private float rotationSpeed = 140f;
        [SerializeField] private Vector3 wallNormal = Vector3.forward;
        [SerializeField] private float positiveSideAngleSign = 1f;

        private Quaternion closedRotation;
        private Quaternion openRotation;
        private bool isOpen;

        private void Awake()
        {
            if (hinge == null)
                hinge = transform;

            closedRotation = hinge.localRotation;
            openRotation = closedRotation * Quaternion.Euler(0f, Mathf.Abs(openAngle), 0f);
        }

        private void Update()
        {
            if (hinge == null)
                return;

            Quaternion targetRotation = isOpen ? openRotation : closedRotation;
            hinge.localRotation = Quaternion.RotateTowards(
                hinge.localRotation,
                targetRotation,
                rotationSpeed * Time.deltaTime);
        }

        public void Interact(GameObject interactor)
        {
            if (isOpen)
                CloseDoor();
            else
                OpenFrom(interactor);

            Debug.Log($"{name} {(isOpen ? "opened" : "closed")}.", this);
        }

        public void OpenFrom(GameObject interactor)
        {
            if (isOpen)
                return;

            if (interactor != null)
            {
                Vector3 localInteractor = hinge.InverseTransformPoint(interactor.transform.position);
                float pushedAngle = CalculatePushAngle(localInteractor, wallNormal, openAngle, positiveSideAngleSign);
                openRotation = closedRotation * Quaternion.Euler(0f, pushedAngle, 0f);
            }

            isOpen = true;
        }

        public void CloseDoor()
        {
            isOpen = false;
        }

        public static float CalculatePushAngle(Vector3 localInteractor, Vector3 normal, float angle, float positiveSideSign)
        {
            float side = Vector3.Dot(localInteractor, normal.normalized);
            float sideSign = Mathf.Abs(side) < 0.01f ? 1f : Mathf.Sign(side);
            return Mathf.Abs(angle) * sideSign * Mathf.Sign(positiveSideSign);
        }
    }
}
