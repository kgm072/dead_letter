using UnityEngine;
using UnityEngine.InputSystem;
using DeadLetter.UI;

namespace DeadLetter.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [SerializeField] private Transform viewPivot;
        [SerializeField, Min(0.1f)] private float moveSpeed = 4.5f;
        [SerializeField, Min(0.1f)] private float gravity = 20f;
        [SerializeField, Min(0.01f)] private float lookSensitivity = 0.08f;
        [SerializeField, Range(1f, 89f)] private float lookLimit = 80f;

        private CharacterController characterController;
        private float pitch;
        private float verticalSpeed;
        private bool cursorLocked;
        private bool inputEnabled = true;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            if (PlayerPrefs.HasKey(MainMenuController.MouseSensitivityPreference))
                lookSensitivity = Mathf.Lerp(0.025f, 0.18f, PlayerPrefs.GetFloat(MainMenuController.MouseSensitivityPreference));
            LockCursor();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null || mouse == null)
                return;

            if (!inputEnabled)
                return;

            if (keyboard.escapeKey.wasPressedThisFrame)
                UnlockCursor();
            else if (!cursorLocked && mouse.leftButton.wasPressedThisFrame)
                LockCursor();

            if (cursorLocked)
            {
                ReadLook(mouse);
                Move(keyboard);
            }
        }

        private void ReadLook(Mouse mouse)
        {
            Vector2 delta = mouse.delta.ReadValue();
            transform.Rotate(Vector3.up, delta.x * lookSensitivity, Space.World);

            pitch = Mathf.Clamp(pitch - delta.y * lookSensitivity, -lookLimit, lookLimit);
            if (viewPivot != null)
                viewPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void Move(Keyboard keyboard)
        {
            Vector2 input = Vector2.zero;
            if (keyboard.wKey.isPressed) input.y += 1f;
            if (keyboard.sKey.isPressed) input.y -= 1f;
            if (keyboard.dKey.isPressed) input.x += 1f;
            if (keyboard.aKey.isPressed) input.x -= 1f;
            input = Vector2.ClampMagnitude(input, 1f);

            if (characterController.isGrounded && verticalSpeed < 0f)
                verticalSpeed = -2f;
            verticalSpeed -= gravity * Time.deltaTime;
            Vector3 planar = (transform.right * input.x + transform.forward * input.y) * moveSpeed;
            Vector3 velocity = planar + Vector3.up * verticalSpeed;
            characterController.Move(velocity * Time.deltaTime);
        }

        private void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            cursorLocked = true;
        }

        private void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            cursorLocked = false;
        }

        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
            if (enabled)
                LockCursor();
            else
                UnlockCursor();
        }
    }
}
