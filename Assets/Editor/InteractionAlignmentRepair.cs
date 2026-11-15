using System;
using System.Linq;
using DeadLetter.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadLetter.Editor
{
    public static class InteractionAlignmentRepair
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string PrefabFolder = "Assets/Art/Prefabs";
        private const string ColliderPrefix = "Collider - ";

        [MenuItem("Tools/DEAD LETTER/Art/Repair Interaction Collider Alignment")]
        public static void Repair()
        {
            int prefabBindings = RepairPrefabBindings();
            int sceneBindings = RepairSceneBindings();
            Debug.Log($"Interaction collider alignment repaired. Prefab bindings={prefabBindings}, scene bindings={sceneBindings}.");
        }

        public static void RepairBatch()
        {
            try
            {
                RepairPrefabBindings();
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                RepairSceneBindings();
                InteractionAlignmentValidator.ValidateCurrentSceneOrThrow();
                Debug.Log("INTERACTION_ALIGNMENT_REPAIR_SUCCESS");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static int RepairPrefabBindings()
        {
            int bindingCount = 0;
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder });
            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                    BoxCollider[] colliders = root.GetComponentsInChildren<BoxCollider>(true);
                    bool changed = false;

                    foreach (BoxCollider box in colliders)
                    {
                        if (!box.name.StartsWith(ColliderPrefix, StringComparison.Ordinal))
                            continue;

                        string rendererName = box.name.Substring(ColliderPrefix.Length);
                        Renderer target = renderers.FirstOrDefault(renderer => renderer.gameObject.name == rendererName);
                        if (target == null)
                        {
                            Debug.LogWarning($"No renderer named '{rendererName}' was found for {path}/{box.name}.");
                            continue;
                        }

                        ArtColliderFollower follower = box.GetComponent<ArtColliderFollower>();
                        if (follower == null)
                        {
                            follower = box.gameObject.AddComponent<ArtColliderFollower>();
                            changed = true;
                        }

                        if (follower.TargetRenderer != target)
                            changed = true;
                        follower.Configure(target);
                        EditorUtility.SetDirty(follower);
                        EditorUtility.SetDirty(box);
                        bindingCount++;
                    }

                    if (changed || colliders.Any(box => box.GetComponent<ArtColliderFollower>() != null))
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            return bindingCount;
        }

        private static int RepairSceneBindings()
        {
            Scene scene = SceneManager.GetActiveScene();
            int count = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (ArtColliderFollower follower in root.GetComponentsInChildren<ArtColliderFollower>(true))
                {
                    if (follower.SyncNow())
                    {
                        EditorUtility.SetDirty(follower.transform);
                        EditorUtility.SetDirty(follower.BoundCollider);
                        count++;
                    }
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return count;
        }
    }
}
