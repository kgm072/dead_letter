using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadLetter.UI
{
    /// <summary>Owns pause state, cursor state, and the temporary in-game pause overlay.</summary>
    public sealed class PauseMenuController : MonoBehaviour
    {
        private bool isPaused;
        private GUIStyle titleStyle;
        private GUIStyle buttonStyle;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                SetPaused(!isPaused);
        }

        private void OnDisable()
        {
            if (isPaused)
                Time.timeScale = 1f;
        }

        private void SetPaused(bool paused)
        {
            isPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = paused;
        }

        private void OnGUI()
        {
            if (!isPaused)
                return;

            EnsureStyles();

            Color previousColor = GUI.color;
            GUI.color = new Color(0.015f, 0.018f, 0.025f, 0.88f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previousColor;

            const float panelWidth = 380f;
            const float panelHeight = 330f;
            Rect panel = new Rect(
                (Screen.width - panelWidth) * 0.5f,
                (Screen.height - panelHeight) * 0.5f,
                panelWidth,
                panelHeight);

            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x, panel.y + 42f, panel.width, 56f), "DEAD LETTER", titleStyle);

            if (GUI.Button(new Rect(panel.x + 70f, panel.y + 135f, panel.width - 140f, 48f), "계속하기", buttonStyle))
                SetPaused(false);

            GUI.enabled = false;
            GUI.Button(new Rect(panel.x + 70f, panel.y + 195f, panel.width - 140f, 48f), "설정 (준비 중)", buttonStyle);
            GUI.enabled = true;

            if (GUI.Button(new Rect(panel.x + 70f, panel.y + 255f, panel.width - 140f, 40f), "게임 종료", buttonStyle))
                QuitGame();
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
                return;

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.92f, 0.25f, 0.16f) }
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
