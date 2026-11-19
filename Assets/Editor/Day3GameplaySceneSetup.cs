using System;
using DeadLetter.Mail;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadLetter.Editor
{
    public static class Day3GameplaySceneSetup
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string RootName = "DAY 3 Gameplay Objects";
        private const string ItemPrefabPath = "Assets/Day2/Prefabs/Day2_ItemPickup.prefab";

        [MenuItem("Tools/DEAD LETTER/DAY 3/Install Gameplay Items")]
        public static void InstallFromMenu()
        {
            Install();
            EditorUtility.DisplayDialog("DEAD LETTER", "DAY 3 물품 8개와 인형탈 풍선 연출을 SampleScene에 배치했습니다.", "확인");
        }

        public static void InstallBatch()
        {
            try
            {
                Install();
                Debug.Log("DAY3_GAMEPLAY_SCENE_SETUP_SUCCESS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        internal static void Install()
        {
            GameObject itemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ItemPrefabPath);
            if (itemPrefab == null)
                throw new InvalidOperationException($"DAY 2 공용 물품 프리팹을 찾을 수 없습니다: {ItemPrefabPath}");

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (UnityEngine.Object.FindAnyObjectByType<DayCycleManager>() == null)
                throw new InvalidOperationException("SampleScene에서 DayCycleManager를 찾을 수 없습니다.");

            GameObject previous = GameObject.Find(RootName);
            if (previous != null)
                UnityEngine.Object.DestroyImmediate(previous);

            GameObject root = new(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);

            // Dispatch items share the DAY 2 counter handoff flow. Only the current
            // customer's items are rendered, so these positions can safely reuse the counter.
            CreateItem(root.transform, itemPrefab, "Counter Driver License", "day3_drunk_driver_license",
                Day3RequestCatalog.DrunkDriverRequestId, Day2ItemType.DriverLicense, Day2ItemSource.Counter,
                new Vector3(-0.85f, 1.3f, -11.0f), new Vector3(0.34f, 0.035f, 0.22f));
            CreateItem(root.transform, itemPrefab, "Counter Troublemaker Soap", "day3_troublemaker_soap",
                Day3RequestCatalog.TroublemakerRequestId, Day2ItemType.Soap, Day2ItemSource.Counter,
                new Vector3(-0.35f, 1.3f, -11.0f), new Vector3(0.24f, 0.12f, 0.16f));
            CreateItem(root.transform, itemPrefab, "Counter Troublemaker Bandage", "day3_troublemaker_bandage",
                Day3RequestCatalog.TroublemakerRequestId, Day2ItemType.Bandage, Day2ItemSource.Counter,
                new Vector3(0.05f, 1.3f, -11.0f), new Vector3(0.2f, 0.16f, 0.2f));

            // The model student's pair is hidden until the linked troublemaker dispatch
            // is completed. It then becomes the storage-side handoff inventory.
            CreateItem(root.transform, itemPrefab, "Storage Linked Soap", "day3_model_student_soap",
                Day3RequestCatalog.ModelStudentRequestId, Day2ItemType.Soap, Day2ItemSource.Storage,
                new Vector3(11.8f, 1.15f, -2.75f), new Vector3(0.24f, 0.12f, 0.16f));
            CreateItem(root.transform, itemPrefab, "Storage Linked Bandage", "day3_model_student_bandage",
                Day3RequestCatalog.ModelStudentRequestId, Day2ItemType.Bandage, Day2ItemSource.Storage,
                new Vector3(12.35f, 1.15f, -2.75f), new Vector3(0.2f, 0.16f, 0.2f));
            CreateItem(root.transform, itemPrefab, "Storage Soju", "day3_bad_construction_soju",
                Day3RequestCatalog.BadConstructionRequestId, Day2ItemType.Soju, Day2ItemSource.Storage,
                new Vector3(12.5f, 1.2f, -2.75f), new Vector3(0.16f, 0.45f, 0.16f));
            CreateItem(root.transform, itemPrefab, "Storage Dried Snack", "day3_bad_construction_dried_snack",
                Day3RequestCatalog.BadConstructionRequestId, Day2ItemType.DriedSnack, Day2ItemSource.Storage,
                new Vector3(13.4f, 1.1f, -2.75f), new Vector3(0.38f, 0.12f, 0.28f));
            CreateItem(root.transform, itemPrefab, "Storage Unknown Cargo", "day3_mascot_unknown_cargo",
                Day3RequestCatalog.MascotRequestId, Day2ItemType.UnknownCargo, Day2ItemSource.Storage,
                new Vector3(14.25f, 1.12f, -2.75f), new Vector3(0.46f, 0.3f, 0.36f));

            TestCustomerNPC customer = UnityEngine.Object.FindAnyObjectByType<TestCustomerNPC>(FindObjectsInactive.Include);
            if (customer == null)
                throw new InvalidOperationException("SampleScene에서 민원인 오브젝트를 찾을 수 없습니다.");
            if (customer.GetComponent<Day3MascotBalloonSequence>() == null)
                customer.gameObject.AddComponent<Day3MascotBalloonSequence>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void CreateItem(
            Transform parent,
            GameObject prefab,
            string name,
            string instanceId,
            string requestId,
            Day2ItemType item,
            Day2ItemSource source,
            Vector3 position,
            Vector3 scale)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance == null)
                throw new InvalidOperationException($"DAY 3 물품 프리팹 생성 실패: {name}");

            instance.name = name;
            instance.transform.SetPositionAndRotation(position, Quaternion.identity);
            instance.transform.localScale = scale;
            Day2WorldItem component = instance.GetComponent<Day2WorldItem>();
            if (component == null)
                throw new InvalidOperationException($"DAY 3 물품에 Day2WorldItem이 없습니다: {name}");
            component.Configure(instanceId, requestId, item, source);
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
        }
    }
}
