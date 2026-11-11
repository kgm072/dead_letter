using System;
using System.IO;
using System.Linq;
using DeadLetter.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace DeadLetter.Editor
{
    public static class UIStage1Setup
    {
        private const string FontPath = "Assets/Resources/UI/Fonts/GalmuriMono11/GalmuriMono11.ttf";
        private const string FontAssetPath = "Assets/Resources/UI/Fonts/GalmuriMono11/GalmuriMono11 TMP.asset";
        private const string RuntimeFontResourcePath = "UI/Fonts/GalmuriMono11/GalmuriMono11";
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        private const string ThemePath = "Assets/UI/Common/DeadLetterUITheme.asset";
        private const string TestScenePath = "Assets/Scenes/UI_Font_Test.unity";
        private const string PreviewPath = "Assets/UI/Tests/UI_Font_Test_Preview.png";

        private const string ValidationCharacters =
            "DEAD LETTER 0123456789 ABCDEFGHIJKLMNOPQRSTUVWXYZ abcdefghijklmnopqrstuvwxyz " +
            "가나다라마바사아자차카타파하 데드 레터 우편국 민원인 편지 작업 도구 우표 도장 " +
             "불 물 전기 충격 약 술 업무 완료 일시정지 다음 DAY 시작 " +
             "인형탈 음주운전 모범생 부실공사 익사자 선택 신원 확인 일치 불일치 경고 성공 시스템 오류 대기 중 접수 발송 수령 " +
             "[F] [ESC] !?.,:;+-/()%";

        [MenuItem("Tools/DEAD LETTER/UI/Build Stage 1 Font Test")]
        public static void BuildFromMenu()
        {
            BuildAssetsAndTestScene();
            EditorUtility.DisplayDialog("DEAD LETTER UI", "공통 폰트와 테스트 씬 준비가 완료되었습니다.", "확인");
        }

        public static void BuildBatch()
        {
            try
            {
                BuildAssetsAndTestScene();
                Debug.Log("UI_STAGE1_SETUP_SUCCESS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void BuildAssetsAndTestScene()
        {
            EnsureFolder("Assets/UI/Common");
            EnsureFolder("Assets/UI/Tests");

            AssetDatabase.ImportAsset(FontPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (sourceFont == null)
                throw new InvalidOperationException($"GalmuriMono11 font was not imported: {FontPath}");
            ValidateSourceFontCoverage(sourceFont);
            ValidateRuntimeGuiFont();

            TMP_Settings tmpSettings = CreateOrUpdateTmpSettings();
            TMP_FontAsset fontAsset = CreateOrUpdateFontAsset(sourceFont);
            ConfigureTmpSettings(tmpSettings, fontAsset);
            DeadLetterUITheme theme = CreateOrUpdateTheme(fontAsset);
            BuildTestScene(theme);
            ValidateFont(fontAsset);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"UI Stage 1 prepared: {FontAssetPath}, {ThemePath}, {TestScenePath}");
        }

        private static TMP_Settings CreateOrUpdateTmpSettings()
        {
            TMP_Settings settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
            if (settings == null)
                throw new InvalidOperationException("TMP Essential Resources are missing. Import them before building the UI font test.");
            if (Shader.Find("TextMeshPro/Distance Field") == null)
                throw new InvalidOperationException("TMP Essential Resources are present, but the distance field shader is unavailable.");

            SerializedObject serializedSettings = new(settings);
            serializedSettings.FindProperty("assetVersion").stringValue = "2";
            serializedSettings.FindProperty("m_TextWrappingMode").enumValueIndex = (int)TextWrappingModes.Normal;
            serializedSettings.FindProperty("m_enableParseEscapeCharacters").boolValue = true;
            serializedSettings.FindProperty("m_EnableRaycastTarget").boolValue = false;
            serializedSettings.FindProperty("m_GetFontFeaturesAtRuntime").boolValue = true;
            serializedSettings.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;
            serializedSettings.FindProperty("m_defaultFontSize").floatValue = 18f;
            serializedSettings.FindProperty("m_defaultAutoSizeMinRatio").floatValue = 0.5f;
            serializedSettings.FindProperty("m_defaultAutoSizeMaxRatio").floatValue = 2f;
            serializedSettings.FindProperty("m_defaultTextMeshProTextContainerSize").vector2Value = new Vector2(20f, 5f);
            serializedSettings.FindProperty("m_defaultTextMeshProUITextContainerSize").vector2Value = new Vector2(200f, 50f);
            serializedSettings.FindProperty("m_UseModernHangulLineBreakingRules").boolValue = true;
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(TmpSettingsPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            TMP_Settings loadedSettings = TMP_Settings.LoadDefaultSettings();
            if (loadedSettings == null)
                throw new InvalidOperationException($"TMP Settings could not be loaded from Resources: {TmpSettingsPath}");
            return loadedSettings;
        }

        private static void ConfigureTmpSettings(TMP_Settings settings, TMP_FontAsset fontAsset)
        {
            SerializedObject serializedSettings = new(settings);
            serializedSettings.FindProperty("m_defaultFontAsset").objectReferenceValue = fontAsset;
            SerializedProperty fallbacks = serializedSettings.FindProperty("m_fallbackFontAssets");
            fallbacks.arraySize = 0;
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }

        private static TMP_FontAsset CreateOrUpdateFontAsset(Font sourceFont)
        {
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (fontAsset == null)
            {
                fontAsset = TMP_FontAsset.CreateFontAsset(
                    sourceFont,
                    48,
                    4,
                    GlyphRenderMode.SDFAA,
                    2048,
                    2048,
                    AtlasPopulationMode.Dynamic,
                    true);
                if (fontAsset == null)
                    throw new InvalidOperationException("TMP failed to create the GalmuriMono11 font asset.");

                fontAsset.name = "GalmuriMono11 TMP";
                AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
            }

            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            fontAsset.isMultiAtlasTexturesEnabled = true;
            string missingCharacters = new(
                ValidationCharacters
                    .Where(character => !char.IsWhiteSpace(character) && !fontAsset.HasCharacter(character))
                    .Distinct()
                    .ToArray());
            if (!string.IsNullOrEmpty(missingCharacters))
                fontAsset.TryAddCharacters(missingCharacters, out missingCharacters, true);
            AttachGeneratedSubAssets(fontAsset);
            EditorUtility.SetDirty(fontAsset);

            if (!string.IsNullOrEmpty(missingCharacters))
                throw new InvalidOperationException($"GalmuriMono11 is missing validation glyphs: {missingCharacters}");

            return fontAsset;
        }

        private static void AttachGeneratedSubAssets(TMP_FontAsset fontAsset)
        {
            if (fontAsset.material != null && !AssetDatabase.Contains(fontAsset.material))
            {
                fontAsset.material.name = "GalmuriMono11 TMP Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            foreach (Texture2D atlasTexture in fontAsset.atlasTextures.Where(texture => texture != null))
            {
                if (!AssetDatabase.Contains(atlasTexture))
                {
                    atlasTexture.name = "GalmuriMono11 TMP Atlas";
                    AssetDatabase.AddObjectToAsset(atlasTexture, fontAsset);
                }
            }
        }

        private static DeadLetterUITheme CreateOrUpdateTheme(TMP_FontAsset fontAsset)
        {
            DeadLetterUITheme theme = AssetDatabase.LoadAssetAtPath<DeadLetterUITheme>(ThemePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<DeadLetterUITheme>();
                AssetDatabase.CreateAsset(theme, ThemePath);
            }

            SerializedObject serializedTheme = new(theme);
            serializedTheme.FindProperty("primaryFont").objectReferenceValue = fontAsset;
            serializedTheme.FindProperty("titleSize").intValue = 36;
            serializedTheme.FindProperty("headingSize").intValue = 24;
            serializedTheme.FindProperty("bodySize").intValue = 18;
            serializedTheme.FindProperty("captionSize").intValue = 16;
            serializedTheme.FindProperty("background").colorValue = new Color(0.018f, 0.018f, 0.02f, 1f);
            serializedTheme.FindProperty("secondaryBackground").colorValue = new Color(0.12f, 0.075f, 0.045f, 1f);
            serializedTheme.FindProperty("primaryText").colorValue = new Color(0.95f, 0.91f, 0.82f, 1f);
            serializedTheme.FindProperty("mutedText").colorValue = new Color(0.62f, 0.59f, 0.54f, 1f);
            serializedTheme.FindProperty("accent").colorValue = new Color(0.82f, 0.36f, 0.12f, 1f);
            serializedTheme.FindProperty("monitor").colorValue = new Color(0.18f, 0.42f, 0.27f, 1f);
            serializedTheme.FindProperty("success").colorValue = new Color(0.34f, 0.66f, 0.43f, 1f);
            serializedTheme.FindProperty("danger").colorValue = new Color(0.78f, 0.21f, 0.16f, 1f);
            serializedTheme.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(theme);
            return theme;
        }

        private static void BuildTestScene(DeadLetterUITheme theme)
        {
            SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "UI Font Test";

            GameObject cameraObject = new("UI Test Camera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = theme.Background;
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.transform.position = new Vector3(0f, 0f, -10f);

            GameObject canvasObject = new("UI Font Test - Stage 1", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Image panel = CreatePanel(canvas.transform, "Font Test Panel", new Color(0.035f, 0.03f, 0.028f, 0.98f));
            SetRect(panel.rectTransform, new Vector2(0.12f, 0.14f), new Vector2(0.88f, 0.86f), Vector2.zero, Vector2.zero);

            TextMeshProUGUI title = CreateText(panel.transform, "Title", "DEAD LETTER", theme, theme.TitleSize, theme.PrimaryText, TextAlignmentOptions.Left);
            SetRect(title.rectTransform, new Vector2(0.07f, 0.78f), new Vector2(0.93f, 0.92f), Vector2.zero, Vector2.zero);

            Image rule = CreatePanel(panel.transform, "Accent Rule", theme.Accent);
            SetRect(rule.rectTransform, new Vector2(0.07f, 0.755f), new Vector2(0.93f, 0.765f), Vector2.zero, Vector2.zero);

            TextMeshProUGUI korean = CreateText(
                panel.transform,
                "Korean Sample",
                "한글 테스트  데드 레터 우편국\n민원인 편지 접수 · 우표 · 도장 · 업무 완료\n인형탈 · 음주운전 · 모범생 · 부실공사 · 익사자",
                theme,
                theme.HeadingSize,
                theme.PrimaryText,
                TextAlignmentOptions.Left);
            SetRect(korean.rectTransform, new Vector2(0.07f, 0.49f), new Vector2(0.93f, 0.72f), Vector2.zero, Vector2.zero);

            TextMeshProUGUI latin = CreateText(
                panel.transform,
                "Latin Sample",
                "ENGLISH  ABCDEFGHIJKLMNOPQRSTUVWXYZ\n0123456789  [F] INTERACT  [ESC] CLOSE  !?.,:;+-/()%",
                theme,
                theme.BodySize,
                new Color(0.78f, 0.75f, 0.7f, 1f),
                TextAlignmentOptions.Left);
            SetRect(latin.rectTransform, new Vector2(0.07f, 0.29f), new Vector2(0.93f, 0.48f), Vector2.zero, Vector2.zero);

            Image button = CreatePanel(panel.transform, "Button Sample", theme.SecondaryBackground);
            SetRect(button.rectTransform, new Vector2(0.07f, 0.1f), new Vector2(0.35f, 0.23f), Vector2.zero, Vector2.zero);
            TextMeshProUGUI buttonLabel = CreateText(button.transform, "Label", "선택 1  신원 확인  [F]", theme, theme.BodySize, theme.PrimaryText, TextAlignmentOptions.Center);
            Stretch(buttonLabel.rectTransform);

            Image success = CreatePanel(panel.transform, "Success Sample", new Color(0.025f, 0.08f, 0.045f, 1f));
            SetRect(success.rectTransform, new Vector2(0.39f, 0.1f), new Vector2(0.64f, 0.23f), Vector2.zero, Vector2.zero);
            TextMeshProUGUI successLabel = CreateText(success.transform, "Label", "일치 · 처리 완료", theme, theme.BodySize, theme.Success, TextAlignmentOptions.Center);
            Stretch(successLabel.rectTransform);

            Image danger = CreatePanel(panel.transform, "Danger Sample", new Color(0.12f, 0.035f, 0.03f, 1f));
            SetRect(danger.rectTransform, new Vector2(0.68f, 0.1f), new Vector2(0.93f, 0.23f), Vector2.zero, Vector2.zero);
            TextMeshProUGUI dangerLabel = CreateText(danger.transform, "Label", "불일치 · SYSTEM ERROR", theme, theme.BodySize, theme.Danger, TextAlignmentOptions.Center);
            Stretch(dangerLabel.rectTransform);

            Canvas.ForceUpdateCanvases();
            EditorSceneManager.SaveScene(scene, TestScenePath);
            RenderPreview(camera);

            if (previousSetup.Length > 0 && previousSetup.Any(setup => !string.IsNullOrEmpty(setup.path)))
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
        }

        private static Image CreatePanel(Transform parent, string name, Color color)
        {
            GameObject gameObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static TextMeshProUGUI CreateText(
            Transform parent,
            string name,
            string value,
            DeadLetterUITheme theme,
            int size,
            Color color,
            TextAlignmentOptions alignment)
        {
            GameObject gameObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            gameObject.transform.SetParent(parent, false);
            TextMeshProUGUI text = gameObject.GetComponent<TextMeshProUGUI>();
            text.font = theme.PrimaryFont;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }

        private static void ValidateFont(TMP_FontAsset fontAsset)
        {
            foreach (char character in ValidationCharacters.Where(character => !char.IsWhiteSpace(character)).Distinct())
            {
                if (!fontAsset.HasCharacter(character))
                    throw new InvalidOperationException($"TMP validation failed for U+{(int)character:X4} '{character}'.");
            }

            if (fontAsset.atlasPopulationMode != AtlasPopulationMode.Dynamic || !fontAsset.isMultiAtlasTexturesEnabled)
                throw new InvalidOperationException("TMP font asset must remain dynamic with multi-atlas enabled for Korean glyph coverage.");
        }

        private static void ValidateSourceFontCoverage(Font sourceFont)
        {
            for (int unicode = 0xAC00; unicode <= 0xD7A3; unicode++)
            {
                if (!sourceFont.HasCharacter((char)unicode))
                    throw new InvalidOperationException($"GalmuriMono11 is missing Hangul syllable U+{unicode:X4}.");
            }

            for (int unicode = 0x20; unicode <= 0x7E; unicode++)
            {
                if (!sourceFont.HasCharacter((char)unicode))
                    throw new InvalidOperationException($"GalmuriMono11 is missing basic Latin character U+{unicode:X4}.");
            }
        }

        private static void ValidateRuntimeGuiFont()
        {
            Font runtimeFont = Resources.Load<Font>(RuntimeFontResourcePath);
            if (runtimeFont == null)
                throw new InvalidOperationException($"Runtime IMGUI font could not be loaded: {RuntimeFontResourcePath}");

            foreach (char character in "한글DEAD LETTER 0123456789")
            {
                if (!char.IsWhiteSpace(character) && !runtimeFont.HasCharacter(character))
                    throw new InvalidOperationException($"Runtime IMGUI font is missing U+{(int)character:X4} '{character}'.");
            }
        }

        private static void RenderPreview(Camera camera)
        {
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

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void Stretch(RectTransform rect)
        {
            SetRect(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }
    }
}
