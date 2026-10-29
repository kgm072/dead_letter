using DeadLetter.Interaction;
using UnityEngine;

namespace DeadLetter.Mail
{
    public sealed class MailDropBox : MonoBehaviour, IInteractable
    {
        [SerializeField] private LetterTaskState taskState;

        private string message;
        private float messageUntil;
        private GUIStyle messageStyle;

        public void Interact(GameObject interactor)
        {
            if (taskState == null || !taskState.HasLetter)
            {
                ShowMessage("발송할 편지가 없습니다.");
                return;
            }

            if (taskState.LetterMailed)
            {
                ShowMessage("편지를 이미 우편함에 넣었습니다. 민원인에게 결과를 알려 주세요.");
                return;
            }

            if (!taskState.HasOnlyRequiredStamps)
            {
                ShowMessage("우표 구성이 올바르지 않습니다. 작업대에서 수정해 주세요.");
                return;
            }

            if (!taskState.PostmarkApplied)
            {
                ShowMessage("편지용 도장이 필요합니다.");
                return;
            }

            if (taskState.TryMailLetter())
                ShowMessage("발송 접수 완료. 민원인에게 돌아가 상호작용하세요.");
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(message) || Time.time > messageUntil)
                return;

            messageStyle ??= new GUIStyle(GUI.skin.label)
            {
                font = KoreanGuiFont.Get(),
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = new Color(0.96f, 0.92f, 0.84f, 1f) }
            };
            Rect panel = new Rect(Screen.width * 0.5f - 340f, Screen.height - 155f, 680f, 80f);
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
