using DeadLetter.Interaction;
using UnityEngine;

namespace DeadLetter.Mail
{
    public sealed class DaySleepSofa : MonoBehaviour, IInteractable
    {
        [SerializeField] private LetterTaskState taskState;
        [SerializeField] private DayCycleManager dayCycle;

        private string message;
        private float messageUntil;
        private GUIStyle messageStyle;

        public void Interact(GameObject interactor)
        {
            if (taskState == null || dayCycle == null)
                return;

            if (!taskState.RequiredDayComplete)
            {
                int remaining = Mathf.Max(0, taskState.RequiredRequestsPerDay - taskState.CompletedRequests);
                ShowMessage($"아직 필수 업무가 {remaining}건 남았습니다.");
                return;
            }

            int finishedDay = taskState.CurrentDay;
            if (dayCycle.TryAdvanceDay())
                ShowMessage($"DAY {finishedDay} 종료. DAY {taskState.CurrentDay} 업무를 시작합니다.");
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(message) || Time.time > messageUntil)
                return;

            messageStyle ??= new GUIStyle(GUI.skin.label)
            {
                font = KoreanGuiFont.Get(),
                fontSize = 21,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = new Color(0.96f, 0.92f, 0.84f, 1f) }
            };
            Rect panel = new Rect(Screen.width * 0.5f - 330f, Screen.height - 155f, 660f, 80f);
            Color previous = GUI.color;
            GUI.color = new Color(0.025f, 0.02f, 0.03f, 0.94f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Label(panel, message, messageStyle);
        }

        private void ShowMessage(string value)
        {
            message = value;
            messageUntil = Time.time + 4f;
        }
    }
}
