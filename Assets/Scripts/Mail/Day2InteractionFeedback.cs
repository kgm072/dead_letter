using UnityEngine;

namespace DeadLetter.Mail
{
    public sealed class Day2InteractionFeedback : MonoBehaviour
    {
        [SerializeField] private DayCycleManager dayCycle;
        private string message;
        private float messageUntil;
        private GUIStyle messageStyle;

        private void OnEnable()
        {
            dayCycle ??= FindAnyObjectByType<DayCycleManager>();
            if (dayCycle != null)
                dayCycle.ActionFeedback += Show;
        }

        private void OnDisable()
        {
            if (dayCycle != null)
                dayCycle.ActionFeedback -= Show;
        }

        private void Show(string value)
        {
            message = value;
            messageUntil = Time.time + 3.5f;
        }

        private void OnGUI()
        {
            if (string.IsNullOrWhiteSpace(message) || Time.time > messageUntil)
                return;

            messageStyle ??= new GUIStyle(GUI.skin.label)
            {
                font = KoreanGuiFont.Get(),
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = new Color(0.96f, 0.92f, 0.84f, 1f) }
            };
            Rect panel = new(Screen.width * 0.5f - 310f, Screen.height - 145f, 620f, 64f);
            Color previous = GUI.color;
            GUI.color = new Color(0.025f, 0.02f, 0.03f, 0.92f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Label(panel, message, messageStyle);
        }
    }
}
