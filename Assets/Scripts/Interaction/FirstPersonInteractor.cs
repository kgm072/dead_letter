using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadLetter.Interaction
{
    public sealed class FirstPersonInteractor : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField, Min(0.1f)] private float interactionDistance = 3f;
        [SerializeField] private LayerMask interactionLayers = Physics.DefaultRaycastLayers;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [SerializeField] private bool debugRaycast;
        private Collider lastLoggedCollider;
#endif

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
            if (!Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactionLayers, QueryTriggerInteraction.Ignore))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                DrawDebugRay(ray, interactionDistance, false);
                LogDebugHit(null, null, 0f);
#endif
                return;
            }

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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    DrawDebugRay(ray, hit.distance, true);
                    LogDebugHit(hit.collider, behaviour, hit.distance);
#endif
                    return;
                }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DrawDebugRay(ray, hit.distance, false);
            LogDebugHit(hit.collider, null, hit.distance);
#endif
        }

        private void ClearTarget()
        {
            currentInteractable = null;
            currentPrompt = string.Empty;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void DrawDebugRay(Ray ray, float distance, bool interactable)
        {
            if (debugRaycast)
                Debug.DrawRay(ray.origin, ray.direction * distance, interactable ? Color.green : Color.yellow);
        }

        private void LogDebugHit(Collider hitCollider, MonoBehaviour interactable, float distance)
        {
            if (!debugRaycast || hitCollider == lastLoggedCollider)
                return;

            lastLoggedCollider = hitCollider;
            if (hitCollider == null)
            {
                Debug.Log($"[Interaction Raycast] No hit within {interactionDistance:F2}m. LayerMask={interactionLayers.value}", this);
                return;
            }

            string interactableName = interactable != null ? interactable.gameObject.name : "None";
            string interactableType = interactable != null ? interactable.GetType().Name : "None";
            Debug.Log(
                $"[Interaction Raycast] Collider={hitCollider.name}, Position={hitCollider.transform.position}, " +
                $"Interactable={interactableName} ({interactableType}), Distance={distance:F2}m, " +
                $"LayerMask={interactionLayers.value}",
                hitCollider);
        }
#endif
    }
}
