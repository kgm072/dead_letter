using UnityEngine;

namespace DeadLetter.Interaction
{
    /// <summary>Rotates a greybox door around its hinge when the player interacts with it.</summary>
    public sealed class SwingDoor : MonoBehaviour, IInteractable
    {
        [SerializeField, Range(1f, 160f)] private float openAngle = 95f;
        [SerializeField, Min(1f)] private float rotationSpeed = 220f;

        private Quaternion closedRotation;
        private Quaternion openRotation;
        private bool isOpen;

        private void Awake()
        {
            closedRotation = transform.localRotation;
            openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
        }

        private void Update()
        {
            Quaternion target = isOpen ? openRotation : closedRotation;
            transform.localRotation = Quaternion.RotateTowards(
                transform.localRotation,
                target,
                rotationSpeed * Time.deltaTime);
        }

        public void Interact(GameObject interactor)
        {
            isOpen = !isOpen;
        }
    }
}
