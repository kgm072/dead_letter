using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DeadLetter.Mail;

namespace DeadLetter.Editor
{
    public static class FinalMapAnalyzer
    {
        private const string ModelPath = "Assets/Art/Models/DeadLetter_FinalMap.fbx";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Tools/DEAD LETTER/Art/Analyze Final Map")]
        public static void AnalyzeFromMenu()
        {
            Analyze();
            EditorUtility.DisplayDialog("DEAD LETTER", "최종 맵 FBX 분석이 Console에 기록되었습니다.", "확인");
        }

        public static void AnalyzeBatch()
        {
            try
            {
                Analyze();
                Debug.Log("FINAL_MAP_ANALYSIS_SUCCESS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void Analyze()
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
                throw new InvalidOperationException($"FBX를 불러올 수 없습니다: {ModelPath}");

            ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            Debug.Log(
                $"[FinalMap] importer scale={importer?.globalScale:F4}, useFileScale={importer?.useFileScale}, " +
                $"unitConversion={importer?.useFileUnits}, materialMode={importer?.materialImportMode}");

            Mesh[] meshes = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Mesh>().ToArray();
            Material[] materials = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Material>().ToArray();
            Debug.Log($"[FinalMap] meshCount={meshes.Length}, materialCount={materials.Length}");
            foreach (Mesh mesh in meshes.OrderBy(value => value.name, StringComparer.Ordinal))
            {
                Debug.Log(
                    $"[FinalMapMesh] {mesh.name} vertices={mesh.vertexCount} subMeshes={mesh.subMeshCount} " +
                    $"localBoundsCenter={Format(mesh.bounds.center)} localBoundsSize={Format(mesh.bounds.size)}");
            }

            foreach (Material material in materials.OrderBy(value => value.name, StringComparer.Ordinal))
            {
                string texture = material.mainTexture != null
                    ? AssetDatabase.GetAssetPath(material.mainTexture)
                    : "<missing>";
                Debug.Log(
                    $"[FinalMapMaterial] {material.name} shader={material.shader?.name ?? "<none>"} " +
                    $"mainTexture={texture}");
            }

            GameObject instance = UnityEngine.Object.Instantiate(model);
            instance.name = "FINAL MAP ANALYSIS INSTANCE";
            try
            {
                Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
                Bounds total = CalculateBounds(renderers);
                Debug.Log(
                    $"[FinalMap] rendererCount={renderers.Length}, worldBoundsCenter={Format(total.center)}, " +
                    $"worldBoundsSize={Format(total.size)}");
                foreach (Renderer renderer in renderers.OrderBy(value => GetPath(value.transform), StringComparer.Ordinal))
                {
                    Debug.Log(
                        $"[FinalMapRenderer] {GetPath(renderer.transform)} position={Format(renderer.transform.position)} " +
                        $"rotation={Format(renderer.transform.eulerAngles)} scale={Format(renderer.transform.lossyScale)} " +
                        $"boundsCenter={Format(renderer.bounds.center)} boundsSize={Format(renderer.bounds.size)} " +
                        $"boundsMin={Format(renderer.bounds.min)} boundsMax={Format(renderer.bounds.max)} materials=" +
                        string.Join(",", renderer.sharedMaterials.Select(value => value != null ? value.name : "<null>")));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject graybox = GameObject.Find("DEAD LETTER - Graybox");
            if (graybox == null)
                throw new InvalidOperationException("SampleScene의 기존 Graybox 루트를 찾을 수 없습니다.");
            Renderer[] currentRenderers = graybox.GetComponentsInChildren<Renderer>(true);
            Bounds currentBounds = CalculateBounds(currentRenderers);
            Debug.Log(
                $"[CurrentMap] rendererCount={currentRenderers.Length}, worldBoundsCenter={Format(currentBounds.center)}, " +
                $"worldBoundsSize={Format(currentBounds.size)}");

            string[] referenceNames =
            {
                "Counter Floor",
                "Storage Floor",
                "Pantry Floor",
                "Workroom Floor",
                "Hidden Room Floor",
                "Counter Hall Door Leaf",
                "Storage Hall Door Leaf",
                "Pantry Hall Door Leaf",
                "Workroom Hall Door Leaf",
                "Hidden Room Door Leaf",
                "Storage Rear Shelves",
                "Storage East Shelves",
                "Storage West Shelves",
                "Storage Production Art",
                "DAY 2 Gameplay Objects"
            };
            foreach (string referenceName in referenceNames)
            {
                GameObject reference = FindSceneObject(referenceName);
                if (reference == null)
                {
                    Debug.Log($"[CurrentReference] {referenceName} <missing>");
                    continue;
                }

                Renderer[] referenceRenderers = reference.GetComponentsInChildren<Renderer>(true);
                Collider[] referenceColliders = reference.GetComponentsInChildren<Collider>(true);
                Bounds referenceBounds = CalculateBounds(referenceRenderers);
                Debug.Log(
                    $"[CurrentReference] {referenceName} path={GetPath(reference.transform)} " +
                    $"position={Format(reference.transform.position)} rotation={Format(reference.transform.eulerAngles)} " +
                    $"scale={Format(reference.transform.lossyScale)} renderers={referenceRenderers.Length} " +
                    $"colliders={referenceColliders.Length} boundsCenter={Format(referenceBounds.center)} " +
                    $"boundsSize={Format(referenceBounds.size)} active={reference.activeInHierarchy}");
            }

            foreach (Day2WorldItem item in Resources.FindObjectsOfTypeAll<Day2WorldItem>()
                         .Where(value => value.gameObject.scene.IsValid())
                         .OrderBy(value => value.name, StringComparer.Ordinal))
            {
                Collider[] colliders = item.GetComponentsInChildren<Collider>(true);
                Renderer[] renderers = item.GetComponentsInChildren<Renderer>(true);
                Debug.Log(
                    $"[StorageItem] {item.name} instance={item.InstanceId} request={item.RequestId} " +
                    $"type={item.ItemType} source={item.Source} path={GetPath(item.transform)} " +
                    $"position={Format(item.transform.position)} scale={Format(item.transform.lossyScale)} " +
                    $"renderers={renderers.Length} colliders={colliders.Length} " +
                    $"boundsCenter={Format(CalculateBounds(renderers).center)} boundsSize={Format(CalculateBounds(renderers).size)}");
            }

            foreach (Day2EmptyBox box in Resources.FindObjectsOfTypeAll<Day2EmptyBox>()
                         .Where(value => value.gameObject.scene.IsValid())
                         .OrderBy(value => value.name, StringComparer.Ordinal))
            {
                Collider[] colliders = box.GetComponentsInChildren<Collider>(true);
                Renderer[] renderers = box.GetComponentsInChildren<Renderer>(true);
                Debug.Log(
                    $"[StorageBox] {box.name} id={box.BoxId} path={GetPath(box.transform)} " +
                    $"position={Format(box.transform.position)} scale={Format(box.transform.lossyScale)} " +
                    $"renderers={renderers.Length} colliders={colliders.Length} " +
                    $"boundsCenter={Format(CalculateBounds(renderers).center)} boundsSize={Format(CalculateBounds(renderers).size)}");
            }

            string[] dependencies = AssetDatabase.GetDependencies(ModelPath, true);
            Debug.Log("[FinalMapDependencies] " + string.Join(" | ", dependencies));
        }

        private static Bounds CalculateBounds(Renderer[] renderers)
        {
            if (renderers == null || renderers.Length == 0)
                return new Bounds(Vector3.zero, Vector3.zero);
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);
            return bounds;
        }

        private static GameObject FindSceneObject(string objectName)
        {
            return Resources.FindObjectsOfTypeAll<GameObject>()
                .FirstOrDefault(value => value.scene.IsValid() && value.name == objectName);
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

        private static string Format(Vector3 value)
        {
            return $"({value.x:F3},{value.y:F3},{value.z:F3})";
        }
    }
}
