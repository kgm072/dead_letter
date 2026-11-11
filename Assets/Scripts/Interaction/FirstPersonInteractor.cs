using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadLetter.Interaction
{
    public sealed class FirstPersonInteractor : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField, Min(0.1f)] private float interactionDistance = 3f;

        private IInteractable currentInteractable;
        private string currentPrompt = string.Empty;

        public bool HasInteractionTarget => currentInteractable != null;
        public string CurrentPrompt => currentPrompt;

        public float InteractionDistance
        {
            get => interactionDistance;
            set => interactionDistance = Mathf.Max(0.1f, value);
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            bool cursorLocked = Cursor.lockState == CursorLockMode.Locked;

            FindInteractable(cursorLocked);

            bool rightClicked = mouse != null && mouse.rightButton.wasPressedThisFrame;
            if (currentInteractable != null && rightClicked)
                currentInteractable.Interact(gameObject);
        }

        private void OnDisable()
        {
            ClearTarget();
        }

        private void FindInteractable(bool cursorLocked)
        {
            ClearTarget();

            if (!cursorLocked || playerCamera == null)
                return;

            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, interactionDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return;

            MonoBehaviour[] behaviours = hit.collider.GetComponentsInParent<MonoBehaviour>();
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour is IInteractable interactable)
                {
                    if (behaviour is IConditionalInteractable conditional && !conditional.CanInteract(gameObject))
                        continue;

                    currentInteractable = interactable;
                    currentPrompt = behaviour is IInteractionPromptProvider promptProvider
                        ? promptProvider.GetInteractionPrompt(gameObject)
                        : "상호작용";
                    return;
                }
            }
        }

        private void ClearTarget()
        {
            currentInteractable = null;
            currentPrompt = string.Empty;
        }
    }
}
