using System;
using DeadLetter.Mail;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadLetter.Editor
{
    public static class Day2GameplaySceneSetup
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string RootName = "DAY 2 Gameplay Objects";
        private const string BaseFolder = "Assets/Day2";
        private const string PrefabFolder = BaseFolder + "/Prefabs";
        private const string MaterialFolder = BaseFolder + "/Materials";

        [MenuItem("Tools/DEAD LETTER/DAY 2/Install Gameplay Objects")]
        public static void InstallFromMenu()
        {
            Install();
            EditorUtility.DisplayDialog("DEAD LETTER", "DAY 2 임시 프리팹과 월드 배치를 적용했습니다.", "확인");
        }

        public static void InstallBatch()
        {
            try
            {
                Install();
                Debug.Log("DAY2_GAMEPLAY_SCENE_SETUP_SUCCESS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void Install()
        {
            EnsureFolder("Assets", "Day2");
            EnsureFolder(BaseFolder, "Prefabs");
            EnsureFolder(BaseFolder, "Materials");

            Material itemMaterial = EnsureMaterial("Day2_Item_Temp", new Color(0.36f, 0.52f, 0.42f));
            Material boxMaterial = EnsureMaterial("Day2_Box_Temp", new Color(0.48f, 0.28f, 0.12f));
            Material stationMaterial = EnsureMaterial("Day2_Station_Temp", new Color(0.16f, 0.22f, 0.26f));
            Material stampMaterial = EnsureMaterial("Day2_Stamp_Temp", new Color(0.62f, 0.24f, 0.12f));
            Material markMaterial = EnsureMaterial("Day2_Mark_Temp", new Color(0.92f, 0.52f, 0.12f));

            GameObject itemPrefab = EnsureItemPrefab(itemMaterial);
            GameObject boxPrefab = EnsureBoxPrefab(boxMaterial);
            GameObject packingPrefab = EnsurePackingPrefab(stationMaterial);
            GameObject stampPrefab = EnsureStampPrefab(stampMaterial);
            GameObject packedBoxPrefab = EnsurePackedBoxPrefab(boxMaterial, markMaterial);
            GameObject conveyorPrefab = EnsureConveyorPrefab(stationMaterial);

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            DayCycleManager dayCycle = UnityEngine.Object.FindAnyObjectByType<DayCycleManager>();
            if (dayCycle == null)
                throw new InvalidOperationException("SampleScene에서 DayCycleManager를 찾을 수 없습니다.");

            GameObject previous = GameObject.Find(RootName);
            if (previous != null)
                UnityEngine.Object.DestroyImmediate(previous);

            GameObject root = new(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<Day2InteractionFeedback>();

            CreateItem(root.transform, itemPrefab, "Counter Cigarettes", "day2_mascot_cigarettes",
                Day2RequestCatalog.MascotRequestId, Day2ItemType.Cigarettes, Day2ItemSource.Counter,
                new Vector3(-1.15f, 1.3f, -11.0f), new Vector3(0.34f, 0.12f, 0.22f));
            CreateItem(root.transform, itemPrefab, "Counter Lighter", "day2_mascot_lighter",
                Day2RequestCatalog.MascotRequestId, Day2ItemType.Lighter, Day2ItemSource.Counter,
                new Vector3(-0.7f, 1.3f, -11.0f), new Vector3(0.12f, 0.28f, 0.1f));
            CreateItem(root.transform, itemPrefab, "Counter Student Uniform", "day2_model_uniform",
                Day2RequestCatalog.ModelStudentRequestId, Day2ItemType.BulliedUniform, Day2ItemSource.Counter,
                new Vector3(0.35f, 1.3f, -11.0f), new Vector3(0.72f, 0.12f, 0.5f));

            CreateItem(root.transform, itemPrefab, "Storage Hangover Remedy", "day2_drunk_remedy",
                Day2RequestCatalog.DrunkDriverRequestId, Day2ItemType.HangoverRemedy, Day2ItemSource.Storage,
                new Vector3(-3.45f, 1.15f, 11.2f), new Vector3(0.2f, 0.42f, 0.2f));
            CreateItem(root.transform, itemPrefab, "Storage Safety Helmet", "day2_construction_helmet",
                Day2RequestCatalog.BadConstructionRequestId, Day2ItemType.SafetyHelmet, Day2ItemSource.Storage,
                new Vector3(3.45f, 1.2f, 11.2f), new Vector3(0.46f, 0.34f, 0.46f));
            CreateItem(root.transform, itemPrefab, "Storage Insulated Gloves", "day2_construction_gloves",
                Day2RequestCatalog.BadConstructionRequestId, Day2ItemType.InsulatedGloves, Day2ItemSource.Storage,
                new Vector3(3.45f, 0.7f, 12.0f), new Vector3(0.45f, 0.16f, 0.34f));
            CreateItem(root.transform, itemPrefab, "Storage Linked Uniform", "day2_troublemaker_uniform",
                Day2RequestCatalog.TroublemakerRequestId, Day2ItemType.BulliedUniform, Day2ItemSource.Storage,
                new Vector3(0f, 1.45f, 15.1f), new Vector3(0.72f, 0.12f, 0.5f));

            Vector3[] boxPositions =
            {
                new(-2.4f, 0.36f, 9.55f), new(-1.2f, 0.36f, 9.55f), new(0f, 0.36f, 9.55f),
                new(1.2f, 0.36f, 9.55f), new(2.4f, 0.36f, 9.55f)
            };
            for (int index = 0; index < boxPositions.Length; index++)
                CreateBox(root.transform, boxPrefab, $"Empty Box {index + 1}", $"day2_empty_box_{index + 1}", boxPositions[index]);

            CreatePrefabInstance(root.transform, packingPrefab, "DAY2 Packing Station", new Vector3(10.25f, 1.16f, -3.03f));
            CreatePrefabInstance(root.transform, packedBoxPrefab, "DAY2 Packed Box", new Vector3(11.2f, 1.32f, -3.62f));

            LetterStamp[] pictograms =
            {
                LetterStamp.Fire, LetterStamp.Water, LetterStamp.Electricity,
                LetterStamp.Shock, LetterStamp.Medicine, LetterStamp.Alcohol
            };
            for (int index = 0; index < pictograms.Length; index++)
            {
                GameObject tool = CreatePrefabInstance(root.transform, stampPrefab,
                    $"Box Stamp {Day2Labels.GetStampLabel(pictograms[index])}",
                    new Vector3(8.85f + index * 0.34f, 1.22f, -3.12f));
                Day2BoxStampTool component = tool.GetComponent<Day2BoxStampTool>();
                component.Configure(pictograms[index]);
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }

            CreatePrefabInstance(root.transform, conveyorPrefab, "DAY2 Conveyor Terminal", new Vector3(14.55f, 1.05f, -1.15f));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static GameObject EnsureItemPrefab(Material material)
        {
            GameObject root = CreateCube("DAY2 Item Pickup", new Vector3(0.3f, 0.3f, 0.3f), material);
            root.AddComponent<Day2WorldItem>();
            return SavePrefab(root, PrefabFolder + "/Day2_ItemPickup.prefab");
        }

        private static GameObject EnsureBoxPrefab(Material material)
        {
            GameObject root = CreateCube("DAY2 Empty Box", new Vector3(0.82f, 0.56f, 0.68f), material);
            root.AddComponent<Day2EmptyBox>();
            return SavePrefab(root, PrefabFolder + "/Day2_EmptyBox.prefab");
        }

        private static GameObject EnsurePackingPrefab(Material material)
        {
            GameObject root = CreateCube("DAY2 Packing Station", new Vector3(0.85f, 0.18f, 0.7f), material);
            root.AddComponent<Day2PackingStation>();
            return SavePrefab(root, PrefabFolder + "/Day2_PackingStation.prefab");
        }

        private static GameObject EnsureStampPrefab(Material material)
        {
            GameObject root = CreateCube("DAY2 Box Stamp Tool", new Vector3(0.22f, 0.28f, 0.22f), material);
            root.AddComponent<Day2BoxStampTool>();
            return SavePrefab(root, PrefabFolder + "/Day2_BoxStampTool.prefab");
        }

        private static GameObject EnsurePackedBoxPrefab(Material boxMaterial, Material markMaterial)
        {
            GameObject root = CreateCube("DAY2 Packed Box", new Vector3(0.82f, 0.56f, 0.68f), boxMaterial);
            GameObject[] marks = new GameObject[6];
            for (int index = 0; index < marks.Length; index++)
            {
                marks[index] = CreateCube($"Pictogram Mark {index + 1}", new Vector3(0.12f, 0.018f, 0.12f), markMaterial);
                UnityEngine.Object.DestroyImmediate(marks[index].GetComponent<Collider>());
                marks[index].transform.SetParent(root.transform, false);
                marks[index].transform.localPosition = new Vector3(-0.3f + index * 0.12f, 0.51f, 0f);
                marks[index].SetActive(false);
            }
            Day2PackedBox component = root.AddComponent<Day2PackedBox>();
            component.ConfigureMarks(marks[0], marks[1], marks[2], marks[3], marks[4], marks[5]);
            return SavePrefab(root, PrefabFolder + "/Day2_PackedBox.prefab");
        }

        private static GameObject EnsureConveyorPrefab(Material material)
        {
            GameObject root = CreateCube("DAY2 Conveyor Terminal", new Vector3(0.7f, 0.78f, 0.42f), material);
            root.AddComponent<Day2ConveyorTerminal>();
            return SavePrefab(root, PrefabFolder + "/Day2_ConveyorTerminal.prefab");
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
            GameObject instance = CreatePrefabInstance(parent, prefab, name, position);
            instance.transform.localScale = scale;
            Day2WorldItem component = instance.GetComponent<Day2WorldItem>();
            component.Configure(instanceId, requestId, item, source);
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
        }

        private static void CreateBox(Transform parent, GameObject prefab, string name, string boxId, Vector3 position)
        {
            GameObject instance = CreatePrefabInstance(parent, prefab, name, position);
            Day2EmptyBox component = instance.GetComponent<Day2EmptyBox>();
            component.Configure(boxId);
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }

        private static GameObject CreatePrefabInstance(Transform parent, GameObject prefab, string name, Vector3 position)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance == null)
                throw new InvalidOperationException($"프리팹 인스턴스 생성 실패: {prefab.name}");
            instance.name = name;
            instance.transform.position = position;
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            return instance;
        }

        private static GameObject CreateCube(string name, Vector3 scale, Material material)
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = name;
            root.transform.localScale = scale;
            root.GetComponent<Renderer>().sharedMaterial = material;
            return root;
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            if (prefab == null)
                throw new InvalidOperationException($"프리팹 저장 실패: {path}");
            return prefab;
        }

        private static Material EnsureMaterial(string name, Color color)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException("URP/Lit 셰이더를 찾을 수 없습니다.");
            material = new Material(shader) { name = name, color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
