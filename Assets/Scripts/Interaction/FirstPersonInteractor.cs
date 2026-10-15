using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadLetter.Interaction
{
    public sealed class FirstPersonInteractor : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField, Min(0.1f)] private float interactionDistance = 3f;

        private IInteractable focusedInteractable;

        public float InteractionDistance
        {
            get => interactionDistance;
            set => interactionDistance = Mathf.Max(0.1f, value);
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || playerCamera == null || Cursor.lockState != CursorLockMode.Locked)
            {
                focusedInteractable = null;
                return;
            }

            focusedInteractable = FindFocusedInteractable();
            if (!mouse.rightButton.wasPressedThisFrame || focusedInteractable == null)
                return;

            focusedInteractable.Interact(gameObject);
        }

        private IInteractable FindFocusedInteractable()
        {
            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, interactionDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return null;

            MonoBehaviour[] behaviours = hit.collider.GetComponentsInParent<MonoBehaviour>();
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour is IInteractable interactable)
                    return interactable;
            }

            return null;
        }

        private void OnGUI()
        {
            if (focusedInteractable == null || Cursor.lockState != CursorLockMode.Locked)
                return;

            const float width = 34f;
            const float height = 48f;
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height * 0.68f;
            Rect mouseBody = new Rect(x, y, width, height);

            Color previousColor = GUI.color;
            GUI.color = new Color(0.03f, 0.035f, 0.045f, 0.9f);
            GUI.Box(mouseBody, GUIContent.none);

            float pulse = 0.72f + Mathf.Sin(Time.unscaledTime * 4.5f) * 0.18f;
            GUI.color = new Color(1f, 0.42f, 0.12f, pulse);
            GUI.DrawTexture(new Rect(x + width * 0.52f, y + 3f, width * 0.38f, 17f), Texture2D.whiteTexture);

            GUI.color = new Color(0.8f, 0.84f, 0.9f, 0.9f);
            GUI.DrawTexture(new Rect(x + width * 0.48f, y + 5f, 2f, 13f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x + width * 0.46f, y + 25f, 3f, 8f), Texture2D.whiteTexture);
            GUI.color = previousColor;
        }
    }
}
