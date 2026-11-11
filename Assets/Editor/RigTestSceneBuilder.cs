using System;
using System.IO;
using System.Linq;
using DeadLetter.Debugging;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadLetter.Editor
{
    public static class RigTestSceneBuilder
    {
        private const string ModelPath = "Assets/Art/Models/RigTests/tripo_rig_test.fbx";
        internal const string ScenePath = "Assets/Scenes/RigTestScene.unity";
        private const string ControllerPath = "Assets/Art/Models/RigTests/RigTest.controller";
        internal const string ReportPath = "Assets/Art/Models/RigTests/RigTestReport.txt";

        [MenuItem("Tools/DEAD LETTER/Rig Test/Build and Open Test Scene")]
        public static void BuildAndOpenTestScene()
        {
            BuildTestScene(true);
        }

        internal static void BuildTestSceneInBackground()
        {
            BuildTestScene(false);
        }

        private static void BuildTestScene(bool keepOpen)
        {
            ConfigureImporter();

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
                throw new InvalidOperationException($"Rig test model was not imported: {ModelPath}");

            Scene previousScene = SceneManager.GetActiveScene();
            bool canUseAdditiveScene =
                !keepOpen && previousScene.IsValid() && !string.IsNullOrEmpty(previousScene.path);
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                canUseAdditiveScene ? NewSceneMode.Additive : NewSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            GameObject character = PrefabUtility.InstantiatePrefab(model) as GameObject;
            if (character == null)
                throw new InvalidOperationException("Could not instantiate the rig test FBX.");

            character.name = "Rig Test Character";
            NormalizeCharacterSize(character, 1.8f);
            character.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            SkinnedMeshRenderer[] skinnedMeshes = character.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Transform[] bones = skinnedMeshes
                .SelectMany(renderer => renderer.bones)
                .Where(bone => bone != null)
                .Distinct()
                .ToArray();
            Transform testBone = bones.FirstOrDefault(bone =>
                skinnedMeshes.All(renderer => bone != renderer.rootBone) && bone.childCount > 0)
                ?? bones.FirstOrDefault();

            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            Animator animator = character.GetComponent<Animator>() ?? character.AddComponent<Animator>();
            bool hasPlayableClip = clips.Length > 0;
            if (hasPlayableClip)
                animator.runtimeAnimatorController = CreateController(clips[0]);

            string summary =
                $"Skinned meshes: {skinnedMeshes.Length}  |  Bones: {bones.Length}  |  Animation clips: {clips.Length}\n" +
                (hasPlayableClip
                    ? $"Playing imported clip: {clips[0].name}"
                    : testBone != null
                        ? $"No clip found. Moving test bone: {testBone.name}"
                        : "No usable bone was found.");

            RigTestDriver driver = character.AddComponent<RigTestDriver>();
            driver.Configure(testBone, !hasPlayableClip && testBone != null, summary);

            CreateEnvironment(character);
            EditorSceneManager.SaveScene(scene, ScenePath);

            if (keepOpen)
            {
                Selection.activeGameObject = character;
                EditorGUIUtility.PingObject(character);
            }
            else if (canUseAdditiveScene)
            {
                if (previousScene.IsValid() && previousScene.isLoaded)
                    SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(scene, true);
            }

            WriteReport(skinnedMeshes.Length, bones.Length, clips);

            if (skinnedMeshes.Length == 0 || bones.Length == 0)
                Debug.LogError($"Rig test failed: {summary}");
            else
                Debug.Log($"Rig test scene saved. {summary}");
        }

        private static void WriteReport(int skinnedMeshCount, int boneCount, AnimationClip[] clips)
        {
            string clipNames = clips.Length == 0
                ? "None"
                : string.Join(", ", clips.Select(clip => clip.name));
            string report =
                $"Model: {ModelPath}{Environment.NewLine}" +
                $"Skinned meshes: {skinnedMeshCount}{Environment.NewLine}" +
                $"Bones: {boneCount}{Environment.NewLine}" +
                $"Animation clips: {clips.Length}{Environment.NewLine}" +
                $"Clip names: {clipNames}{Environment.NewLine}" +
                $"Texture/material import: intentionally disabled for this rig-only test{Environment.NewLine}" +
                $"Test scene: {ScenePath}{Environment.NewLine}";
            File.WriteAllText(ReportPath, report);
            AssetDatabase.ImportAsset(ReportPath, ImportAssetOptions.ForceUpdate);
        }

        [MenuItem("Tools/DEAD LETTER/Rig Test/Validate Imported Rig")]
        public static void ValidateImportedRig()
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
                throw new InvalidOperationException($"Missing model: {ModelPath}");

            SkinnedMeshRenderer[] renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            int boneCount = renderers.SelectMany(renderer => renderer.bones)
                .Where(bone => bone != null)
                .Distinct()
                .Count();
            int clipCount = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
                .OfType<AnimationClip>()
                .Count(clip => !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase));

            if (renderers.Length == 0 || boneCount == 0)
                throw new InvalidOperationException(
                    $"Rig validation failed. Skinned meshes: {renderers.Length}, bones: {boneCount}, clips: {clipCount}.");

            Debug.Log($"Rig validation passed. Skinned meshes: {renderers.Length}, bones: {boneCount}, clips: {clipCount}.");
        }

        private static void ConfigureImporter()
        {
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(ModelPath) is not ModelImporter importer)
                throw new InvalidOperationException($"ModelImporter not found for {ModelPath}");

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.importBlendShapes = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.preserveHierarchy = true;
            importer.SaveAndReimport();
        }

        private static RuntimeAnimatorController CreateController(AnimationClip clip)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
                AssetDatabase.DeleteAsset(ControllerPath);

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(ControllerPath, clip);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static void NormalizeCharacterSize(GameObject character, float targetHeight)
        {
            Renderer[] renderers = character.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return;

            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);

            if (bounds.size.y > 0.0001f)
                character.transform.localScale = Vector3.one * (targetHeight / bounds.size.y);

            bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);
            character.transform.position += new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
        }

        private static void CreateEnvironment(GameObject character)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Rig Test Floor";
            floor.transform.localScale = new Vector3(0.5f, 1f, 0.5f);

            GameObject keyLightObject = new GameObject("Rig Test Key Light");
            Light keyLight = keyLightObject.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.intensity = 1.25f;
            keyLightObject.transform.rotation = Quaternion.Euler(38f, -32f, 0f);

            GameObject fillLightObject = new GameObject("Rig Test Fill Light");
            Light fillLight = fillLightObject.AddComponent<Light>();
            fillLight.type = LightType.Point;
            fillLight.intensity = 5f;
            fillLight.range = 8f;
            fillLightObject.transform.position = new Vector3(-2f, 2.2f, -2f);

            GameObject cameraObject = new GameObject("Rig Test Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.04f, 0.05f);
            cameraObject.transform.position = new Vector3(0f, 1.05f, -3.2f);
            cameraObject.transform.LookAt(new Vector3(0f, 0.9f, 0f));
        }
    }

    [InitializeOnLoad]
    internal static class RigTestAutoRunner
    {
        static RigTestAutoRunner()
        {
            if (!Application.isBatchMode && !File.Exists(RigTestSceneBuilder.ReportPath))
                EditorApplication.delayCall += RunOnce;
        }

        private static void RunOnce()
        {
            EditorApplication.delayCall -= RunOnce;
            try
            {
                RigTestSceneBuilder.BuildTestSceneInBackground();
                RigTestSceneBuilder.ValidateImportedRig();
            }
            catch (Exception exception)
            {
                File.WriteAllText(
                    RigTestSceneBuilder.ReportPath,
                    $"Rig test failed.{Environment.NewLine}{exception}");
                AssetDatabase.ImportAsset(RigTestSceneBuilder.ReportPath, ImportAssetOptions.ForceUpdate);
                Debug.LogException(exception);
            }
        }
    }

    public static class RigTestBatchEntry
    {
        private const string PreviewPath = "Assets/Art/Models/RigTests/RigTestPreview.png";

        public static void Run()
        {
            try
            {
                RigTestSceneBuilder.BuildTestSceneInBackground();
                RigTestSceneBuilder.ValidateImportedRig();
                RenderPreview();
                AssetDatabase.SaveAssets();
                Debug.Log("RIG_TEST_BATCH_SUCCESS");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                File.WriteAllText(
                    RigTestSceneBuilder.ReportPath,
                    $"Rig test failed.{Environment.NewLine}{exception}");
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void RenderPreview()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/RigTestScene.unity", OpenSceneMode.Single);
            Camera camera = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                .FirstOrDefault();
            if (camera == null)
                throw new InvalidOperationException("Rig test camera was not found.");

            const int width = 960;
            const int height = 720;
            RenderTexture renderTexture = new RenderTexture(width, height, 24);
            Texture2D preview = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;

            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                preview.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                preview.Apply();
                File.WriteAllBytes(PreviewPath, preview.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(preview);
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }

            AssetDatabase.ImportAsset(PreviewPath, ImportAssetOptions.ForceUpdate);
        }
    }

    public static class RigTestPlayerBuildEntry
    {
        public static void Run()
        {
            try
            {
                RigTestSceneBuilder.BuildTestSceneInBackground();
                BuildPlayerOptions options = new BuildPlayerOptions
                {
                    scenes = new[] { RigTestSceneBuilder.ScenePath },
                    locationPathName = "RigTestBuild/DeadLetterRigTest.exe",
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException($"Rig test player build failed: {report.summary.result}");

                Debug.Log($"RIG_TEST_PLAYER_BUILD_SUCCESS Size={report.summary.totalSize}");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }
    }
}
