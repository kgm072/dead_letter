using DeadLetter.UI;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadLetter.Editor
{
    public static class MainMenuSceneGenerator
    {
        private const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const string GameScenePath = "Assets/Scenes/SampleScene.unity";

        [InitializeOnLoadMethod]
        private static void ConfigureMainMenu()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    return;

                if (!File.Exists(MenuScenePath))
                    Generate();

                SceneAsset menuScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScenePath);
                if (menuScene != null && EditorSceneManager.playModeStartScene != menuScene)
                    EditorSceneManager.playModeStartScene = menuScene;
            };
        }

        [MenuItem("Tools/DEAD LETTER/Generate Main Menu")]
        public static void Generate()
        {
            Scene previousScene = SceneManager.GetActiveScene();
            bool preserveCurrentScene = previousScene.IsValid() && !string.IsNullOrEmpty(previousScene.path);
            Scene menuScene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                preserveCurrentScene ? NewSceneMode.Additive : NewSceneMode.Single);
            menuScene.name = "MainMenu";

            GameObject root = new GameObject("DEAD LETTER - Main Menu");
            SceneManager.MoveGameObjectToScene(root, menuScene);
            root.AddComponent<MainMenuController>();

            EditorSceneManager.SaveScene(menuScene, MenuScenePath);
            if (preserveCurrentScene)
            {
                EditorSceneManager.CloseScene(menuScene, true);
                SceneManager.SetActiveScene(previousScene);
            }

            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes
                .Where(scene => scene.path != MenuScenePath && scene.path != GameScenePath)
                .ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(GameScenePath, true));
            scenes.Insert(0, new EditorBuildSettingsScene(MenuScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            SceneAsset menuSceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScenePath);
            EditorSceneManager.playModeStartScene = menuSceneAsset;
            Selection.activeObject = menuSceneAsset;
            Debug.Log("DEAD LETTER main menu scene generated and added as the first build scene.");
        }
    }
}
