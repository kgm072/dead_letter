using System;
using DeadLetter.DebugTools;
using DeadLetter.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadLetter.Editor
{
    public static class DeveloperTestModeSetup
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string ThemePath = "Assets/UI/Common/DeadLetterUITheme.asset";
        private const string RootName = "Developer Test Mode";

        [MenuItem("Tools/DEAD LETTER/Install Developer Test Mode")]
        public static void InstallFromMenu()
        {
            Install();
            EditorUtility.DisplayDialog("DEAD LETTER", "개발자 테스트 모드가 설치되었습니다.", "확인");
        }

        public static void InstallBatch()
        {
            try
            {
                Install();
                Debug.Log("DEVELOPER_TEST_MODE_SETUP_SUCCESS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void Install()
        {
            DeadLetterUITheme theme = AssetDatabase.LoadAssetAtPath<DeadLetterUITheme>(ThemePath);
            if (theme == null || theme.PrimaryFont == null)
                throw new InvalidOperationException("DeadLetterUITheme or GalmuriMono11 TMP font is missing.");

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            DeveloperTestMode mode = UnityEngine.Object.FindAnyObjectByType<DeveloperTestMode>(FindObjectsInactive.Include);
            if (mode == null)
            {
                GameObject root = new(RootName);
                SceneManager.MoveGameObjectToScene(root, scene);
                mode = root.AddComponent<DeveloperTestMode>();
            }

            SerializedObject serialized = new(mode);
            serialized.FindProperty("theme").objectReferenceValue = theme;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(mode);
            EditorSceneManager.SaveScene(scene);

            DeveloperTestMode[] modes = UnityEngine.Object.FindObjectsByType<DeveloperTestMode>(FindObjectsInactive.Include);
            if (modes.Length != 1)
                throw new InvalidOperationException($"Expected one developer mode root, found {modes.Length}.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
