using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadLetter.Interaction
{
    public sealed class FirstPersonInteractor : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField, Min(0.1f)] private float interactionDistance = 3f;

        private IInteractable currentInteractable;
        private bool showInteractionPrompt;
        private GUIStyle promptStyle;

        public float InteractionDistance
        {
            get => interactionDistance;
            set => interactionDistance = Mathf.Max(0.1f, value);
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            bool cursorLocked = Cursor.lockState == CursorLockMode.Locked;

            FindInteractable(cursorLocked);

            bool rightClicked = mouse != null && mouse.rightButton.wasPressedThisFrame;
            bool pressedF = keyboard != null && keyboard.fKey.wasPressedThisFrame;
            if (currentInteractable != null && (rightClicked || pressedF))
                currentInteractable.Interact(gameObject);
        }

        private void FindInteractable(bool cursorLocked)
        {
            currentInteractable = null;
            showInteractionPrompt = false;

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
                    currentInteractable = interactable;
                    showInteractionPrompt = true;
                    return;
                }
            }
        }

        private void OnGUI()
        {
            if (!showInteractionPrompt)
                return;

            if (promptStyle == null)
            {
                promptStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white }
                };
            }

            float centerX = Screen.width * 0.5f;
            float centerY = Screen.height * 0.68f;
            Rect background = new Rect(centerX - 82f, centerY - 34f, 164f, 68f);
            DrawRect(background, new Color(0.035f, 0.025f, 0.04f, 0.82f));
            DrawMouseIcon(new Rect(centerX - 65f, centerY - 23f, 30f, 40f));
            GUI.Label(new Rect(centerX - 24f, centerY - 21f, 94f, 24f), "RMB  /  F", promptStyle);
            GUI.Label(new Rect(centerX - 24f, centerY + 1f, 94f, 22f), "INTERACT", promptStyle);
        }

        private static void DrawMouseIcon(Rect rect)
        {
            Color outline = new Color(0.95f, 0.95f, 0.95f, 1f);
            Color highlight = new Color(1f, 0.34f, 0.12f, 1f);
            DrawRect(new Rect(rect.x, rect.y, rect.width, 2f), outline);
            DrawRect(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), outline);
            DrawRect(new Rect(rect.x, rect.y, 2f, rect.height), outline);
            DrawRect(new Rect(rect.xMax - 2f, rect.y, 2f, rect.height), outline);
            DrawRect(new Rect(rect.center.x - 1f, rect.y, 2f, 16f), outline);
            DrawRect(new Rect(rect.center.x + 1f, rect.y + 2f, rect.width * 0.5f - 3f, 14f), highlight);
            DrawRect(new Rect(rect.center.x - 2f, rect.y + 7f, 4f, 8f), outline);
        }

        private static void DrawRect(Rect rect, Color color)
        {
            Color previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }
    }
}
