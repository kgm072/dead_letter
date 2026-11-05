using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DeadLetter.Interaction;
using DeadLetter.Mail;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadLetter.Editor
{
    public static class FbxArtIntegrator
    {
        private const string ModelsFolder = "Assets/Art/Models";
        private const string TexturesFolder = "Assets/Art/Textures";
        private const string MaterialsFolder = "Assets/Art/Materials";
        private const string PrefabsFolder = "Assets/Art/Prefabs";
        private const string ArtRootName = "Production Room Art";

        private readonly struct ArtSpec
        {
            public ArtSpec(string name, float importScale)
            {
                Name = name;
                ImportScale = importScale;
            }

            public string Name { get; }
            public float ImportScale { get; }
            public string ModelPath => $"{ModelsFolder}/{Name}.fbx";
            public string TexturePath => $"{TexturesFolder}/{Name}_BaseColor.png";
            public string MaterialPath => $"{MaterialsFolder}/{Name}_URP.mat";
            public string PrefabPath => $"{PrefabsFolder}/{Name}_Art.prefab";
        }

        private static readonly ArtSpec[] ArtSpecs =
        {
            new ArtSpec("작업실", 0.1f),
            new ArtSpec("창구", 0.1f),
            new ArtSpec("창고", 0.2f),
            new ArtSpec("탕비실", 0.1f),
            new ArtSpec("히든방", 0.1f)
        };

        [MenuItem("Tools/DEAD LETTER/Art/Prepare Prefabs and Apply to Scene")]
        public static void PrepareAndApplyToScene()
        {
            PrepareArtAssets();
            GameObject mapRoot = GameObject.Find("DEAD LETTER - Graybox");
            if (mapRoot == null)
            {
                Debug.LogError("The generated DEAD LETTER map root was not found.");
                return;
            }

            LetterTaskState taskState = mapRoot.GetComponentInChildren<LetterTaskState>(true);
            DayCycleManager dayCycle = mapRoot.GetComponentInChildren<DayCycleManager>(true);
            ApplyToGeneratedMap(mapRoot.transform, taskState, dayCycle);
            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Production FBX art was prepared and applied to the active scene.");
        }

        [MenuItem("Tools/DEAD LETTER/Art/Test View/Counter")]
        private static void ViewCounter() => TeleportPlayer(new Vector3(0f, 0.05f, -10.1f), 180f);

        [MenuItem("Tools/DEAD LETTER/Art/Test View/Workshop")]
        private static void ViewWorkshop() => TeleportPlayer(new Vector3(8.3f, 0.05f, 0f), 90f);

        [MenuItem("Tools/DEAD LETTER/Art/Test View/Pantry")]
        private static void ViewPantry() => TeleportPlayer(new Vector3(-8.3f, 0.05f, 0f), -90f);

        [MenuItem("Tools/DEAD LETTER/Art/Test View/Storage")]
        private static void ViewStorage() => TeleportPlayer(new Vector3(0f, 0.05f, 8.3f), 0f);

        [MenuItem("Tools/DEAD LETTER/Art/Test View/Hidden Room")]
        private static void ViewHiddenRoom() => TeleportPlayer(new Vector3(-4.1f, 0.05f, 7f), -90f);

        private static void TeleportPlayer(Vector3 position, float yaw)
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("Enter Play Mode before using an art test view.");
                return;
            }

            GameObject player = GameObject.Find("Player Controller");
            if (player == null)
                return;

            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller != null)
                controller.enabled = false;
            player.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            if (controller != null)
                controller.enabled = true;
        }

        public static void ApplyToGeneratedMap(Transform mapRoot, LetterTaskState taskState, DayCycleManager dayCycle)
        {
            if (mapRoot == null)
                return;

            PrepareArtAssets();
            Transform previousArt = mapRoot.Find(ArtRootName);
            if (previousArt != null)
                Object.DestroyImmediate(previousArt.gameObject);

            GameObject artRoot = new GameObject(ArtRootName);
            artRoot.transform.SetParent(mapRoot, false);

            GameObject counterArt = InstantiateArt(artRoot.transform, "Counter Production Art", FindSpec("창구"), new Vector3(0f, 0f, -11.5f), 0f);
            GameObject workshopArt = InstantiateArt(artRoot.transform, "Workshop Production Art", FindSpec("작업실"), new Vector3(12.32f, 0f, 0.35f), 0f);
            GameObject pantryArt = InstantiateArt(artRoot.transform, "Pantry Production Art", FindSpec("탕비실"), new Vector3(-12.17f, 0f, 0.1f), 0f);
            GameObject hiddenArt = InstantiateArt(artRoot.transform, "Hidden Room Production Art", FindSpec("히든방"), new Vector3(-7.6f, 0f, 7f), 90f);

            CreateStorageShelves(artRoot.transform);
            ConfigureWorkstation(workshopArt, mapRoot, taskState);
            ConfigureDaySofa(pantryArt, mapRoot, taskState, dayCycle);
            DisableLegacyFurniture(mapRoot);

            if (counterArt == null || workshopArt == null || pantryArt == null || hiddenArt == null)
                Debug.LogError("One or more room art prefabs could not be instantiated.");
        }

        private static void PrepareArtAssets()
        {
            EnsureAssetFolder(MaterialsFolder);
            EnsureAssetFolder(PrefabsFolder);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            foreach (ArtSpec spec in ArtSpecs)
            {
                ConfigureModelImporter(spec);
                ConfigureTextureImporter(spec);
                Material material = CreateOrUpdateMaterial(spec);
                CreateOrUpdatePrefab(spec, material);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void ConfigureModelImporter(ArtSpec spec)
        {
            if (AssetImporter.GetAtPath(spec.ModelPath) is not ModelImporter importer)
            {
                Debug.LogError($"Missing FBX model: {spec.ModelPath}");
                return;
            }

            bool changed = false;
            if (!Mathf.Approximately(importer.globalScale, spec.ImportScale))
            {
                importer.globalScale = spec.ImportScale;
                changed = true;
            }
            if (importer.importAnimation)
            {
                importer.importAnimation = false;
                changed = true;
            }
            if (importer.materialImportMode != ModelImporterMaterialImportMode.None)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                changed = true;
            }
            if (!importer.preserveHierarchy)
            {
                importer.preserveHierarchy = true;
                changed = true;
            }

            if (changed)
                importer.SaveAndReimport();
        }

        private static void ConfigureTextureImporter(ArtSpec spec)
        {
            if (AssetImporter.GetAtPath(spec.TexturePath) is not TextureImporter importer)
            {
                Debug.LogError($"Missing texture: {spec.TexturePath}");
                return;
            }

            bool changed = !importer.sRGBTexture || !importer.mipmapEnabled || importer.maxTextureSize != 2048 || !importer.alphaIsTransparency;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 2048;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            if (changed)
                importer.SaveAndReimport();
        }

        private static Material CreateOrUpdateMaterial(ArtSpec spec)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("URP/Lit shader was not found.");
                return null;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(spec.MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = $"{spec.Name}_URP" };
                AssetDatabase.CreateAsset(material, spec.MaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(spec.TexturePath);
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Metallic", spec.Name == "창고" ? 0.35f : 0.05f);
            material.SetFloat("_Smoothness", spec.Name == "창고" ? 0.28f : 0.18f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void CreateOrUpdatePrefab(ArtSpec spec, Material material)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(spec.ModelPath);
            if (model == null || material == null)
                return;

            GameObject wrapper = new GameObject($"{spec.Name} Art Prefab");
            GameObject content = new GameObject($"{spec.Name} Art Content");
            content.transform.SetParent(wrapper.transform, false);
            GameObject instance = PrefabUtility.InstantiatePrefab(model) as GameObject;
            if (instance == null)
            {
                Object.DestroyImmediate(wrapper);
                return;
            }

            instance.transform.SetParent(content.transform, false);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                Material[] materials = renderer.sharedMaterials;
                for (int index = 0; index < materials.Length; index++)
                    materials[index] = material;
                renderer.sharedMaterials = materials;
                ResizeRendererToTarget(spec.Name, renderer, content.transform);
            }

            Bounds bounds = CalculateBounds(renderers);
            content.transform.position += new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            foreach (Renderer renderer in renderers)
            {
                string objectName = renderer.gameObject.name;
                ArtInteractionRole role = GetInteractionRole(objectName);
                if (ShouldHaveCollider(spec.Name, objectName) || role != ArtInteractionRole.None)
                    CreateColliderProxy(wrapper.transform, renderer, role);
            }
            wrapper.name = $"{spec.Name} Art Prefab";
            PrefabUtility.SaveAsPrefabAsset(wrapper, spec.PrefabPath);
            Object.DestroyImmediate(wrapper);
        }

        private static void CreateColliderProxy(Transform wrapper, Renderer renderer, ArtInteractionRole role)
        {
            GameObject proxy = new GameObject($"Collider - {renderer.gameObject.name}");
            proxy.transform.SetParent(wrapper, false);
            proxy.transform.position = renderer.bounds.center;
            BoxCollider collider = proxy.AddComponent<BoxCollider>();
            collider.center = Vector3.zero;
            collider.size = renderer.bounds.size;
            if (role != ArtInteractionRole.None)
            {
                ArtInteractionMarker marker = proxy.AddComponent<ArtInteractionMarker>();
                marker.Role = role;
            }
        }

        private static bool ShouldHaveCollider(string modelName, string objectName)
        {
            if (modelName == "창고")
                return true;

            string[] largeFurniture = { "책상", "상자", "콘베이어", "냉장고", "선빈", "소파", "탁자" };
            return largeFurniture.Any(objectName.Contains);
        }

        private static void ResizeRendererToTarget(string modelName, Renderer renderer, Transform wrapper)
        {
            Vector3 target = GetTargetSize(modelName, renderer.gameObject.name);
            if (target == Vector3.zero)
                return;

            Bounds current = renderer.bounds;
            if (current.size.x < 0.0001f || current.size.y < 0.0001f || current.size.z < 0.0001f)
                return;

            bool longSideIsX = current.size.x >= current.size.z;
            float targetX = longSideIsX ? target.x : target.z;
            float targetZ = longSideIsX ? target.z : target.x;

            GameObject sizeRoot = new GameObject($"Size - {renderer.gameObject.name}");
            sizeRoot.transform.SetParent(wrapper, false);
            sizeRoot.transform.position = new Vector3(current.center.x, current.min.y, current.center.z);
            renderer.transform.SetParent(sizeRoot.transform, true);
            sizeRoot.transform.localScale = new Vector3(
                targetX / current.size.x,
                target.y / current.size.y,
                targetZ / current.size.z);
        }

        // x = longest horizontal side, y = height, z = shorter horizontal side.
        private static Vector3 GetTargetSize(string modelName, string objectName)
        {
            if (modelName == "창구")
            {
                if (objectName.Contains("책상")) return new Vector3(2.4f, 1.05f, 1f);
                if (objectName.Contains("녹음기")) return new Vector3(0.36f, 0.24f, 0.22f);
                if (objectName.Contains("시계")) return new Vector3(0.62f, 0.62f, 0.08f);
                if (objectName.Contains("액자")) return new Vector3(0.9f, 1.2f, 0.06f);
            }
            else if (modelName == "작업실")
            {
                if (objectName.Contains("책상")) return new Vector3(1.8f, 0.78f, 0.85f);
                if (objectName.Contains("콘베이어")) return new Vector3(4f, 0.75f, 1.1f);
                if (objectName.Contains("상자")) return new Vector3(2.4f, 0.45f, 1.2f);
                if (objectName.Contains("도장")) return new Vector3(0.15f, 0.22f, 0.15f);
                if (objectName.Contains("테이프")) return new Vector3(0.25f, 0.12f, 0.18f);
                if (objectName.Contains("우표")) return new Vector3(0.45f, 0.015f, 0.35f);
            }
            else if (modelName == "탕비실")
            {
                if (objectName.Contains("냉장고")) return new Vector3(0.9f, 1.85f, 0.8f);
                if (objectName.Contains("선빈")) return new Vector3(2.8f, 2.05f, 0.45f);
                if (objectName.Contains("소파")) return new Vector3(2f, 0.9f, 0.9f);
                if (objectName.Contains("탁자")) return new Vector3(1.8f, 0.75f, 0.85f);
            }
            else if (modelName == "히든방")
            {
                if (objectName.Contains("책상")) return new Vector3(1.7f, 0.75f, 0.8f);
                if (objectName.Contains("메모지")) return new Vector3(0.3f, 0.015f, 0.2f);
            }
            else if (modelName == "창고")
            {
                return new Vector3(7.2f, 2.1f, 5.4f);
            }

            return Vector3.zero;
        }

        private static ArtInteractionRole GetInteractionRole(string objectName)
        {
            if (objectName.Contains("우표") || objectName.Contains("도장"))
                return ArtInteractionRole.Stamp;
            if (objectName.Contains("테이프"))
                return ArtInteractionRole.Tape;
            if (objectName.Contains("녹음기"))
                return ArtInteractionRole.VoiceRecorder;
            if (objectName.Contains("소파"))
                return ArtInteractionRole.DaySofa;
            if (objectName.Contains("메모지"))
                return ArtInteractionRole.Note;
            return ArtInteractionRole.None;
        }

        private static GameObject InstantiateArt(Transform parent, string name, ArtSpec spec, Vector3 position, float rotationY)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.PrefabPath);
            if (prefab == null)
                return null;

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance == null)
                return null;

            instance.name = name;
            instance.transform.localPosition = position;
            instance.transform.localRotation = Quaternion.Euler(0f, rotationY, 0f);
            instance.transform.localScale = Vector3.one;
            return instance;
        }

        private static void CreateStorageShelves(Transform artRoot)
        {
            GameObject storageRoot = new GameObject("Storage Production Art");
            storageRoot.transform.SetParent(artRoot, false);
            ArtSpec spec = FindSpec("창고");
            InstantiateArt(storageRoot.transform, "Storage Shelf Set", spec, new Vector3(0f, 0f, 11.7f), 0f);
        }

        private static void ConfigureWorkstation(GameObject workshopArt, Transform mapRoot, LetterTaskState taskState)
        {
            if (workshopArt == null || taskState == null)
                return;

            GameObject desk = FindDescendant(workshopArt.transform, "Collider - 작업실-책상");
            if (desk == null)
            {
                Debug.LogError("The workshop desk mesh was not found.");
                return;
            }

            LetterWorkstation workstation = desk.GetComponent<LetterWorkstation>();
            if (workstation == null)
                workstation = desk.AddComponent<LetterWorkstation>();
            ArtInteractionMarker marker = desk.GetComponent<ArtInteractionMarker>();
            if (marker == null)
                marker = desk.AddComponent<ArtInteractionMarker>();
            marker.Role = ArtInteractionRole.LetterWorkstation;

            SerializedObject data = new SerializedObject(workstation);
            data.FindProperty("taskState").objectReferenceValue = taskState;
            data.FindProperty("fireStampTexture").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UI/Stamps/FireStamp.png");
            data.FindProperty("waterStampTexture").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UI/Stamps/WaterStamp.png");
            data.FindProperty("electricityStampTexture").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UI/Stamps/ElectricityStamp.png");
            data.FindProperty("impactStampTexture").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UI/Stamps/ImpactStamp.png");
            data.FindProperty("medicineStampTexture").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UI/Stamps/MedicineStamp.png");
            data.FindProperty("alcoholStampTexture").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UI/Stamps/AlcoholStamp.png");
            data.ApplyModifiedPropertiesWithoutUndo();

            Transform legacy = mapRoot.Find("Workshop Packing Bench");
            if (legacy != null)
                legacy.gameObject.SetActive(false);
        }

        private static void ConfigureDaySofa(GameObject pantryArt, Transform mapRoot, LetterTaskState taskState, DayCycleManager dayCycle)
        {
            if (pantryArt == null || taskState == null || dayCycle == null)
                return;

            GameObject sofa = FindDescendant(pantryArt.transform, "Collider - 탕비실-소파");
            if (sofa == null)
            {
                Debug.LogError("The pantry sofa mesh was not found.");
                return;
            }

            DaySleepSofa sleep = sofa.GetComponent<DaySleepSofa>();
            if (sleep == null)
                sleep = sofa.AddComponent<DaySleepSofa>();
            SerializedObject data = new SerializedObject(sleep);
            data.FindProperty("taskState").objectReferenceValue = taskState;
            data.FindProperty("dayCycle").objectReferenceValue = dayCycle;
            data.ApplyModifiedPropertiesWithoutUndo();

            Transform legacy = mapRoot.Find("Pantry Sleep Sofa");
            if (legacy != null)
                legacy.gameObject.SetActive(false);
        }

        private static void DisableLegacyFurniture(Transform mapRoot)
        {
            string[] names =
            {
                "Storage West Shelves", "Storage East Shelves", "Storage Rear Shelves",
                "Storage Parcel A1", "Storage Parcel A2", "Storage Parcel B1", "Storage Parcel B2",
                "Storage Shelf Parcel W1", "Storage Shelf Parcel W2", "Storage Shelf Parcel E1", "Storage Shelf Parcel Rear",
                "Storage Mail Cart", "Storage Document Box",
                "Pantry Refrigerator", "Pantry Kitchen Counter", "Pantry Sleep Sofa", "Pantry Break Table",
                "Pantry Chair A", "Pantry Chair B", "Pantry Staff Locker",
                "Workshop Sorting Desk", "Workshop CRT Monitor", "Workshop Conveyor", "Workshop Packing Bench",
                "Workshop Parcel Shelves", "Workshop Shelf Parcel", "Workshop Open Parcel",
                "Workshop Pictogram Stamp Set", "Workshop Letters",
                "Counter Service Desk", "Counter CRT Monitor", "Counter Parcel Scale", "Counter Waiting Bench",
                "Counter Side Cabinet", "Counter Incoming Letter Tray", "Counter Voice Recorder",
                "Hidden Engine", "Hidden Pipe Rack", "Hidden Tool Cabinet"
            };

            foreach (string objectName in names)
            {
                Transform target = mapRoot.Find(objectName);
                if (target != null)
                    target.gameObject.SetActive(false);
            }
        }

        private static GameObject FindDescendant(Transform root, string objectName)
        {
            Transform target = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == objectName);
            return target != null ? target.gameObject : null;
        }

        private static ArtSpec FindSpec(string name)
        {
            return ArtSpecs.First(spec => spec.Name == name);
        }

        private static void EnsureAssetFolder(string path)
        {
            string[] segments = path.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = $"{current}/{segments[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[index]);
                current = next;
            }
        }

        [MenuItem("Tools/DEAD LETTER/Art/Analyze FBX Assets")]
        public static void AnalyzeFbxAssets()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            StringBuilder report = new StringBuilder();
            report.AppendLine("DEAD LETTER FBX ANALYSIS");

            foreach (ArtSpec spec in ArtSpecs)
            {
                string modelName = spec.Name;
                string path = spec.ModelPath;
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                report.AppendLine($"\n=== {modelName} ===");
                if (source == null)
                {
                    report.AppendLine($"MISSING: {path}");
                    continue;
                }

                GameObject instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
                if (instance == null)
                {
                    report.AppendLine("Could not instantiate model.");
                    continue;
                }

                try
                {
                    instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    instance.transform.localScale = Vector3.one;
                    Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
                    Bounds combined = CalculateBounds(renderers);
                    report.AppendLine($"Root scale: {Format(instance.transform.localScale)}");
                    report.AppendLine($"Combined bounds center: {Format(combined.center)}");
                    report.AppendLine($"Combined bounds size: {Format(combined.size)}");
                    report.AppendLine($"Bottom / top: {combined.min.y:F4} / {combined.max.y:F4}");
                    report.AppendLine($"Renderers: {renderers.Length}");

                    foreach (Renderer renderer in renderers.OrderBy(item => GetPath(instance.transform, item.transform)))
                    {
                        string materials = string.Join(", ", renderer.sharedMaterials.Select(material => material != null ? material.name : "<null>"));
                        report.AppendLine($"- {GetPath(instance.transform, renderer.transform)} | bounds {Format(renderer.bounds.size)} | pos {Format(renderer.transform.position)} | mats [{materials}]");
                    }
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }

            string reportPath = Path.GetFullPath("Library/DeadLetterFbxAnalysis.txt");
            File.WriteAllText(reportPath, report.ToString(), Encoding.UTF8);
            Debug.Log($"FBX analysis written to {reportPath}\n{report}");
        }

        private static Bounds CalculateBounds(IReadOnlyList<Renderer> renderers)
        {
            if (renderers.Count == 0)
                return new Bounds(Vector3.zero, Vector3.zero);

            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Count; index++)
                bounds.Encapsulate(renderers[index].bounds);
            return bounds;
        }

        private static string GetPath(Transform root, Transform current)
        {
            List<string> parts = new List<string>();
            while (current != null && current != root)
            {
                parts.Add(current.name);
                current = current.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static string Format(Vector3 value)
        {
            return $"({value.x:F4}, {value.y:F4}, {value.z:F4})";
        }
    }
}
