using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadLetter.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadLetter.Editor
{
    /// <summary>
    /// Reuses each room's authored URP material on the graybox lintel that fills the
    /// open area above its door. Physics remains on the legacy colliders and the source
    /// FBX is never modified.
    /// </summary>
    public static class FinalMapDoorSurroundingIntegrator
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string FinalMapRootName = "Final Map Visuals (Visual Only)";
        private const string DerivedMeshFolder = "Assets/Art/Models/Derived/DoorLintels";

        private static readonly LintelBinding[] Bindings =
        {
            new("Counter Exterior Door Lintel", "창구", "Counter Exterior Door Pivot", "CounterExterior"),
            new("Counter Hall Door Lintel", "창구", "Counter Hall Door Pivot", "CounterHall"),
            new("Workshop Hall Door Lintel", "작업실", "Workshop Hall Door Pivot", "WorkshopHall"),
            new("Storage Hall Door Lintel", "창고", "Storage Hall Door Pivot", "StorageHall"),
            new("Pantry Hall Door Lintel", "탕비실", "Pantry Hall Door Pivot", "PantryHall"),
            new("Hidden Room Door Lintel", "비밀의방", "Hidden Room Door Pivot", "HiddenRoom")
        };

        [MenuItem("Tools/DEAD LETTER/Art/Fix Door Surrounding Materials")]
        public static void ApplyFromMenu()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            ApplyToLoadedScene(scene, true);
            EditorUtility.DisplayDialog(
                "DEAD LETTER",
                "문 상단 벽 6개를 각 방의 기존 URP 벽 Material로 통일했습니다.\n" +
                "새 Material을 만들지 않았으며 원본 FBX, 문 Collider와 상호작용은 변경하지 않았습니다.",
                "확인");
        }

        [MenuItem("Tools/DEAD LETTER/Art/Validate Door Surrounding Materials")]
        public static void ValidateFromMenu()
        {
            ValidateLoadedScene(true);
        }

        public static void ApplyToLoadedScene(Scene scene, bool saveScene)
        {
            if (!scene.IsValid())
                throw new InvalidOperationException("문 주변 벽을 수정할 유효한 씬이 없습니다.");

            Transform finalRoot = FindSceneObject(FinalMapRootName)?.transform;
            if (finalRoot == null)
                throw new InvalidOperationException("최종 맵 시각 레이어를 찾지 못했습니다.");

            foreach (LintelBinding binding in Bindings)
            {
                MeshRenderer lintel = FindSceneComponents<MeshRenderer>()
                    .FirstOrDefault(value => value.name == binding.LintelName);
                MeshRenderer room = finalRoot.GetComponentsInChildren<MeshRenderer>(true)
                    .FirstOrDefault(value => value.name == binding.RoomMeshName);
                HingedDoor door = FindSceneComponents<HingedDoor>()
                    .FirstOrDefault(value => value.name == binding.DoorPivotName);

                if (lintel == null)
                    throw new InvalidOperationException($"기존 문 상단 오브젝트를 찾지 못했습니다: {binding.LintelName}");
                if (room == null)
                    throw new InvalidOperationException($"최종 맵 방 Mesh를 찾지 못했습니다: {binding.RoomMeshName}");
                if (door == null)
                    throw new InvalidOperationException($"기존 기능성 문을 찾지 못했습니다: {binding.DoorPivotName}");
                if (room.sharedMaterial == null)
                    throw new InvalidOperationException($"최종 맵 방 Material이 없습니다: {binding.RoomMeshName}");

                MeshFilter lintelFilter = lintel.GetComponent<MeshFilter>();
                if (lintelFilter == null)
                    throw new InvalidOperationException($"문 상단 MeshFilter가 없습니다: {binding.LintelName}");

                WallUvSample sample = FindNearestWallUv(room, lintel.bounds);
                Mesh derivedMesh = BuildTexturedLintelMesh(lintel.transform, sample, binding.MeshAssetName);
                EnsureFolder(DerivedMeshFolder);
                string meshPath = $"{DerivedMeshFolder}/{binding.MeshAssetName}.asset";
                AssetDatabase.DeleteAsset(meshPath);
                AssetDatabase.CreateAsset(derivedMesh, meshPath);
                lintelFilter.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);

                lintel.sharedMaterial = room.sharedMaterial;
                lintel.enabled = true;
                EditorUtility.SetDirty(lintelFilter);
                EditorUtility.SetDirty(lintel);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (saveScene && !EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException($"문 주변 재질 수정 씬을 저장하지 못했습니다: {ScenePath}");
        }

        public static void ValidateLoadedScene(bool showDialog)
        {
            var failures = new List<string>();
            Transform finalRoot = FindSceneObject(FinalMapRootName)?.transform;
            if (finalRoot == null)
            {
                failures.Add("최종 맵 시각 레이어가 없습니다.");
            }
            else
            {
                foreach (LintelBinding binding in Bindings)
                {
                    MeshRenderer lintel = FindSceneComponents<MeshRenderer>()
                        .FirstOrDefault(value => value.name == binding.LintelName);
                    MeshRenderer room = finalRoot.GetComponentsInChildren<MeshRenderer>(true)
                        .FirstOrDefault(value => value.name == binding.RoomMeshName);
                    HingedDoor door = FindSceneComponents<HingedDoor>()
                        .FirstOrDefault(value => value.name == binding.DoorPivotName);

                    if (lintel == null)
                        failures.Add($"문 상단 오브젝트 누락: {binding.LintelName}");
                    else if (!lintel.enabled)
                        failures.Add($"문 상단 벽 Renderer가 비활성 상태입니다: {binding.LintelName}");

                    if (room == null)
                    {
                        failures.Add($"최종 맵 방 Mesh 누락: {binding.RoomMeshName}");
                    }
                    else
                    {
                        Material material = room.sharedMaterial;
                        if (material == null || material.shader == null ||
                            material.shader.name != "Universal Render Pipeline/Lit")
                        {
                            failures.Add($"최종 벽이 URP/Lit 재질을 사용하지 않습니다: {binding.RoomMeshName}");
                        }
                        else if (material.GetTexture("_BaseMap") == null)
                        {
                            failures.Add($"최종 벽 Base Map이 없습니다: {binding.RoomMeshName}");
                        }

                        if (lintel != null && lintel.sharedMaterial != material)
                            failures.Add($"문 상단과 주변 벽 Material이 다릅니다: {binding.LintelName}");

                        MeshFilter lintelFilter = lintel != null ? lintel.GetComponent<MeshFilter>() : null;
                        string expectedMeshPath = $"{DerivedMeshFolder}/{binding.MeshAssetName}.asset";
                        if (lintelFilter == null || lintelFilter.sharedMesh == null ||
                            AssetDatabase.GetAssetPath(lintelFilter.sharedMesh) != expectedMeshPath)
                        {
                            failures.Add($"문 상단 벽의 주변 UV Mesh 연결 오류: {binding.LintelName}");
                        }
                        else if (lintelFilter.sharedMesh.uv == null || lintelFilter.sharedMesh.uv.Length == 0)
                        {
                            failures.Add($"문 상단 벽 UV가 없습니다: {binding.LintelName}");
                        }
                    }

                    if (door == null || !door.enabled || door.GetComponentInChildren<Collider>(true) == null)
                        failures.Add($"기존 문 기능 또는 Collider 누락: {binding.DoorPivotName}");
                }
            }

            MeshRenderer storageExterior = FindSceneComponents<MeshRenderer>()
                .FirstOrDefault(value => value.name == "Storage Exterior Door Lintel");
            if (storageExterior != null && storageExterior.enabled)
                failures.Add("비활성 상태여야 하는 창고 외부문 그레이박스 Lintel이 표시되고 있습니다.");

            if (failures.Count > 0)
                throw new InvalidOperationException("문 주변 벽 검증 실패:\n- " + string.Join("\n- ", failures));

            const string success =
                "문 6곳의 상단·주변 벽 Material 공유, URP Base Map, 기존 문 Collider·상호작용 검증 통과";
            Debug.Log("[DoorSurroundingValidation] " + success);
            if (showDialog)
                EditorUtility.DisplayDialog("DEAD LETTER", success, "확인");
        }

        private static WallUvSample FindNearestWallUv(MeshRenderer room, Bounds lintelBounds)
        {
            MeshFilter filter = room.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null || mesh.uv == null || mesh.uv.Length != mesh.vertexCount)
                throw new InvalidOperationException($"최종 벽 UV를 읽을 수 없습니다: {room.name}");

            Vector3 wallAxis = lintelBounds.size.x < lintelBounds.size.z
                ? Vector3.right
                : Vector3.forward;
            Vector3 target = lintelBounds.center;
            Vector3[] vertices = mesh.vertices;
            Vector2[] uvs = mesh.uv;
            int[] triangles = mesh.triangles;
            float bestScore = float.PositiveInfinity;
            WallUvSample best = default;
            bool found = false;

            for (int index = 0; index < triangles.Length; index += 3)
            {
                int ia = triangles[index];
                int ib = triangles[index + 1];
                int ic = triangles[index + 2];
                Vector3 a = room.transform.TransformPoint(vertices[ia]);
                Vector3 b = room.transform.TransformPoint(vertices[ib]);
                Vector3 c = room.transform.TransformPoint(vertices[ic]);
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                if (Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.35f ||
                    Mathf.Abs(Vector3.Dot(normal, wallAxis)) < 0.65f)
                {
                    continue;
                }

                Vector3 center = (a + b + c) / 3f;
                Vector3 delta = center - target;
                float planeDistance = Mathf.Abs(Vector3.Dot(delta, wallAxis));
                float alongWallDistance = (delta - Vector3.Dot(delta, wallAxis) * wallAxis).magnitude;
                float score = planeDistance * 5f + alongWallDistance;
                if (score >= bestScore)
                    continue;

                var ratios = new List<float>(3);
                AddUvRatio(ratios, a, b, uvs[ia], uvs[ib]);
                AddUvRatio(ratios, b, c, uvs[ib], uvs[ic]);
                AddUvRatio(ratios, c, a, uvs[ic], uvs[ia]);
                ratios.Sort();
                float uvPerMeter = ratios.Count > 0 ? ratios[ratios.Count / 2] : 0.08f;
                best = new WallUvSample(
                    (uvs[ia] + uvs[ib] + uvs[ic]) / 3f,
                    Mathf.Clamp(uvPerMeter, 0.01f, 0.5f));
                bestScore = score;
                found = true;
            }

            if (!found)
                throw new InvalidOperationException($"문 주변과 방향이 맞는 최종 벽 UV를 찾지 못했습니다: {room.name}");
            return best;
        }

        private static void AddUvRatio(
            ICollection<float> ratios,
            Vector3 worldA,
            Vector3 worldB,
            Vector2 uvA,
            Vector2 uvB)
        {
            float worldLength = Vector3.Distance(worldA, worldB);
            float uvLength = Vector2.Distance(uvA, uvB);
            if (worldLength > 0.01f && uvLength > 0.0001f)
                ratios.Add(uvLength / worldLength);
        }

        private static Mesh BuildTexturedLintelMesh(
            Transform lintel,
            WallUvSample sample,
            string assetName)
        {
            var vertices = new List<Vector3>(24);
            var uvs = new List<Vector2>(24);
            var triangles = new List<int>(36);
            Vector3 size = lintel.lossyScale;
            size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));

            AddFace(vertices, uvs, triangles,
                new Vector3(0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, -0.5f), new Vector3(0.5f, 0.5f, 0.5f),
                size.z, size.y, sample);
            AddFace(vertices, uvs, triangles,
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(-0.5f, -0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, -0.5f),
                size.z, size.y, sample);
            AddFace(vertices, uvs, triangles,
                new Vector3(-0.5f, 0.5f, 0.5f), new Vector3(0.5f, 0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f),
                size.x, size.z, sample);
            AddFace(vertices, uvs, triangles,
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, 0.5f), new Vector3(-0.5f, -0.5f, 0.5f),
                size.x, size.z, sample);
            AddFace(vertices, uvs, triangles,
                new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f),
                size.x, size.y, sample);
            AddFace(vertices, uvs, triangles,
                new Vector3(0.5f, -0.5f, -0.5f), new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3(-0.5f, 0.5f, -0.5f), new Vector3(0.5f, 0.5f, -0.5f),
                size.x, size.y, sample);

            var mesh = new Mesh { name = $"Door Lintel {assetName} Textured" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddFace(
            ICollection<Vector3> vertices,
            ICollection<Vector2> uvs,
            ICollection<int> triangles,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 d,
            float widthMeters,
            float heightMeters,
            WallUvSample sample)
        {
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            float halfU = Mathf.Min(0.45f, widthMeters * sample.UvPerMeter * 0.5f);
            float halfV = Mathf.Min(0.45f, heightMeters * sample.UvPerMeter * 0.5f);
            uvs.Add(sample.Center + new Vector2(-halfU, -halfV));
            uvs.Add(sample.Center + new Vector2(halfU, -halfV));
            uvs.Add(sample.Center + new Vector2(halfU, halfV));
            uvs.Add(sample.Center + new Vector2(-halfU, halfV));
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
                throw new InvalidOperationException($"잘못된 폴더 경로: {path}");
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static T[] FindSceneComponents<T>() where T : Component =>
            Resources.FindObjectsOfTypeAll<T>()
                .Where(value => value != null && value.gameObject.scene.IsValid())
                .ToArray();

        private static GameObject FindSceneObject(string objectName) =>
            Resources.FindObjectsOfTypeAll<GameObject>()
                .FirstOrDefault(value => value.scene.IsValid() && value.name == objectName);

        private readonly struct LintelBinding
        {
            public LintelBinding(
                string lintelName,
                string roomMeshName,
                string doorPivotName,
                string meshAssetName)
            {
                LintelName = lintelName;
                RoomMeshName = roomMeshName;
                DoorPivotName = doorPivotName;
                MeshAssetName = meshAssetName;
            }

            public string LintelName { get; }
            public string RoomMeshName { get; }
            public string DoorPivotName { get; }
            public string MeshAssetName { get; }
        }

        private readonly struct WallUvSample
        {
            public WallUvSample(Vector2 center, float uvPerMeter)
            {
                Center = center;
                UvPerMeter = uvPerMeter;
            }

            public Vector2 Center { get; }
            public float UvPerMeter { get; }
        }
    }
}
