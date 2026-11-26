using System.Collections.Generic;
using System.Linq;
using DeadLetter.Characters;
using DeadLetter.Mail;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadLetter.EditorTools
{
    /// <summary>
    /// 1~3일차 손님 캐릭터(5명 x 3일)를 임포트 설정하고, 게임 씬의 손님 NPC에 연결한다.
    /// 메뉴: Tools/DEAD LETTER/Characters/Setup Customers (DAY 1-3)
    /// 여러 번 실행해도 안전하다 (그레이박스를 다시 생성한 뒤에도 이 메뉴만 다시 누르면 된다).
    /// </summary>
    public static class CustomerCharacterSetup
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string CustomerObjectName = "Test Mascot Customer";
        private const string CharacterRoot = "Assets/Art/Characters";

        private static readonly (string id, string name, float height)[] CustomerTypes =
        {
            ("mascot", "Mascot", 1.95f),                // 인형탈
            ("drunk_driver", "OfficeWorker", 1.75f),    // 직장인 (음주운전)
            ("model_student", "Student", 1.66f),        // 학생 (모범생)
            ("bad_construction", "Laborer", 1.74f),     // 노동자 (부실공사)
            ("troublemaker", "Drowned", 1.70f),         // 익사자
        };

        private struct CustomerAsset
        {
            public int day;
            public string id;
            public float height;
            public string modelPath;
            public string baseMapPath;
            public string closedMapPath;
        }

        private static IEnumerable<CustomerAsset> Customers()
        {
            for (int day = 1; day <= 3; day++)
            {
                foreach (var c in CustomerTypes)
                {
                    string folder = $"{CharacterRoot}/Day{day}";
                    yield return new CustomerAsset
                    {
                        day = day,
                        id = c.id,
                        height = c.height,
                        modelPath = $"{folder}/Day{day}_{c.name}.fbx",
                        baseMapPath = $"{folder}/Day{day}_{c.name}_BaseMap.png",
                        closedMapPath = $"{folder}/Day{day}_{c.name}_BaseMap_EyesClosed.png",
                    };
                }
            }
        }

        [MenuItem("Tools/DEAD LETTER/Characters/Setup Customers (DAY 1-3)")]
        public static void Setup()
        {
            List<string> report = new();

            // 1) FBX 임포트 설정
            foreach (CustomerAsset c in Customers())
            {
                string path = c.modelPath;
                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                {
                    report.Add($"[없음] {path}");
                    continue;
                }

                bool changed = false;
                if (!importer.isReadable) { importer.isReadable = true; changed = true; }
                if (importer.animationType != ModelImporterAnimationType.Human)
                {
                    importer.animationType = ModelImporterAnimationType.Human;
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    changed = true;
                }
                if (importer.materialImportMode == ModelImporterMaterialImportMode.None)
                {
                    importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                    changed = true;
                }
                if (changed)
                    importer.SaveAndReimport();

                Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
                if (avatar == null || !avatar.isValid || !avatar.isHuman)
                {
                    // 휴머노이드 자동 매핑 실패 → Generic으로 되돌림 (절차적 애니메이션은 뼈 이름으로 동작)
                    importer.animationType = ModelImporterAnimationType.Generic;
                    importer.SaveAndReimport();
                    report.Add($"[Generic] DAY{c.day} {c.id}: 휴머노이드 아바타 자동 생성 실패, Generic으로 임포트");
                }
                else
                {
                    report.Add($"[OK] DAY{c.day} {c.id}");
                }
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(c.baseMapPath) == null)
                    report.Add($"  [텍스처 없음] {c.baseMapPath}");
            }

            // 2) 씬의 손님 NPC에 연결
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            TestCustomerNPC npc = Object.FindObjectsByType<TestCustomerNPC>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(n => n.gameObject.name == CustomerObjectName)
                ?? Object.FindObjectsByType<TestCustomerNPC>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (npc == null)
            {
                EditorUtility.DisplayDialog("DAY 1-3 Customers", $"씬에서 TestCustomerNPC를 찾지 못했습니다.\n{ScenePath}", "확인");
                return;
            }

            CustomerVisualSet visualSet = npc.GetComponent<CustomerVisualSet>();
            if (visualSet == null)
                visualSet = Undo.AddComponent<CustomerVisualSet>(npc.gameObject);

            SerializedObject so = new SerializedObject(visualSet);
            SerializedProperty entries = so.FindProperty("entries");
            entries.ClearArray();
            int index = 0;
            foreach (CustomerAsset c in Customers())
            {
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(c.modelPath);
                if (model == null) continue;
                entries.InsertArrayElementAtIndex(index);
                SerializedProperty e = entries.GetArrayElementAtIndex(index);
                FillEntry(e, c, model);
                index++;
            }

            SerializedProperty parts = so.FindProperty("placeholderParts");
            parts.ClearArray();
            int p = 0;
            foreach (Transform child in npc.transform)
            {
                if (!child.name.StartsWith("Mascot ")) continue;
                parts.InsertArrayElementAtIndex(p);
                parts.GetArrayElementAtIndex(p).objectReferenceValue = child.gameObject;
                p++;
            }

            SerializedProperty facesNegZ = so.FindProperty("npcFacesNegativeZ");
            SerializedObject npcSo = new SerializedObject(npc);
            SerializedProperty yawOffset = npcSo.FindProperty("modelForwardYawOffset");
            if (yawOffset != null)
                facesNegZ.boolValue = Mathf.Abs(Mathf.DeltaAngle(yawOffset.floatValue, 180f)) < 90f;
            so.ApplyModifiedProperties();

            SerializedProperty visualRef = npcSo.FindProperty("visualSet");
            if (visualRef != null)
            {
                visualRef.objectReferenceValue = visualSet;
                npcSo.ApplyModifiedProperties();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            report.Add($"손님 NPC '{npc.name}'에 모델 {index}개, 그레이박스 부품 {p}개 연결");
            string text = string.Join("\n", report);
            Debug.Log("[CustomerCharacterSetup]\n" + text);
            EditorUtility.DisplayDialog("DAY 1-3 Customers", text, "확인");
        }

        private static void FillEntry(SerializedProperty e, CustomerAsset c, GameObject model)
        {
            e.FindPropertyRelative("day").intValue = c.day;
            e.FindPropertyRelative("customerId").stringValue = c.id;
            e.FindPropertyRelative("model").objectReferenceValue = model;
            e.FindPropertyRelative("baseMap").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(c.baseMapPath);
            e.FindPropertyRelative("eyesClosedMap").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(c.closedMapPath);
            e.FindPropertyRelative("height").floatValue = c.height;
            e.FindPropertyRelative("extraYaw").floatValue = 0f;
        }

        [MenuItem("Tools/DEAD LETTER/Characters/Preview Customers In Scene View")]
        public static void Preview()
        {
            // 씬 뷰에서 15명(5명 x 3일)을 줄지어 세워 크기/방향/텍스처를 확인한다.
            // 플레이 모드에서 실행하면 숨쉬기와 눈 깜빡임까지 볼 수 있다.
            GameObject root = new GameObject("Customer Preview (delete me)");
            Undo.RegisterCreatedObjectUndo(root, "Preview Customers");
            foreach (CustomerAsset c in Customers())
            {
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(c.modelPath);
                if (model == null) continue;
                GameObject holder = new GameObject($"DAY{c.day} {c.id}");
                holder.transform.SetParent(root.transform, false);
                int column = System.Array.FindIndex(CustomerTypes, t => t.id == c.id);
                holder.transform.localPosition = new Vector3(column * 1.2f, 0f, (c.day - 1) * 2f);
                holder.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                CustomerVisualSet set = holder.AddComponent<CustomerVisualSet>();
                SerializedObject so = new SerializedObject(set);
                SerializedProperty entries = so.FindProperty("entries");
                entries.InsertArrayElementAtIndex(0);
                FillEntry(entries.GetArrayElementAtIndex(0), c, model);
                so.FindProperty("autoDetectVisualFloor").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
                set.Show(c.day, c.id);
            }
            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.FrameSelected();
        }
    }
}
