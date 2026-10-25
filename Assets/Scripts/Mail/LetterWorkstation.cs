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
            ShockStamp,
            Postmark
        }

        [SerializeField] private LetterTaskState taskState;

        private WorkTool selectedTool;
        private bool workspaceOpen;
        private FirstPersonController lockedController;
        private FirstPersonInteractor lockedInteractor;
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle centerStyle;

        public bool CanInteract(GameObject interactor)
        {
            return !workspaceOpen && taskState != null && taskState.HasLetter;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor))
                return;

            workspaceOpen = true;
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
            GUI.Label(new Rect(54f, 34f, 700f, 42f), "LETTER PROCESSING DESK", titleStyle);
            GUI.Label(new Rect(56f, 78f, 700f, 28f), $"CUSTOMER  {taskState.CustomerId}     LETTER  {taskState.LetterId}", labelStyle);

            Rect envelope = new Rect(95f, 145f, 760f, 430f);
            DrawRect(envelope, new Color(0.78f, 0.72f, 0.57f, 1f));
            DrawRect(new Rect(envelope.x + 12f, envelope.y + 12f, envelope.width - 24f, envelope.height - 24f), new Color(0.66f, 0.59f, 0.45f, 1f));
            GUI.Label(new Rect(envelope.x + 220f, envelope.y + 125f, 330f, 40f), "DEAD LETTER POST", centerStyle);

            Rect stampArea = new Rect(envelope.x + 32f, envelope.yMax - 150f, 240f, 110f);
            Rect postmarkArea = new Rect(envelope.xMax - 272f, envelope.yMax - 150f, 240f, 110f);
            DrawRect(stampArea, new Color(0.19f, 0.16f, 0.13f, 0.32f));
            DrawRect(postmarkArea, new Color(0.19f, 0.16f, 0.13f, 0.32f));
            GUI.Label(new Rect(stampArea.x, stampArea.y + 38f, stampArea.width, 24f), "STAMP AREA", centerStyle);
            GUI.Label(new Rect(postmarkArea.x, postmarkArea.y + 38f, postmarkArea.width, 24f), "POSTMARK AREA", centerStyle);

            if (GUI.Button(stampArea, GUIContent.none, GUIStyle.none))
            {
                if (selectedTool == WorkTool.FireStamp)
                    taskState.AttachStamp(LetterStamp.Fire);
                else if (selectedTool == WorkTool.ShockStamp)
                    taskState.AttachStamp(LetterStamp.Shock);
            }

            if (GUI.Button(postmarkArea, GUIContent.none, GUIStyle.none) && selectedTool == WorkTool.Postmark)
                taskState.ApplyPostmark();

            DrawAppliedMarks(stampArea, postmarkArea);
            DrawToolPanel(new Rect(900f, 145f, 315f, 430f));

            if (GUI.Button(new Rect(1055f, 620f, 160f, 48f), "CLOSE  [ESC]"))
                CloseWorkspace();

            GUI.matrix = previousMatrix;
        }

        private void DrawToolPanel(Rect panel)
        {
            DrawRect(panel, new Color(0.08f, 0.065f, 0.075f, 1f));
            GUI.Label(new Rect(panel.x + 22f, panel.y + 20f, panel.width - 44f, 34f), "TOOLS", titleStyle);
            if (GUI.Button(new Rect(panel.x + 24f, panel.y + 78f, panel.width - 48f, 58f), "SELECT  FIRE STAMP"))
                selectedTool = WorkTool.FireStamp;
            if (GUI.Button(new Rect(panel.x + 24f, panel.y + 151f, panel.width - 48f, 58f), "SELECT  SHOCK STAMP"))
                selectedTool = WorkTool.ShockStamp;
            if (GUI.Button(new Rect(panel.x + 24f, panel.y + 224f, panel.width - 48f, 58f), "SELECT  POSTMARK"))
                selectedTool = WorkTool.Postmark;

            GUI.Label(new Rect(panel.x + 24f, panel.y + 306f, panel.width - 48f, 26f), $"SELECTED  {selectedTool}", labelStyle);
            string stampResult = taskState.HasRequiredStamps ? "REQUIRED STAMPS  OK" : "REQUIRED  FIRE + SHOCK";
            GUI.Label(new Rect(panel.x + 24f, panel.y + 345f, panel.width - 48f, 26f), stampResult, labelStyle);
            GUI.Label(new Rect(panel.x + 24f, panel.y + 378f, panel.width - 48f, 26f), taskState.PostmarkApplied ? "POSTMARK  APPLIED" : "POSTMARK  NOT APPLIED", labelStyle);
        }

        private void DrawAppliedMarks(Rect stampArea, Rect postmarkArea)
        {
            if ((taskState.AttachedStamps & LetterStamp.Fire) != 0)
            {
                Rect mark = new Rect(stampArea.x + 18f, stampArea.y + 16f, 92f, 78f);
                DrawRect(mark, new Color(0.52f, 0.12f, 0.055f, 1f));
                GUI.Label(mark, "FIRE", centerStyle);
            }

            if ((taskState.AttachedStamps & LetterStamp.Shock) != 0)
            {
                Rect mark = new Rect(stampArea.x + 128f, stampArea.y + 16f, 92f, 78f);
                DrawRect(mark, new Color(0.22f, 0.19f, 0.48f, 1f));
                GUI.Label(mark, "SHOCK", centerStyle);
            }

            if (taskState.PostmarkApplied)
            {
                DrawRect(new Rect(postmarkArea.x + 24f, postmarkArea.y + 24f, postmarkArea.width - 48f, 5f), new Color(0.22f, 0.07f, 0.07f, 1f));
                DrawRect(new Rect(postmarkArea.x + 24f, postmarkArea.y + 52f, postmarkArea.width - 48f, 5f), new Color(0.22f, 0.07f, 0.07f, 1f));
                GUI.Label(new Rect(postmarkArea.x, postmarkArea.y + 62f, postmarkArea.width, 28f), "ACCEPTED", centerStyle);
            }
        }

        private void CloseWorkspace()
        {
            workspaceOpen = false;
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
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.94f, 0.89f, 0.76f, 1f) }
            };
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.9f, 0.84f, 0.73f, 1f) }
            };
            centerStyle = new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleCenter };
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
