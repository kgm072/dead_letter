using DeadLetter.Interaction;
using DeadLetter.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadLetter.Mail
{
    public sealed class LetterWorkstation : MonoBehaviour, IInteractable, IConditionalInteractable
    {
        private enum WorkTool
        {
            None,
            FireStamp,
            WaterStamp,
            ElectricityStamp,
            ShockStamp,
            MedicineStamp,
            AlcoholStamp,
            Postmark
        }

        [SerializeField] private LetterTaskState taskState;
        [SerializeField] private Texture2D fireStampTexture;
        [SerializeField] private Texture2D waterStampTexture;
        [SerializeField] private Texture2D electricityStampTexture;
        [SerializeField] private Texture2D impactStampTexture;
        [SerializeField] private Texture2D medicineStampTexture;
        [SerializeField] private Texture2D alcoholStampTexture;

        private WorkTool selectedTool;
        private bool workspaceOpen;
        private FirstPersonController lockedController;
        private FirstPersonInteractor lockedInteractor;
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle centerStyle;
        private GUIStyle buttonStyle;

        public bool CanInteract(GameObject interactor)
        {
            return !workspaceOpen && taskState != null && taskState.HasLetter && !taskState.LetterMailed;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor))
                return;

            workspaceOpen = true;
            taskState.SetWorkstationViewOpen(true);
            selectedTool = WorkTool.None;
            lockedController = interactor.GetComponent<FirstPersonController>();
            lockedInteractor = interactor.GetComponent<FirstPersonInteractor>();
            if (lockedController != null)
                lockedController.SetInputEnabled(false);
            if (lockedInteractor != null)
                lockedInteractor.enabled = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            if (workspaceOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                CloseWorkspace();
        }

        private void OnDisable()
        {
            if (workspaceOpen)
                CloseWorkspace();
        }

        private void OnGUI()
        {
            if (!workspaceOpen || taskState == null)
                return;

            EnsureStyles();
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;

            DrawRect(new Rect(0f, 0f, width, height), new Color(0.018f, 0.014f, 0.02f, 0.94f));
            GUI.Label(new Rect(54f, 34f, 700f, 42f), "편지 처리 작업대", titleStyle);
            GUI.Label(new Rect(56f, 78f, 800f, 28f), $"민원인  {GetCustomerDisplayName(taskState.CustomerDisplayName)}     접수 번호  {GetLetterDisplayId(taskState.LetterId)}", labelStyle);

            Rect envelope = new Rect(95f, 145f, 760f, 430f);
            DrawRect(envelope, new Color(0.78f, 0.72f, 0.57f, 1f));
            DrawRect(new Rect(envelope.x + 12f, envelope.y + 12f, envelope.width - 24f, envelope.height - 24f), new Color(0.66f, 0.59f, 0.45f, 1f));
            GUI.Label(new Rect(envelope.x + 220f, envelope.y + 125f, 330f, 40f), "데드 레터 우편국", centerStyle);

            Rect stampArea = new Rect(envelope.x + 32f, envelope.yMax - 150f, 240f, 110f);
            Rect postmarkArea = new Rect(envelope.xMax - 272f, envelope.yMax - 150f, 240f, 110f);
            DrawRect(stampArea, new Color(0.19f, 0.16f, 0.13f, 0.32f));
            DrawRect(postmarkArea, new Color(0.19f, 0.16f, 0.13f, 0.32f));
            GUI.Label(new Rect(stampArea.x, stampArea.y + 38f, stampArea.width, 24f), "우표 부착 위치", centerStyle);
            GUI.Label(new Rect(postmarkArea.x, postmarkArea.y + 38f, postmarkArea.width, 24f), "도장 날인 위치", centerStyle);

            if (GUI.Button(stampArea, GUIContent.none, GUIStyle.none))
            {
                LetterStamp selectedStamp = GetSelectedStamp(selectedTool);
                if (selectedStamp != LetterStamp.None)
                    taskState.ToggleStamp(selectedStamp);
            }

            if (GUI.Button(postmarkArea, GUIContent.none, GUIStyle.none) && selectedTool == WorkTool.Postmark)
            {
                if (taskState.PostmarkApplied)
                    taskState.RemovePostmark();
                else
                    taskState.ApplyPostmark();
            }

            DrawAppliedMarks(stampArea, postmarkArea);
            DrawToolPanel(new Rect(900f, 145f, 315f, 430f));

            if (GUI.Button(new Rect(1055f, 620f, 160f, 48f), "닫기  [ESC]", buttonStyle))
                CloseWorkspace();

            GUI.matrix = previousMatrix;
        }

        private void DrawToolPanel(Rect panel)
        {
            DrawRect(panel, new Color(0.08f, 0.065f, 0.075f, 1f));
            GUI.Label(new Rect(panel.x + 22f, panel.y + 20f, panel.width - 44f, 34f), "작업 도구", titleStyle);
            const float buttonHeight = 34f;
            const float gap = 4f;
            float y = panel.y + 58f;
            Rect fireButton = DrawToolButton(panel, ref y, "불 우표", WorkTool.FireStamp, buttonHeight, gap);
            Rect waterButton = DrawToolButton(panel, ref y, "물 우표", WorkTool.WaterStamp, buttonHeight, gap);
            Rect electricityButton = DrawToolButton(panel, ref y, "전기 우표", WorkTool.ElectricityStamp, buttonHeight, gap);
            Rect impactButton = DrawToolButton(panel, ref y, "충격 우표", WorkTool.ShockStamp, buttonHeight, gap);
            Rect medicineButton = DrawToolButton(panel, ref y, "약 우표", WorkTool.MedicineStamp, buttonHeight, gap);
            Rect alcoholButton = DrawToolButton(panel, ref y, "술 우표", WorkTool.AlcoholStamp, buttonHeight, gap);
            DrawToolButton(panel, ref y, "편지용 도장", WorkTool.Postmark, buttonHeight, gap);

            DrawToolIcon(fireButton, fireStampTexture);
            DrawToolIcon(waterButton, waterStampTexture);
            DrawToolIcon(electricityButton, electricityStampTexture);
            DrawToolIcon(impactButton, impactStampTexture);
            DrawToolIcon(medicineButton, medicineStampTexture);
            DrawToolIcon(alcoholButton, alcoholStampTexture);

            GUI.Label(new Rect(panel.x + 24f, panel.y + 330f, panel.width - 48f, 24f), $"선택  {GetToolName(selectedTool)}", labelStyle);
            string stampResult = taskState.HasOnlyRequiredStamps ? "우표 구성 완료" : $"필수  {GetStampList(taskState.RequiredStamps)}";
            GUI.Label(new Rect(panel.x + 24f, panel.y + 354f, panel.width - 48f, 24f), stampResult, labelStyle);
            GUI.Label(new Rect(panel.x + 24f, panel.y + 378f, panel.width - 48f, 24f), taskState.PostmarkApplied ? "도장 날인 완료" : "도장 미날인", labelStyle);
            GUI.Label(new Rect(panel.x + 24f, panel.y + 402f, panel.width - 48f, 24f), "같은 우표를 다시 적용하면 제거", labelStyle);
        }

        private Rect DrawToolButton(Rect panel, ref float y, string label, WorkTool tool, float height, float gap)
        {
            Rect rect = new Rect(panel.x + 24f, y, panel.width - 48f, height);
            if (GUI.Button(rect, label, buttonStyle))
                selectedTool = tool;
            y += height + gap;
            return rect;
        }

        private void DrawAppliedMarks(Rect stampArea, Rect postmarkArea)
        {
            int stampIndex = 0;
            DrawAppliedStamp(stampArea, LetterStamp.Fire, fireStampTexture, new Color(0.52f, 0.12f, 0.055f, 1f), "불", ref stampIndex);
            DrawAppliedStamp(stampArea, LetterStamp.Water, waterStampTexture, new Color(0.08f, 0.34f, 0.65f, 1f), "물", ref stampIndex);
            DrawAppliedStamp(stampArea, LetterStamp.Electricity, electricityStampTexture, new Color(0.72f, 0.62f, 0.08f, 1f), "전기", ref stampIndex);
            DrawAppliedStamp(stampArea, LetterStamp.Shock, impactStampTexture, new Color(0.22f, 0.19f, 0.48f, 1f), "충격", ref stampIndex);
            DrawAppliedStamp(stampArea, LetterStamp.Medicine, medicineStampTexture, new Color(0.2f, 0.52f, 0.25f, 1f), "약", ref stampIndex);
            DrawAppliedStamp(stampArea, LetterStamp.Alcohol, alcoholStampTexture, new Color(0.5f, 0.23f, 0.48f, 1f), "술", ref stampIndex);

            if (taskState.PostmarkApplied)
            {
                DrawRect(new Rect(postmarkArea.x + 24f, postmarkArea.y + 24f, postmarkArea.width - 48f, 5f), new Color(0.22f, 0.07f, 0.07f, 1f));
                DrawRect(new Rect(postmarkArea.x + 24f, postmarkArea.y + 52f, postmarkArea.width - 48f, 5f), new Color(0.22f, 0.07f, 0.07f, 1f));
                GUI.Label(new Rect(postmarkArea.x, postmarkArea.y + 62f, postmarkArea.width, 28f), "접수 완료", centerStyle);
            }
        }

        private void DrawAppliedStamp(Rect area, LetterStamp stamp, Texture2D texture, Color color, string label, ref int index)
        {
            if ((taskState.AttachedStamps & stamp) == 0)
                return;

            int column = index % 3;
            int row = index / 3;
            Rect mark = new Rect(area.x + 8f + column * 76f, area.y + 8f + row * 49f, 70f, 43f);
            DrawStamp(mark, texture, color, label);
            index++;
        }

        private static void DrawToolIcon(Rect button, Texture2D texture)
        {
            if (texture != null)
                GUI.DrawTexture(new Rect(button.x + 7f, button.y + 4f, 26f, 26f), texture, ScaleMode.ScaleToFit, true);
        }

        private void DrawStamp(Rect rect, Texture2D texture, Color fallbackColor, string fallbackText)
        {
            if (texture != null)
                GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
            else
            {
                DrawRect(rect, fallbackColor);
                GUI.Label(rect, fallbackText, centerStyle);
            }
        }

        private static string GetToolName(WorkTool tool)
        {
            return tool switch
            {
                WorkTool.FireStamp => "불 우표",
                WorkTool.WaterStamp => "물 우표",
                WorkTool.ElectricityStamp => "전기 우표",
                WorkTool.ShockStamp => "충격 우표",
                WorkTool.MedicineStamp => "약 우표",
                WorkTool.AlcoholStamp => "술 우표",
                WorkTool.Postmark => "접수 도장",
                _ => "없음"
            };
        }

        private static LetterStamp GetSelectedStamp(WorkTool tool)
        {
            return tool switch
            {
                WorkTool.FireStamp => LetterStamp.Fire,
                WorkTool.WaterStamp => LetterStamp.Water,
                WorkTool.ElectricityStamp => LetterStamp.Electricity,
                WorkTool.ShockStamp => LetterStamp.Shock,
                WorkTool.MedicineStamp => LetterStamp.Medicine,
                WorkTool.AlcoholStamp => LetterStamp.Alcohol,
                _ => LetterStamp.None
            };
        }

        private static string GetStampList(LetterStamp stamps)
        {
            string value = string.Empty;
            AppendStamp(ref value, stamps, LetterStamp.Fire, "불");
            AppendStamp(ref value, stamps, LetterStamp.Water, "물");
            AppendStamp(ref value, stamps, LetterStamp.Electricity, "전기");
            AppendStamp(ref value, stamps, LetterStamp.Shock, "충격");
            AppendStamp(ref value, stamps, LetterStamp.Medicine, "약");
            AppendStamp(ref value, stamps, LetterStamp.Alcohol, "술");
            return value;
        }

        private static void AppendStamp(ref string value, LetterStamp stamps, LetterStamp target, string label)
        {
            if ((stamps & target) == 0)
                return;
            value = string.IsNullOrEmpty(value) ? label : value + " + " + label;
        }

        private static string GetCustomerDisplayName(string customerId)
        {
            return customerId;
        }

        private static string GetLetterDisplayId(string letterId)
        {
            return letterId == "letter_mascot_001" ? "001" : letterId;
        }

        private void CloseWorkspace()
        {
            workspaceOpen = false;
            if (taskState != null)
                taskState.SetWorkstationViewOpen(false);
            selectedTool = WorkTool.None;
            if (lockedInteractor != null)
                lockedInteractor.enabled = true;
            if (lockedController != null)
                lockedController.SetInputEnabled(true);
            lockedInteractor = null;
            lockedController = null;
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
                return;

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                font = KoreanGuiFont.Get(),
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.94f, 0.89f, 0.76f, 1f) }
            };
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                font = KoreanGuiFont.Get(),
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.9f, 0.84f, 0.73f, 1f) }
            };
            centerStyle = new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleCenter };
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                font = KoreanGuiFont.Get(),
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
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
