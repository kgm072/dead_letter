using System;
using System.IO;
using System.Linq;
using DeadLetter.Interaction;
using DeadLetter.Mail;
using DeadLetter.Player;
using DeadLetter.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DeadLetter.Editor
{
    [InitializeOnLoad]
    public static class DayOneIdentitySetup
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string PrefabPath = "Assets/UI/Prefabs/DayOneIdentityMonitor.prefab";
        private const string ThemePath = "Assets/UI/Common/DeadLetterUITheme.asset";
        private const string CanvasName = "DAY 1 Identity Monitor Canvas";
        private const string MonitorName = "Counter CRT Monitor";

        static DayOneIdentitySetup() => EditorApplication.update += Poll;

        private static void Poll()
        {
            const string request = "Temp/DeadLetterIdentityCommand.txt";
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(request))
                return;
            string command;
            try
            {
                command = File.ReadAllText(request).Trim();
                File.Delete(request);
            }
            catch (IOException)
            {
                return;
            }

            try
            {
                if (command == "setup") BuildAndInstall();
                else if (command == "validate") ValidateAll();
                else if (command == "inspect-monitor") InspectMonitorPlacement();
                else if (command == "preview-monitor-layout") PreviewMonitorLayout();
                else if (command == "enter-play") EnterPlayMode();
                else if (command == "play-open") OpenPlayModePreview();
                else if (command == "play-confirm") ConfirmPlayModePreview();
                else if (command == "play-close") ClosePlayModePreview();
                else if (command == "play-verify-monitor-ray") VerifyMonitorRayDirections();
                else if (command == "play-verify-day2") VerifyDayTwoPlayMode();
                else if (command == "play-right-click-monitor") QueueMonitorRightClick();
                else if (command == "stop-play") EditorApplication.isPlaying = false;
                else if (command == "regression-day2")
                    EditorApplication.ExecuteMenuItem("Tools/DEAD LETTER/DAY 2/Validate Full Gameplay");
                else if (command == "regression-day3")
                    EditorApplication.ExecuteMenuItem("Tools/DEAD LETTER/DAY 3/Validate Gameplay Stage 2");
            }
            catch (Exception exception)
            {
                File.WriteAllText("Temp/DeadLetterIdentityResult.txt", exception.ToString());
                Debug.LogException(exception);
            }
        }

        [MenuItem("Tools/DEAD LETTER/DAY 1/Build Identity Monitor")]
        public static void BuildAndInstall()
        {
            DeadLetterUITheme theme = AssetDatabase.LoadAssetAtPath<DeadLetterUITheme>(ThemePath);
            if (theme == null || theme.PrimaryFont == null)
                throw new InvalidOperationException("DeadLetterUITheme or GalmuriMono11 TMP font is missing.");

            GameObject prefabRoot = CreatePrefab(theme);
            prefabRoot.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            UnityEngine.Object.DestroyImmediate(prefabRoot);

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject canvasObject = FindSceneObject(scene, CanvasName);
            if (canvasObject == null)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                canvasObject = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                if (canvasObject == null)
                    throw new InvalidOperationException("Identity monitor prefab could not be instantiated.");
                canvasObject.name = CanvasName;
            }
            canvasObject.SetActive(false);

            GameObject monitorObject = FindSceneObject(scene, MonitorName);
            if (monitorObject == null)
                throw new InvalidOperationException($"Existing scene monitor was not found: {MonitorName}");

            GameObject counterDeskCollider = FindSceneObject(scene, "Collider - 창구-책상");
            Collider deskCollider = counterDeskCollider != null ? counterDeskCollider.GetComponent<Collider>() : null;
            Vector3 monitorPosition = monitorObject.transform.position;
            monitorPosition.x = deskCollider != null ? deskCollider.bounds.center.x : 0.16f;
            monitorObject.transform.position = monitorPosition;
            monitorObject.SetActive(true);
            foreach (Renderer renderer in monitorObject.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;
            foreach (Collider childCollider in monitorObject.GetComponentsInChildren<Collider>(true))
                childCollider.enabled = false;

            BoxCollider collider = monitorObject.GetComponent<BoxCollider>();
            if (collider == null)
                collider = monitorObject.AddComponent<BoxCollider>();
            collider.enabled = true;
            collider.isTrigger = false;
            collider.center = new Vector3(0f, 0.72f, -0.34f);
            collider.size = new Vector3(1f, 0.68f, 0.16f);

            DayOneIdentityMonitor monitor = monitorObject.GetComponent<DayOneIdentityMonitor>();
            if (monitor == null)
                monitor = monitorObject.AddComponent<DayOneIdentityMonitor>();
            SerializedObject serializedMonitor = new(monitor);
            serializedMonitor.FindProperty("dayCycle").objectReferenceValue =
                UnityEngine.Object.FindAnyObjectByType<DayCycleManager>();
            serializedMonitor.FindProperty("customer").objectReferenceValue =
                UnityEngine.Object.FindAnyObjectByType<TestCustomerNPC>(FindObjectsInactive.Include);
            serializedMonitor.FindProperty("taskState").objectReferenceValue =
                UnityEngine.Object.FindAnyObjectByType<LetterTaskState>();
            serializedMonitor.FindProperty("panel").objectReferenceValue =
                canvasObject.GetComponent<DayOneIdentityPanel>();
            serializedMonitor.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            ValidateSceneAndPrefab(scene);
            string message = "PASS identity monitor prefab and SampleScene installation.";
            File.WriteAllText("Temp/DeadLetterIdentityResult.txt", message);
            Debug.Log(message);
        }

        [MenuItem("Tools/DEAD LETTER/DAY 1/Validate Identity Monitor")]
        public static void ValidateAll()
        {
            ValidateRuntimeState();
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ValidateSceneAndPrefab(scene);
            const string message = "PASS DAY 1 and DAY 2 identity state, five customers per day, reset isolation, monitor prefab, and scene references.";
            File.WriteAllText("Temp/DeadLetterIdentityResult.txt", message);
            Debug.Log(message);
        }

        private static void InspectMonitorPlacement()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Transform[] transforms = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            string[] keywords = { "monitor", "crt", "모니터", "desk", "counter", "책상", "액자", "frame" };
            var lines = transforms
                .Where(transform => keywords.Any(keyword =>
                    transform.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0))
                .Select(transform =>
                {
                    Renderer renderer = transform.GetComponent<Renderer>();
                    Collider collider = transform.GetComponent<Collider>();
                    return $"{PathOf(transform)} | active={transform.gameObject.activeInHierarchy} " +
                           $"pos={transform.position:F3} rot={transform.eulerAngles:F1} scale={transform.lossyScale:F3}" +
                           (renderer != null ? $" renderer={renderer.enabled} bounds={renderer.bounds}" : string.Empty) +
                           (collider != null ? $" collider={collider.enabled} bounds={collider.bounds}" : string.Empty);
                });
            File.WriteAllLines("Temp/DeadLetterIdentityResult.txt", lines);
        }

        private static void PreviewMonitorLayout()
        {
            Require(Application.isPlaying, "Play Mode is required.");
            FirstPersonController player = UnityEngine.Object.FindAnyObjectByType<FirstPersonController>();
            Require(player != null, "Player controller is missing.");
            player.transform.SetPositionAndRotation(new Vector3(0f, 0.05f, -9.25f), Quaternion.Euler(0f, 180f, 0f));
            Camera camera = player.GetComponentInChildren<Camera>();
            if (camera != null)
                camera.transform.localRotation = Quaternion.identity;
            WriteResult("PASS positioned player for counter monitor layout inspection.");
        }

        private static string PathOf(Transform transform)
        {
            return transform.parent == null ? transform.name : PathOf(transform.parent) + "/" + transform.name;
        }

        private static void EnterPlayMode()
        {
            SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Require(scene != null, "SampleScene is missing.");
            EditorSceneManager.playModeStartScene = scene;
            EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
        }

        private static void OpenPlayModePreview()
        {
            Require(Application.isPlaying, "Play Mode is required.");
            DayCycleManager manager = UnityEngine.Object.FindAnyObjectByType<DayCycleManager>();
            DayOneIdentityMonitor monitor = UnityEngine.Object.FindAnyObjectByType<DayOneIdentityMonitor>();
            DayOneIdentityPanel panel = UnityEngine.Object.FindAnyObjectByType<DayOneIdentityPanel>(FindObjectsInactive.Include);
            FirstPersonController player = UnityEngine.Object.FindAnyObjectByType<FirstPersonController>();
            TestCustomerNPC customer = UnityEngine.Object.FindAnyObjectByType<TestCustomerNPC>(FindObjectsInactive.Include);
            Require(manager != null && monitor != null && panel != null && player != null && customer != null,
                "Identity Play Mode references are incomplete.");
            Require(manager.DebugStartDay(1), "DAY 1 preview start failed.");
            Require(manager.DebugSelectDayOneCustomer("mascot"), "Mascot preview selection failed.");
            // Keep this UI-focused preview from starting the normal customer dialogue a frame later.
            customer.gameObject.SetActive(false);
            manager.NotifyDayOneIdentityCheckRequested("mascot");
            Require(!manager.CanAcceptDayOneLetter("mascot"), "Letter was available before identity confirmation.");
            monitor.Interact(player.gameObject);
            Require(panel.IsOpen && DayOneIdentityPanel.IsAnyOpen, "Identity panel did not open.");
            Require(UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null,
                "Identity panel did not provide an EventSystem for its buttons.");
            Require(!player.InputEnabled && !player.CursorLocked && Cursor.visible,
                "Player input or cursor was not switched to UI mode.");
            WriteResult("PASS Play Mode panel opened; letter gated; EventSystem ready; player input disabled; cursor visible.");
        }

        private static void ConfirmPlayModePreview()
        {
            Require(Application.isPlaying, "Play Mode is required.");
            DayCycleManager manager = UnityEngine.Object.FindAnyObjectByType<DayCycleManager>();
            DayOneIdentityPanel panel = UnityEngine.Object.FindAnyObjectByType<DayOneIdentityPanel>(FindObjectsInactive.Include);
            Require(manager != null && panel != null && panel.IsOpen, "Identity panel is not open.");
            Button confirm = panel.GetComponentsInChildren<Button>(true).FirstOrDefault(button => button.name == "Confirm");
            Button lookup = panel.GetComponentsInChildren<Button>(true).FirstOrDefault(button => button.name == "Lookup");
            Require(confirm != null && lookup != null && !confirm.interactable && lookup.interactable,
                "Identity approval must be locked before lookup.");
            lookup.onClick.Invoke();
            Require(confirm.interactable && manager.CurrentDayOneIdentityMatches,
                "Successful lookup did not unlock approval.");
            confirm.onClick.Invoke();
            Require(manager.IsDayOneIdentityVerified("mascot") && manager.CanAcceptDayOneLetter("mascot"),
                "Confirm button did not unlock the current customer's letter.");
            Require(!confirm.interactable, "Confirm button remained enabled after confirmation.");
            WriteResult("PASS Play Mode confirm button verified only the current customer and unlocked the letter.");
        }

        private static void ClosePlayModePreview()
        {
            Require(Application.isPlaying, "Play Mode is required.");
            DayOneIdentityPanel panel = UnityEngine.Object.FindAnyObjectByType<DayOneIdentityPanel>(FindObjectsInactive.Include);
            FirstPersonController player = UnityEngine.Object.FindAnyObjectByType<FirstPersonController>();
            Require(panel != null && player != null && panel.IsOpen, "Identity panel is not open.");
            Button close = panel.GetComponentsInChildren<Button>(true).FirstOrDefault(button => button.name == "Close");
            Require(close != null, "Identity close button is missing.");
            close.onClick.Invoke();
            Require(!panel.IsOpen && !DayOneIdentityPanel.IsAnyOpen, "Identity panel did not close.");
            Require(player.InputEnabled && player.CursorLocked && !Cursor.visible,
                "Player input or cursor was not restored after closing the panel.");
            WriteResult("PASS Play Mode close button restored player input and cursor lock.");
        }

        private static void VerifyDayTwoPlayMode()
        {
            Require(Application.isPlaying, "Play Mode is required.");
            DayCycleManager manager = UnityEngine.Object.FindAnyObjectByType<DayCycleManager>();
            DayOneIdentityMonitor monitor = UnityEngine.Object.FindAnyObjectByType<DayOneIdentityMonitor>();
            DayOneIdentityPanel panel = UnityEngine.Object.FindAnyObjectByType<DayOneIdentityPanel>(FindObjectsInactive.Include);
            TestCustomerNPC customer = UnityEngine.Object.FindAnyObjectByType<TestCustomerNPC>(FindObjectsInactive.Include);
            FirstPersonController player = UnityEngine.Object.FindAnyObjectByType<FirstPersonController>();
            Require(manager != null && monitor != null && panel != null && customer != null && player != null,
                "DAY 2 Play Mode identity references are incomplete.");
            Require(manager.DebugStartDay(2), "DAY 2 Play Mode start failed.");
            foreach (Day2RequestDefinition definition in Day2RequestCatalog.CreateDefinitions())
            {
                Require(manager.DebugSelectDayTwoRequest(definition.RequestId),
                    $"DAY 2 Play Mode selection failed: {definition.RequestId}");
                customer.gameObject.SetActive(false);
                Require(!manager.CurrentIdentityVerified && !manager.CanAcceptCurrentLetter(definition.CustomerId),
                    $"DAY 2 previous approval leaked: {definition.RequestId}");
                manager.NotifyDayTwoIdentityCheckRequested(definition.CustomerId);
                Require(monitor.CanInteract(player.gameObject),
                    $"DAY 2 monitor did not activate: {definition.RequestId}");
                monitor.Interact(player.gameObject);
                Require(panel.IsOpen, $"DAY 2 panel did not open: {definition.RequestId}");
                TextMeshProUGUI[] labels = panel.GetComponentsInChildren<TextMeshProUGUI>(true);
                string day = labels.First(label => label.name == "Day").text;
                string customerName = labels.First(label => label.name == "Customer").text;
                string work = labels.First(label => label.name == "Work").text;
                string item = labels.First(label => label.name == "Item").text;
                string expectedName = definition.CustomerId == "drunk_driver" ? "음주운전자" :
                    CustomerDialogueCatalog.TryGet(2, definition.CustomerId, out CustomerDialogueScenario scenario)
                        ? scenario.SpeakerName : definition.CustomerDisplayName;
                Require(day == "DAY 02" &&
                        customerName.Contains(expectedName) &&
                        work.Contains(definition.RequestType == MailRequestType.Dispatch ? "발송" : "수령") &&
                        definition.RequestedItems.All(type => item.Contains(Day2Labels.GetItemLabel(type))) &&
                        item.Contains("편지 처리: 필요"),
                    $"DAY 2 displayed work data is wrong: {definition.RequestId}");
                Button lookup = panel.GetComponentsInChildren<Button>(true).First(button => button.name == "Lookup");
                Button confirm = panel.GetComponentsInChildren<Button>(true).First(button => button.name == "Confirm");
                Require(!confirm.interactable, "DAY 2 approved before lookup.");
                lookup.onClick.Invoke();
                Require(confirm.interactable && manager.CurrentIdentityMatches,
                    $"DAY 2 lookup did not enable approval: {definition.RequestId}");
                confirm.onClick.Invoke();
                Require(manager.CurrentDayTwoIdentityVerified && !monitor.CanInteract(player.gameObject) &&
                        manager.CanAcceptCurrentLetter(definition.CustomerId),
                    $"DAY 2 approval did not unlock work and disable monitor: {definition.RequestId}");
                panel.Close();
                Require(manager.DebugAcceptCurrentRequest() && manager.CurrentDayTwoRequest.RequestAccepted,
                    $"DAY 2 package acceptance failed after approval: {definition.RequestId}");
            }
            WriteResult("PASS Play Mode DAY 2: all five monitor records, labels, lookup, approval, reinteraction lock, and package acceptance.");
        }

        private static void VerifyMonitorRayDirections()
        {
            Require(Application.isPlaying, "Play Mode is required.");
            DayCycleManager manager = UnityEngine.Object.FindAnyObjectByType<DayCycleManager>();
            TestCustomerNPC customer = UnityEngine.Object.FindAnyObjectByType<TestCustomerNPC>(FindObjectsInactive.Include);
            DayOneIdentityPanel panel = UnityEngine.Object.FindAnyObjectByType<DayOneIdentityPanel>(FindObjectsInactive.Include);
            DayOneIdentityMonitor monitor = UnityEngine.Object.FindAnyObjectByType<DayOneIdentityMonitor>();
            FirstPersonInteractor interactor = UnityEngine.Object.FindAnyObjectByType<FirstPersonInteractor>();
            Camera camera = UnityEngine.Object.FindAnyObjectByType<FirstPersonController>()?.GetComponentInChildren<Camera>();
            Require(manager != null && customer != null && panel != null && monitor != null && interactor != null && camera != null,
                "Monitor ray verification references are incomplete.");

            Require(manager.DebugStartDay(1) && manager.DebugSelectDayOneCustomer("mascot"),
                "DAY 1 monitor ray setup failed.");
            customer.gameObject.SetActive(false);
            manager.NotifyDayOneIdentityCheckRequested("mascot");
            if (panel.IsOpen)
                panel.Close();

            Collider monitorCollider = monitor.GetComponent<Collider>();
            Require(monitorCollider != null && monitorCollider.enabled, "Monitor collider is missing.");
            Vector3 originalPosition = camera.transform.position;
            Quaternion originalRotation = camera.transform.rotation;
            Vector3 center = monitorCollider.bounds.center;
            try
            {
                EvaluateRay(camera, interactor, center + Vector3.forward * 1.5f, center);
                Require(interactor.HasInteractionTarget && interactor.CurrentPrompt == "신원 확인",
                    "Monitor screen center did not select identity interaction.");

                EvaluateDirection(camera, interactor, center + Vector3.forward * 1.5f, Vector3.up, Vector3.forward);
                Require(!interactor.HasInteractionTarget, "Ceiling aim retained identity interaction.");

                EvaluateDirection(camera, interactor, new Vector3(-2f, 1.6f, center.z), Vector3.left, Vector3.up);
                Require(!interactor.HasInteractionTarget, "Wall aim retained identity interaction.");

                EvaluateDirection(camera, interactor, new Vector3(center.x, 1.6f, center.z), Vector3.down, Vector3.forward);
                Require(!interactor.HasInteractionTarget, "Floor aim retained identity interaction.");

                EvaluateRay(camera, interactor, new Vector3(2.8f, 1.9f, -12.02f), new Vector3(4.45f, 1.9f, -12.02f));
                Require(interactor.CurrentPrompt != "신원 확인", "Frame aim selected the identity monitor.");

                EvaluateRay(camera, interactor, center + Vector3.forward * 3.25f, center);
                Require(!interactor.HasInteractionTarget, "Monitor remained interactive beyond 3m.");

                EvaluateRay(camera, interactor, new Vector3(5.5f, 1.6f, center.z), center);
                Require(!interactor.HasInteractionTarget, "Monitor was selected through the east wall.");
            }
            finally
            {
                camera.transform.SetPositionAndRotation(originalPosition, originalRotation);
            }

            WriteResult("PASS monitor-only ray selection; ceiling, wall, floor, frame, >3m, and occluded rays rejected identity interaction.");
        }

        private static void QueueMonitorRightClick()
        {
            Require(Application.isPlaying, "Play Mode is required.");
            Mouse mouse = Mouse.current;
            FirstPersonInteractor interactor = UnityEngine.Object.FindAnyObjectByType<FirstPersonInteractor>();
            DayOneIdentityPanel panel = UnityEngine.Object.FindAnyObjectByType<DayOneIdentityPanel>(FindObjectsInactive.Include);
            Require(mouse != null && interactor != null && panel != null && !panel.IsOpen,
                "Right-click verification references are incomplete.");
            interactor.DebugRefreshTarget();
            Require(interactor.HasInteractionTarget && interactor.CurrentPrompt == "신원 확인",
                "Monitor is not the current right-click target.");

            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
            int updates = 0;
            EditorApplication.CallbackFunction callback = null;
            callback = () =>
            {
                updates++;
                if (updates < 3)
                    return;
                EditorApplication.update -= callback;
                InputSystem.QueueStateEvent(mouse, new MouseState());
                try
                {
                    Require(panel.IsOpen, "Queued right-click did not open the identity panel.");
                    WriteResult("PASS Input System right-click opened the identity panel from the monitor ray target.");
                }
                catch (Exception exception)
                {
                    File.WriteAllText("Temp/DeadLetterIdentityResult.txt", exception.ToString());
                    Debug.LogException(exception);
                }
            };
            EditorApplication.update += callback;
        }


        private static void EvaluateRay(Camera camera, FirstPersonInteractor interactor, Vector3 origin, Vector3 target)
        {
            camera.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(target - origin, Vector3.up));
            Physics.SyncTransforms();
            interactor.DebugRefreshTarget();
        }

        private static void EvaluateDirection(Camera camera, FirstPersonInteractor interactor, Vector3 origin,
            Vector3 direction, Vector3 up)
        {
            camera.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(direction, up));
            Physics.SyncTransforms();
            interactor.DebugRefreshTarget();
        }

        private static void WriteResult(string message)
        {
            File.WriteAllText("Temp/DeadLetterIdentityResult.txt", message);
            Debug.Log(message);
        }

        private static GameObject CreatePrefab(DeadLetterUITheme theme)
        {
            GameObject root = new(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(DayOneIdentityPanel));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Image dim = Panel(root.transform, "Dim", new Color(0f, 0f, 0f, 0.72f));
            Stretch(dim.rectTransform);
            Image window = Panel(dim.transform, "Monitor Window", new Color(0.025f, 0.055f, 0.04f, 0.98f));
            Anchor(window.rectTransform, Vector2.one * 0.5f, Vector2.zero, new Vector2(780f, 520f));

            TextMeshProUGUI title = Text(window.transform, "Title", "DEAD LETTER 신원 확인", theme, 30,
                theme.PrimaryText, TextAlignmentOptions.Center);
            Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -45f), new Vector2(700f, 48f));
            TextMeshProUGUI day = Text(window.transform, "Day", "DAY 1", theme, 22, theme.Accent,
                TextAlignmentOptions.Center);
            Anchor(day.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -94f), new Vector2(700f, 36f));
            TextMeshProUGUI customer = Text(window.transform, "Customer", "현재 민원인: 조회 대상 없음", theme,
                theme.BodySize, theme.PrimaryText, TextAlignmentOptions.Left);
            Anchor(customer.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -164f), new Vector2(620f, 34f));
            TextMeshProUGUI work = Text(window.transform, "Work", "업무 유형: 편지 발송", theme,
                theme.BodySize, theme.PrimaryText, TextAlignmentOptions.Left);
            Anchor(work.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -208f), new Vector2(620f, 34f));
            TextMeshProUGUI item = Text(window.transform, "Item", "접수 물품: 편지 1통", theme,
                theme.BodySize, theme.PrimaryText, TextAlignmentOptions.Left);
            Anchor(item.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -252f), new Vector2(620f, 34f));
            TextMeshProUGUI status = Text(window.transform, "Status", "신원 확인 상태: 확인 필요", theme,
                theme.BodySize, theme.PrimaryText, TextAlignmentOptions.Left);
            Anchor(status.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -310f), new Vector2(620f, 38f));

            Button lookup = Button(window.transform, "Lookup", "조회", theme, theme.Monitor);
            Anchor(lookup.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(-245f, 62f), new Vector2(150f, 58f));
            Button confirm = Button(window.transform, "Confirm", "접수 승인", theme, theme.Monitor);
            Anchor(confirm.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(-35f, 62f), new Vector2(220f, 58f));
            Button close = Button(window.transform, "Close", "닫기", theme, theme.SecondaryBackground);
            Anchor(close.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(205f, 62f), new Vector2(150f, 58f));

            DayOneIdentityPanel panel = root.GetComponent<DayOneIdentityPanel>();
            SerializedObject serialized = new(panel);
            serialized.FindProperty("customerNameText").objectReferenceValue = customer;
            serialized.FindProperty("statusText").objectReferenceValue = status;
            serialized.FindProperty("lookupButton").objectReferenceValue = lookup;
            serialized.FindProperty("confirmButton").objectReferenceValue = confirm;
            serialized.FindProperty("closeButton").objectReferenceValue = close;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(lookup.onClick, panel.LookUpIdentity);
            UnityEventTools.AddPersistentListener(confirm.onClick, panel.ConfirmIdentity);
            UnityEventTools.AddPersistentListener(close.onClick, panel.Close);
            return root;
        }

        private static void ValidateRuntimeState()
        {
            GameObject root = new("Identity Validation Root");
            GameObject customerObject = new("Identity Validation Customer");
            try
            {
                LetterTaskState state = root.AddComponent<LetterTaskState>();
                TestCustomerNPC customer = customerObject.AddComponent<TestCustomerNPC>();
                DayCycleManager manager = root.AddComponent<DayCycleManager>();
                SerializedObject serialized = new(manager);
                serialized.FindProperty("taskState").objectReferenceValue = state;
                serialized.FindProperty("customer").objectReferenceValue = customer;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Require(manager.DebugStartDay(1), "DAY 1 test start failed.");
                string[] ids = { "mascot", "drunk_driver", "model_student", "bad_construction", "troublemaker" };
                foreach (string id in ids)
                {
                    Require(manager.DebugSelectDayOneCustomer(id), $"DAY 1 customer selection failed: {id}");
                    manager.NotifyDayOneIdentityCheckRequested(id);
                    Require(manager.CurrentDayOneIdentityRequired, $"Identity objective not activated: {id}");
                    Require(!manager.CanAcceptDayOneLetter(id), $"Letter was accepted before identity check: {id}");
                    Require(!manager.ConfirmCurrentDayOneIdentity(), $"Approval succeeded before lookup: {id}");
                    Require(manager.LookUpCurrentDayOneIdentity(out string result) && result == "정보 일치",
                        $"Identity lookup failed: {id}");
                    Require(manager.ConfirmCurrentDayOneIdentity(), $"Identity confirmation failed: {id}");
                    Require(manager.IsDayOneIdentityVerified(id) && manager.CanAcceptDayOneLetter(id),
                        $"Identity result was not isolated to current customer: {id}");
                    Require(!manager.CurrentDayOneIdentityRequired,
                        $"Monitor remained available after approval: {id}");
                    Require(manager.ConfirmCurrentDayOneIdentity(), $"Repeated confirmation must be idempotent: {id}");
                }

                Require(manager.DebugStartDay(2), "DAY 2 regression start failed.");
                Require(!manager.CanUseDayOneIdentityMonitor, "Identity monitor leaked into DAY 2.");
                Require(!manager.CurrentIdentityVerified && !manager.CurrentIdentityLookedUp,
                    "DAY 1 identity state leaked into DAY 2.");
                Day2RequestDefinition[] expected = Day2RequestCatalog.CreateDefinitions().ToArray();
                int studentIndex = Array.FindIndex(manager.DayTwoOrder.ToArray(), request =>
                    request.Definition.RequestId == Day2RequestCatalog.ModelStudentRequestId);
                int drownedIndex = Array.FindIndex(manager.DayTwoOrder.ToArray(), request =>
                    request.Definition.RequestId == Day2RequestCatalog.TroublemakerRequestId);
                Require(studentIndex >= 0 && drownedIndex > studentIndex,
                    "DAY 2 student transfer order changed.");
                foreach (Day2RequestDefinition definition in expected)
                {
                    Require(manager.DebugSelectDayTwoRequest(definition.RequestId),
                        $"DAY 2 customer selection failed: {definition.RequestId}");
                    Require(!manager.CanAcceptCurrentLetter(definition.CustomerId),
                        $"DAY 2 letter was available before approval: {definition.RequestId}");
                    manager.NotifyDayTwoIdentityCheckRequested(definition.CustomerId);
                    Require(manager.CurrentIdentityRequired && !manager.ConfirmCurrentIdentity(),
                        $"DAY 2 approved before lookup: {definition.RequestId}");
                    Require(manager.CurrentDayTwoRequest.Definition.RequestType == definition.RequestType &&
                            manager.CurrentDayTwoRequest.Definition.RequestedItems.SequenceEqual(definition.RequestedItems),
                        $"DAY 2 work data changed: {definition.RequestId}");
                    Require(manager.LookUpCurrentIdentity(out string dayTwoResult) && dayTwoResult == "정보 일치" &&
                            manager.CurrentIdentityMatches, $"DAY 2 lookup failed: {definition.RequestId}");
                    Require(manager.ConfirmCurrentIdentity() && manager.CurrentDayTwoIdentityVerified &&
                            !manager.CurrentIdentityRequired && manager.CanAcceptCurrentLetter(definition.CustomerId),
                        $"DAY 2 approval failed: {definition.RequestId}");
                    Require(manager.DebugAcceptCurrentRequest() && manager.CurrentDayTwoRequest.RequestAccepted,
                        $"DAY 2 original package acceptance failed: {definition.RequestId}");
                }
                Require(manager.DebugSelectDayTwoRequest(expected[0].RequestId) &&
                        !manager.CurrentDayTwoIdentityVerified && !manager.CurrentIdentityLookedUp,
                    "Previous DAY 2 identity state leaked into the next request.");
                Require(manager.DebugStartDay(1), "DAY 1 reset failed.");
                Require(manager.DebugSelectDayOneCustomer("mascot"), "DAY 1 reset customer selection failed.");
                manager.NotifyDayOneIdentityCheckRequested("mascot");
                Require(!manager.IsDayOneIdentityVerified("mascot"), "Identity record survived a new day initialization.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(customerObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateSceneAndPrefab(Scene scene)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Require(prefab != null && prefab.GetComponent<Canvas>() != null &&
                    prefab.GetComponent<DayOneIdentityPanel>() != null,
                "Identity monitor prefab is incomplete.");
            Require(prefab.GetComponentsInChildren<TextMeshProUGUI>(true).Length >= 8,
                "Identity monitor prefab is missing TMP labels.");
            DeadLetterUITheme theme = AssetDatabase.LoadAssetAtPath<DeadLetterUITheme>(ThemePath);
            Require(prefab.GetComponentsInChildren<TextMeshProUGUI>(true).All(text => text.font == theme.PrimaryFont),
                "Identity monitor prefab is not using GalmuriMono11.");

            Require(scene.IsValid() && scene.path == ScenePath, "SampleScene is not loaded.");
            GameObject monitor = FindSceneObject(scene, MonitorName);
            GameObject canvas = FindSceneObject(scene, CanvasName);
            Require(monitor != null && monitor.GetComponent<DayOneIdentityMonitor>() != null,
                "Existing counter monitor is not wired for identity interaction.");
            BoxCollider monitorCollider = monitor.GetComponent<BoxCollider>();
            Require(monitorCollider is { enabled: true }, "Monitor interaction collider is missing.");
            GameObject deskObject = FindSceneObject(scene, "Collider - 창구-책상");
            Collider deskCollider = deskObject != null ? deskObject.GetComponent<Collider>() : null;
            Require(deskCollider != null && Mathf.Abs(monitor.transform.position.x - deskCollider.bounds.center.x) < 0.01f,
                "Identity interaction is not aligned to the production counter desk.");
            Require(monitorCollider.size.y <= 0.7f && monitorCollider.size.z <= 0.2f,
                "Identity interaction collider is larger than the CRT screen.");
            Require(canvas != null && canvas.GetComponent<DayOneIdentityPanel>() != null,
                "Identity monitor canvas instance is missing.");
        }

        private static GameObject FindSceneObject(Scene scene, string name)
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(transform => transform.name == name)?.gameObject;
        }

        private static Image Panel(Transform parent, string name, Color color)
        {
            GameObject gameObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static TextMeshProUGUI Text(Transform parent, string name, string value, DeadLetterUITheme theme,
            float size, Color color, TextAlignmentOptions alignment)
        {
            GameObject gameObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            gameObject.transform.SetParent(parent, false);
            TextMeshProUGUI text = gameObject.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = theme.PrimaryFont;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            return text;
        }

        private static Button Button(Transform parent, string name, string label, DeadLetterUITheme theme, Color color)
        {
            GameObject gameObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            gameObject.transform.SetParent(parent, false);
            gameObject.GetComponent<Image>().color = color;
            Button button = gameObject.GetComponent<Button>();
            TextMeshProUGUI text = Text(gameObject.transform, "Label", label, theme, theme.BodySize,
                theme.PrimaryText, TextAlignmentOptions.Center);
            Stretch(text.rectTransform);
            return button;
        }

        private static void Anchor(RectTransform target, Vector2 anchor, Vector2 position, Vector2 size)
        {
            target.anchorMin = target.anchorMax = target.pivot = anchor;
            target.anchoredPosition = position;
            target.sizeDelta = size;
        }

        private static void Stretch(RectTransform target)
        {
            target.anchorMin = Vector2.zero;
            target.anchorMax = Vector2.one;
            target.offsetMin = target.offsetMax = Vector2.zero;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
