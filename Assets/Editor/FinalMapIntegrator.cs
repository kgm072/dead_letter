using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadLetter.Interaction;
using DeadLetter.Mail;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadLetter.Editor
{
    /// <summary>
    /// Installs the final-map FBX as a visual-only layer. Existing scene objects keep all
    /// colliders, interaction components and stable Transform references.
    /// </summary>
    public static class FinalMapIntegrator
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        // Keep the previously integrated source for recovery; the handoff FBX is imported
        // as a separate asset so the original is never overwritten.
        private const string ModelPath = "Assets/Art/Models/DeadLetter_FinalMap_Latest.fbx";
        private const string MaterialFolder = "Assets/Art/Materials/FinalMap";
        private const string RoomTextureFolder = "Assets/Art/Textures/FinalMap";
        private const string RootName = "Final Map Visuals (Visual Only)";

        private static readonly HashSet<string> StructuralMeshNames = new(StringComparer.Ordinal)
        {
            "복도",
            "비밀의방",
            "작업실",
            "창고",
            "창구",
            "탕비실"
        };

        [MenuItem("Tools/DEAD LETTER/Art/Apply Final Map Visual Layer")]
        public static void ApplyFromMenu()
        {
            ApplyAndSave();
            EditorUtility.DisplayDialog(
                "DEAD LETTER",
                "최종 맵의 구조 Mesh를 시각 레이어로 적용했습니다. 기존 Collider와 상호작용 오브젝트는 유지됩니다.",
                "확인");
        }

        [MenuItem("Tools/DEAD LETTER/Art/Validate Final Map Integration")]
        public static void ValidateFromMenu()
        {
            ValidateOpenScene(true);
        }

        public static void ApplyBatch()
        {
            try
            {
                ApplyAndSave();
                ValidateOpenScene(false);
                Debug.Log("FINAL_MAP_INTEGRATION_SUCCESS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void ValidateBatch()
        {
            try
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                ValidateOpenScene(false);
                Debug.Log("FINAL_MAP_VALIDATION_SUCCESS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void ApplyAndSave()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject graybox = FindSceneObject("DEAD LETTER - Graybox");
            if (graybox == null)
                throw new InvalidOperationException("기존 맵 루트 'DEAD LETTER - Graybox'를 찾지 못했습니다.");

            GameObject previousRoot = FindSceneObject(RootName);
            if (previousRoot != null)
                UnityEngine.Object.DestroyImmediate(previousRoot);

            Renderer[] legacyRenderers = graybox.GetComponentsInChildren<Renderer>(true);
            Bounds currentBounds = CalculateBounds(legacyRenderers);
            if (currentBounds.size.x <= 0f || currentBounds.size.z <= 0f)
                throw new InvalidOperationException("현재 맵의 외곽 크기를 계산할 수 없습니다.");

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
                throw new InvalidOperationException($"최종 맵 FBX를 불러올 수 없습니다: {ModelPath}");

            GameObject root = PrefabUtility.InstantiatePrefab(model, scene) as GameObject;
            if (root == null)
                throw new InvalidOperationException("최종 맵 FBX 인스턴스 생성에 실패했습니다.");
            root.name = RootName;
            root.transform.SetParent(graybox.transform, true);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;
            // Door meshes from the authored FBX must become children of the existing
            // functional hinge pivots. Unpack only the scene instance; the FBX asset stays intact.
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            Renderer[] finalRenderers = root.GetComponentsInChildren<Renderer>(true);
            Bounds unscaledBounds = CalculateBounds(finalRenderers);
            float scaleX = currentBounds.size.x / unscaledBounds.size.x;
            float scaleZ = currentBounds.size.z / unscaledBounds.size.z;
            float uniformScale = (scaleX + scaleZ) * 0.5f;
            float ratioDifference = Mathf.Abs(scaleX - scaleZ) / Mathf.Max(scaleX, scaleZ);
            if (ratioDifference > 0.025f)
            {
                throw new InvalidOperationException(
                    $"현재 맵과 최종 FBX의 X/Z 비율 차이가 큽니다. X={scaleX:F4}, Z={scaleZ:F4}");
            }

            root.transform.localScale = Vector3.one * uniformScale;
            Bounds scaledBounds = CalculateBounds(finalRenderers);
            root.transform.position += new Vector3(
                currentBounds.center.x - scaledBounds.center.x,
                0f,
                currentBounds.center.z - scaledBounds.center.z);

            Renderer[] structuralRenderers = finalRenderers
                .Where(value => StructuralMeshNames.Contains(value.gameObject.name))
                .ToArray();
            if (structuralRenderers.Length != StructuralMeshNames.Count)
            {
                string found = string.Join(", ", structuralRenderers.Select(value => value.gameObject.name));
                throw new InvalidOperationException(
                    $"최종 FBX의 구조 Mesh 수가 예상과 다릅니다. found={structuralRenderers.Length}: {found}");
            }

            // The handoff child transforms were measured with the FBX root in its imported
            // orientation. The older map needed a 180-degree root correction, but applying
            // that correction again after scene_changes.json mirrors the final layout and
            // separates the visual rooms from their gameplay objects/colliders.

            float structuralFloor = structuralRenderers.Min(value => value.bounds.min.y);
            root.transform.position += Vector3.up * -structuralFloor;

            foreach (Renderer renderer in finalRenderers)
            {
                bool isStructural = StructuralMeshNames.Contains(renderer.gameObject.name);
                renderer.enabled = isStructural;
                if (!isStructural)
                    continue;

                renderer.sharedMaterial = GetOrCreateRoomMaterial(renderer.gameObject.name, renderer.sharedMaterial);
                GameObjectUtility.SetStaticEditorFlags(
                    renderer.gameObject,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            }

            // The FBX is visual-only: physics and all interaction remain on the stable scene objects.
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(collider);

            ArrangeStorageBoxes();

            int hiddenLegacyStructureCount = 0;
            foreach (Renderer renderer in legacyRenderers)
            {
                if (renderer == null || renderer.transform.IsChildOf(root.transform))
                    continue;
                if (!IsLegacyStructureRenderer(renderer.gameObject.name))
                    continue;

                renderer.enabled = false;
                hiddenLegacyStructureCount++;
            }

            // The handoff delta is measured against the final build. Applying it here makes
            // map reinstallation deterministic instead of leaving scene-only overrides.
            HandoffSceneApplier.ApplyToLoadedScene(false);
            // Normalize small authored floor/ceiling height drift into derived Unity meshes.
            // The source FBX stays untouched and physics remains on the stable legacy colliders.
            FinalMapFlatteningTool.ApplyToLoadedScene(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException($"씬 저장에 실패했습니다: {ScenePath}");

            Bounds installedBounds = CalculateBounds(structuralRenderers);
            Debug.Log(
                $"[FinalMapApply] uniformScale={uniformScale:F6} scaleX={scaleX:F6} scaleZ={scaleZ:F6} " +
                $"position={Format(root.transform.position)} structuralBoundsCenter={Format(installedBounds.center)} " +
                $"structuralBoundsSize={Format(installedBounds.size)} hiddenLegacyStructures={hiddenLegacyStructureCount}");
        }

        private static void ValidateOpenScene(bool showDialog)
        {
            var failures = new List<string>();
            GameObject graybox = FindSceneObject("DEAD LETTER - Graybox");
            GameObject root = FindSceneObject(RootName);
            if (graybox == null)
                failures.Add("기존 Graybox 루트가 없습니다.");
            if (root == null)
                failures.Add("최종 맵 시각 레이어가 없습니다.");

            if (root != null)
            {
                Renderer[] finalRenderers = root.GetComponentsInChildren<Renderer>(true);
                Renderer[] enabledRenderers = finalRenderers.Where(value => value.enabled).ToArray();
                if (enabledRenderers.Length != StructuralMeshNames.Count)
                    failures.Add($"활성 최종 맵 구조 Renderer가 {enabledRenderers.Length}개입니다.");
                foreach (Renderer renderer in enabledRenderers)
                {
                    if (!StructuralMeshNames.Contains(renderer.gameObject.name))
                        failures.Add($"예상하지 않은 최종 맵 Renderer가 활성화됨: {renderer.gameObject.name}");
                    if (renderer.sharedMaterial == null ||
                        renderer.sharedMaterial.shader == null ||
                        renderer.sharedMaterial.shader.name != "Universal Render Pipeline/Lit")
                    {
                        failures.Add($"URP/Lit가 아닌 최종 맵 Material: {renderer.gameObject.name}");
                    }
                    else
                    {
                        string expectedTexturePath = GetRoomTexturePath(renderer.gameObject.name);
                        string actualTexturePath = AssetDatabase.GetAssetPath(renderer.sharedMaterial.mainTexture);
                        if (!string.Equals(actualTexturePath, expectedTexturePath, StringComparison.Ordinal))
                        {
                            failures.Add(
                                $"최종 맵 구조 텍스처 불일치: {renderer.gameObject.name} " +
                                $"expected={expectedTexturePath}, actual={actualTexturePath}");
                        }
                    }
                }

                if (root.GetComponentsInChildren<Collider>(true).Length != 0)
                    failures.Add("시각 전용 최종 맵 레이어에 Collider가 남아 있습니다.");

                if (Vector3.Dot(root.transform.forward, Vector3.forward) < 0.999f)
                    failures.Add("최종 맵 시각 루트가 인계 좌표의 기준 방향과 일치하지 않습니다.");
            }

            HingedDoor[] doors = Resources.FindObjectsOfTypeAll<HingedDoor>()
                .Where(value => value.gameObject.scene.IsValid())
                .ToArray();
            if (doors.Length < 5)
                failures.Add($"기존 상호작용 문 수가 부족합니다: {doors.Length}");
            if (doors.Any(value => !value.enabled || value.GetComponentInChildren<Collider>(true) == null))
                failures.Add("비활성 또는 Collider가 없는 기존 문이 있습니다.");

            if (graybox != null)
            {
                int legacyColliderCount = graybox.GetComponentsInChildren<Collider>(true)
                    .Count(value => root == null || !value.transform.IsChildOf(root.transform));
                if (legacyColliderCount < 25)
                    failures.Add($"기존 맵 Collider가 비정상적으로 적습니다: {legacyColliderCount}");
            }

            ValidateStorage(failures);

            int missingScriptCount = SceneManager.GetActiveScene().GetRootGameObjects()
                .Sum(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount);
            if (missingScriptCount != 0)
                failures.Add($"Missing Script가 {missingScriptCount}개 있습니다.");

            if (failures.Count > 0)
                throw new InvalidOperationException("최종 맵 검증 실패:\n- " + string.Join("\n- ", failures));

            string success =
                $"최종 맵 검증 통과: 구조 Mesh {StructuralMeshNames.Count}개, 상호작용 문 {doors.Length}개, " +
                $"DAY 2 물품 {FindSceneComponents<Day2WorldItem>().Length}개, 빈 박스 {FindSceneComponents<Day2EmptyBox>().Length}개";
            Debug.Log("[FinalMapValidation] " + success);
            if (showDialog)
                EditorUtility.DisplayDialog("DEAD LETTER", success, "확인");
        }

        private static void ValidateStorage(List<string> failures)
        {
            GameObject finalRoot = FindSceneObject(RootName);
            Renderer storageRoom = finalRoot != null
                ? finalRoot.GetComponentsInChildren<Renderer>(true)
                    .FirstOrDefault(value => value.name == "창고")
                : null;
            GameObject shelf = FindSceneObject("Storage Production Art");
            if (storageRoom == null)
            {
                failures.Add("최종 맵 창고 Renderer가 없습니다.");
                return;
            }
            if (shelf == null || !shelf.activeInHierarchy)
                failures.Add("활성 창고 선반(Production Art)이 없습니다.");
            else
            {
                Renderer[] shelfRenderers = shelf.GetComponentsInChildren<Renderer>(true);
                Collider[] shelfColliders = shelf.GetComponentsInChildren<Collider>(true);
                if (shelfRenderers.Length == 0 || shelfColliders.Length == 0)
                    failures.Add("창고 선반 Mesh 또는 Collider가 없습니다.");
                else
                {
                    Bounds shelfBounds = CalculateBounds(shelfRenderers);
                    if (shelfBounds.size.y < 1.8f || shelfBounds.size.y > 2.8f)
                        failures.Add($"창고 선반 높이가 비정상입니다: {shelfBounds.size.y:F2}m");
                    if (shelfBounds.size.x >= 8.5f || shelfBounds.size.z >= 8.5f)
                        failures.Add("창고 선반이 방 전체 통로를 막을 가능성이 있습니다.");
                }
            }

            Bounds floorBounds = storageRoom.bounds;
            foreach (Day2WorldItem item in FindSceneComponents<Day2WorldItem>()
                         .Where(value => value.Source == Day2ItemSource.Storage))
            {
                Renderer[] renderers = item.GetComponentsInChildren<Renderer>(true);
                Collider[] colliders = item.GetComponentsInChildren<Collider>(true);
                if (renderers.Length == 0 || colliders.Length == 0)
                {
                    failures.Add($"창고 물품 Mesh/Collider 누락: {item.name}");
                    continue;
                }

                Bounds itemBounds = CalculateBounds(renderers);
                if (!ContainsXZ(floorBounds, itemBounds.center))
                    failures.Add($"창고 밖에 배치된 물품: {item.name} at {Format(itemBounds.center)}");
                if (itemBounds.min.y < -0.05f)
                    failures.Add($"바닥에 파묻힌 창고 물품: {item.name} minY={itemBounds.min.y:F3}");
            }

            foreach (Day2EmptyBox box in FindSceneComponents<Day2EmptyBox>())
            {
                Renderer[] renderers = box.GetComponentsInChildren<Renderer>(true);
                Collider[] colliders = box.GetComponentsInChildren<Collider>(true);
                if (renderers.Length == 0 || colliders.Length == 0)
                {
                    failures.Add($"빈 박스 Mesh/Collider 누락: {box.name}");
                    continue;
                }

                Bounds boxBounds = CalculateBounds(renderers);
                if (!ContainsXZ(floorBounds, boxBounds.center))
                    failures.Add($"창고 밖에 배치된 빈 박스: {box.name} at {Format(boxBounds.center)}");
                if (boxBounds.min.y < -0.05f)
                    failures.Add($"바닥에 파묻힌 빈 박스: {box.name} minY={boxBounds.min.y:F3}");
                if (Mathf.Abs(boxBounds.center.x) < 2.5f &&
                    boxBounds.center.z > 8f && boxBounds.center.z < 15f)
                {
                    failures.Add($"창고 중앙 통로에 남은 빈 박스: {box.name} at {Format(boxBounds.center)}");
                }
            }
        }

        private static void ArrangeStorageBoxes()
        {
            // Keep the central aisle clear. Boxes remain individual interactive objects with
            // their original IDs and are staged on the inside edge of the west shelf.
            Vector3[] positions =
            {
                new(-3.18f, 0.36f, 9.30f),
                new(-3.18f, 0.36f, 10.50f),
                new(-3.18f, 0.36f, 11.70f),
                new(-3.18f, 0.36f, 12.90f),
                new(-3.18f, 0.36f, 14.10f)
            };

            Day2EmptyBox[] boxes = FindSceneComponents<Day2EmptyBox>()
                .OrderBy(value => value.BoxId, StringComparer.Ordinal)
                .ToArray();
            if (boxes.Length != positions.Length)
                throw new InvalidOperationException($"빈 박스 수가 예상과 다릅니다: {boxes.Length}");

            for (int index = 0; index < boxes.Length; index++)
            {
                Transform target = boxes[index].transform;
                target.SetPositionAndRotation(positions[index], Quaternion.Euler(0f, 90f, 0f));
                EditorUtility.SetDirty(target);
            }
        }

        private static Material GetOrCreateRoomMaterial(string meshName, Material source)
        {
            EnsureFolder("Assets/Art/Materials");
            EnsureFolder(MaterialFolder);
            string safeName = meshName switch
            {
                "복도" => "Corridor",
                "비밀의방" => "HiddenRoom",
                "작업실" => "Workshop",
                "창고" => "Storage",
                "창구" => "Counter",
                "탕비실" => "Pantry",
                _ => "Room"
            };
            string path = $"{MaterialFolder}/FinalMap_{safeName}_URP.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException("URP/Lit 셰이더를 찾지 못했습니다.");

            if (material == null)
            {
                material = new Material(shader) { name = $"FinalMap_{safeName}_URP" };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            string roomTexturePath = GetRoomTexturePath(meshName);
            Texture2D roomTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(roomTexturePath);
            if (roomTexture == null)
                throw new InvalidOperationException($"최종 맵 구조 텍스처를 찾지 못했습니다: {roomTexturePath}");

            material.SetTexture("_BaseMap", roomTexture);
            material.SetTexture("_MainTex", roomTexture);
            material.SetColor("_BaseColor", Color.white);
            material.SetColor("_Color", Color.white);
            material.SetFloat("_Smoothness", 0.12f);
            material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static string GetRoomTexturePath(string meshName)
        {
            string textureName = meshName switch
            {
                "복도" => "복도_Room.png",
                "비밀의방" => "비밀의방_Room.png",
                "작업실" => "작업실_Room.png",
                "창고" => "창고_Room.png",
                "창구" => "창구_Room.png",
                "탕비실" => "탕비실_Room.png",
                _ => throw new ArgumentOutOfRangeException(nameof(meshName), meshName, "알 수 없는 최종 맵 구조 Mesh입니다.")
            };
            return $"{RoomTextureFolder}/{textureName}";
        }

        private static Color ResolveSourceColor(Material source, string meshName)
        {
            if (source != null)
            {
                if (source.HasProperty("_BaseColor"))
                    return source.GetColor("_BaseColor");
                if (source.HasProperty("_Color"))
                    return source.GetColor("_Color");
            }

            return meshName switch
            {
                "복도" => new Color(0.23f, 0.22f, 0.20f, 1f),
                "비밀의방" => new Color(0.17f, 0.18f, 0.18f, 1f),
                "창고" => new Color(0.25f, 0.23f, 0.19f, 1f),
                "탕비실" => new Color(0.29f, 0.25f, 0.20f, 1f),
                _ => new Color(0.27f, 0.25f, 0.22f, 1f)
            };
        }

        private static bool IsLegacyStructureRenderer(string objectName)
        {
            return objectName.Contains("Wall", StringComparison.OrdinalIgnoreCase) ||
                   objectName.EndsWith("Floor", StringComparison.OrdinalIgnoreCase) ||
                   objectName.EndsWith("Ceiling", StringComparison.OrdinalIgnoreCase);
        }

        private static T[] FindSceneComponents<T>() where T : Component
        {
            return Resources.FindObjectsOfTypeAll<T>()
                .Where(value => value.gameObject.scene.IsValid())
                .ToArray();
        }

        private static GameObject FindSceneObject(string objectName)
        {
            return Resources.FindObjectsOfTypeAll<GameObject>()
                .FirstOrDefault(value => value.scene.IsValid() && value.name == objectName);
        }

        private static Bounds CalculateBounds(IEnumerable<Renderer> renderers)
        {
            Renderer[] valid = renderers.Where(value => value != null).ToArray();
            if (valid.Length == 0)
                return new Bounds(Vector3.zero, Vector3.zero);
            Bounds bounds = valid[0].bounds;
            for (int index = 1; index < valid.Length; index++)
                bounds.Encapsulate(valid[index].bounds);
            return bounds;
        }

        private static bool ContainsXZ(Bounds bounds, Vector3 point)
        {
            return point.x >= bounds.min.x && point.x <= bounds.max.x &&
                   point.z >= bounds.min.z && point.z <= bounds.max.z;
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

        private static string Format(Vector3 value)
        {
            return $"({value.x:F3},{value.y:F3},{value.z:F3})";
        }
    }
}
