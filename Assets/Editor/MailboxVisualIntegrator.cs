using System;
using System.Linq;
using DeadLetter.Mail;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadLetter.Editor
{
    public static class MailboxVisualIntegrator
    {
        public const string ModelPath = "Assets/Art/Models/Mailbox/Mailbox.fbx";
        public const string VisualName = "MailboxVisual";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const float TargetHeight = 2f;

        [MenuItem("Tools/DEAD LETTER/Art/Apply Mailbox Visual")]
        public static void ApplyFromMenu()
        {
            MailDropBox dropBox = UnityEngine.Object.FindAnyObjectByType<MailDropBox>(FindObjectsInactive.Include);
            if (dropBox == null)
                throw new InvalidOperationException("No MailDropBox exists in the open scene.");

            ApplyTo(dropBox.gameObject);
            EditorSceneManager.MarkSceneDirty(dropBox.gameObject.scene);
            Transform visual = FindExistingVisual(dropBox.transform);
            Selection.activeGameObject = visual != null ? visual.gameObject : dropBox.gameObject;
            Debug.Log("MAILBOX_VISUAL_APPLIED");
        }

        public static void ApplyBatch()
        {
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            MailDropBox[] dropBoxes = UnityEngine.Object.FindObjectsByType<MailDropBox>(FindObjectsInactive.Include);
            if (dropBoxes.Length != 1)
                throw new InvalidOperationException($"Expected exactly one MailDropBox, found {dropBoxes.Length}.");

            ApplyTo(dropBoxes[0].gameObject);
            Validate(dropBoxes[0].gameObject);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("MAILBOX_VISUAL_INTEGRATION_SUCCESS");
        }

        [MenuItem("Tools/DEAD LETTER/Art/Focus Mailbox %#m")]
        public static void FocusMailbox()
        {
            MailDropBox dropBox = UnityEngine.Object.FindAnyObjectByType<MailDropBox>(FindObjectsInactive.Include);
            if (dropBox == null)
                throw new InvalidOperationException("No MailDropBox exists in the open scene.");

            Selection.activeGameObject = dropBox.gameObject;
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
                throw new InvalidOperationException("No Scene View is available.");
            sceneView.FrameSelected();
            sceneView.Repaint();
        }

        public static void ApplyTo(GameObject dropBoxRoot, Material bodyMaterial = null)
        {
            if (dropBoxRoot == null)
                throw new ArgumentNullException(nameof(dropBoxRoot));

            MailDropBox logic = dropBoxRoot.GetComponent<MailDropBox>();
            BoxCollider interactionCollider = dropBoxRoot.GetComponent<BoxCollider>();
            if (logic == null || interactionCollider == null)
                throw new InvalidOperationException($"{dropBoxRoot.name} must retain MailDropBox and BoxCollider components.");

            Transform existing = FindExistingVisual(dropBoxRoot.transform);
            Renderer[] legacyRenderers = dropBoxRoot.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => existing == null || (renderer.transform != existing && !renderer.transform.IsChildOf(existing)))
                .ToArray();
            bodyMaterial ??= legacyRenderers.SelectMany(renderer => renderer.sharedMaterials).FirstOrDefault(material => material != null);
            foreach (Renderer renderer in legacyRenderers)
                renderer.enabled = false;

            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing.gameObject);

            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (asset == null)
                throw new InvalidOperationException($"Mailbox FBX is missing: {ModelPath}");

            GameObject visual = PrefabUtility.InstantiatePrefab(asset, dropBoxRoot.transform) as GameObject;
            if (visual == null)
                throw new InvalidOperationException("Could not instantiate the mailbox FBX.");

            visual.name = VisualName;
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            Vector3 parentScale = dropBoxRoot.transform.lossyScale;
            visual.transform.localScale = new Vector3(
                SafeInverse(parentScale.x),
                SafeInverse(parentScale.y),
                SafeInverse(parentScale.z));

            Renderer[] visualRenderers = visual.GetComponentsInChildren<Renderer>(true);
            if (visualRenderers.Length == 0)
                throw new InvalidOperationException("Mailbox FBX contains no Renderer.");

            Bounds visualBounds = CalculateWorldBounds(visualRenderers);
            float scaleFactor = TargetHeight / Mathf.Max(visualBounds.size.y, 0.0001f);
            visual.transform.localScale *= scaleFactor;

            visualBounds = CalculateWorldBounds(visualRenderers);
            Vector3 targetBottomCenter = new Vector3(dropBoxRoot.transform.position.x, 0f, dropBoxRoot.transform.position.z);
            Vector3 currentBottomCenter = new Vector3(visualBounds.center.x, visualBounds.min.y, visualBounds.center.z);
            visual.transform.position += targetBottomCenter - currentBottomCenter;

            if (bodyMaterial != null)
            {
                foreach (Renderer renderer in visualRenderers)
                {
                    Material[] slots = renderer.sharedMaterials;
                    for (int index = 0; index < slots.Length; index++)
                        slots[index] = bodyMaterial;
                    renderer.sharedMaterials = slots;
                    renderer.enabled = true;
                }
            }
            else
            {
                Debug.LogWarning("Mailbox FBX has no usable texture/material; its imported material is being retained.");
            }

            FitColliderToRenderers(interactionCollider, visualRenderers);
            EditorUtility.SetDirty(dropBoxRoot);
            EditorUtility.SetDirty(interactionCollider);
            EditorUtility.SetDirty(visual);
        }

        private static Transform FindExistingVisual(Transform root)
        {
            if (root == null)
                return null;
            return root.Cast<Transform>().FirstOrDefault(child => child.name == VisualName);
        }

        private static void FitColliderToRenderers(BoxCollider collider, Renderer[] renderers)
        {
            Transform root = collider.transform;
            Bounds worldBounds = CalculateWorldBounds(renderers);
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            Vector3[] corners =
            {
                new(min.x, min.y, min.z), new(min.x, min.y, max.z),
                new(min.x, max.y, min.z), new(min.x, max.y, max.z),
                new(max.x, min.y, min.z), new(max.x, min.y, max.z),
                new(max.x, max.y, min.z), new(max.x, max.y, max.z)
            };

            Vector3 first = root.InverseTransformPoint(corners[0]);
            Bounds localBounds = new(first, Vector3.zero);
            foreach (Vector3 corner in corners.Skip(1))
                localBounds.Encapsulate(root.InverseTransformPoint(corner));

            collider.center = localBounds.center;
            collider.size = localBounds.size + new Vector3(0.04f, 0.02f, 0.04f);
            collider.enabled = true;
            collider.isTrigger = false;
        }

        private static Bounds CalculateWorldBounds(Renderer[] renderers)
        {
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);
            return bounds;
        }

        private static float SafeInverse(float value)
        {
            return Mathf.Abs(value) > 0.0001f ? 1f / Mathf.Abs(value) : 1f;
        }

        private static void Validate(GameObject root)
        {
            MailDropBox logic = root.GetComponent<MailDropBox>();
            BoxCollider collider = root.GetComponent<BoxCollider>();
            Transform visual = FindExistingVisual(root.transform);
            if (logic == null || collider == null || visual == null)
                throw new InvalidOperationException("Mailbox logic, collider, or visual is missing after integration.");

            Renderer[] visualRenderers = visual.GetComponentsInChildren<Renderer>(true);
            Renderer[] legacyRenderers = root.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => !renderer.transform.IsChildOf(visual))
                .ToArray();
            if (visualRenderers.Length == 0 || visualRenderers.Any(renderer => !renderer.enabled))
                throw new InvalidOperationException("The new mailbox visual is not visible.");
            if (legacyRenderers.Any(renderer => renderer.enabled))
                throw new InvalidOperationException("A legacy mailbox renderer remains enabled.");

            Bounds bounds = CalculateWorldBounds(visualRenderers);
            if (Mathf.Abs(bounds.min.y) > 0.025f)
                throw new InvalidOperationException($"Mailbox bottom is not on the floor: {bounds.min.y:F4}");
            if (Mathf.Abs(bounds.size.y - TargetHeight) > 0.025f)
                throw new InvalidOperationException($"Mailbox height is unexpected: {bounds.size.y:F4}");

            Debug.Log($"MAILBOX_VISUAL_BOUNDS center={bounds.center:F3} size={bounds.size:F3} bottom={bounds.min.y:F3}");
            Debug.Log($"MAILBOX_COLLIDER center={collider.center:F3} size={collider.size:F3}");
            Debug.Log($"MAILBOX_LOGIC component={logic.GetType().FullName} enabled={logic.enabled}");
        }
    }
}
