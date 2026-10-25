using DeadLetter.Interaction;
using DeadLetter.Mail;
using DeadLetter.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace DeadLetter.Editor
{
    [InitializeOnLoad]
    public static class GrayboxMapGenerator
    {
        private const string RootName = "DEAD LETTER - Graybox";
        private const float WallHeight = 2.7f;
        private const float WallThickness = 0.18f;
        private const float DoorWidth = 1.9f;
        private const float DoorHeight = 2.2f;
        private const string LayoutMarkerName = "Reference Layout Marker - Letter Intake v3";

        static GrayboxMapGenerator()
        {
            EditorApplication.delayCall += EnsureReferenceLayoutExists;
        }

        private static void EnsureReferenceLayoutExists()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != "Assets/Scenes/SampleScene.unity")
                return;

            GameObject root = GameObject.Find(RootName);
            Transform player = root != null ? root.transform.Find("Player Controller") : null;
            bool playerNeedsRelocation = player == null || Vector3.Distance(player.position, new Vector3(0f, 0.05f, -10.1f)) > 0.05f || Mathf.Abs(Mathf.DeltaAngle(player.eulerAngles.y, 180f)) > 1f;
            if (root == null || playerNeedsRelocation || root.transform.Find(LayoutMarkerName) == null || root.transform.Find("Letter Task Runtime") == null || root.transform.Find("Test Mascot Customer") == null || root.transform.Find("Interactable Fuse Box") == null || root.transform.Find("Storage Hall Door Pivot") == null || root.transform.Find("Pantry Hall Door Pivot") == null || root.transform.Find("Workshop Hall Door Pivot") == null || root.transform.Find("Counter Hall Door Pivot") == null || root.transform.Find("Hidden Room Door Pivot") == null)
                Generate();
        }

        [MenuItem("Tools/DEAD LETTER/Generate Graybox Map")]
        public static void Generate()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (Application.isBatchMode && string.IsNullOrEmpty(scene.path))
            {
                foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
                {
                    if (buildScene.enabled && !string.IsNullOrEmpty(buildScene.path))
                    {
                        scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
                        break;
                    }
                }
            }

            if (!scene.IsValid())
                return;

            GameObject previousRoot = GameObject.Find(RootName);
            if (previousRoot != null)
                Undo.DestroyObjectImmediate(previousRoot);

            GameObject root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Generate DEAD LETTER horror map");
            GameObject layoutMarker = new GameObject(LayoutMarkerName);
            layoutMarker.transform.SetParent(root.transform, false);
            DisableTemplateCamerasAndPostProcessing();

            Material wall = GetOrCreateMaterial("Graybox_Walls", new Color(0.32f, 0.27f, 0.30f));
            Material floor = GetOrCreateMaterial("Graybox_Floors", new Color(0.13f, 0.12f, 0.14f));
            Material ceiling = GetOrCreateMaterial("Graybox_Ceilings", new Color(0.16f, 0.14f, 0.17f));
            Material furniture = GetOrCreateMaterial("Graybox_Furniture", new Color(0.34f, 0.19f, 0.15f));
            Material equipment = GetOrCreateMaterial("Graybox_Equipment", new Color(0.13f, 0.15f, 0.18f));
            Material trim = GetOrCreateMaterial("Graybox_Trim", new Color(0.32f, 0.23f, 0.13f));
            Material closedDoor = GetOrCreateMaterial("Graybox_SealedDoor", new Color(0.12f, 0.055f, 0.06f));
            Material glow = GetOrCreateMaterial("Graybox_LightGlow", new Color(0.85f, 0.32f, 0.07f));
            Material paper = GetOrCreateMaterial("Graybox_Paper", new Color(0.72f, 0.65f, 0.49f));
            Material cardboard = GetOrCreateMaterial("Graybox_Cardboard", new Color(0.42f, 0.27f, 0.14f));
            Material upholstery = GetOrCreateMaterial("Graybox_Upholstery", new Color(0.28f, 0.075f, 0.065f));
            Material screen = GetOrCreateMaterial("Graybox_ScreenGlow", new Color(0.055f, 0.13f, 0.12f));
            ConfigureEmission(glow, new Color(1f, 0.32f, 0.055f) * 1.8f);
            ConfigureEmission(screen, new Color(0.08f, 0.4f, 0.32f) * 0.8f);

            SetRestaurantAtmosphere();
            CreateLighting(root.transform);
            CreateLightFixtures(root.transform, glow);

            // Reference layout: a central cross corridor with one room at each end.
            CreateBox(root.transform, "East-West Hall Floor", new Vector3(0f, -0.1f, 0f), new Vector3(14f, 0.2f, 3.6f), floor);
            CreateBox(root.transform, "North-South Hall Floor", new Vector3(0f, -0.1f, 0f), new Vector3(3.6f, 0.2f, 14f), floor);
            CreateBox(root.transform, "East-West Hall Ceiling", new Vector3(0f, 2.89f, 0f), new Vector3(14f, 0.18f, 3.6f), ceiling);
            CreateBox(root.transform, "North-South Hall Ceiling", new Vector3(0f, 2.89f, 0f), new Vector3(3.6f, 0.18f, 14f), ceiling);
            CreateHorizontalWall(root.transform, "West Hall North Wall", -7f, -1.8f, 1.8f, wall);
            CreateHorizontalWall(root.transform, "West Hall South Wall", -7f, -1.8f, -1.8f, wall);
            CreateHorizontalWall(root.transform, "East Hall North Wall", 1.8f, 7f, 1.8f, wall);
            CreateHorizontalWall(root.transform, "East Hall South Wall", 1.8f, 7f, -1.8f, wall);
            CreateVerticalWall(root.transform, "North Hall West Wall Lower", -1.8f, 1.8f, 4.75f, wall);
            CreateVerticalWall(root.transform, "North Hall West Wall Upper", -1.8f, 6.65f, 7f, wall);
            CreateVerticalWall(root.transform, "North Hall East Wall", 1.8f, 1.8f, 7f, wall);
            CreateVerticalWall(root.transform, "South Hall West Wall", -1.8f, -7f, -1.8f, wall);
            CreateVerticalWall(root.transform, "South Hall East Wall", 1.8f, -7f, -1.8f, wall);
            CreateFloorArrow(root.transform, "North Hall Arrow", new Vector3(0f, 0.025f, 3.7f), 0f, trim);
            CreateFloorArrow(root.transform, "South Hall Arrow", new Vector3(0f, 0.025f, -3.7f), 180f, trim);
            CreateFloorArrow(root.transform, "West Hall Arrow", new Vector3(-3.7f, 0.025f, 0f), -90f, trim);
            CreateFloorArrow(root.transform, "East Hall Arrow", new Vector3(3.7f, 0.025f, 0f), 90f, trim);

            // Storage room, north.
            CreateRoomShell(root.transform, "Storage", 0f, 11.5f, 9f, 9f, floor, ceiling);
            CreateHorizontalWall(root.transform, "Storage North Wall Left", -4.5f, -1.05f, 16f, wall);
            CreateHorizontalWall(root.transform, "Storage North Wall Right", 1.05f, 4.5f, 16f, wall);
            CreateDoorFrame(root.transform, "Storage Exterior Door", 0f, 16f, true, wall);
            CreateHingedDoor(root.transform, "Storage Exterior Door", 0f, 16f, true, 90f, closedDoor, trim);
            CreateVerticalWall(root.transform, "Storage West Wall", -4.5f, 7f, 16f, wall);
            CreateVerticalWall(root.transform, "Storage East Wall", 4.5f, 7f, 16f, wall);
            CreateHorizontalWall(root.transform, "Storage Hall Wall Left", -4.5f, -1.05f, 7f, wall);
            CreateHorizontalWall(root.transform, "Storage Hall Wall Right", 1.05f, 4.5f, 7f, wall);
            CreateDoorFrame(root.transform, "Storage Hall Door", 0f, 7f, true, wall);
            CreateHingedDoor(root.transform, "Storage Hall Door", 0f, 7f, true, -90f, closedDoor, trim);

            // Hidden engine room, clearly separated to the north-west and joined by a short passage.
            CreateRoomShell(root.transform, "Hidden Room", -7.6f, 7f, 5.6f, 5.2f, floor, ceiling);
            CreateBox(root.transform, "Hidden Connector Floor", new Vector3(-3.3f, -0.1f, 5.7f), new Vector3(3f, 0.2f, 1.9f), floor);
            CreateBox(root.transform, "Hidden Connector Ceiling", new Vector3(-3.3f, 2.89f, 5.7f), new Vector3(3f, 0.18f, 1.9f), ceiling);
            CreateHorizontalWall(root.transform, "Hidden Connector South Wall", -4.8f, -1.8f, 4.75f, wall);
            CreateHorizontalWall(root.transform, "Hidden Connector North Wall", -4.8f, -1.8f, 6.65f, wall);
            CreateVerticalWall(root.transform, "Hidden West Wall", -10.4f, 4.4f, 9.6f, wall);
            CreateVerticalWall(root.transform, "Hidden East Wall Lower", -4.8f, 4.4f, 4.75f, wall);
            CreateVerticalWall(root.transform, "Hidden East Wall Upper", -4.8f, 6.65f, 9.6f, wall);
            CreateHorizontalWall(root.transform, "Hidden North Wall", -10.4f, -4.8f, 9.6f, wall);
            CreateHorizontalWall(root.transform, "Hidden South Wall", -10.4f, -4.8f, 4.4f, wall);
            CreateDoorFrame(root.transform, "Hidden Room Door", -1.8f, 5.7f, false, wall);
            CreateHingedDoor(root.transform, "Hidden Room Door", -1.8f, 5.7f, false, -90f, closedDoor, trim, true);

            // Pantry and staff lounge, west.
            CreateRoomShell(root.transform, "Pantry", -11.5f, 0f, 9f, 9f, floor, ceiling);
            CreateVerticalWall(root.transform, "Pantry West Wall", -16f, -4.5f, 4.5f, wall);
            CreateHorizontalWall(root.transform, "Pantry North Wall", -16f, -7f, 4.5f, wall);
            CreateHorizontalWall(root.transform, "Pantry South Wall", -16f, -7f, -4.5f, wall);
            CreateVerticalWall(root.transform, "Pantry Hall Wall Upper", -7f, 1.05f, 4.5f, wall);
            CreateVerticalWall(root.transform, "Pantry Hall Wall Lower", -7f, -4.5f, -1.05f, wall);
            CreateDoorFrame(root.transform, "Pantry Hall Door", -7f, 0f, false, wall);
            CreateHingedDoor(root.transform, "Pantry Hall Door", -7f, 0f, false, -90f, closedDoor, trim);

            // Mail and parcel workroom, east.
            CreateRoomShell(root.transform, "Workshop", 11.5f, 0f, 9f, 9f, floor, ceiling);
            CreateVerticalWall(root.transform, "Workshop East Wall", 16f, -4.5f, 4.5f, wall);
            CreateHorizontalWall(root.transform, "Workshop North Wall", 7f, 16f, 4.5f, wall);
            CreateHorizontalWall(root.transform, "Workshop South Wall", 7f, 16f, -4.5f, wall);
            CreateVerticalWall(root.transform, "Workshop Hall Wall Upper", 7f, 1.05f, 4.5f, wall);
            CreateVerticalWall(root.transform, "Workshop Hall Wall Lower", 7f, -4.5f, -1.05f, wall);
            CreateDoorFrame(root.transform, "Workshop Hall Door", 7f, 0f, false, wall);
            CreateHingedDoor(root.transform, "Workshop Hall Door", 7f, 0f, false, 90f, closedDoor, trim);

            // Public service counter, south.
            CreateRoomShell(root.transform, "Counter", 0f, -11.5f, 9f, 9f, floor, ceiling);
            CreateBox(root.transform, "Counter Exterior Approach Floor", new Vector3(0f, -0.1f, -17.55f), new Vector3(2.8f, 0.2f, 3.1f), floor);
            CreateHorizontalWall(root.transform, "Counter South Wall Left", -4.5f, -1.05f, -16f, wall);
            CreateHorizontalWall(root.transform, "Counter South Wall Right", 1.05f, 4.5f, -16f, wall);
            CreateDoorFrame(root.transform, "Counter Exterior Door", 0f, -16f, true, wall);
            HingedDoor counterExteriorDoor = CreateHingedDoor(root.transform, "Counter Exterior Door", 0f, -16f, true, -90f, closedDoor, trim);
            CreateVerticalWall(root.transform, "Counter West Wall", -4.5f, -16f, -7f, wall);
            CreateVerticalWall(root.transform, "Counter East Wall", 4.5f, -16f, -7f, wall);
            CreateHorizontalWall(root.transform, "Counter Hall Wall Left", -4.5f, -1.05f, -7f, wall);
            CreateHorizontalWall(root.transform, "Counter Hall Wall Right", 1.05f, 4.5f, -7f, wall);
            CreateDoorFrame(root.transform, "Counter Hall Door", 0f, -7f, true, wall);
            CreateHingedDoor(root.transform, "Counter Hall Door", 0f, -7f, true, 90f, closedDoor, trim);

            LetterTaskState letterTask = CreateLetterTaskState(root.transform);
            CreateStorageFurniture(root.transform, furniture, equipment, trim, cardboard, paper);
            CreatePantryFurniture(root.transform, furniture, equipment, trim, upholstery);
            CreateWorkshopFurniture(root.transform, letterTask, furniture, equipment, trim, cardboard, paper, screen, glow);
            CreateCounterFurniture(root.transform, furniture, equipment, trim, upholstery, paper, screen);
            CreateHiddenRoomFurniture(root.transform, furniture, equipment, trim, glow);
            CreateInteractableFuseBox(root.transform, equipment, trim, closedDoor, glow);
            CreateTestCustomer(root.transform, letterTask, counterExteriorDoor, cardboard, upholstery, equipment, paper, trim, screen);

            CreatePlayer(root.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrEmpty(scene.path))
                EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = root;
            Debug.Log("DEAD LETTER reference map generated with storage, pantry, workshop, counter, and hidden room.");
        }

        [MenuItem("Tools/DEAD LETTER/Open Letter Workstation Test")]
        private static void OpenLetterWorkstationTest()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("Enter Play Mode before opening the letter workstation test.");
                return;
            }

            LetterTaskState state = Object.FindAnyObjectByType<LetterTaskState>();
            LetterWorkstation workstation = Object.FindAnyObjectByType<LetterWorkstation>();
            FirstPersonController player = Object.FindAnyObjectByType<FirstPersonController>();
            if (state == null || workstation == null || player == null)
            {
                Debug.LogError("Letter workstation test objects are missing from the active scene.");
                return;
            }

            if (!state.HasLetter)
                state.TryReceiveLetter("letter_mascot_001", "customer_mascot_test", LetterStamp.Fire | LetterStamp.Shock);

            bool orderIndependent =
                LetterTaskState.ContainsRequiredStamps(LetterStamp.Shock | LetterStamp.Fire, LetterStamp.Fire | LetterStamp.Shock) &&
                LetterTaskState.ContainsRequiredStamps(LetterStamp.Fire | LetterStamp.Shock, LetterStamp.Shock | LetterStamp.Fire);
            if (!orderIndependent)
            {
                Debug.LogError("Letter stamp order-independence verification failed.");
                return;
            }

            workstation.Interact(player.gameObject);
            Debug.Log("Letter workstation opened. Stamp order-independence verification passed.");
        }

        private static void SetRestaurantAtmosphere()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.13f, 0.18f);
            RenderSettings.ambientIntensity = 0.95f;
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.055f, 0.045f, 0.065f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.018f;
        }

        private static void CreateLighting(Transform parent)
        {
            CreatePointLight(parent, "Hall Amber Light", new Vector3(0f, 2.35f, 0f), new Color(1f, 0.52f, 0.22f), 8f, 9f);
            CreatePointLight(parent, "Storage Dusty Light", new Vector3(0f, 2.35f, 11.5f), new Color(0.78f, 0.66f, 0.42f), 6f, 8f);
            CreatePointLight(parent, "Pantry Warm Light", new Vector3(-11.5f, 2.3f, 0f), new Color(0.92f, 0.45f, 0.24f), 5f, 7.5f);
            CreatePointLight(parent, "Workshop Sickly Light", new Vector3(11.5f, 2.3f, 0f), new Color(0.58f, 0.72f, 0.46f), 5.5f, 7.5f);
            CreatePointLight(parent, "Counter Warm Light", new Vector3(0f, 2.3f, -11.5f), new Color(1f, 0.48f, 0.2f), 5.5f, 7.5f);
            CreatePointLight(parent, "Hidden Engine Red Glow", new Vector3(-7.6f, 2.2f, 7f), new Color(0.85f, 0.08f, 0.1f), 4f, 6f);
        }

        private static void CreateLightFixtures(Transform parent, Material glow)
        {
            CreateBox(parent, "Hall Ceiling Fixture", new Vector3(0f, 2.62f, 0f), new Vector3(0.55f, 0.08f, 1.1f), glow);
            CreateBox(parent, "Storage Ceiling Fixture", new Vector3(0f, 2.62f, 11.5f), new Vector3(0.55f, 0.08f, 1.1f), glow);
            CreateBox(parent, "Pantry Ceiling Fixture", new Vector3(-11.5f, 2.62f, 0f), new Vector3(0.55f, 0.08f, 1.1f), glow);
            CreateBox(parent, "Workshop Ceiling Fixture", new Vector3(11.5f, 2.62f, 0f), new Vector3(0.55f, 0.08f, 1.1f), glow);
            CreateBox(parent, "Counter Ceiling Fixture", new Vector3(0f, 2.62f, -11.5f), new Vector3(0.55f, 0.08f, 1.1f), glow);
            CreateBox(parent, "Hidden Ceiling Fixture", new Vector3(-7.6f, 2.62f, 7f), new Vector3(0.45f, 0.08f, 0.8f), glow);
        }

        private static void ConfigureEmission(Material material, Color emission)
        {
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission);
                EditorUtility.SetDirty(material);
            }
        }

        private static void CreatePointLight(Transform parent, string name, Vector3 position, Color color, float intensity, float range)
        {
            GameObject lightObject = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(lightObject, "Create restaurant lighting");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = position;
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }

        private static void CreateStorageFurniture(Transform parent, Material furniture, Material equipment, Material trim, Material cardboard, Material paper)
        {
            CreateShelfUnit(parent, "Storage West Shelves", new Vector3(-3.85f, 0f, 12.5f), new Vector3(0.72f, 2.15f, 5.5f), equipment);
            CreateShelfUnit(parent, "Storage East Shelves", new Vector3(3.85f, 0f, 12.5f), new Vector3(0.72f, 2.15f, 5.5f), equipment);
            CreateShelfUnit(parent, "Storage Rear Shelves", new Vector3(0f, 0f, 15.35f), new Vector3(5.1f, 2.15f, 0.72f), equipment);

            CreateParcelBox(parent, "Storage Parcel A1", new Vector3(-2.55f, 0.38f, 9.75f), new Vector3(1.35f, 0.76f, 1.25f), cardboard, paper, 8f);
            CreateParcelBox(parent, "Storage Parcel A2", new Vector3(-2.4f, 1.02f, 9.9f), new Vector3(1.05f, 0.52f, 0.92f), cardboard, paper, -6f);
            CreateParcelBox(parent, "Storage Parcel B1", new Vector3(2.2f, 0.32f, 10.15f), new Vector3(1.65f, 0.64f, 1.2f), cardboard, paper, -5f);
            CreateParcelBox(parent, "Storage Parcel B2", new Vector3(2.35f, 0.82f, 10.1f), new Vector3(0.95f, 0.38f, 0.78f), cardboard, paper, 11f);
            CreateParcelBox(parent, "Storage Shelf Parcel W1", new Vector3(-3.8f, 0.72f, 11.2f), new Vector3(0.48f, 0.38f, 0.7f), cardboard, paper, 0f);
            CreateParcelBox(parent, "Storage Shelf Parcel W2", new Vector3(-3.8f, 1.42f, 13.2f), new Vector3(0.5f, 0.34f, 0.82f), cardboard, paper, 0f);
            CreateParcelBox(parent, "Storage Shelf Parcel E1", new Vector3(3.8f, 0.72f, 12f), new Vector3(0.48f, 0.4f, 0.72f), cardboard, paper, 0f);
            CreateParcelBox(parent, "Storage Shelf Parcel Rear", new Vector3(1.45f, 1.42f, 15.25f), new Vector3(0.82f, 0.34f, 0.48f), cardboard, paper, 0f);
            CreateMailCart(parent, "Storage Mail Cart", new Vector3(0.75f, 0f, 13.1f), trim, equipment);
            CreateFileBox(parent, "Storage Document Box", new Vector3(-0.9f, 1.35f, 15.05f), cardboard, paper);
        }

        private static void CreatePantryFurniture(Transform parent, Material furniture, Material equipment, Material trim, Material upholstery)
        {
            CreateRefrigerator(parent, "Pantry Refrigerator", new Vector3(-14.65f, 0f, 3.05f), equipment, trim);
            CreateKitchenCounter(parent, "Pantry Kitchen Counter", new Vector3(-11.3f, 0f, 3.85f), furniture, equipment, trim);
            CreateSofa(parent, "Pantry Sleep Sofa", new Vector3(-14.25f, 0f, -2.45f), 90f, upholstery, furniture);
            CreateTable(parent, "Pantry Break Table", new Vector3(-11.05f, 0f, -1.1f), new Vector2(2.3f, 1.55f), furniture, trim);
            CreateChair(parent, "Pantry Chair A", new Vector3(-10.95f, 0f, -2.35f), 0f, furniture, trim);
            CreateChair(parent, "Pantry Chair B", new Vector3(-10.95f, 0f, 0.15f), 180f, furniture, trim);
            CreateLocker(parent, "Pantry Staff Locker", new Vector3(-8.9f, 0f, 2.45f), new Vector3(0.72f, 2f, 1.8f), equipment, trim);
        }

        private static void CreateWorkshopFurniture(Transform parent, LetterTaskState letterTask, Material furniture, Material equipment, Material trim, Material cardboard, Material paper, Material screen, Material glow)
        {
            CreateWorkDesk(parent, "Workshop Sorting Desk", new Vector3(11.3f, 0f, 2.75f), 180f, 3.9f, furniture, equipment, trim);
            CreateCrtMonitor(parent, "Workshop CRT Monitor", new Vector3(11.3f, 0.92f, 2.25f), 0f, equipment, trim, screen);
            CreateConveyor(parent, "Workshop Conveyor", new Vector3(14.55f, 0f, -0.5f), 5.4f, equipment, trim);
            GameObject packingBench = CreateWorkBench(parent, "Workshop Packing Bench", new Vector3(10.25f, 0f, -3.65f), 4.4f, furniture, equipment);
            LetterWorkstation workstation = Undo.AddComponent<LetterWorkstation>(packingBench);
            SerializedObject workstationData = new SerializedObject(workstation);
            workstationData.FindProperty("taskState").objectReferenceValue = letterTask;
            workstationData.ApplyModifiedPropertiesWithoutUndo();
            CreateShelfUnit(parent, "Workshop Parcel Shelves", new Vector3(15.15f, 0f, 3.05f), new Vector3(0.72f, 2.1f, 2.4f), equipment);
            CreateParcelBox(parent, "Workshop Shelf Parcel", new Vector3(15.1f, 0.72f, 3.1f), new Vector3(0.48f, 0.36f, 0.72f), cardboard, paper, 0f);
            CreateParcelBox(parent, "Workshop Open Parcel", new Vector3(12.25f, 1.04f, -3.65f), new Vector3(0.9f, 0.38f, 0.7f), cardboard, paper, 0f);
            CreateStampSet(parent, "Workshop Pictogram Stamp Set", new Vector3(9.4f, 1.03f, -3.65f), trim, glow, paper);
            CreateLetterStack(parent, "Workshop Letters", new Vector3(10.9f, 1.02f, -3.65f), paper, trim);
        }

        private static void CreateCounterFurniture(Transform parent, Material furniture, Material equipment, Material trim, Material upholstery, Material paper, Material screen)
        {
            CreateServiceCounter(parent, "Counter Service Desk", new Vector3(0f, 0f, -11.55f), furniture, equipment, trim);
            CreateCrtMonitor(parent, "Counter CRT Monitor", new Vector3(1.35f, 0.92f, -11.12f), 180f, equipment, trim, screen);
            CreateParcelScale(parent, "Counter Parcel Scale", new Vector3(-1.85f, 0.94f, -11.35f), equipment, trim, screen);
            CreateBench(parent, "Counter Waiting Bench", new Vector3(3.82f, 0f, -13.35f), 3.7f, furniture, upholstery, 90f);
            CreateDrawerCabinet(parent, "Counter Side Cabinet", new Vector3(-3.8f, 0f, -13.2f), 90f, new Vector3(2.2f, 1.45f, 0.72f), equipment, trim);
            CreateLetterTray(parent, "Counter Incoming Letter Tray", new Vector3(-0.35f, 1.05f, -11.28f), paper, trim);
        }

        private static LetterTaskState CreateLetterTaskState(Transform parent)
        {
            GameObject stateObject = new GameObject("Letter Task Runtime");
            Undo.RegisterCreatedObjectUndo(stateObject, "Create letter task state");
            stateObject.transform.SetParent(parent, false);
            return Undo.AddComponent<LetterTaskState>(stateObject);
        }

        private static void CreateTestCustomer(
            Transform parent,
            LetterTaskState taskState,
            HingedDoor entranceDoor,
            Material mascotBody,
            Material mascotAccent,
            Material dark,
            Material paper,
            Material trim,
            Material screen)
        {
            GameObject route = new GameObject("Customer Route");
            Undo.RegisterCreatedObjectUndo(route, "Create customer route");
            route.transform.SetParent(parent, false);
            Transform[] waypoints = new Transform[3];
            Vector3[] routePoints =
            {
                new Vector3(0f, 0f, -16.9f),
                new Vector3(0f, 0f, -15.1f),
                new Vector3(0f, 0f, -13.05f)
            };
            string[] waypointNames = { "Exterior Entry", "Interior Entry", "Counter Waiting Point" };
            for (int index = 0; index < routePoints.Length; index++)
            {
                GameObject waypoint = new GameObject(waypointNames[index]);
                Undo.RegisterCreatedObjectUndo(waypoint, "Create customer waypoint");
                waypoint.transform.SetParent(route.transform, false);
                waypoint.transform.localPosition = routePoints[index];
                waypoints[index] = waypoint.transform;
            }

            GameObject customer = new GameObject("Test Mascot Customer");
            Undo.RegisterCreatedObjectUndo(customer, "Create test mascot customer");
            customer.transform.SetParent(parent, false);
            customer.transform.localPosition = new Vector3(0f, 0f, -18.45f);
            customer.transform.localRotation = Quaternion.identity;
            CapsuleCollider customerCollider = Undo.AddComponent<CapsuleCollider>(customer);
            customerCollider.height = 1.95f;
            customerCollider.radius = 0.4f;
            customerCollider.center = new Vector3(0f, 0.98f, 0f);

            CreateCylinder(customer.transform, "Mascot Torso", new Vector3(0f, 0.76f, 0f), 0.38f, 1.25f, mascotBody, Vector3.zero, false);
            CreateSphere(customer.transform, "Mascot Head", new Vector3(0f, 1.58f, 0f), 0.82f, mascotAccent, false);
            CreateBox(customer.transform, "Mascot Left Ear", new Vector3(-0.29f, 1.98f, 0f), new Vector3(0.25f, 0.34f, 0.22f), mascotBody, false);
            CreateBox(customer.transform, "Mascot Right Ear", new Vector3(0.29f, 1.98f, 0f), new Vector3(0.25f, 0.34f, 0.22f), mascotBody, false);
            CreateSphere(customer.transform, "Mascot Left Eye", new Vector3(-0.17f, 1.66f, -0.37f), 0.11f, screen, false);
            CreateSphere(customer.transform, "Mascot Right Eye", new Vector3(0.17f, 1.66f, -0.37f), 0.11f, screen, false);
            CreateBox(customer.transform, "Mascot Snout", new Vector3(0f, 1.48f, -0.4f), new Vector3(0.28f, 0.18f, 0.12f), dark, false);
            CreateCylinder(customer.transform, "Mascot Left Arm", new Vector3(-0.48f, 0.9f, -0.04f), 0.1f, 0.82f, mascotAccent, new Vector3(0f, 0f, -10f), false);
            CreateCylinder(customer.transform, "Mascot Right Arm", new Vector3(0.48f, 0.9f, -0.04f), 0.1f, 0.82f, mascotAccent, new Vector3(0f, 0f, 10f), false);
            CreateCylinder(customer.transform, "Mascot Left Leg", new Vector3(-0.2f, 0.25f, 0f), 0.12f, 0.5f, dark, Vector3.zero, false);
            CreateCylinder(customer.transform, "Mascot Right Leg", new Vector3(0.2f, 0.25f, 0f), 0.12f, 0.5f, dark, Vector3.zero, false);

            GameObject letter = CreateBox(customer.transform, "Customer Letter", new Vector3(0f, 1.05f, -0.48f), new Vector3(0.58f, 0.38f, 0.035f), paper, false);
            CreateBox(letter.transform, "Letter Seal", new Vector3(0f, 0f, -0.6f), new Vector3(0.13f, 0.13f, 0.03f), trim, false);
            foreach (Transform child in customer.GetComponentsInChildren<Transform>())
                child.gameObject.isStatic = false;

            TestCustomerNPC npc = Undo.AddComponent<TestCustomerNPC>(customer);
            SerializedObject npcData = new SerializedObject(npc);
            npcData.FindProperty("taskState").objectReferenceValue = taskState;
            npcData.FindProperty("entranceDoor").objectReferenceValue = entranceDoor;
            npcData.FindProperty("letterProp").objectReferenceValue = letter;
            SerializedProperty waypointData = npcData.FindProperty("waypoints");
            waypointData.arraySize = waypoints.Length;
            for (int index = 0; index < waypoints.Length; index++)
                waypointData.GetArrayElementAtIndex(index).objectReferenceValue = waypoints[index];
            npcData.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateHiddenRoomFurniture(Transform parent, Material furniture, Material equipment, Material trim, Material glow)
        {
            CreateEngine(parent, "Hidden Engine", new Vector3(-7.55f, 0f, 7.85f), equipment, trim, glow);
            CreatePipeRack(parent, "Hidden Pipe Rack", new Vector3(-9.85f, 0f, 6.65f), trim, equipment);
            CreateDrawerCabinet(parent, "Hidden Tool Cabinet", new Vector3(-6.35f, 0f, 4.88f), 0f, new Vector3(1.8f, 1.55f, 0.66f), furniture, trim);
        }

        private static void CreateInteractableFuseBox(Transform parent, Material equipment, Material trim, Material indicator, Material glow)
        {
            GameObject panel = CreateBox(parent, "Interactable Fuse Box", new Vector3(-10.18f, 1.35f, 7f), new Vector3(0.82f, 0.92f, 0.32f), equipment);
            // The local front points toward +X so the controls face into the room from its west wall.
            panel.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);

            CreateBox(panel.transform, "Fuse Box Raised Frame", new Vector3(0f, 0f, -0.56f), new Vector3(0.72f, 0.82f, 0.05f), trim, false);
            CreateBox(panel.transform, "Fuse Box Face Plate", new Vector3(0f, 0f, -0.6f), new Vector3(0.62f, 0.72f, 0.035f), equipment, false);
            CreateBox(panel.transform, "Fuse Box Warning Stripe", new Vector3(0f, 0.29f, -0.625f), new Vector3(0.48f, 0.06f, 0.02f), glow, false);

            GameObject indicatorObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Undo.RegisterCreatedObjectUndo(indicatorObject, "Create fuse box indicator");
            indicatorObject.name = "Fuse Box Indicator";
            indicatorObject.transform.SetParent(panel.transform, false);
            indicatorObject.transform.localPosition = new Vector3(0.2f, 0.15f, -0.65f);
            indicatorObject.transform.localScale = new Vector3(0.15f, 0.15f, 0.08f);
            indicatorObject.GetComponent<MeshRenderer>().sharedMaterial = indicator;
            indicatorObject.GetComponent<Collider>().enabled = false;

            GameObject lever = CreateBox(panel.transform, "Fuse Lever", new Vector3(-0.08f, -0.12f, -0.66f), new Vector3(0.12f, 0.4f, 0.12f), trim, false);
            lever.transform.localRotation = Quaternion.Euler(0f, 0f, -25f);

            for (int index = 0; index < 3; index++)
                CreateBox(panel.transform, $"Fuse Switch {index + 1}", new Vector3(0.2f, -0.08f - index * 0.13f, -0.65f), new Vector3(0.12f, 0.07f, 0.05f), trim, false);

            TinyFuseBox interactable = Undo.AddComponent<TinyFuseBox>(panel);
            SerializedObject data = new SerializedObject(interactable);
            data.FindProperty("lever").objectReferenceValue = lever.transform;
            data.FindProperty("indicatorRenderer").objectReferenceValue = indicatorObject.GetComponent<Renderer>();
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreatePropRoot(Transform parent, string name, Vector3 position, float rotationY = 0f)
        {
            GameObject root = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(root, "Create detailed prop");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            root.transform.localRotation = Quaternion.Euler(0f, rotationY, 0f);
            return root;
        }

        private static void CreateShelfUnit(Transform parent, string name, Vector3 position, Vector3 size, Material frame, float rotationY = 0f)
        {
            GameObject root = CreatePropRoot(parent, name, position, rotationY);
            const float post = 0.08f;
            float halfX = size.x * 0.5f - post * 0.5f;
            float halfZ = size.z * 0.5f - post * 0.5f;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                    CreateBox(root.transform, $"Post {x} {z}", new Vector3(halfX * x, size.y * 0.5f, halfZ * z), new Vector3(post, size.y, post), frame);
            }

            for (int shelf = 0; shelf < 4; shelf++)
            {
                float y = 0.08f + shelf * ((size.y - 0.16f) / 3f);
                CreateBox(root.transform, $"Shelf {shelf + 1}", new Vector3(0f, y, 0f), new Vector3(size.x, 0.08f, size.z), frame);
            }

            GameObject braceA = CreateBox(root.transform, "Rear Diagonal Brace A", new Vector3(0f, size.y * 0.5f, halfZ), new Vector3(0.045f, size.y * 1.05f, 0.045f), frame, false);
            GameObject braceB = CreateBox(root.transform, "Rear Diagonal Brace B", new Vector3(0f, size.y * 0.5f, halfZ), new Vector3(0.045f, size.y * 1.05f, 0.045f), frame, false);
            braceA.transform.localRotation = Quaternion.Euler(0f, 0f, 22f);
            braceB.transform.localRotation = Quaternion.Euler(0f, 0f, -22f);
        }

        private static void CreateParcelBox(Transform parent, string name, Vector3 position, Vector3 size, Material cardboard, Material paper, float rotationY)
        {
            GameObject root = CreatePropRoot(parent, name, position, rotationY);
            CreateBox(root.transform, "Cardboard Body", Vector3.zero, size, cardboard);
            CreateBox(root.transform, "Packing Tape", new Vector3(0f, size.y * 0.505f, 0f), new Vector3(size.x * 0.16f, 0.018f, size.z * 1.01f), paper, false);
            CreateBox(root.transform, "Shipping Label", new Vector3(size.x * 0.18f, size.y * 0.12f, -size.z * 0.505f), new Vector3(size.x * 0.34f, size.y * 0.34f, 0.015f), paper, false);
            CreateBox(root.transform, "Label Line A", new Vector3(size.x * 0.18f, size.y * 0.2f, -size.z * 0.517f), new Vector3(size.x * 0.24f, 0.018f, 0.008f), cardboard, false);
            CreateBox(root.transform, "Label Line B", new Vector3(size.x * 0.18f, size.y * 0.1f, -size.z * 0.517f), new Vector3(size.x * 0.2f, 0.018f, 0.008f), cardboard, false);
        }

        private static void CreateMailCart(Transform parent, string name, Vector3 position, Material trim, Material equipment)
        {
            GameObject root = CreatePropRoot(parent, name, position, -8f);
            CreateBox(root.transform, "Cart Base", new Vector3(0f, 0.28f, 0f), new Vector3(1.25f, 0.12f, 1.55f), equipment);
            CreateBox(root.transform, "Cart Back", new Vector3(0f, 0.78f, 0.7f), new Vector3(1.25f, 1.08f, 0.08f), trim);
            for (int x = -1; x <= 1; x += 2)
                CreateBox(root.transform, $"Handle Post {x}", new Vector3(x * 0.52f, 1.2f, 0.7f), new Vector3(0.07f, 0.9f, 0.07f), trim);
            CreateBox(root.transform, "Handle Bar", new Vector3(0f, 1.63f, 0.7f), new Vector3(1.12f, 0.08f, 0.08f), trim);
            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                    CreateCylinder(root.transform, $"Wheel {x} {z}", new Vector3(x * 0.48f, 0.12f, z * 0.58f), 0.13f, 0.09f, trim, new Vector3(0f, 0f, 90f));
            }
        }

        private static void CreateFileBox(Transform parent, string name, Vector3 position, Material cardboard, Material paper)
        {
            GameObject root = CreatePropRoot(parent, name, position, 2f);
            CreateBox(root.transform, "Archive Box", Vector3.zero, new Vector3(1.05f, 0.48f, 0.55f), cardboard);
            CreateBox(root.transform, "Archive Label", new Vector3(0f, 0f, -0.285f), new Vector3(0.52f, 0.2f, 0.018f), paper, false);
            CreateBox(root.transform, "Archive Lid", new Vector3(0f, 0.27f, 0f), new Vector3(1.12f, 0.08f, 0.62f), cardboard, false);
        }

        private static void CreateRefrigerator(Transform parent, string name, Vector3 position, Material body, Material trim)
        {
            GameObject root = CreatePropRoot(parent, name, position);
            CreateBox(root.transform, "Fridge Body", new Vector3(0f, 0.95f, 0f), new Vector3(1.45f, 1.9f, 1.48f), body);
            CreateBox(root.transform, "Upper Door", new Vector3(0f, 1.3f, -0.755f), new Vector3(1.34f, 1.08f, 0.07f), body, false);
            CreateBox(root.transform, "Lower Door", new Vector3(0f, 0.43f, -0.755f), new Vector3(1.34f, 0.58f, 0.07f), body, false);
            CreateBox(root.transform, "Upper Handle", new Vector3(0.51f, 1.25f, -0.82f), new Vector3(0.07f, 0.68f, 0.07f), trim, false);
            CreateBox(root.transform, "Lower Handle", new Vector3(0.51f, 0.45f, -0.82f), new Vector3(0.07f, 0.32f, 0.07f), trim, false);
            for (int x = -1; x <= 1; x += 2)
                CreateBox(root.transform, $"Foot {x}", new Vector3(x * 0.5f, 0.05f, -0.45f), new Vector3(0.16f, 0.1f, 0.16f), trim);
        }

        private static void CreateKitchenCounter(Transform parent, string name, Vector3 position, Material furniture, Material equipment, Material trim)
        {
            GameObject root = CreatePropRoot(parent, name, position);
            CreateBox(root.transform, "Counter Base", new Vector3(0f, 0.43f, 0f), new Vector3(4.7f, 0.86f, 0.82f), furniture);
            CreateBox(root.transform, "Counter Top", new Vector3(0f, 0.91f, 0f), new Vector3(4.9f, 0.12f, 0.98f), trim);
            for (int index = -2; index <= 2; index++)
            {
                CreateBox(root.transform, $"Cabinet Seam {index + 3}", new Vector3(index * 0.9f, 0.44f, -0.425f), new Vector3(0.035f, 0.72f, 0.025f), equipment, false);
                CreateBox(root.transform, $"Cabinet Handle {index + 3}", new Vector3(index * 0.9f + 0.25f, 0.55f, -0.455f), new Vector3(0.22f, 0.04f, 0.04f), trim, false);
            }
            CreateBox(root.transform, "Sink Basin", new Vector3(0.85f, 0.985f, 0f), new Vector3(1.15f, 0.045f, 0.62f), equipment, false);
            CreateCylinder(root.transform, "Faucet Stem", new Vector3(1.48f, 1.16f, 0.25f), 0.055f, 0.38f, trim);
            CreateBox(root.transform, "Faucet Spout", new Vector3(1.31f, 1.33f, 0.25f), new Vector3(0.36f, 0.06f, 0.06f), trim, false);
        }

        private static void CreateSofa(Transform parent, string name, Vector3 position, float rotationY, Material upholstery, Material frame)
        {
            GameObject root = CreatePropRoot(parent, name, position, rotationY);
            CreateBox(root.transform, "Sofa Base", new Vector3(0f, 0.28f, 0f), new Vector3(3.15f, 0.35f, 1.2f), frame);
            CreateBox(root.transform, "Seat Cushion Left", new Vector3(-0.76f, 0.55f, -0.08f), new Vector3(1.42f, 0.26f, 0.92f), upholstery);
            CreateBox(root.transform, "Seat Cushion Right", new Vector3(0.76f, 0.55f, -0.08f), new Vector3(1.42f, 0.26f, 0.92f), upholstery);
            CreateBox(root.transform, "Back Cushion Left", new Vector3(-0.76f, 1.02f, 0.46f), new Vector3(1.42f, 0.78f, 0.24f), upholstery);
            CreateBox(root.transform, "Back Cushion Right", new Vector3(0.76f, 1.02f, 0.46f), new Vector3(1.42f, 0.78f, 0.24f), upholstery);
            CreateBox(root.transform, "Left Arm", new Vector3(-1.52f, 0.68f, 0f), new Vector3(0.22f, 0.78f, 1.25f), upholstery);
            CreateBox(root.transform, "Right Arm", new Vector3(1.52f, 0.68f, 0f), new Vector3(0.22f, 0.78f, 1.25f), upholstery);
        }

        private static void CreateTable(Transform parent, string name, Vector3 position, Vector2 size, Material top, Material legs)
        {
            GameObject root = CreatePropRoot(parent, name, position);
            CreateBox(root.transform, "Table Top", new Vector3(0f, 0.75f, 0f), new Vector3(size.x, 0.12f, size.y), top);
            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                    CreateBox(root.transform, $"Leg {x} {z}", new Vector3(x * (size.x * 0.4f), 0.36f, z * (size.y * 0.38f)), new Vector3(0.11f, 0.72f, 0.11f), legs);
            }
        }

        private static void CreateChair(Transform parent, string name, Vector3 position, float rotationY, Material seat, Material frame)
        {
            GameObject root = CreatePropRoot(parent, name, position, rotationY);
            CreateBox(root.transform, "Seat", new Vector3(0f, 0.48f, 0f), new Vector3(0.62f, 0.12f, 0.62f), seat);
            CreateBox(root.transform, "Back", new Vector3(0f, 0.91f, 0.27f), new Vector3(0.62f, 0.72f, 0.1f), seat);
            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                    CreateBox(root.transform, $"Leg {x} {z}", new Vector3(x * 0.24f, 0.23f, z * 0.24f), new Vector3(0.07f, 0.46f, 0.07f), frame);
            }
        }

        private static void CreateLocker(Transform parent, string name, Vector3 position, Vector3 size, Material body, Material trim)
        {
            GameObject root = CreatePropRoot(parent, name, position);
            CreateBox(root.transform, "Locker Body", new Vector3(0f, size.y * 0.5f, 0f), size, body);
            CreateBox(root.transform, "Locker Door A", new Vector3(0f, size.y * 0.74f, -size.z * 0.505f), new Vector3(size.x * 0.9f, size.y * 0.46f, 0.025f), body, false);
            CreateBox(root.transform, "Locker Door B", new Vector3(0f, size.y * 0.25f, -size.z * 0.505f), new Vector3(size.x * 0.9f, size.y * 0.46f, 0.025f), body, false);
            for (int index = 0; index < 4; index++)
                CreateBox(root.transform, $"Vent {index + 1}", new Vector3(0f, size.y * 0.84f - index * 0.09f, -size.z * 0.525f), new Vector3(size.x * 0.46f, 0.025f, 0.015f), trim, false);
        }

        private static void CreateWorkDesk(Transform parent, string name, Vector3 position, float rotationY, float width, Material top, Material body, Material trim)
        {
            GameObject root = CreatePropRoot(parent, name, position, rotationY);
            CreateBox(root.transform, "Desktop", new Vector3(0f, 0.82f, 0f), new Vector3(width, 0.14f, 1.25f), top);
            CreateBox(root.transform, "Left Pedestal", new Vector3(-width * 0.36f, 0.39f, 0.05f), new Vector3(0.82f, 0.78f, 1.05f), body);
            CreateBox(root.transform, "Right Side Panel", new Vector3(width * 0.43f, 0.39f, 0.05f), new Vector3(0.12f, 0.78f, 1.05f), body);
            CreateBox(root.transform, "Rear Modesty Panel", new Vector3(0.25f, 0.48f, 0.53f), new Vector3(width * 0.62f, 0.5f, 0.08f), body);
            for (int drawer = 0; drawer < 3; drawer++)
            {
                CreateBox(root.transform, $"Drawer {drawer + 1}", new Vector3(-width * 0.36f, 0.18f + drawer * 0.24f, -0.495f), new Vector3(0.72f, 0.19f, 0.05f), body, false);
                CreateBox(root.transform, $"Drawer Pull {drawer + 1}", new Vector3(-width * 0.36f, 0.18f + drawer * 0.24f, -0.535f), new Vector3(0.28f, 0.035f, 0.035f), trim, false);
            }
            for (int shelf = 0; shelf < 2; shelf++)
                CreateBox(root.transform, $"Side Shelf {shelf + 1}", new Vector3(width * 0.34f, 0.18f + shelf * 0.31f, 0.05f), new Vector3(0.7f, 0.08f, 1.0f), trim);
        }

        private static void CreateCrtMonitor(Transform parent, string name, Vector3 position, float rotationY, Material body, Material trim, Material screen)
        {
            GameObject root = CreatePropRoot(parent, name, position, rotationY);
            CreateBox(root.transform, "Monitor Foot", new Vector3(0f, 0.055f, 0.05f), new Vector3(0.82f, 0.11f, 0.56f), trim);
            CreateBox(root.transform, "Monitor Neck", new Vector3(0f, 0.2f, 0.08f), new Vector3(0.34f, 0.3f, 0.3f), body);
            CreateBox(root.transform, "CRT Case", new Vector3(0f, 0.69f, 0.08f), new Vector3(1.28f, 0.82f, 0.78f), body);
            CreateBox(root.transform, "CRT Bezel", new Vector3(0f, 0.72f, -0.325f), new Vector3(1.12f, 0.64f, 0.07f), trim, false);
            CreateBox(root.transform, "CRT Screen", new Vector3(-0.05f, 0.74f, -0.37f), new Vector3(0.88f, 0.47f, 0.025f), screen, false);
            CreateBox(root.transform, "Power Button", new Vector3(0.48f, 0.49f, -0.38f), new Vector3(0.1f, 0.1f, 0.035f), trim, false);
            CreateBox(root.transform, "Status Light", new Vector3(0.32f, 0.49f, -0.38f), new Vector3(0.055f, 0.055f, 0.035f), screen, false);
            for (int vent = 0; vent < 4; vent++)
                CreateBox(root.transform, $"Side Vent {vent + 1}", new Vector3(0.655f, 0.76f - vent * 0.1f, 0.1f), new Vector3(0.025f, 0.045f, 0.38f), trim, false);
        }

        private static void CreateConveyor(Transform parent, string name, Vector3 position, float length, Material body, Material trim)
        {
            GameObject root = CreatePropRoot(parent, name, position);
            CreateBox(root.transform, "Conveyor Frame Left", new Vector3(-0.72f, 0.66f, 0f), new Vector3(0.12f, 0.18f, length), body);
            CreateBox(root.transform, "Conveyor Frame Right", new Vector3(0.72f, 0.66f, 0f), new Vector3(0.12f, 0.18f, length), body);
            CreateBox(root.transform, "Conveyor Belt", new Vector3(0f, 0.72f, 0f), new Vector3(1.34f, 0.09f, length - 0.12f), trim);
            int rollerCount = Mathf.FloorToInt(length / 0.55f);
            for (int roller = 0; roller <= rollerCount; roller++)
            {
                float z = -length * 0.5f + 0.22f + roller * ((length - 0.44f) / rollerCount);
                CreateCylinder(root.transform, $"Roller {roller + 1}", new Vector3(0f, 0.79f, z), 0.08f, 1.34f, body, new Vector3(0f, 0f, 90f), false);
            }
            for (int z = -1; z <= 1; z += 2)
            {
                CreateBox(root.transform, $"Leg Left {z}", new Vector3(-0.58f, 0.31f, z * (length * 0.38f)), new Vector3(0.1f, 0.62f, 0.1f), body);
                CreateBox(root.transform, $"Leg Right {z}", new Vector3(0.58f, 0.31f, z * (length * 0.38f)), new Vector3(0.1f, 0.62f, 0.1f), body);
            }
        }

        private static GameObject CreateWorkBench(Transform parent, string name, Vector3 position, float width, Material top, Material body)
        {
            GameObject root = CreatePropRoot(parent, name, position);
            CreateBox(root.transform, "Packing Surface", new Vector3(0f, 0.92f, 0f), new Vector3(width, 0.14f, 0.82f), top);
            for (int x = -1; x <= 1; x += 2)
                CreateBox(root.transform, $"Bench Leg {x}", new Vector3(x * (width * 0.43f), 0.44f, 0f), new Vector3(0.16f, 0.88f, 0.65f), body);
            CreateBox(root.transform, "Lower Shelf", new Vector3(0f, 0.3f, 0f), new Vector3(width * 0.78f, 0.1f, 0.64f), body);
            return root;
        }

        private static void CreateStampSet(Transform parent, string name, Vector3 position, Material handles, Material markings, Material tray)
        {
            GameObject root = CreatePropRoot(parent, name, position);
            CreateBox(root.transform, "Stamp Tray", new Vector3(0f, 0.03f, 0f), new Vector3(1.45f, 0.06f, 0.42f), tray, false);
            for (int stamp = 0; stamp < 6; stamp++)
            {
                float x = -0.58f + stamp * 0.232f;
                CreateCylinder(root.transform, $"Pictogram Stamp {stamp + 1} Base", new Vector3(x, 0.12f, 0f), 0.09f, 0.08f, markings, Vector3.zero, false);
                CreateBox(root.transform, $"Pictogram Stamp {stamp + 1} Handle", new Vector3(x, 0.32f, 0f), new Vector3(0.11f, 0.32f, 0.11f), handles, false);
                CreateBox(root.transform, $"Pictogram Stamp {stamp + 1} Grip", new Vector3(x, 0.5f, 0f), new Vector3(0.17f, 0.08f, 0.13f), handles, false);
            }
        }

        private static void CreateLetterStack(Transform parent, string name, Vector3 position, Material paper, Material trim)
        {
            GameObject root = CreatePropRoot(parent, name, position, -4f);
            for (int letter = 0; letter < 4; letter++)
                CreateBox(root.transform, $"Envelope {letter + 1}", new Vector3(letter * 0.035f, letter * 0.018f, -letter * 0.02f), new Vector3(0.78f, 0.018f, 0.48f), paper, false);
            CreateBox(root.transform, "Letter Strap", new Vector3(0.05f, 0.075f, -0.03f), new Vector3(0.14f, 0.02f, 0.52f), trim, false);
        }

        private static void CreateServiceCounter(Transform parent, string name, Vector3 position, Material furniture, Material equipment, Material trim)
        {
            GameObject root = CreatePropRoot(parent, name, position);
            CreateBox(root.transform, "Counter Body", new Vector3(0f, 0.47f, 0f), new Vector3(6.7f, 0.94f, 1.16f), furniture);
            CreateBox(root.transform, "Counter Top", new Vector3(0f, 1.0f, 0f), new Vector3(6.9f, 0.12f, 1.36f), trim);
            CreateBox(root.transform, "Front Kick Plate", new Vector3(0f, 0.16f, -0.595f), new Vector3(6.45f, 0.23f, 0.04f), equipment, false);
            for (int panel = -2; panel <= 2; panel++)
                CreateBox(root.transform, $"Front Panel Trim {panel + 3}", new Vector3(panel * 1.25f, 0.59f, -0.602f), new Vector3(0.045f, 0.58f, 0.025f), trim, false);
            CreateBox(root.transform, "Privacy Shelf", new Vector3(2.55f, 1.27f, 0.28f), new Vector3(1.2f, 0.48f, 0.12f), equipment);
        }

        private static void CreateParcelScale(Transform parent, string name, Vector3 position, Material body, Material trim, Material screen)
        {
            GameObject root = CreatePropRoot(parent, name, position);
            CreateBox(root.transform, "Scale Base", new Vector3(0f, 0.09f, 0f), new Vector3(1.18f, 0.18f, 0.82f), body);
            CreateBox(root.transform, "Scale Platform", new Vector3(-0.12f, 0.22f, 0f), new Vector3(0.9f, 0.08f, 0.72f), trim);
            CreateBox(root.transform, "Scale Display Housing", new Vector3(0.48f, 0.34f, 0f), new Vector3(0.26f, 0.44f, 0.36f), body);
            CreateBox(root.transform, "Scale Display", new Vector3(0.615f, 0.4f, -0.02f), new Vector3(0.025f, 0.18f, 0.24f), screen, false);
        }

        private static void CreateBench(Transform parent, string name, Vector3 position, float width, Material frame, Material cushion, float rotationY = 0f)
        {
            GameObject root = CreatePropRoot(parent, name, position, rotationY);
            CreateBox(root.transform, "Bench Cushion", new Vector3(0f, 0.58f, 0f), new Vector3(width, 0.24f, 0.78f), cushion);
            CreateBox(root.transform, "Bench Back", new Vector3(0f, 1.0f, 0.34f), new Vector3(width, 0.72f, 0.16f), cushion);
            for (int x = -1; x <= 1; x += 2)
                CreateBox(root.transform, $"Bench Support {x}", new Vector3(x * (width * 0.4f), 0.28f, 0f), new Vector3(0.16f, 0.56f, 0.65f), frame);
        }

        private static void CreateDrawerCabinet(Transform parent, string name, Vector3 position, float rotationY, Vector3 size, Material body, Material trim)
        {
            GameObject root = CreatePropRoot(parent, name, position, rotationY);
            CreateBox(root.transform, "Cabinet Body", new Vector3(0f, size.y * 0.5f, 0f), size, body);
            int drawerCount = 4;
            for (int drawer = 0; drawer < drawerCount; drawer++)
            {
                float y = size.y * (0.14f + drawer * 0.235f);
                CreateBox(root.transform, $"Drawer Face {drawer + 1}", new Vector3(0f, y, -size.z * 0.505f), new Vector3(size.x * 0.9f, size.y * 0.18f, 0.035f), body, false);
                CreateBox(root.transform, $"Drawer Pull {drawer + 1}", new Vector3(0f, y, -size.z * 0.54f), new Vector3(size.x * 0.32f, 0.04f, 0.04f), trim, false);
            }
        }

        private static void CreateLetterTray(Transform parent, string name, Vector3 position, Material paper, Material trim)
        {
            GameObject root = CreatePropRoot(parent, name, position, 2f);
            CreateBox(root.transform, "Tray Bottom", Vector3.zero, new Vector3(1.05f, 0.06f, 0.62f), trim, false);
            CreateBox(root.transform, "Tray Left", new Vector3(-0.5f, 0.12f, 0f), new Vector3(0.06f, 0.24f, 0.62f), trim, false);
            CreateBox(root.transform, "Tray Right", new Vector3(0.5f, 0.12f, 0f), new Vector3(0.06f, 0.24f, 0.62f), trim, false);
            for (int letter = 0; letter < 3; letter++)
                CreateBox(root.transform, $"Incoming Envelope {letter + 1}", new Vector3(0f, 0.06f + letter * 0.018f, 0f), new Vector3(0.78f, 0.016f, 0.45f), paper, false);
        }

        private static void CreateEngine(Transform parent, string name, Vector3 position, Material body, Material trim, Material glow)
        {
            GameObject root = CreatePropRoot(parent, name, position);
            CreateBox(root.transform, "Engine Base", new Vector3(0f, 0.18f, 0f), new Vector3(2.65f, 0.36f, 1.85f), trim);
            CreateBox(root.transform, "Engine Block", new Vector3(0f, 0.82f, 0f), new Vector3(2.2f, 1.05f, 1.45f), body);
            for (int cylinder = -1; cylinder <= 1; cylinder++)
                CreateCylinder(root.transform, $"Engine Cylinder {cylinder + 2}", new Vector3(cylinder * 0.65f, 1.48f, 0f), 0.26f, 0.65f, trim);
            CreateCylinder(root.transform, "Flywheel", new Vector3(1.18f, 0.82f, 0f), 0.48f, 0.16f, trim, new Vector3(0f, 0f, 90f));
            CreateCylinder(root.transform, "Pressure Gauge", new Vector3(-0.65f, 1.6f, -0.46f), 0.2f, 0.08f, glow, new Vector3(90f, 0f, 0f), false);
            CreateBox(root.transform, "Control Panel", new Vector3(0f, 0.95f, -0.755f), new Vector3(0.72f, 0.44f, 0.08f), trim, false);
            for (int light = -1; light <= 1; light++)
                CreateSphere(root.transform, $"Engine Indicator {light + 2}", new Vector3(light * 0.2f, 1.02f, -0.81f), 0.065f, glow, false);
        }

        private static void CreatePipeRack(Transform parent, string name, Vector3 position, Material pipes, Material frame)
        {
            GameObject root = CreatePropRoot(parent, name, position);
            CreateBox(root.transform, "Pipe Rack Frame", new Vector3(0f, 1.05f, 0f), new Vector3(0.18f, 2.1f, 2.35f), frame);
            for (int pipe = -1; pipe <= 1; pipe++)
                CreateCylinder(root.transform, $"Vertical Pipe {pipe + 2}", new Vector3(-0.18f, 1.05f, pipe * 0.67f), 0.11f, 2.1f, pipes);
            CreateCylinder(root.transform, "Header Pipe", new Vector3(-0.18f, 1.85f, 0f), 0.13f, 1.55f, pipes, new Vector3(90f, 0f, 0f));
            for (int valve = -1; valve <= 1; valve++)
                CreateCylinder(root.transform, $"Valve Wheel {valve + 2}", new Vector3(-0.38f, 1.15f, valve * 0.67f), 0.2f, 0.05f, frame, new Vector3(0f, 0f, 90f), false);
        }

        private static void CreateRoomShell(
            Transform parent,
            string name,
            float centerX,
            float centerZ,
            float width,
            float depth,
            Material floor,
            Material ceiling)
        {
            CreateRoomFloor(parent, name, centerX, centerZ, width, depth, floor);
            CreateBox(parent, name + " Ceiling", new Vector3(centerX, 2.89f, centerZ), new Vector3(width, 0.18f, depth), ceiling);
        }

        private static void CreateFloorArrow(Transform parent, string name, Vector3 position, float angle, Material material)
        {
            GameObject arrow = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(arrow, "Create hall direction marker");
            arrow.transform.SetParent(parent, false);
            arrow.transform.localPosition = position;
            arrow.transform.localRotation = Quaternion.Euler(0f, angle, 0f);

            GameObject stem = CreateBox(arrow.transform, "Stem", new Vector3(0f, 0f, 0f), new Vector3(0.16f, 0.03f, 0.85f), material);
            GameObject leftHead = CreateBox(arrow.transform, "Left Head", new Vector3(-0.17f, 0f, 0.46f), new Vector3(0.15f, 0.03f, 0.52f), material);
            GameObject rightHead = CreateBox(arrow.transform, "Right Head", new Vector3(0.17f, 0f, 0.46f), new Vector3(0.15f, 0.03f, 0.52f), material);
            leftHead.transform.localRotation = Quaternion.Euler(0f, -42f, 0f);
            rightHead.transform.localRotation = Quaternion.Euler(0f, 42f, 0f);
            stem.GetComponent<Collider>().enabled = false;
            leftHead.GetComponent<Collider>().enabled = false;
            rightHead.GetComponent<Collider>().enabled = false;
        }

        private static void CreateRoomFloor(Transform parent, string name, float centerX, float centerZ, float width, float depth, Material material)
        {
            CreateBox(parent, name + " Floor", new Vector3(centerX, -0.1f, centerZ), new Vector3(width, 0.2f, depth), material);
        }

        private static void CreateHorizontalWall(Transform parent, string name, float minX, float maxX, float z, Material material)
        {
            float length = maxX - minX;
            CreateBox(parent, name, new Vector3((minX + maxX) * 0.5f, WallHeight * 0.5f, z), new Vector3(length, WallHeight, WallThickness), material);
        }

        private static void CreateVerticalWall(Transform parent, string name, float x, float minZ, float maxZ, Material material)
        {
            float length = maxZ - minZ;
            CreateBox(parent, name, new Vector3(x, WallHeight * 0.5f, (minZ + maxZ) * 0.5f), new Vector3(WallThickness, WallHeight, length), material);
        }

        private static void CreateDoorFrame(Transform parent, string name, float x, float z, bool horizontalWall, Material material)
        {
            float sideOffset = DoorWidth * 0.5f;
            if (horizontalWall)
            {
                CreateBox(parent, name + " Left Post", new Vector3(x - sideOffset, DoorHeight * 0.5f, z), new Vector3(0.14f, DoorHeight, WallThickness), material);
                CreateBox(parent, name + " Right Post", new Vector3(x + sideOffset, DoorHeight * 0.5f, z), new Vector3(0.14f, DoorHeight, WallThickness), material);
                CreateBox(parent, name + " Lintel", new Vector3(x, DoorHeight + (WallHeight - DoorHeight) * 0.5f, z), new Vector3(DoorWidth, WallHeight - DoorHeight, WallThickness), material);
            }
            else
            {
                CreateBox(parent, name + " Near Post", new Vector3(x, DoorHeight * 0.5f, z - sideOffset), new Vector3(WallThickness, DoorHeight, 0.14f), material);
                CreateBox(parent, name + " Far Post", new Vector3(x, DoorHeight * 0.5f, z + sideOffset), new Vector3(WallThickness, DoorHeight, 0.14f), material);
                CreateBox(parent, name + " Lintel", new Vector3(x, DoorHeight + (WallHeight - DoorHeight) * 0.5f, z), new Vector3(WallThickness, WallHeight - DoorHeight, DoorWidth), material);
            }
        }

        private static HingedDoor CreateHingedDoor(
            Transform parent,
            string name,
            float x,
            float z,
            bool horizontalWall,
            float openAngle,
            Material doorMaterial,
            Material handleMaterial,
            bool addWarningPlate = false)
        {
            float leafWidth = DoorWidth - 0.16f;
            float leafHeight = DoorHeight - 0.08f;
            float halfWidth = leafWidth * 0.5f;

            GameObject pivot = new GameObject(name + " Pivot");
            Undo.RegisterCreatedObjectUndo(pivot, "Create hinged door");
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = horizontalWall
                ? new Vector3(x - halfWidth, 0.04f, z)
                : new Vector3(x, 0.04f, z - halfWidth);

            Vector3 leafPosition = horizontalWall
                ? new Vector3(halfWidth, leafHeight * 0.5f, 0f)
                : new Vector3(0f, leafHeight * 0.5f, halfWidth);
            Vector3 leafSize = horizontalWall
                ? new Vector3(leafWidth, leafHeight, 0.12f)
                : new Vector3(0.12f, leafHeight, leafWidth);
            GameObject leaf = CreateBox(pivot.transform, name + " Leaf", leafPosition, leafSize, doorMaterial);
            leaf.isStatic = false;

            Vector3 upperPanelPosition = horizontalWall
                ? new Vector3(halfWidth, 1.56f, -0.071f)
                : new Vector3(-0.071f, 1.56f, halfWidth);
            Vector3 lowerPanelPosition = horizontalWall
                ? new Vector3(halfWidth, 0.62f, -0.071f)
                : new Vector3(-0.071f, 0.62f, halfWidth);
            Vector3 panelSize = horizontalWall
                ? new Vector3(leafWidth * 0.72f, 0.58f, 0.025f)
                : new Vector3(0.025f, 0.58f, leafWidth * 0.72f);
            GameObject upperPanel = CreateBox(pivot.transform, name + " Upper Panel", upperPanelPosition, panelSize, handleMaterial, false);
            GameObject lowerPanel = CreateBox(pivot.transform, name + " Lower Panel", lowerPanelPosition, panelSize, handleMaterial, false);
            upperPanel.isStatic = false;
            lowerPanel.isStatic = false;

            Vector3 handlePosition = horizontalWall
                ? new Vector3(leafWidth * 0.78f, 1.02f, -0.09f)
                : new Vector3(-0.09f, 1.02f, leafWidth * 0.78f);
            GameObject handle = CreateBox(pivot.transform, name + " Handle", handlePosition, new Vector3(0.11f, 0.11f, 0.11f), handleMaterial);
            handle.isStatic = false;
            handle.GetComponent<Collider>().enabled = false;

            for (int hingeIndex = 0; hingeIndex < 3; hingeIndex++)
            {
                Vector3 hingePosition = horizontalWall
                    ? new Vector3(0f, 0.45f + hingeIndex * 0.65f, 0f)
                    : new Vector3(0f, 0.45f + hingeIndex * 0.65f, 0f);
                GameObject hingePin = CreateCylinder(pivot.transform, name + $" Hinge Pin {hingeIndex + 1}", hingePosition, 0.055f, 0.22f, handleMaterial, Vector3.zero, false);
                hingePin.isStatic = false;
            }

            if (addWarningPlate)
            {
                Vector3 platePosition = horizontalWall
                    ? new Vector3(halfWidth, 1.67f, -0.071f)
                    : new Vector3(-0.071f, 1.67f, halfWidth);
                Vector3 plateSize = horizontalWall
                    ? new Vector3(0.8f, 0.28f, 0.02f)
                    : new Vector3(0.02f, 0.28f, 0.8f);
                GameObject plate = CreateBox(pivot.transform, "Hidden Door Warning Plate", platePosition, plateSize, handleMaterial);
                plate.isStatic = false;
                plate.GetComponent<Collider>().enabled = false;
            }

            HingedDoor door = Undo.AddComponent<HingedDoor>(pivot);
            SerializedObject doorData = new SerializedObject(door);
            doorData.FindProperty("hinge").objectReferenceValue = pivot.transform;
            doorData.FindProperty("openAngle").floatValue = Mathf.Abs(openAngle);
            doorData.FindProperty("wallNormal").vector3Value = horizontalWall ? Vector3.forward : Vector3.right;
            doorData.FindProperty("positiveSideAngleSign").floatValue = horizontalWall ? 1f : -1f;
            doorData.ApplyModifiedPropertiesWithoutUndo();
            return door;
        }

        private static GameObject CreateBox(Transform parent, string name, Vector3 position, Vector3 size, Material material, bool colliderEnabled = true)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(box, "Create graybox piece");
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = size;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
            box.GetComponent<Collider>().enabled = colliderEnabled;
            box.isStatic = true;
            return box;
        }

        private static GameObject CreateCylinder(
            Transform parent,
            string name,
            Vector3 position,
            float radius,
            float height,
            Material material,
            Vector3 eulerAngles = default,
            bool colliderEnabled = true)
        {
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Undo.RegisterCreatedObjectUndo(cylinder, "Create graybox cylinder");
            cylinder.name = name;
            cylinder.transform.SetParent(parent, false);
            cylinder.transform.localPosition = position;
            cylinder.transform.localRotation = Quaternion.Euler(eulerAngles);
            cylinder.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            cylinder.GetComponent<MeshRenderer>().sharedMaterial = material;
            cylinder.GetComponent<Collider>().enabled = colliderEnabled;
            cylinder.isStatic = true;
            return cylinder;
        }

        private static GameObject CreateSphere(Transform parent, string name, Vector3 position, float diameter, Material material, bool colliderEnabled = true)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Undo.RegisterCreatedObjectUndo(sphere, "Create graybox sphere");
            sphere.name = name;
            sphere.transform.SetParent(parent, false);
            sphere.transform.localPosition = position;
            sphere.transform.localScale = Vector3.one * diameter;
            sphere.GetComponent<MeshRenderer>().sharedMaterial = material;
            sphere.GetComponent<Collider>().enabled = colliderEnabled;
            sphere.isStatic = true;
            return sphere;
        }

        private static void CreatePlayer(Transform parent)
        {
            GameObject player = new GameObject("Player Controller");
            Undo.RegisterCreatedObjectUndo(player, "Create player controller");
            player.transform.SetParent(parent, false);
            player.transform.position = new Vector3(0f, 0.05f, -10.1f);
            player.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.32f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.3f;
            controller.slopeLimit = 45f;

            GameObject pivot = new GameObject("View Pivot");
            Undo.RegisterCreatedObjectUndo(pivot, "Create player view pivot");
            pivot.transform.SetParent(player.transform, false);
            pivot.transform.localPosition = new Vector3(0f, 1.55f, 0f);

            GameObject cameraObject = new GameObject("Player Camera");
            Undo.RegisterCreatedObjectUndo(cameraObject, "Create player camera");
            cameraObject.transform.SetParent(pivot.transform, false);
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.fieldOfView = 72f;
            cameraObject.AddComponent<AudioListener>();

            FirstPersonController movement = player.AddComponent<FirstPersonController>();
            SerializedObject movementData = new SerializedObject(movement);
            movementData.FindProperty("viewPivot").objectReferenceValue = pivot.transform;
            movementData.ApplyModifiedPropertiesWithoutUndo();

            FirstPersonInteractor interactor = player.AddComponent<FirstPersonInteractor>();
            SerializedObject interactionData = new SerializedObject(interactor);
            interactionData.FindProperty("playerCamera").objectReferenceValue = camera;
            interactionData.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void DisableTemplateCamerasAndPostProcessing()
        {
            foreach (Camera camera in Object.FindObjectsByType<Camera>())
            {
                camera.enabled = false;
                if (camera.gameObject.CompareTag("MainCamera"))
                    camera.gameObject.tag = "Untagged";
            }

            foreach (AudioListener listener in Object.FindObjectsByType<AudioListener>())
                listener.enabled = false;

            foreach (Volume volume in Object.FindObjectsByType<Volume>())
                volume.enabled = false;

            foreach (Light light in Object.FindObjectsByType<Light>())
                light.enabled = false;
        }

        private static Material GetOrCreateMaterial(string assetName, Color color)
        {
            const string folder = "Assets/Graybox/Materials";
            EnsureFolder("Assets", "Graybox");
            EnsureFolder("Assets/Graybox", "Materials");

            string path = folder + "/" + assetName + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                    shader = Shader.Find("Standard");
                material = new Material(shader) { name = assetName };
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            else if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}
