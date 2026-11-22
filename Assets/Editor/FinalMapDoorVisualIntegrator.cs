using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadLetter.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace DeadLetter.Editor
{
    /// <summary>
    /// Reuses only the authored door meshes from the source FBX. The FBX asset is never
    /// modified: gameplay pivots, colliders and HingedDoor components stay on the scene
    /// objects while the UV-preserving FBX mesh is installed as a visual child.
    /// </summary>
    public static class FinalMapDoorVisualIntegrator
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string SourceFbxPath = "Assets/Art/Models/DeadLetter_FinalMap.fbx";
        private const string DoorTexturePath = "Assets/Art/Textures/FinalMap/문_Door.png";
        private const string DoorMaterialPath = "Assets/Art/Materials/FinalMap/FinalMap_Door_URP.mat";
        private const string VisualName = "FBX Door Visual";

        private static readonly DoorBinding[] Bindings =
        {
            new("Counter Exterior Door Pivot", "창구 바깥 문"),
            new("Counter Hall Door Pivot", "창구 안쪽 문"),
            new("Workshop Hall Door Pivot", "작업실 문"),
            new("Hidden Room Door Pivot", "비밀의방 문"),
            new("Pantry Hall Door Pivot", "탕비실 문"),
            new("Storage Hall Door Pivot", "창고 문")
        };

        [MenuItem("Tools/DEAD LETTER/Art/Apply FBX Door Visuals")]
        public static void ApplyFromMenu()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            ApplyToLoadedScene(scene, true);
            EditorUtility.DisplayDialog(
                "DEAD LETTER",
                "원본 FBX의 문 메시 6개와 문 텍스처를 기존 기능성 문에 적용했습니다.\n" +
                "원본 FBX, 문 피벗, Collider와 상호작용 스크립트는 변경하지 않았습니다.",
                "확인");
        }

        [MenuItem("Tools/DEAD LETTER/Art/Validate FBX Door Visuals")]
        public static void ValidateFromMenu()
        {
            ValidateLoadedScene(true);
        }

        public static void ApplyBatch()
        {
            try
            {
                Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                ApplyToLoadedScene(scene, true);
                ValidateLoadedScene(false);
                Debug.Log("FINAL_MAP_DOOR_VISUAL_APPLY_SUCCESS");
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
                ValidateLoadedScene(false);
                Debug.Log("FINAL_MAP_DOOR_VISUAL_VALIDATION_SUCCESS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void ApplyToLoadedScene(Scene scene, bool saveScene)
        {
            if (!scene.IsValid())
                throw new InvalidOperationException("문 외형을 적용할 유효한 씬이 없습니다.");

            GameObject sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(SourceFbxPath);
            if (sourceModel == null)
                throw new FileNotFoundException("원본 최종 맵 FBX를 찾을 수 없습니다.", SourceFbxPath);

            Material doorMaterial = PrepareDoorMaterial();
            var sourceTransforms = sourceModel.GetComponentsInChildren<Transform>(true)
                .ToDictionary(value => value.name, value => value, StringComparer.Ordinal);

            foreach (DoorBinding binding in Bindings)
            {
                HingedDoor targetDoor = FindSceneComponents<HingedDoor>()
                    .FirstOrDefault(value => value.name == binding.TargetPivotName);
                if (targetDoor == null)
                    throw new InvalidOperationException($"기존 기능성 문을 찾지 못했습니다: {binding.TargetPivotName}");

                if (!sourceTransforms.TryGetValue(binding.SourceMeshName, out Transform sourceTransform))
                    throw new InvalidOperationException($"FBX 문 메시를 찾지 못했습니다: {binding.SourceMeshName}");

                MeshFilter sourceFilter = sourceTransform.GetComponent<MeshFilter>();
                Mesh sourceMesh = sourceFilter != null ? sourceFilter.sharedMesh : null;
                if (sourceMesh == null)
                    throw new InvalidOperationException($"FBX 문 MeshFilter가 없습니다: {binding.SourceMeshName}");
                if (sourceMesh.uv == null || sourceMesh.uv.Length == 0)
                    throw new InvalidOperationException($"FBX 문 메시의 UV가 없습니다: {binding.SourceMeshName}");

                BoxCollider leafCollider = targetDoor.GetComponentsInChildren<BoxCollider>(true)
                    .FirstOrDefault(value => value.name.EndsWith(" Leaf", StringComparison.Ordinal));
                if (leafCollider == null)
                    throw new InvalidOperationException($"문짝 Collider가 없습니다: {binding.TargetPivotName}");

                Transform leaf = leafCollider.transform;
                Transform previousVisual = leaf.Find(VisualName);
                if (previousVisual != null)
                    UnityEngine.Object.DestroyImmediate(previousVisual.gameObject);

                DisableLegacyDoorRenderers(targetDoor.transform);

                var visual = new GameObject(VisualName);
                Undo.RegisterCreatedObjectUndo(visual, "Apply FBX door visual");
                visual.transform.SetParent(leaf, false);

                MeshFilter filter = visual.AddComponent<MeshFilter>();
                filter.sharedMesh = sourceMesh;

                MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = Enumerable.Repeat(
                    doorMaterial,
                    Mathf.Max(1, sourceMesh.subMeshCount)).ToArray();
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;

                Vector3 sourceSize = sourceMesh.bounds.size;
                if (sourceSize.x <= Mathf.Epsilon || sourceSize.y <= Mathf.Epsilon || sourceSize.z <= Mathf.Epsilon)
                    throw new InvalidOperationException($"FBX 문 메시 Bounds가 비정상입니다: {binding.SourceMeshName}");

                Vector3 inverseSize = new(1f / sourceSize.x, 1f / sourceSize.y, 1f / sourceSize.z);
                bool targetWidthUsesX = Mathf.Abs(leaf.localScale.x) > Mathf.Abs(leaf.localScale.z);
                Quaternion fitRotation = targetWidthUsesX
                    ? Quaternion.Euler(0f, 90f, 0f)
                    : Quaternion.identity;

                visual.transform.localRotation = fitRotation;
                visual.transform.localScale = inverseSize;
                Vector3 normalizedCenter = Vector3.Scale(sourceMesh.bounds.center, inverseSize);
                visual.transform.localPosition = -(fitRotation * normalizedCenter);

                EditorUtility.SetDirty(leaf);
                EditorUtility.SetDirty(renderer);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (saveScene && !EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException($"문 외형이 적용된 씬을 저장하지 못했습니다: {ScenePath}");
        }

        private static void ValidateLoadedScene(bool showDialog)
        {
            var failures = new List<string>();
            Material expectedMaterial = AssetDatabase.LoadAssetAtPath<Material>(DoorMaterialPath);
            Texture expectedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(DoorTexturePath);

            if (expectedMaterial == null)
                failures.Add("문 URP 머티리얼이 없습니다.");
            if (expectedTexture == null)
                failures.Add("문 Base Map 텍스처가 없습니다.");
            if (expectedMaterial != null && expectedMaterial.GetTexture("_BaseMap") != expectedTexture)
                failures.Add("문 머티리얼 Base Map이 문 텍스처와 연결되지 않았습니다.");

            foreach (DoorBinding binding in Bindings)
            {
                HingedDoor door = FindSceneComponents<HingedDoor>()
                    .FirstOrDefault(value => value.name == binding.TargetPivotName);
                if (door == null)
                {
                    failures.Add($"기능성 문 누락: {binding.TargetPivotName}");
                    continue;
                }

                BoxCollider leafCollider = door.GetComponentsInChildren<BoxCollider>(true)
                    .FirstOrDefault(value => value.name.EndsWith(" Leaf", StringComparison.Ordinal));
                if (leafCollider == null)
                {
                    failures.Add($"문짝 Collider 누락: {binding.TargetPivotName}");
                    continue;
                }

                Transform visual = leafCollider.transform.Find(VisualName);
                MeshFilter filter = visual != null ? visual.GetComponent<MeshFilter>() : null;
                MeshRenderer renderer = visual != null ? visual.GetComponent<MeshRenderer>() : null;
                if (filter == null || renderer == null || filter.sharedMesh == null)
                {
                    failures.Add($"FBX 문 외형 누락: {binding.TargetPivotName}");
                    continue;
                }

                if (AssetDatabase.GetAssetPath(filter.sharedMesh) != SourceFbxPath)
                    failures.Add($"원본 FBX가 아닌 메시를 참조합니다: {binding.TargetPivotName}");
                if (filter.sharedMesh.uv == null || filter.sharedMesh.uv.Length == 0)
                    failures.Add($"문 UV 누락: {binding.TargetPivotName}");
                if (renderer.sharedMaterials.Length != filter.sharedMesh.subMeshCount ||
                    renderer.sharedMaterials.Any(value => value != expectedMaterial))
                {
                    failures.Add($"문 재질 슬롯 연결 오류: {binding.TargetPivotName}");
                }
                if (visual.GetComponentsInChildren<Collider>(true).Length != 0)
                    failures.Add($"시각 전용 문에 불필요한 Collider가 있습니다: {binding.TargetPivotName}");
                if (!door.enabled || !leafCollider.enabled)
                    failures.Add($"기존 문 기능 또는 Collider가 비활성화됨: {binding.TargetPivotName}");
            }

            if (failures.Count > 0)
                throw new InvalidOperationException("FBX 문 외형 검증 실패:\n- " + string.Join("\n- ", failures));

            const string success = "FBX 문 메시 6개, UV, URP 문 텍스처, 기존 피벗·Collider·상호작용 참조 검증 통과";
            Debug.Log("[FinalMapDoorValidation] " + success);
            if (showDialog)
                EditorUtility.DisplayDialog("DEAD LETTER", success, "확인");
        }

        private static Material PrepareDoorMaterial()
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(DoorTexturePath);
            if (texture == null)
                throw new FileNotFoundException("문 텍스처를 찾을 수 없습니다.", DoorTexturePath);

            Material material = AssetDatabase.LoadAssetAtPath<Material>(DoorMaterialPath);
            if (material == null)
                throw new FileNotFoundException("문 URP 머티리얼을 찾을 수 없습니다.", DoorMaterialPath);

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException("URP/Lit 셰이더를 찾을 수 없습니다.");

            material.shader = shader;
            material.SetTexture("_BaseMap", texture);
            material.SetTexture("_MainTex", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetColor("_Color", Color.white);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void DisableLegacyDoorRenderers(Transform doorRoot)
        {
            foreach (Renderer renderer in doorRoot.GetComponentsInChildren<Renderer>(true))
            {
                // The generated visual is a direct child of the existing leaf. During the
                // first installation it does not exist yet, so never call IsChildOf(null).
                if (renderer.transform.name == VisualName)
                    continue;

                string name = renderer.gameObject.name;
                if (name.EndsWith(" Leaf", StringComparison.Ordinal) ||
                    name.Contains(" Panel ", StringComparison.Ordinal) ||
                    name.Contains(" Handle", StringComparison.Ordinal) ||
                    name.Contains(" Hinge Pin", StringComparison.Ordinal))
                {
                    renderer.enabled = false;
                    EditorUtility.SetDirty(renderer);
                }
            }
        }

        private static T[] FindSceneComponents<T>() where T : Component =>
            Resources.FindObjectsOfTypeAll<T>()
                .Where(value => value != null && value.gameObject.scene.IsValid())
                .ToArray();

        private readonly struct DoorBinding
        {
            public DoorBinding(string targetPivotName, string sourceMeshName)
            {
                TargetPivotName = targetPivotName;
                SourceMeshName = sourceMeshName;
            }

            public string TargetPivotName { get; }
            public string SourceMeshName { get; }
        }
    }
}
