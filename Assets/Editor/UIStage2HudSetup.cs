using System;
using System.IO;
using DeadLetter.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DeadLetter.Editor
{
    public static class UIStage2HudSetup
    {
        private const string ThemePath = "Assets/UI/Common/DeadLetterUITheme.asset";
        private const string PrefabPath = "Assets/UI/Prefabs/BasicHUD.prefab";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string PreviewPath = "Assets/UI/Tests/UI_Stage2_HUD_Preview.png";
        private const string RootName = "Basic HUD Canvas";

        [MenuItem("Tools/DEAD LETTER/UI/Build Stage 2 HUD")]
        public static void BuildFromMenu()
        {
            Build();
            EditorUtility.DisplayDialog("DEAD LETTER UI", "2단계 기본 HUD가 적용되었습니다.", "확인");
        }

        public static void BuildBatch()
        {
            try
            {
                Build();
                Debug.Log("UI_STAGE2_SETUP_SUCCESS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void RenderPreviewBatch()
        {
            try
            {
                RenderPreview();
                Debug.Log("UI_STAGE2_PREVIEW_SUCCESS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void Build()
        {
            EnsureFolder("Assets/UI/Prefabs");
            EnsureFolder("Assets/UI/Tests");

            DeadLetterUITheme theme = AssetDatabase.LoadAssetAtPath<DeadLetterUITheme>(ThemePath);
            if (theme == null || theme.PrimaryFont == null)
                throw new InvalidOperationException("Stage 1 theme or GalmuriMono11 TMP font is missing.");

            GameObject prefabRoot = CreateHudHierarchy(theme);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            UnityEngine.Object.DestroyImmediate(prefabRoot);

            InstallInGameScene();
            ValidatePrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Stage 2 HUD prepared: {PrefabPath}, {ScenePath}");
        }

        private static GameObject CreateHudHierarchy(DeadLetterUITheme theme)
        {
            GameObject root = new(RootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(BasicHUDController));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Image dayPanel = CreatePanel(root.transform, "DAY Panel", new Color(theme.Background.r, theme.Background.g, theme.Background.b, 0.78f));
            SetTopLeft(dayPanel.rectTransform, new Vector2(28f, -28f), new Vector2(178f, 54f));
            CreateAccent(dayPanel.transform, theme.Accent);
            TextMeshProUGUI dayText = CreateText(dayPanel.transform, "DAY Text", "DAY 01", theme, theme.HeadingSize, theme.PrimaryText, TextAlignmentOptions.Center);
            Stretch(dayText.rectTransform, 12f, 4f, 6f, 4f);

            Image taskPanel = CreatePanel(root.transform, "Task Panel", new Color(theme.Background.r, theme.Background.g, theme.Background.b, 0.82f));
            SetTopRight(taskPanel.rectTransform, new Vector2(-28f, -28f), new Vector2(420f, 126f));
            CreateAccent(taskPanel.transform, theme.Accent);
            TextMeshProUGUI progress = CreateText(taskPanel.transform, "Progress", "업무 00 / 05", theme, theme.BodySize, theme.PrimaryText, TextAlignmentOptions.TopLeft);
            SetAnchored(progress.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -14f), new Vector2(370f, 30f), new Vector2(0f, 1f));
            TextMeshProUGUI current = CreateText(taskPanel.transform, "Current Work", "현재 업무: 편지 처리", theme, theme.CaptionSize, theme.PrimaryText, TextAlignmentOptions.TopLeft);
            SetAnchored(current.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -50f), new Vector2(370f, 26f), new Vector2(0f, 1f));
            TextMeshProUGUI next = CreateText(taskPanel.transform, "Next Action", "다음 행동: 창구 민원인에게 편지 받기", theme, theme.CaptionSize, theme.MutedText, TextAlignmentOptions.TopLeft);
            SetAnchored(next.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -80f), new Vector2(370f, 32f), new Vector2(0f, 1f));

            Image crosshair = CreatePanel(root.transform, "Crosshair", theme.PrimaryText);
            SetAnchored(crosshair.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, 6f), new Vector2(0.5f, 0.5f));

            Image prompt = CreatePanel(root.transform, "Interaction Prompt", new Color(theme.Background.r, theme.Background.g, theme.Background.b, 0.84f));
            SetAnchored(prompt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -64f), new Vector2(340f, 38f), new Vector2(0.5f, 0.5f));
            TextMeshProUGUI promptButton = CreateText(prompt.transform, "Button", "[우클릭]", theme, theme.CaptionSize, theme.Accent, TextAlignmentOptions.Center);
            SetAnchored(promptButton.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(62f, 0f), new Vector2(104f, 30f), new Vector2(0.5f, 0.5f));
            TextMeshProUGUI promptAction = CreateText(prompt.transform, "Action", "문 열기", theme, theme.CaptionSize, theme.PrimaryText, TextAlignmentOptions.MidlineLeft);
            SetAnchored(promptAction.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(125f, 0f), new Vector2(190f, 30f), new Vector2(0f, 0.5f));

            BasicHUDController controller = root.GetComponent<BasicHUDController>();
            SerializedObject serialized = new(controller);
            SetReference(serialized, "theme", theme);
            SetReference(serialized, "dayText", dayText);
            SetReference(serialized, "progressText", progress);
            SetReference(serialized, "currentWorkText", current);
            SetReference(serialized, "nextActionText", next);
            SetReference(serialized, "crosshair", crosshair);
            SetReference(serialized, "promptPanel", prompt.gameObject);
            SetReference(serialized, "promptButtonText", promptButton);
            SetReference(serialized, "promptActionText", promptAction);
            SetReference(serialized, "taskPanel", taskPanel.gameObject);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            prompt.gameObject.SetActive(false);
            return root;
        }

        private static void InstallInGameScene()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject existing = GameObject.Find(RootName);
            if (existing == null)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (prefab == null)
                    throw new InvalidOperationException($"HUD prefab could not be loaded: {PrefabPath}");
                PrefabUtility.InstantiatePrefab(prefab, scene);
            }

            EditorSceneManager.SaveScene(scene);
        }

        private static void ValidatePrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null || prefab.GetComponent<Canvas>() == null || prefab.GetComponent<BasicHUDController>() == null)
                throw new InvalidOperationException("Basic HUD prefab validation failed.");
            if (prefab.GetComponentInChildren<TextMeshProUGUI>(true) == null)
                throw new InvalidOperationException("Basic HUD prefab does not contain TMP text.");
        }

        private static void RenderPreview()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Stage 2 HUD Preview";

            GameObject cameraObject = new("HUD Preview Camera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.05f, 0.045f, 1f);
            camera.transform.position = new Vector3(0f, 0f, -10f);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null)
                throw new InvalidOperationException("HUD preview instance could not be created.");

            Canvas canvas = instance.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            Transform prompt = instance.transform.Find("Interaction Prompt");
            if (prompt != null)
                prompt.gameObject.SetActive(true);

            const int width = 1920;
            const int height = 1080;
            RenderTexture renderTexture = new(width, height, 24, RenderTextureFormat.ARGB32);
            Texture2D image = new(width, height, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = renderTexture;
                RenderTexture.active = renderTexture;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                image.Apply();
                File.WriteAllBytes(PreviewPath, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(renderTexture);
                UnityEngine.Object.DestroyImmediate(image);
            }

            AssetDatabase.ImportAsset(PreviewPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
        }

        private static Image CreatePanel(Transform parent, string name, Color color)
        {
            GameObject gameObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void CreateAccent(Transform parent, Color color)
        {
            Image accent = CreatePanel(parent, "Accent", color);
            RectTransform rect = accent.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(4f, 0f);
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string value, DeadLetterUITheme theme, int size, Color color, TextAlignmentOptions alignment)
        {
            GameObject gameObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            gameObject.transform.SetParent(parent, false);
            TextMeshProUGUI text = gameObject.GetComponent<TextMeshProUGUI>();
            text.font = theme.PrimaryFont;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        private static void SetReference(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
                throw new InvalidOperationException($"HUD field is missing: {propertyName}");
            property.objectReferenceValue = value;
        }

        private static void SetTopLeft(RectTransform rect, Vector2 position, Vector2 size)
        {
            SetAnchored(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), position, size, new Vector2(0f, 1f));
        }

        private static void SetTopRight(RectTransform rect, Vector2 position, Vector2 size)
        {
            SetAnchored(rect, new Vector2(1f, 1f), new Vector2(1f, 1f), position, size, new Vector2(1f, 1f));
        }

        private static void SetAnchored(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Vector2 pivot)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = $"{current}/{parts[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }
    }
}
