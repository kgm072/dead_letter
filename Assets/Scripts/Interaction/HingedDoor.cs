using UnityEngine;

namespace DeadLetter.Interaction
{
    public sealed class HingedDoor : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform hinge;
        [SerializeField] private float openAngle = 90f;
        [SerializeField, Min(1f)] private float rotationSpeed = 140f;

        private Quaternion closedRotation;
        private Quaternion openRotation;
        private bool isOpen;

        private void Awake()
        {
            if (hinge == null)
                hinge = transform;

            closedRotation = hinge.localRotation;
            openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
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
            isOpen = !isOpen;
            Debug.Log($"{name} {(isOpen ? "opened" : "closed")}.", this);
        }
    }
}
