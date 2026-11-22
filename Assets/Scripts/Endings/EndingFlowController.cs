using System.Collections;
using DeadLetter.Interaction;
using DeadLetter.Mail;
using DeadLetter.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DeadLetter.Endings
{
    // Coordinates the two ending sequences without replacing normal day/task progression.
    [RequireComponent(typeof(EndingStateController))]
    public sealed class EndingFlowController : MonoBehaviour
    {
        [Header("Existing scene references")]
        [SerializeField] private DayCycleManager dayCycle;
        [SerializeField] private LetterTaskState tasks;
        [SerializeField] private TestCustomerNPC customer;
        [SerializeField] private FirstPersonController player;
        [SerializeField] private FirstPersonInteractor interactor;
        [SerializeField] private Camera view;
        [SerializeField] private HingedDoor hiddenDoor;
        [SerializeField] private Transform frame;
        [SerializeField] private EndingTallyMarks marks;
        [SerializeField] private Transform whiteLetter;
        [SerializeField] private Material pencilMaterial;
        [Header("Replaceable ending artwork / timing")]
        [SerializeField] private Sprite normalImage;
        [SerializeField] private Sprite trueImage;
        [SerializeField, Min(.1f)] private float fadeSeconds = 1.5f;
        [SerializeField, Min(.1f)] private float loopReturnSeconds = 2.5f;
        [SerializeField, Min(.1f)] private float strikeSeconds = .45f;
        [SerializeField, Min(.1f)] private float frameFallSeconds = 1.2f;
        [SerializeField, Min(.1f)] private float scratchSeconds = 2f;
        [SerializeField, Min(.1f)] private float cardSeconds = 5f;
        [SerializeField, Min(0)] private float revealPauseSeconds = .6f;
        [SerializeField, Min(0)] private float completedMarkPauseSeconds = .8f;
        [SerializeField, Min(.1f)] private float letterOpenSeconds = .7f;
        [SerializeField, Min(0)] private float whiteHoldSeconds = .7f;
        [SerializeField, Min(.1f)] private float skipHoldSeconds = 1.5f;
        [SerializeField] private string mainMenuScene = "MainMenu";
        private EndingStateController state;
        private Image cover;
        private TextMeshProUGUI title, skip;
        private Canvas overlay;
        private Canvas[] oldCanvases;
        private bool[] canvasStates;
        private bool oldPlayerInput, oldInteractorEnabled, oldCursorVisible, controlsCaptured;
        private CursorLockMode oldCursor;
        private Vector3 oldCameraPosition;
        private Quaternion oldCameraRotation;
        private float held;
        private bool lowering, returning;
        private GameObject scratchTool;
        private Vector3 originalFramePosition;
        private Quaternion originalFrameRotation;
        private bool oldCustomerEnabled;
        public string SequenceStage { get; private set; } = "Idle";
        public bool IsBusy => lowering || (state != null && state.Playing) || returning;
        public bool CanLowerFrame => !IsBusy && state != null && tasks != null && tasks.CurrentDay == 1 &&
                                     !state.FrameLowered && EndingRecords.HasSeen(EndingKind.Normal);
        public bool CanOpenLetter => !IsBusy && state != null && state.MarksDiscovered && !state.LetterOpened;
        public bool NormalReady => tasks != null && dayCycle != null &&
            EndingStateController.NormalReady(tasks.CurrentDay, tasks.CompletedRequests, tasks.RequiredRequestsPerDay,
                !dayCycle.AllCustomersDeparted, customer != null && customer.DialogueActive);

        private void Awake()
        {
            state = GetComponent<EndingStateController>();
            if (frame != null) { originalFramePosition=frame.position; originalFrameRotation=frame.rotation; }
            if (marks != null) marks.GetComponent<MeshRenderer>().enabled = false;
            BuildOverlay();
        }

        private void Start()
        {
            // All scene Awake methods have run, so HingedDoor has captured its authored closed rotation.
            if (hiddenDoor != null) hiddenDoor.SetLocked(true);
        }

        public bool TryStartNormal()
        {
            if (!ReadyReferences() || IsBusy || !NormalReady || !state.Begin(EndingKind.Normal, NormalReady, EndingRecords.HasSeen(EndingKind.Normal))) return false;
            CaptureControls(); StartCoroutine(NormalSequence()); return true;
        }
        public bool TryStartTrue()
        {
            if (!ReadyReferences() || !CanOpenLetter || !state.Begin(EndingKind.True, false, EndingRecords.HasSeen(EndingKind.True))) return false;
            CaptureControls(); StartCoroutine(TrueSequence()); return true;
        }
        public bool TryLowerFrame()
        {
            if (!ReadyReferences() || !CanLowerFrame) return false;
            lowering = true; CaptureControls(); StartCoroutine(LowerFrame()); return true;
        }
        private bool ReadyReferences() => frame != null && marks != null && view != null && player != null && hiddenDoor != null && whiteLetter != null;

        private void Update()
        {
            if (state == null || !state.Playing || returning || !state.CanSkip) return;
            held = Keyboard.current != null && Keyboard.current.escapeKey.isPressed ? held + Time.unscaledDeltaTime : 0;
            skip.text = $"ESC 길게 눌러 건너뛰기  {Mathf.Clamp01(held / skipHoldSeconds):P0}";
            if (held >= skipHoldSeconds) ReturnToMenu(false);
        }
        private IEnumerator LowerFrame()
        {
            SequenceStage = "Lowering frame";
            yield return DropFrame();
            if (state.Discover(EndingRecords.HasSeen(EndingKind.Normal)))
            {
                marks.Show(true);
                hiddenDoor.SetLocked(false);
            }
            RestoreControls(); lowering = false; SequenceStage = "Marks discovered";
        }
        private IEnumerator NormalSequence()
        {
            SequenceStage = "Sleep";
            yield return Fade(Color.black, 0, 1);
            frame.SetPositionAndRotation(originalFramePosition,originalFrameRotation);
            marks.GetComponent<MeshRenderer>().enabled=false;
            // A cinematic DAY 1 return, deliberately NOT DayCycleManager.PrepareDay or a task reset.
            title.text = "DAY 01";
            SequenceStage = "Ending-only DAY 01 return";
            view.transform.position = marks.transform.position + marks.transform.forward * 1.45f;
            view.transform.rotation = Quaternion.LookRotation(-marks.transform.forward, Vector3.up);
            yield return Fade(Color.black, 1, 0);
            yield return Wait(loopReturnSeconds);
            title.text = "";
            SequenceStage = "Wall strike";
            Vector3 basePosition = view.transform.position;
            yield return Animate(strikeSeconds, t => view.transform.position = basePosition - marks.transform.forward * (Mathf.Sin(t * Mathf.PI) * .2f));
            view.transform.position = basePosition;
            marks.Show(false, 0);
            SequenceStage = "Frame fall";
            yield return DropFrame();
            yield return Wait(revealPauseSeconds);
            SequenceStage = "Final diagonal";
            scratchTool = GameObject.CreatePrimitive(PrimitiveType.Cube);
            scratchTool.name = "Ending scratch tool (placeholder)";
            Destroy(scratchTool.GetComponent<Collider>());
            scratchTool.GetComponent<MeshRenderer>().sharedMaterial = pencilMaterial;
            scratchTool.transform.localScale = new Vector3(.012f, .012f, .16f);
            int columns = Mathf.CeilToInt(Mathf.Sqrt(marks.NormalCount * 1.25f));
            int rows = Mathf.CeilToInt((float)marks.NormalCount / columns);
            Vector3 start = new(-.48f + ((marks.NormalCount-1) % columns) * .96f / columns,
                -.46f + ((marks.NormalCount-1) / columns) * .92f / rows + .65f / rows * .15f, .025f);
            Vector3 end = start + new Vector3(.75f / columns, .65f / rows * .7f, 0);
            yield return Animate(scratchSeconds, t =>
            {
                marks.Show(false,t);
                scratchTool.transform.SetPositionAndRotation(marks.transform.TransformPoint(Vector3.Lerp(start,end,t)), marks.transform.rotation);
            });
            Destroy(scratchTool);
            yield return Wait(completedMarkPauseSeconds);
            yield return Fade(Color.black,0,1);
            yield return EndCard(EndingKind.Normal);
        }
        private IEnumerator TrueSequence()
        {
            SequenceStage = "White letter opening";
            // Temporary envelope opening: unfold its own lid, never touch a gameplay letter.
            Transform flap = whiteLetter.Find("Flap");
            if (flap != null)
                yield return Animate(letterOpenSeconds, t => flap.localRotation = Quaternion.Euler(-150f*t,0,0));
            yield return Fade(Color.white,0,1);
            yield return Wait(whiteHoldSeconds);
            yield return EndCard(EndingKind.True);
        }
        private IEnumerator DropFrame()
        {
            Bounds bounds = frame.GetComponent<Renderer>().bounds;
            Vector3 localCenter = frame.InverseTransformPoint(bounds.center);
            Vector3 from = bounds.center;
            Quaternion rotation = frame.rotation;
            // The installer supplies a bottom landing marker based on this room's actual floor.
            Transform landing = transform.Find("Frame Landing");
            Vector3 to = landing != null ? landing.position : from - Vector3.up * .8f;
            yield return Animate(frameFallSeconds, t =>
            {
                frame.rotation = Quaternion.Slerp(rotation,Quaternion.AngleAxis(90,marks.transform.right) * rotation,t);
                frame.position += Vector3.Lerp(from,to,t*t) - frame.TransformPoint(localCenter);
            });
        }
        private IEnumerator EndCard(EndingKind kind)
        {
            SequenceStage = kind + " ending card";
            cover.sprite = kind == EndingKind.Normal ? normalImage : trueImage;
            cover.color = cover.sprite != null ? Color.white : kind == EndingKind.Normal ? Color.black : new Color(.88f,.87f,.81f);
            title.color = kind == EndingKind.Normal ? new Color(.95f,.91f,.82f) : new Color(.12f,.12f,.13f);
            skip.color = title.color;
            title.text = kind == EndingKind.Normal ? "NORMAL ENDING" : "TRUE ENDING";
            yield return Wait(cardSeconds);
            ReturnToMenu(true);
        }
        private IEnumerator Fade(Color color, float from, float to)
        {
            cover.sprite = null;
            yield return Animate(fadeSeconds,t => { color.a = Mathf.Lerp(from,to,t); cover.color = color; });
        }
        private static IEnumerator Wait(float duration) { yield return new WaitForSecondsRealtime(Mathf.Max(.01f,duration)); }
        private static IEnumerator Animate(float duration, System.Action<float> action)
        {
            float elapsed = 0; duration = Mathf.Max(.01f,duration);
            while (elapsed < duration) { action(elapsed / duration); elapsed += Time.unscaledDeltaTime; yield return null; }
            action(1);
        }
        private void ReturnToMenu(bool finishedViewing)
        {
            if (returning || (!finishedViewing && !state.CanSkip)) return;
            returning = true;
            StopAllCoroutines();
            if (finishedViewing) EndingRecords.Record(state.Kind);
            if (scratchTool != null) Destroy(scratchTool);
            RestoreControls(); state.Finish();
            SceneManager.LoadScene(mainMenuScene);
        }
        private void CaptureControls()
        {
            controlsCaptured = true;
            oldPlayerInput = player.InputEnabled; oldInteractorEnabled = interactor != null && interactor.enabled;
            oldCursor = Cursor.lockState; oldCursorVisible = Cursor.visible;
            oldCameraPosition = view.transform.position; oldCameraRotation = view.transform.rotation;
            oldCustomerEnabled = customer != null && customer.enabled;
            if(customer != null) customer.enabled=false; // An approaching NPC must not begin dialogue during a cutscene.
            oldCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            canvasStates = new bool[oldCanvases.Length];
            for (int i=0;i<oldCanvases.Length;i++) { canvasStates[i]=oldCanvases[i].enabled; if (oldCanvases[i]!=overlay) oldCanvases[i].enabled=false; }
            player.SetInputEnabled(false);
            if (interactor != null) interactor.enabled=false;
            Cursor.visible=false;
            overlay.enabled=true;
        }
        private void RestoreControls()
        {
            if (!controlsCaptured) return;
            controlsCaptured=false;
            if (view != null) view.transform.SetPositionAndRotation(oldCameraPosition, oldCameraRotation);
            if (player != null) player.SetInputEnabled(oldPlayerInput);
            if (interactor != null) interactor.enabled=oldInteractorEnabled;
            if (customer != null) customer.enabled=oldCustomerEnabled;
            Cursor.lockState=oldCursor; Cursor.visible=oldCursorVisible;
            for (int i=0;i<oldCanvases.Length;i++) if (oldCanvases[i]!=null) oldCanvases[i].enabled=canvasStates[i];
            if (overlay != null) overlay.enabled=false;
        }
        private void OnDisable() { StopAllCoroutines(); RestoreControls(); }
        private void BuildOverlay()
        {
            var go = new GameObject("Ending overlay",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            go.transform.SetParent(transform,false); overlay=go.GetComponent<Canvas>();
            overlay.renderMode=RenderMode.ScreenSpaceOverlay; overlay.sortingOrder=30000;
            var scaler=go.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080); scaler.matchWidthOrHeight=.5f;
            cover=Child<Image>("Fade / ending artwork",go.transform); Stretch(cover.rectTransform); cover.color=Color.clear; cover.raycastTarget=false;
            title=Child<TextMeshProUGUI>("Ending title",go.transform); Stretch(title.rectTransform); title.alignment=TextAlignmentOptions.Center;
            title.font=Resources.Load<TMP_FontAsset>("UI/Fonts/GalmuriMono11/GalmuriMono11 TMP"); title.fontSize=44; title.color=new Color(.95f,.91f,.82f); title.raycastTarget=false;
            skip=Child<TextMeshProUGUI>("Repeat viewing skip",go.transform); Stretch(skip.rectTransform); skip.rectTransform.offsetMin=new Vector2(20,35); skip.rectTransform.offsetMax=new Vector2(-20,-20);
            skip.font=title.font; skip.fontSize=20; skip.alignment=TextAlignmentOptions.BottomRight; skip.raycastTarget=false;
            overlay.enabled=false;
        }
        private static T Child<T>(string label, Transform parent) where T : Component
        { var go=new GameObject(label,typeof(RectTransform),typeof(T)); go.transform.SetParent(parent,false); return go.GetComponent<T>(); }
        private static void Stretch(RectTransform rect) { rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=rect.offsetMax=Vector2.zero; }
    }
}
