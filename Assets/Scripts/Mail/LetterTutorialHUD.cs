using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadLetter.Mail
{
    public sealed class LetterTutorialHUD : MonoBehaviour
    {
        [SerializeField] private LetterTaskState taskState;

        private GUIStyle itemStyle;
        private GUIStyle completedStyle;
        private GUIStyle activeStyle;
        private GUIStyle headerStyle;
        private GUIStyle toggleStyle;
        private bool expanded = true;

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
                expanded = !expanded;
        }

        private void OnGUI()
        {
            if (taskState == null || taskState.WorkstationViewOpen)
                return;

            EnsureStyles();
            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1920f, Screen.height / 1080f), 0.72f, 1.15f);
            float panelWidth = 390f * scale;
            float panelHeight = (expanded ? 326f : 62f) * scale;
            Rect panel = new Rect(Screen.width - panelWidth - 28f * scale, 28f * scale, panelWidth, panelHeight);

            DrawRect(panel, new Color(0.025f, 0.02f, 0.03f, 0.9f));
            DrawRect(new Rect(panel.x, panel.y, 5f * scale, panel.height), new Color(0.92f, 0.28f, 0.08f, 1f));
            Rect header = new Rect(panel.x + 18f * scale, panel.y + 10f * scale, panel.width - 76f * scale, 42f * scale);
            GUI.Label(header, $"DAY {taskState.CurrentDay}   업무 {taskState.CompletedRequests}/{taskState.RequiredRequestsPerDay}  [T]", headerStyle);
            Rect toggle = new Rect(panel.xMax - 52f * scale, panel.y + 12f * scale, 36f * scale, 36f * scale);
            if (GUI.Button(toggle, expanded ? "−" : "+", toggleStyle))
                expanded = !expanded;

            if (!expanded)
                return;

            bool hasLetter = taskState.HasLetter;
            bool workbenchOpened = taskState.WorkstationOpened;
            bool stampsCorrect = taskState.HasOnlyRequiredStamps;
            bool postmarked = taskState.PostmarkApplied;
            bool mailed = taskState.LetterMailed;
            bool dayComplete = taskState.RequiredDayComplete;

            float y = panel.y + 62f * scale;
            if (dayComplete)
            {
                DrawTask(panel, ref y, "오늘의 필수 민원 처리 완료", true, false, scale);
                DrawTask(panel, ref y, "탕비실 소파에서 다음 DAY 시작", false, true, scale);
                return;
            }

            DrawTask(panel, ref y, "민원인에게 편지를 접수하기", hasLetter, !hasLetter, scale);
            DrawTask(panel, ref y, "편지 처리실 작업대 열기", workbenchOpened, hasLetter && !workbenchOpened, scale);
            DrawTask(panel, ref y, "필수 우표 구성을 맞추기", stampsCorrect, workbenchOpened && !stampsCorrect, scale);
            DrawTask(panel, ref y, "편지 오른쪽 아래에 도장 찍기", postmarked, workbenchOpened && stampsCorrect && !postmarked, scale);
            DrawTask(panel, ref y, "처리실 우편함에 편지 넣기", mailed, hasLetter && stampsCorrect && postmarked && !mailed, scale);
            DrawTask(panel, ref y, "민원인에게 발송 결과 알리기", false, mailed, scale);
        }

        private void DrawTask(Rect panel, ref float y, string text, bool completed, bool active, float scale)
        {
            GUIStyle style = completed ? completedStyle : active ? activeStyle : itemStyle;
            Rect box = new Rect(panel.x + 24f * scale, y + 8f * scale, 19f * scale, 19f * scale);
            DrawRect(box, completed ? new Color(0.28f, 0.65f, 0.35f, 1f) : active ? new Color(0.95f, 0.31f, 0.08f, 1f) : new Color(0.28f, 0.26f, 0.28f, 1f));
            DrawRect(new Rect(box.x + 3f * scale, box.y + 3f * scale, box.width - 6f * scale, box.height - 6f * scale), completed ? new Color(0.8f, 0.95f, 0.82f, 1f) : new Color(0.045f, 0.038f, 0.05f, 1f));
            GUI.Label(new Rect(panel.x + 54f * scale, y, panel.width - 72f * scale, 36f * scale), text, style);
            y += 40f * scale;
        }

        private void EnsureStyles()
        {
            if (headerStyle != null)
                return;

            Font koreanFont = KoreanGuiFont.Get();
            headerStyle = new GUIStyle(GUI.skin.label)
            {
                font = koreanFont,
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.96f, 0.89f, 0.76f, 1f) }
            };
            toggleStyle = new GUIStyle(GUI.skin.button)
            {
                font = koreanFont,
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.96f, 0.89f, 0.76f, 1f) }
            };
            itemStyle = new GUIStyle(GUI.skin.label)
            {
                font = koreanFont,
                fontSize = 17,
                normal = { textColor = new Color(0.62f, 0.59f, 0.58f, 1f) }
            };
            completedStyle = new GUIStyle(itemStyle)
            {
                normal = { textColor = new Color(0.42f, 0.74f, 0.48f, 1f) }
            };
            activeStyle = new GUIStyle(itemStyle)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.48f, 0.17f, 1f) }
            };
        }

        private static void DrawRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
