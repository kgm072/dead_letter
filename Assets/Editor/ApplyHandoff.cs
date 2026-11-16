using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DeadLetter.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadLetter.Editor
{
    /// <summary>
    /// Applies the measured build-to-build scene delta from Docs/handoff without guessing
    /// renamed or missing hierarchy paths. This is also called after graybox regeneration.
    /// </summary>
    public static class HandoffSceneApplier
    {
        public const string DataPath = "Docs/handoff/scene_changes.json";
        private const string ReportPath = "Library/DeadLetterHandoffApplyReport.txt";
        private const string DoorTexturePath = "Assets/Art/Textures/FinalMap/문_Door.png";
        private const string DoorMaterialPath = "Assets/Art/Materials/FinalMap/FinalMap_Door_URP.mat";

        [MenuItem("Tools/DEAD LETTER/Art/Apply Handoff Scene Changes")]
        public static void ApplyFromMenu()
        {
            ApplyReport report = ApplyToLoadedScene(true);
            EditorUtility.DisplayDialog(
                "DEAD LETTER",
                $"Handoff applied.\nApplied: {report.Applied}\nMissing: {report.Missing.Count}\nReport: {ReportPath}",
                "OK");
        }

        [MenuItem("Tools/DEAD LETTER/Art/Analyze Handoff Paths")]
        public static void AnalyzePathsFromMenu()
        {
            ApplyReport report = AnalyzeLoadedScene();
            EditorUtility.DisplayDialog(
                "DEAD LETTER",
                $"Handoff path analysis complete.\nFound: {report.Applied}\nMissing: {report.Missing.Count}\nReport: {ReportPath}",
                "OK");
        }

        public static ApplyReport AnalyzeLoadedScene()
        {
            HandoffData data = LoadData();
            Dictionary<string, Transform> paths = BuildPathMap();
            var report = new ApplyReport("ANALYZE", SceneManager.GetActiveScene().path);
            AnalyzeEntries(data.transforms, value => value.path, paths, report, "transform");
            AnalyzeEntries(data.active, value => value.path, paths, report, "active");
            AnalyzeEntries(data.renderers, value => value.path, paths, report, "renderer");
            AnalyzeEntries(data.boxColliders.Where(IsStorageShelfCollider), value => value.path, paths, report, "boxCollider");
            AnalyzeEntries(data.scripts, value => value.path, paths, report, "script");
            AnalyzeEntries(data.materialSlots, value => value.path, paths, report, "materialSlot");
            WriteReport(report);
            return report;
        }

        public static ApplyReport ApplyToLoadedScene(bool saveScene)
        {
            HandoffData data = LoadData();
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
                throw new InvalidOperationException("No active scene is available for handoff application.");

            Dictionary<string, Transform> originalPaths = BuildPathMap();
            var report = new ApplyReport("APPLY", scene.path);

            foreach (TransformChange change in data.transforms ?? Array.Empty<TransformChange>())
            {
                if (!TryResolve(originalPaths, change.path, report, "transform", out Transform target))
                    continue;
                if (!string.IsNullOrEmpty(change.newParentPath))
                {
                    if (!TryResolve(originalPaths, change.newParentPath, report, "parent", out Transform newParent))
                        continue;
                    Transform previousChild = newParent.Find(target.name);
                    if (previousChild != null && previousChild != target)
                        UnityEngine.Object.DestroyImmediate(previousChild.gameObject);
                    target.SetParent(newParent, false);
                }
                target.localPosition = ToVector3(change.localPosition, target.localPosition);
                target.localRotation = ToQuaternion(change.localRotationQuat_xyzw, target.localRotation);
                target.localScale = ToVector3(change.localScale, target.localScale);
                EditorUtility.SetDirty(target);
                report.Applied++;
            }

            // Rebuild after reparenting while retaining original-path aliases.
            Dictionary<string, Transform> currentPaths = BuildPathMap();
            foreach (var pair in originalPaths)
                currentPaths.TryAdd(pair.Key, pair.Value);

            foreach (ActiveChange change in data.active ?? Array.Empty<ActiveChange>())
            {
                if (!TryResolve(currentPaths, change.path, report, "active", out Transform target))
                    continue;
                target.gameObject.SetActive(change.activeSelf);
                EditorUtility.SetDirty(target.gameObject);
                report.Applied++;
            }

            foreach (RendererChange change in data.renderers ?? Array.Empty<RendererChange>())
            {
                if (!TryResolve(currentPaths, change.path, report, "renderer", out Transform target))
                    continue;
                MeshRenderer renderer = target.GetComponent<MeshRenderer>();
                if (renderer == null)
                {
                    report.Missing.Add($"renderer component: {change.path}");
                    continue;
                }
                renderer.enabled = change.meshRendererEnabled;
                EditorUtility.SetDirty(renderer);
                report.Applied++;
            }

            foreach (BoxColliderChange change in (data.boxColliders ?? Array.Empty<BoxColliderChange>()).Where(IsStorageShelfCollider))
            {
                if (!TryResolve(currentPaths, change.path, report, "boxCollider", out Transform target))
                    continue;
                BoxCollider collider = target.GetComponent<BoxCollider>();
                if (collider == null)
                {
                    report.Missing.Add($"boxCollider component: {change.path}");
                    continue;
                }
                collider.center = ToVector3(change.center, collider.center);
                collider.size = ToVector3(change.size, collider.size);
                EditorUtility.SetDirty(collider);
                report.Applied++;
            }

            foreach (ScriptChange change in data.scripts ?? Array.Empty<ScriptChange>())
            {
                if (!TryResolve(currentPaths, change.path, report, "script", out Transform target))
                    continue;
                if (!string.Equals(change.component, nameof(ArtColliderFollower), StringComparison.Ordinal))
                {
                    report.Missing.Add($"unsupported script {change.component}: {change.path}");
                    continue;
                }
                ArtColliderFollower follower = target.GetComponent<ArtColliderFollower>();
                if (follower == null)
                {
                    report.Missing.Add($"script component {change.component}: {change.path}");
                    continue;
                }
                follower.enabled = change.enabled;
                var serialized = new SerializedObject(follower);
                serialized.FindProperty("targetRenderer").objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(follower);
                report.Applied++;
            }

            ConfigureMaterials(currentPaths, data.materialSlots ?? Array.Empty<MaterialSlotChange>(), report);
            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);
            if (saveScene && !EditorSceneManager.SaveScene(scene))
                throw new IOException($"Could not save handoff scene: {scene.path}");
            AssetDatabase.SaveAssets();
            WriteReport(report);
            Debug.Log($"[HandoffApply] applied={report.Applied} missing={report.Missing.Count} scene={scene.path}");
            foreach (string missing in report.Missing)
                Debug.LogWarning("[HandoffMissing] " + missing);
            return report;
        }

        private static void ConfigureMaterials(
            IReadOnlyDictionary<string, Transform> paths,
            IReadOnlyCollection<MaterialSlotChange> materialSlots,
            ApplyReport report)
        {
            var mappings = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Assets/Art/Materials/FinalMap/FinalMap_Corridor_URP.mat"] = "Assets/Art/Textures/FinalMap/복도_Room.png",
                ["Assets/Art/Materials/FinalMap/FinalMap_HiddenRoom_URP.mat"] = "Assets/Art/Textures/FinalMap/비밀의방_Room.png",
                ["Assets/Art/Materials/FinalMap/FinalMap_Workshop_URP.mat"] = "Assets/Art/Textures/FinalMap/작업실_Room.png",
                ["Assets/Art/Materials/FinalMap/FinalMap_Storage_URP.mat"] = "Assets/Art/Textures/FinalMap/창고_Room.png",
                ["Assets/Art/Materials/FinalMap/FinalMap_Counter_URP.mat"] = "Assets/Art/Textures/FinalMap/창구_Room.png",
                ["Assets/Art/Materials/FinalMap/FinalMap_Pantry_URP.mat"] = "Assets/Art/Textures/FinalMap/탕비실_Room.png",
                ["Assets/Art/Materials/작업실_URP.mat"] = "Assets/Art/Textures/작업실_BaseColor.png",
                ["Assets/Art/Materials/창고_URP.mat"] = "Assets/Art/Textures/창고_BaseColor.png",
                ["Assets/Art/Materials/창구_URP.mat"] = "Assets/Art/Textures/창구_BaseColor.png",
                ["Assets/Art/Materials/탕비실_URP.mat"] = "Assets/Art/Textures/탕비실_BaseColor.png",
                ["Assets/Art/Materials/히든방_URP.mat"] = "Assets/Art/Textures/히든방_BaseColor.png"
            };

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException("URP/Lit shader was not found.");
            foreach (var pair in mappings)
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(pair.Key);
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(pair.Value);
                if (material == null || texture == null)
                {
                    report.Missing.Add($"material mapping: {pair.Key} <- {pair.Value}");
                    continue;
                }
                material.shader = shader;
                material.SetTexture("_BaseMap", texture);
                material.SetTexture("_MainTex", texture);
                material.SetColor("_BaseColor", Color.white);
                material.SetColor("_Color", Color.white);
                EditorUtility.SetDirty(material);
                report.Applied++;
            }

            Texture2D doorTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(DoorTexturePath);
            if (doorTexture == null)
            {
                report.Missing.Add("door texture: " + DoorTexturePath);
                return;
            }
            Material doorMaterial = AssetDatabase.LoadAssetAtPath<Material>(DoorMaterialPath);
            if (doorMaterial == null)
            {
                doorMaterial = new Material(shader) { name = "FinalMap_Door_URP" };
                AssetDatabase.CreateAsset(doorMaterial, DoorMaterialPath);
            }
            doorMaterial.shader = shader;
            doorMaterial.SetTexture("_BaseMap", doorTexture);
            doorMaterial.SetTexture("_MainTex", doorTexture);
            doorMaterial.SetColor("_BaseColor", Color.white);
            doorMaterial.SetColor("_Color", Color.white);
            EditorUtility.SetDirty(doorMaterial);

            foreach (MaterialSlotChange slot in materialSlots)
            {
                if (!TryResolve(paths, slot.path, report, "materialSlot", out Transform target))
                    continue;
                MeshRenderer renderer = target.GetComponent<MeshRenderer>();
                if (renderer == null)
                {
                    report.Missing.Add($"door renderer: {slot.path}");
                    continue;
                }
                int count = Mathf.Max(2, renderer.sharedMaterials.Length);
                renderer.sharedMaterials = Enumerable.Repeat(doorMaterial, count).ToArray();
                EditorUtility.SetDirty(renderer);
                report.Applied++;
            }
        }

        private static HandoffData LoadData()
        {
            string fullPath = Path.GetFullPath(DataPath);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException("Handoff scene data was not found.", fullPath);
            HandoffData data = JsonUtility.FromJson<HandoffData>(File.ReadAllText(fullPath, Encoding.UTF8));
            if (data == null)
                throw new InvalidDataException("Handoff scene data could not be parsed.");
            return data;
        }

        private static Dictionary<string, Transform> BuildPathMap()
        {
            var result = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                AddPaths(root.transform, root.name, result);
            return result;
        }

        private static void AddPaths(Transform target, string path, IDictionary<string, Transform> result)
        {
            result[path] = target;
            foreach (Transform child in target)
                AddPaths(child, path + "/" + child.name, result);
        }

        private static bool TryResolve(
            IReadOnlyDictionary<string, Transform> paths,
            string path,
            ApplyReport report,
            string kind,
            out Transform target)
        {
            if (!string.IsNullOrEmpty(path) && paths.TryGetValue(path, out target) && target != null)
                return true;
            report.Missing.Add($"{kind}: {path}");
            target = null;
            return false;
        }

        private static void AnalyzeEntries<T>(
            IEnumerable<T> entries,
            Func<T, string> pathSelector,
            IReadOnlyDictionary<string, Transform> paths,
            ApplyReport report,
            string kind)
        {
            foreach (T entry in entries ?? Enumerable.Empty<T>())
            {
                string path = pathSelector(entry);
                if (paths.ContainsKey(path))
                    report.Applied++;
                else
                    report.Missing.Add($"{kind}: {path}");
            }
        }

        private static bool IsStorageShelfCollider(BoxColliderChange change) =>
            change != null && change.path != null && change.path.EndsWith("Collider - 칭고", StringComparison.Ordinal);

        private static Vector3 ToVector3(float[] values, Vector3 fallback) =>
            values != null && values.Length == 3 ? new Vector3(values[0], values[1], values[2]) : fallback;

        private static Quaternion ToQuaternion(float[] values, Quaternion fallback) =>
            values != null && values.Length == 4 ? new Quaternion(values[0], values[1], values[2], values[3]) : fallback;

        private static void WriteReport(ApplyReport report)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath) ?? "Library");
            var builder = new StringBuilder();
            builder.AppendLine($"DEAD LETTER HANDOFF {report.Mode} REPORT");
            builder.AppendLine($"Scene: {report.ScenePath}");
            builder.AppendLine($"Applied/Found: {report.Applied}");
            builder.AppendLine($"Missing: {report.Missing.Count}");
            foreach (string missing in report.Missing.Distinct(StringComparer.Ordinal))
                builder.AppendLine("- " + missing);
            File.WriteAllText(ReportPath, builder.ToString(), Encoding.UTF8);
        }

        public sealed class ApplyReport
        {
            public ApplyReport(string mode, string scenePath)
            {
                Mode = mode;
                ScenePath = scenePath;
            }
            public string Mode { get; }
            public string ScenePath { get; }
            public int Applied { get; set; }
            public List<string> Missing { get; } = new();
        }

        [Serializable] private sealed class HandoffData
        {
            public TransformChange[] transforms;
            public ActiveChange[] active;
            public RendererChange[] renderers;
            public BoxColliderChange[] boxColliders;
            public ScriptChange[] scripts;
            public MaterialSlotChange[] materialSlots;
        }
        [Serializable] private sealed class TransformChange
        {
            public string path;
            public float[] localPosition;
            public float[] localRotationQuat_xyzw;
            public float[] localScale;
            public string newParentPath;
        }
        [Serializable] private sealed class ActiveChange { public string path; public bool activeSelf; }
        [Serializable] private sealed class RendererChange { public string path; public bool meshRendererEnabled; }
        [Serializable] private sealed class BoxColliderChange { public string path; public float[] size; public float[] center; }
        [Serializable] private sealed class ScriptChange { public string path; public string component; public bool enabled; }
        [Serializable] private sealed class MaterialSlotChange { public string path; public string note; }
    }
}
