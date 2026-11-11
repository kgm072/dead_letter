using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeadLetter.Editor
{
    public static class MapObjExporter
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string RootName = "DEAD LETTER - Graybox";
        private const string OutputFolder = "Exports/DeadLetter_Map_Current";
        private const string ObjFileName = "DeadLetter_Map_Current.obj";
        private const string MtlFileName = "DeadLetter_Map_Current.mtl";

        [MenuItem("Tools/DEAD LETTER/Export/Map Structure as OBJ")]
        public static void ExportFromMenu()
        {
            Export();
            EditorUtility.RevealInFinder(Path.GetFullPath(OutputFolder));
        }

        public static void ExportBatch()
        {
            try
            {
                Export();
                Debug.Log("MAP_OBJ_EXPORT_SUCCESS");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void Export()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject root = GameObject.Find(RootName);
            if (root == null)
                throw new InvalidOperationException($"Map root was not found: {RootName}");

            List<MeshFilter> filters = root.GetComponentsInChildren<MeshFilter>(true)
                .Where(filter => filter.sharedMesh != null && IsMapStructure(filter.gameObject.name))
                .OrderBy(filter => GetHierarchyPath(filter.transform), StringComparer.Ordinal)
                .ToList();
            if (filters.Count == 0)
                throw new InvalidOperationException("No structural map meshes were found.");

            Directory.CreateDirectory(OutputFolder);
            string objPath = Path.Combine(OutputFolder, ObjFileName);
            string mtlPath = Path.Combine(OutputFolder, MtlFileName);
            string readmePath = Path.Combine(OutputFolder, "README_KO.txt");

            Dictionary<Material, string> materialNames = CollectMaterials(filters);
            WriteObj(objPath, filters, materialNames);
            WriteMtl(mtlPath, materialNames);
            WriteReadme(readmePath, filters.Count, materialNames.Count);
            ValidateRoundTripAndRenderPreview(objPath, mtlPath);

            Debug.Log(
                $"Map OBJ exported. Mesh objects: {filters.Count}, materials: {materialNames.Count}, path: {Path.GetFullPath(objPath)}");
        }

        private static void ValidateRoundTripAndRenderPreview(string objPath, string mtlPath)
        {
            const string validationFolder = "Assets/__MapExportValidation";
            if (AssetDatabase.IsValidFolder(validationFolder))
                AssetDatabase.DeleteAsset(validationFolder);
            AssetDatabase.CreateFolder("Assets", "__MapExportValidation");

            string validationObj = $"{validationFolder}/{ObjFileName}";
            string validationMtl = $"{validationFolder}/{MtlFileName}";
            File.Copy(objPath, validationObj, true);
            File.Copy(mtlPath, validationMtl, true);
            AssetDatabase.ImportAsset(validationMtl, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(validationObj, ImportAssetOptions.ForceSynchronousImport);

            GameObject importedModel = AssetDatabase.LoadAssetAtPath<GameObject>(validationObj);
            if (importedModel == null)
                throw new InvalidOperationException("Round-trip validation failed: Unity could not import the exported OBJ.");

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject instance = PrefabUtility.InstantiatePrefab(importedModel) as GameObject;
            if (instance == null)
                throw new InvalidOperationException("Round-trip validation failed: imported OBJ could not be instantiated.");

            MeshFilter[] importedFilters = instance.GetComponentsInChildren<MeshFilter>(true);
            if (importedFilters.Length == 0 || importedFilters.Any(filter => filter.sharedMesh == null))
                throw new InvalidOperationException("Round-trip validation failed: imported OBJ contains missing meshes.");

            foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.Contains("Ceiling", StringComparison.OrdinalIgnoreCase))
                    child.gameObject.SetActive(false);
            }

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(false);
            if (renderers.Length == 0)
                throw new InvalidOperationException("Round-trip validation failed: imported OBJ has no visible renderers.");

            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);

            GameObject lightObject = new GameObject("Preview Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            lightObject.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            GameObject cameraObject = new GameObject("Preview Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.03f, 0.04f);
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(bounds.size.x, bounds.size.z) * 0.64f;
            cameraObject.transform.position = bounds.center + new Vector3(28f, 34f, -28f);
            cameraObject.transform.LookAt(bounds.center + Vector3.up * 0.5f);

            const int previewSize = 1200;
            RenderTexture target = new RenderTexture(previewSize, previewSize, 24);
            Texture2D preview = new Texture2D(previewSize, previewSize, TextureFormat.RGB24, false);
            RenderTexture previousActive = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                preview.ReadPixels(new Rect(0, 0, previewSize, previewSize), 0, 0);
                preview.Apply();
                File.WriteAllBytes(Path.Combine(OutputFolder, "DeadLetter_Map_Current_Preview.png"), preview.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(preview);
                UnityEngine.Object.DestroyImmediate(target);
            }

            AssetDatabase.DeleteAsset(validationFolder);
            Debug.Log($"OBJ round-trip validation passed. Imported mesh objects: {importedFilters.Length}.");
        }

        private static bool IsMapStructure(string objectName)
        {
            if (objectName.Contains("Ceiling Fixture", StringComparison.OrdinalIgnoreCase) ||
                objectName.Contains("Arrow", StringComparison.OrdinalIgnoreCase) ||
                objectName.Contains("Warning Plate", StringComparison.OrdinalIgnoreCase))
                return false;

            return objectName.Contains("Floor", StringComparison.OrdinalIgnoreCase) ||
                   objectName.Contains("Ceiling", StringComparison.OrdinalIgnoreCase) ||
                   objectName.Contains("Wall", StringComparison.OrdinalIgnoreCase) ||
                   objectName.Contains("Door", StringComparison.OrdinalIgnoreCase) ||
                   objectName.Contains("Lintel", StringComparison.OrdinalIgnoreCase) ||
                   objectName.Contains("Threshold", StringComparison.OrdinalIgnoreCase);
        }

        private static Dictionary<Material, string> CollectMaterials(IEnumerable<MeshFilter> filters)
        {
            Dictionary<Material, string> result = new Dictionary<Material, string>();
            HashSet<string> usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (MeshFilter filter in filters)
            {
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null)
                    continue;

                foreach (Material material in renderer.sharedMaterials.Where(material => material != null))
                {
                    if (result.ContainsKey(material))
                        continue;

                    string baseName = SanitizeName(material.name);
                    string uniqueName = baseName;
                    int suffix = 2;
                    while (!usedNames.Add(uniqueName))
                        uniqueName = $"{baseName}_{suffix++}";
                    result.Add(material, uniqueName);
                }
            }
            return result;
        }

        private static void WriteObj(
            string path,
            IReadOnlyList<MeshFilter> filters,
            IReadOnlyDictionary<Material, string> materialNames)
        {
            CultureInfo invariant = CultureInfo.InvariantCulture;
            StringBuilder builder = new StringBuilder(1024 * 1024);
            builder.AppendLine("# DEAD LETTER map structure export");
            builder.AppendLine("# Scale: 1 OBJ unit = 1 meter");
            builder.AppendLine($"mtllib {MtlFileName}");

            int vertexOffset = 1;
            int uvOffset = 1;
            int normalOffset = 1;

            foreach (MeshFilter filter in filters)
            {
                Mesh mesh = filter.sharedMesh;
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                Matrix4x4 localToWorld = filter.transform.localToWorldMatrix;
                Matrix4x4 normalMatrix = localToWorld.inverse.transpose;
                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;
                Vector2[] uvs = mesh.uv;

                builder.AppendLine();
                builder.AppendLine($"o {SanitizeName(GetHierarchyPath(filter.transform))}");
                foreach (Vector3 vertex in vertices)
                {
                    Vector3 world = localToWorld.MultiplyPoint3x4(vertex);
                    builder.Append("v ")
                        .Append((-world.x).ToString("R", invariant)).Append(' ')
                        .Append(world.y.ToString("R", invariant)).Append(' ')
                        .Append(world.z.ToString("R", invariant)).AppendLine();
                }

                foreach (Vector2 uv in uvs)
                {
                    builder.Append("vt ")
                        .Append(uv.x.ToString("R", invariant)).Append(' ')
                        .Append(uv.y.ToString("R", invariant)).AppendLine();
                }

                foreach (Vector3 normal in normals)
                {
                    Vector3 worldNormal = normalMatrix.MultiplyVector(normal).normalized;
                    builder.Append("vn ")
                        .Append((-worldNormal.x).ToString("R", invariant)).Append(' ')
                        .Append(worldNormal.y.ToString("R", invariant)).Append(' ')
                        .Append(worldNormal.z.ToString("R", invariant)).AppendLine();
                }

                int subMeshCount = Math.Max(1, mesh.subMeshCount);
                for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
                {
                    Material material = renderer != null && renderer.sharedMaterials.Length > 0
                        ? renderer.sharedMaterials[Math.Min(subMesh, renderer.sharedMaterials.Length - 1)]
                        : null;
                    if (material != null && materialNames.TryGetValue(material, out string materialName))
                        builder.AppendLine($"usemtl {materialName}");

                    int[] triangles = mesh.GetTriangles(subMesh);
                    for (int index = 0; index + 2 < triangles.Length; index += 3)
                    {
                        AppendFaceVertex(builder, triangles[index], vertices.Length, uvs.Length, normals.Length,
                            vertexOffset, uvOffset, normalOffset);
                        AppendFaceVertex(builder, triangles[index + 1], vertices.Length, uvs.Length, normals.Length,
                            vertexOffset, uvOffset, normalOffset);
                        AppendFaceVertex(builder, triangles[index + 2], vertices.Length, uvs.Length, normals.Length,
                            vertexOffset, uvOffset, normalOffset);
                        builder.AppendLine();
                    }
                }

                vertexOffset += vertices.Length;
                uvOffset += uvs.Length;
                normalOffset += normals.Length;
            }

            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
        }

        private static void AppendFaceVertex(
            StringBuilder builder,
            int localIndex,
            int vertexCount,
            int uvCount,
            int normalCount,
            int vertexOffset,
            int uvOffset,
            int normalOffset)
        {
            int vertex = vertexOffset + Math.Min(localIndex, vertexCount - 1);
            builder.Append(builder.Length == 0 || builder[builder.Length - 1] == '\n' ? "f " : " ");

            bool hasUv = uvCount == vertexCount;
            bool hasNormal = normalCount == vertexCount;
            builder.Append(vertex);
            if (hasUv || hasNormal)
            {
                builder.Append('/');
                if (hasUv)
                    builder.Append(uvOffset + localIndex);
                if (hasNormal)
                    builder.Append('/').Append(normalOffset + localIndex);
            }
        }

        private static void WriteMtl(string path, IReadOnlyDictionary<Material, string> materialNames)
        {
            CultureInfo invariant = CultureInfo.InvariantCulture;
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("# Material groups preserved from Unity for texture assignment");
            foreach (KeyValuePair<Material, string> pair in materialNames.OrderBy(pair => pair.Value))
            {
                Color color = pair.Key.HasProperty("_BaseColor")
                    ? pair.Key.GetColor("_BaseColor")
                    : pair.Key.color;
                builder.AppendLine();
                builder.AppendLine($"newmtl {pair.Value}");
                builder.AppendLine($"Kd {color.r.ToString("R", invariant)} {color.g.ToString("R", invariant)} {color.b.ToString("R", invariant)}");
                builder.AppendLine("Ka 0 0 0");
                builder.AppendLine("Ks 0.08 0.08 0.08");
                builder.AppendLine("Ns 24");
                builder.AppendLine($"d {color.a.ToString("R", invariant)}");
                builder.AppendLine("illum 2");
            }
            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
        }

        private static void WriteReadme(string path, int meshCount, int materialCount)
        {
            string text =
                "DEAD LETTER 현재 맵 구조 OBJ 내보내기\r\n" +
                $"원본 씬: {ScenePath}\r\n" +
                "스케일: 1 Unit = 1m\r\n" +
                "좌표계: Y-Up, OBJ용 오른손 좌표계로 변환됨\r\n" +
                $"구조 메시 오브젝트: {meshCount}개\r\n" +
                $"재질 슬롯: {materialCount}개\r\n\r\n" +
                "포함: 방/복도 바닥, 벽, 천장, 출입 공간, 문틀, 문짝과 문 장식\r\n" +
                "제외: 가구, NPC, 플레이어, UI, 조명 장식, 상호작용 로직, 업무 오브젝트\r\n" +
                "MTL은 현재 Unity 재질 구분과 기본 색상만 보존하며 새 텍스처 제작용입니다.\r\n";
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }

        private static string GetHierarchyPath(Transform transform)
        {
            Stack<string> names = new Stack<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }
            return string.Join("/", names);
        }

        private static string SanitizeName(string value)
        {
            StringBuilder result = new StringBuilder(value.Length);
            foreach (char character in value)
                result.Append(char.IsLetterOrDigit(character) || character == '_' || character == '-' ? character : '_');
            return result.Length == 0 ? "Unnamed" : result.ToString();
        }
    }
}
