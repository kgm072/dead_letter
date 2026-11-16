using System;
using System.Collections.Generic;
using DeadLetter.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadLetter.Editor
{
    public static class InteractionAlignmentValidator
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const float BoundsTolerance = 0.06f;

        [MenuItem("Tools/DEAD LETTER/Validate Interaction Alignment")]
        public static void ValidateCurrentScene()
        {
            try
            {
                ValidateCurrentSceneOrThrow();
                Debug.Log("INTERACTION_ALIGNMENT_VALIDATION_SUCCESS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        public static void ValidateBatch()
        {
            try
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                ValidateCurrentSceneOrThrow();
                Debug.Log("INTERACTION_ALIGNMENT_VALIDATION_SUCCESS");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void ValidateCurrentSceneOrThrow()
        {
            List<string> failures = new List<string>();
            ArtColliderFollower[] followers = UnityEngine.Object.FindObjectsByType<ArtColliderFollower>(
                FindObjectsInactive.Include);

            if (followers.Length == 0)
                failures.Add("No ArtColliderFollower bindings were found.");

            foreach (ArtColliderFollower follower in followers)
            {
                Renderer target = follower.TargetRenderer;
                BoxCollider box = follower.BoundCollider;
                // The U-shaped storage shelf is deliberately not fitted to one renderer:
                // doing so fills the whole room with a single collider. Handoff keeps a
                // narrow disabled proxy plus the three legacy shelf-row colliders instead.
                if (follower.name == "Collider - 칭고" && !follower.enabled && target == null && box != null)
                    continue;
                if (target == null || box == null)
                {
                    failures.Add($"{follower.name} has an incomplete renderer/collider binding.");
                    continue;
                }

                follower.SyncNow();
                Bounds targetBounds = target.bounds;
                Bounds colliderBounds = box.bounds;
                if (Vector3.Distance(targetBounds.center, colliderBounds.center) > BoundsTolerance)
                    failures.Add($"{follower.name} center does not follow {target.name}.");

                Vector3 sizeDifference = targetBounds.size - colliderBounds.size;
                if (Mathf.Abs(sizeDifference.x) > BoundsTolerance ||
                    Mathf.Abs(sizeDifference.y) > BoundsTolerance ||
                    Mathf.Abs(sizeDifference.z) > BoundsTolerance)
                {
                    failures.Add($"{follower.name} bounds do not match {target.name}.");
                }

                ArtInteractionMarker marker = follower.GetComponent<ArtInteractionMarker>();
                bool requiresDirectInteraction = marker != null &&
                    (marker.Role == ArtInteractionRole.LetterWorkstation || marker.Role == ArtInteractionRole.DaySofa);
                if (requiresDirectInteraction &&
                    follower.GetComponent<IInteractable>() == null)
                {
                    failures.Add($"{follower.name} has interaction role {marker.Role} but no IInteractable component.");
                }
            }

            FirstPersonInteractor interactor = UnityEngine.Object.FindAnyObjectByType<FirstPersonInteractor>(FindObjectsInactive.Include);
            if (interactor == null)
                failures.Add("FirstPersonInteractor is missing.");
            else if (Mathf.Abs(interactor.InteractionDistance - 3f) > 0.001f)
                failures.Add($"Interaction distance changed from 3m to {interactor.InteractionDistance:F2}m.");

            ValidateMovedRenderer(failures);

            if (failures.Count > 0)
                throw new InvalidOperationException(string.Join("\n", failures));
        }

        private static void ValidateMovedRenderer(List<string> failures)
        {
            GameObject root = new GameObject("Interaction Alignment Movement Test");
            try
            {
                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.name = "Moved Visual";
                visual.transform.SetParent(root.transform, false);
                UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());

                GameObject proxy = new GameObject("Moved Interaction Proxy");
                proxy.transform.SetParent(root.transform, false);
                BoxCollider box = proxy.AddComponent<BoxCollider>();
                ArtColliderFollower follower = proxy.AddComponent<ArtColliderFollower>();
                follower.Configure(visual.GetComponent<Renderer>());

                Vector3 oldCenter = box.bounds.center;
                visual.transform.localPosition = new Vector3(4f, 0.5f, -2f);
                visual.transform.localScale = new Vector3(1.4f, 0.8f, 1.2f);
                follower.SyncNow();
                Physics.SyncTransforms();

                Bounds targetBounds = visual.GetComponent<Renderer>().bounds;
                Bounds colliderBounds = box.bounds;
                if (Vector3.Distance(targetBounds.center, colliderBounds.center) > BoundsTolerance)
                    failures.Add("A moved renderer did not move its interaction collider to the new position.");
                if (colliderBounds.Contains(oldCenter))
                    failures.Add("A moved interaction collider still occupies its previous position.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
