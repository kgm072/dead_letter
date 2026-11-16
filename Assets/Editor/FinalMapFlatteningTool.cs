using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeadLetter.Interaction;
using DeadLetter.Mail;

namespace DeadLetter.Editor
{
    /// <summary>
    /// Inspects the final-map candidate without modifying the imported FBX.
    /// Structural corrections are intentionally kept as scene-owned objects.
    /// </summary>
    public static class FinalMapFlatteningTool
    {
        public const string TestScenePath = "Assets/Scenes/SampleScene_FinalMap_Flatten_Test.unity";
        public const string MainScenePath = "Assets/Scenes/SampleScene.unity";
        public const string CorrectionRootName = "Final Map Structural Corrections";
        private const string DerivedMeshFolder = "Assets/Art/Models/FinalMapCorrections";
        private const float FloorBottom = 0f;
        private const float FloorTop = 0.1993f;
        private const float CeilingBottom = 2.8899f;
        private const float CeilingTop = 3.0692f;
        private static readonly string[] StructuralMeshNames =
        {
            "복도", "비밀의방", "작업실", "창고", "창구", "탕비실"
        };

        [MenuItem("Tools/DEAD LETTER/Art/Flattening/Analyze Test Scene Levels")]
        public static void AnalyzeTestSceneFromMenu()
        {
            AnalyzeScene(TestScenePath);
            EditorUtility.DisplayDialog("DEAD LETTER", "Final-map level analysis was written to the Console.", "OK");
        }

        public static void AnalyzeBatch()
        {
            try
            {
                AnalyzeScene(TestScenePath);
                Debug.Log("FINAL_MAP_FLATTEN_ANALYSIS_SUCCESS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void AnalyzeMainBatch()
        {
            try
            {
                AnalyzeScene(MainScenePath);
                Debug.Log("FINAL_MAP_MAIN_FLATTEN_ANALYSIS_SUCCESS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Tools/DEAD LETTER/Art/Flattening/Apply To Test Scene")]
        public static void ApplyToTestSceneFromMenu()
        {
            ApplyToScene(TestScenePath);
            ValidateScene(TestScenePath);
            EditorUtility.DisplayDialog("DEAD LETTER", "Test-scene floors and ceilings were normalized and validated.", "OK");
        }

        [MenuItem("Tools/DEAD LETTER/Art/Flattening/Validate Test Scene")]
        public static void ValidateTestSceneFromMenu()
        {
            ValidateScene(TestScenePath);
            EditorUtility.DisplayDialog("DEAD LETTER", "Test-scene flattening validation passed. See Console for details.", "OK");
        }

        [MenuItem("Tools/DEAD LETTER/Art/Flattening/Promote Validated Test Scene To SampleScene")]
        public static void PromoteValidatedTestSceneFromMenu()
        {
            ValidateScene(TestScenePath);
            Scene scene = SceneManager.GetActiveScene();
            if (!EditorSceneManager.SaveScene(scene, MainScenePath, true))
                throw new InvalidOperationException("Could not save the validated test scene as SampleScene.");
            EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            ValidateLoadedScene(MainScenePath);
            EditorUtility.DisplayDialog("DEAD LETTER", "Validated final map was applied to SampleScene.", "OK");
        }

        public static void ApplyAndValidateBatch()
        {
            try
            {
                ApplyToScene(TestScenePath);
                ValidateScene(TestScenePath);
                Debug.Log("FINAL_MAP_FLATTEN_APPLY_VALIDATE_SUCCESS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void ApplyMainAndValidateBatch()
        {
            try
            {
                ApplyToScene(MainScenePath);
                ValidateScene(MainScenePath);
                Debug.Log("FINAL_MAP_MAIN_FLATTEN_APPLY_VALIDATE_SUCCESS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void ApplyToScene(string scenePath)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            ApplyToLoadedScene(scene);
        }

        public static void ApplyToLoadedScene(Scene scene)
        {
            GameObject artRoot = GameObject.Find("Final Map Visuals (Visual Only)");
            GameObject graybox = GameObject.Find("DEAD LETTER - Graybox");
            if (artRoot == null || graybox == null)
                throw new InvalidOperationException("Final-map visual or graybox root is missing.");

            EnsureAssetFolder(DerivedMeshFolder);
            Transform previous = graybox.transform.Find(CorrectionRootName);
            if (previous != null)
                UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var correctionRoot = new GameObject(CorrectionRootName);
            correctionRoot.transform.SetParent(graybox.transform, false);
            CreateReferenceMarker(correctionRoot.transform, $"Floor Standard Y={FloorTop:F4}m");
            CreateReferenceMarker(correctionRoot.transform, $"Ceiling Standard Y={CeilingBottom:F4}m");

            foreach (string meshName in StructuralMeshNames)
            {
                Transform structure = artRoot.transform.Find(meshName);
                if (structure == null)
                    throw new InvalidOperationException($"Structural mesh is missing: {meshName}");
                MeshFilter filter = structure.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                    throw new InvalidOperationException($"Structural mesh filter is missing: {meshName}");

                Mesh normalized = BuildNormalizedMesh(filter, meshName);
                filter.sharedMesh = normalized;
                Debug.Log($"[FlattenApply] {meshName} mesh={AssetDatabase.GetAssetPath(normalized)} " +
                          $"floor={FloorTop:F4} ceiling={CeilingBottom:F4}");
            }

            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException($"Could not save scene: {scene.path}");
            AssetDatabase.SaveAssets();
        }

        private static Mesh BuildNormalizedMesh(MeshFilter filter, string meshName)
        {
            Mesh source = filter.sharedMesh;
            Mesh derived = UnityEngine.Object.Instantiate(source);
            derived.name = $"{meshName}_Flattened";
            Vector3[] vertices = derived.vertices;
            int changed = 0;
            for (int index = 0; index < vertices.Length; index++)
            {
                Vector3 world = filter.transform.TransformPoint(vertices[index]);
                float normalizedY = NormalizeStructuralHeight(world.y);
                if (Mathf.Abs(normalizedY - world.y) <= 0.0001f)
                    continue;
                world.y = normalizedY;
                vertices[index] = filter.transform.InverseTransformPoint(world);
                changed++;
            }

            derived.vertices = vertices;
            derived.RecalculateBounds();
            derived.RecalculateNormals();
            if (derived.tangents != null && derived.tangents.Length == derived.vertexCount)
                derived.RecalculateTangents();

            string safeName = meshName.Replace('/', '_').Replace('\\', '_');
            string assetPath = $"{DerivedMeshFolder}/{safeName}_Flattened.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(derived, assetPath);
                existing = derived;
            }
            else
            {
                EditorUtility.CopySerialized(derived, existing);
                UnityEngine.Object.DestroyImmediate(derived);
                EditorUtility.SetDirty(existing);
            }
            Debug.Log($"[FlattenDerivedMesh] {meshName} changedVertices={changed}/{vertices.Length} source={source.name}");
            return existing;
        }

        private static float NormalizeStructuralHeight(float worldY)
        {
            if (worldY > -0.08f && worldY < 0.08f)
                return FloorBottom;
            if (worldY > 0.14f && worldY < 0.27f)
                return FloorTop;
            if (worldY > 2.84f && worldY < 2.94f)
                return CeilingBottom;
            if (worldY > 3.01f && worldY < 3.12f)
                return CeilingTop;
            return worldY;
        }

        private static void ValidateScene(string scenePath)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            ValidateLoadedScene(scenePath);
        }

        private static void ValidateLoadedScene(string expectedPath)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != expectedPath)
                throw new InvalidOperationException($"Unexpected active scene: {scene.path}");
            GameObject artRoot = GameObject.Find("Final Map Visuals (Visual Only)");
            GameObject graybox = GameObject.Find("DEAD LETTER - Graybox");
            if (artRoot == null || graybox == null || graybox.transform.Find(CorrectionRootName) == null)
                throw new InvalidOperationException("Final-map correction structure is incomplete.");

            foreach (string meshName in StructuralMeshNames)
            {
                Transform structure = artRoot.transform.Find(meshName);
                MeshFilter filter = structure != null ? structure.GetComponent<MeshFilter>() : null;
                if (filter == null || filter.sharedMesh == null)
                    throw new InvalidOperationException($"Missing normalized structure: {meshName}");
                string assetPath = AssetDatabase.GetAssetPath(filter.sharedMesh);
                if (!assetPath.StartsWith(DerivedMeshFolder, StringComparison.Ordinal))
                    throw new InvalidOperationException($"{meshName} is not using a derived correction mesh: {assetPath}");
                if (structure.GetComponentsInChildren<Collider>(true).Length != 0)
                    throw new InvalidOperationException($"{meshName} visual-only structure contains a Collider.");
                ValidateReferenceHeights(filter, meshName);
            }

            ValidateDoorPassages();
            int itemCount = UnityEngine.Object.FindObjectsByType<Day2WorldItem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            int boxCount = UnityEngine.Object.FindObjectsByType<Day2EmptyBox>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            int workstationCount = UnityEngine.Object.FindObjectsByType<LetterWorkstation>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            int mailDropCount = UnityEngine.Object.FindObjectsByType<MailDropBox>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            if (itemCount == 0 || boxCount < 5 || workstationCount == 0 || mailDropCount == 0)
                throw new InvalidOperationException($"Gameplay references are incomplete: items={itemCount}, boxes={boxCount}, workstations={workstationCount}, mailDrops={mailDropCount}");
            Debug.Log($"[FlattenReferences] items={itemCount} boxes={boxCount} workstations={workstationCount} mailDrops={mailDropCount}");
            Debug.Log($"[FlattenValidation] PASS scene={scene.path} doors=6 floor={FloorTop:F4} ceiling={CeilingBottom:F4}");
        }

        private static void ValidateReferenceHeights(MeshFilter filter, string meshName)
        {
            Vector3[] vertices = filter.sharedMesh.vertices;
            var worldYs = vertices.Select(value => filter.transform.TransformPoint(value).y).ToArray();
            bool floorTopFound = worldYs.Any(value => Mathf.Abs(value - FloorTop) < 0.001f);
            bool ceilingBottomFound = worldYs.Any(value => Mathf.Abs(value - CeilingBottom) < 0.001f);
            bool strayFloor = worldYs.Any(value => value > 0.14f && value < 0.27f && Mathf.Abs(value - FloorTop) >= 0.001f);
            bool strayCeiling = worldYs.Any(value => value > 2.84f && value < 2.94f && Mathf.Abs(value - CeilingBottom) >= 0.001f);
            if (!floorTopFound || !ceilingBottomFound || strayFloor || strayCeiling)
                throw new InvalidOperationException($"Height normalization failed for {meshName}: floor={floorTopFound}/{strayFloor}, ceiling={ceilingBottomFound}/{strayCeiling}");
        }

        private static void ValidateDoorPassages()
        {
            HingedDoor[] doors = UnityEngine.Object.FindObjectsByType<HingedDoor>(FindObjectsSortMode.None)
                .Where(value => value.gameObject.activeInHierarchy)
                .ToArray();
            if (doors.Length != 6)
                throw new InvalidOperationException($"Expected 6 active functional doors, found {doors.Length}.");
            var probe = new GameObject("Temporary Final Map Passage Probe");
            CharacterController controller = probe.AddComponent<CharacterController>();
            controller.radius = 0.32f;
            controller.height = 1.8f;
            controller.center = Vector3.up * 0.9f;
            controller.stepOffset = 0.3f;
            controller.skinWidth = 0.08f;
            var failures = new List<string>();
            try
            {
                foreach (HingedDoor door in doors)
                {
                    BoxCollider leaf = door.GetComponentsInChildren<BoxCollider>(true).First(value => value.name.EndsWith(" Leaf", StringComparison.Ordinal));
                    Vector3 center = leaf.bounds.center;
                    center.y = 0.03f;
                    Vector3 axis = leaf.bounds.size.x < leaf.bounds.size.z ? Vector3.right : Vector3.forward;
                    Quaternion closed = door.transform.localRotation;
                    try
                    {
                        for (int side = -1; side <= 1; side += 2)
                        {
                            float best = 0f;
                            foreach (float angle in new[] { 90f, -90f })
                            {
                                door.transform.localRotation = closed * Quaternion.Euler(0f, angle, 0f);
                                controller.enabled = false;
                                probe.transform.position = center + axis * side * 0.62f;
                                controller.enabled = true;
                                Physics.SyncTransforms();
                                Vector3 start = probe.transform.position;
                                for (int index = 0; index < 48; index++)
                                    controller.Move(-axis * side * 0.04f + Vector3.down * 0.03f);
                                best = Mathf.Max(best, Vector3.Dot(probe.transform.position - start, -axis * side));
                            }
                            bool passed = best > 0.78f;
                            Debug.Log($"[FlattenDoorPassage] {door.name} side={side} distance={best:F3} passed={passed}");
                            if (!passed)
                                failures.Add($"{door.name} side={side}");
                        }
                    }
                    finally
                    {
                        door.transform.localRotation = closed;
                        Physics.SyncTransforms();
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probe);
            }
            if (failures.Count > 0)
                throw new InvalidOperationException("Blocked door passages: " + string.Join(", ", failures));
        }

        private static void CreateReferenceMarker(Transform parent, string name)
        {
            var marker = new GameObject(name);
            marker.transform.SetParent(parent, false);
        }

        private static void EnsureAssetFolder(string folderPath)
        {
            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }

        private static void AnalyzeScene(string scenePath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var artRoot = GameObject.Find("Final Map Visuals (Visual Only)");
            if (artRoot == null)
                throw new InvalidOperationException("Final Map Visuals (Visual Only) was not found in the test scene.");

            Debug.Log($"[FlattenScene] path={scene.path} roots={scene.rootCount}");
            foreach (var filter in artRoot.GetComponentsInChildren<MeshFilter>(true)
                         .Where(value => value.sharedMesh != null)
                         .OrderBy(value => value.name, StringComparer.Ordinal))
            {
                AnalyzeMesh(filter);
            }

            var graybox = GameObject.Find("DEAD LETTER - Graybox");
            if (graybox == null)
                throw new InvalidOperationException("DEAD LETTER - Graybox was not found.");

            foreach (var collider in graybox.GetComponentsInChildren<Collider>(true)
                         .Where(value => value.enabled)
                         .OrderBy(value => GetPath(value.transform), StringComparer.Ordinal))
            {
                Bounds bounds = collider.bounds;
                Debug.Log($"[FlattenCollider] {GetPath(collider.transform)} type={collider.GetType().Name} " +
                          $"center={Format(bounds.center)} size={Format(bounds.size)} minY={bounds.min.y:F4} maxY={bounds.max.y:F4} " +
                          $"trigger={collider.isTrigger}");
            }
        }

        private static void AnalyzeMesh(MeshFilter filter)
        {
            Mesh mesh = filter.sharedMesh;
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            var upward = new List<FaceSample>();
            var downward = new List<FaceSample>();
            var vertical = new List<FaceSample>();

            for (int index = 0; index + 2 < triangles.Length; index += 3)
            {
                Vector3 a = filter.transform.TransformPoint(vertices[triangles[index]]);
                Vector3 b = filter.transform.TransformPoint(vertices[triangles[index + 1]]);
                Vector3 c = filter.transform.TransformPoint(vertices[triangles[index + 2]]);
                Vector3 cross = Vector3.Cross(b - a, c - a);
                float area = cross.magnitude * 0.5f;
                if (area < 0.000001f)
                    continue;
                Vector3 normal = cross.normalized;
                var sample = new FaceSample((a + b + c) / 3f, Encapsulate(a, b, c), area);
                float up = Vector3.Dot(normal, Vector3.up);
                if (up > 0.92f)
                    upward.Add(sample);
                else if (up < -0.92f)
                    downward.Add(sample);
                else if (Mathf.Abs(up) < 0.15f)
                    vertical.Add(sample);
            }

            Renderer renderer = filter.GetComponent<Renderer>();
            Bounds worldBounds = renderer != null ? renderer.bounds : TransformBounds(filter.transform, mesh.bounds);
            Debug.Log($"[FlattenMesh] {GetPath(filter.transform)} worldMin={Format(worldBounds.min)} worldMax={Format(worldBounds.max)} " +
                      $"upFaces={upward.Count} downFaces={downward.Count} verticalFaces={vertical.Count}");
            LogClusters(filter.name, "UP", upward);
            LogClusters(filter.name, "DOWN", downward);
        }

        private static void LogClusters(string meshName, string direction, List<FaceSample> samples)
        {
            foreach (var cluster in ClusterByHeight(samples, 0.015f))
            {
                Bounds bounds = cluster[0].Bounds;
                float totalArea = 0f;
                float weightedY = 0f;
                foreach (var sample in cluster)
                {
                    bounds.Encapsulate(sample.Bounds);
                    totalArea += sample.Area;
                    weightedY += sample.Center.y * sample.Area;
                }
                float height = totalArea > 0f ? weightedY / totalArea : cluster.Average(value => value.Center.y);
                Debug.Log($"[FlattenLevel] mesh={meshName} face={direction} y={height:F4} area={totalArea:F3} " +
                          $"xzMin=({bounds.min.x:F3},{bounds.min.z:F3}) xzMax=({bounds.max.x:F3},{bounds.max.z:F3}) faces={cluster.Count}");
            }
        }

        private static List<List<FaceSample>> ClusterByHeight(List<FaceSample> samples, float tolerance)
        {
            var result = new List<List<FaceSample>>();
            foreach (var sample in samples.OrderBy(value => value.Center.y))
            {
                var cluster = result.LastOrDefault();
                if (cluster == null || Mathf.Abs(cluster.Average(value => value.Center.y) - sample.Center.y) > tolerance)
                {
                    cluster = new List<FaceSample>();
                    result.Add(cluster);
                }
                cluster.Add(sample);
            }
            return result;
        }

        private static Bounds Encapsulate(Vector3 a, Vector3 b, Vector3 c)
        {
            var bounds = new Bounds(a, Vector3.zero);
            bounds.Encapsulate(b);
            bounds.Encapsulate(c);
            return bounds;
        }

        private static Bounds TransformBounds(Transform transform, Bounds localBounds)
        {
            Vector3 center = transform.TransformPoint(localBounds.center);
            Vector3 extents = localBounds.extents;
            Vector3 axisX = transform.TransformVector(extents.x, 0f, 0f);
            Vector3 axisY = transform.TransformVector(0f, extents.y, 0f);
            Vector3 axisZ = transform.TransformVector(0f, 0f, extents.z);
            extents = new Vector3(
                Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
                Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
                Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z));
            return new Bounds(center, extents * 2f);
        }

        private static string GetPath(Transform target)
        {
            string path = target.name;
            while (target.parent != null)
            {
                target = target.parent;
                path = target.name + "/" + path;
            }
            return path;
        }

        private static string Format(Vector3 value) => $"({value.x:F3},{value.y:F3},{value.z:F3})";

        private readonly struct FaceSample
        {
            public FaceSample(Vector3 center, Bounds bounds, float area)
            {
                Center = center;
                Bounds = bounds;
                Area = area;
            }

            public Vector3 Center { get; }
            public Bounds Bounds { get; }
            public float Area { get; }
        }
    }
}
