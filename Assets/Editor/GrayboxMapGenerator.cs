using DeadLetter.Interaction;
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
            bool playerNeedsRelocation = player == null || Vector3.Distance(player.position, new Vector3(0f, 0.05f, -12.2f)) > 0.05f || Mathf.Abs(Mathf.DeltaAngle(player.eulerAngles.y, 0f)) > 1f;
            if (root == null || playerNeedsRelocation || root.transform.Find("Reference Layout Marker") == null || root.transform.Find("Interactable Fuse Box") == null || root.transform.Find("Storage Hall Door Pivot") == null || root.transform.Find("Pantry Hall Door Pivot") == null || root.transform.Find("Workshop Hall Door Pivot") == null || root.transform.Find("Counter Hall Door Pivot") == null || root.transform.Find("Hidden Room Door Pivot") == null)
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
            GameObject layoutMarker = new GameObject("Reference Layout Marker");
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
            ConfigureEmission(glow, new Color(1f, 0.32f, 0.055f) * 1.8f);

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
            CreateHorizontalWall(root.transform, "Counter South Wall Left", -4.5f, -1.05f, -16f, wall);
            CreateHorizontalWall(root.transform, "Counter South Wall Right", 1.05f, 4.5f, -16f, wall);
            CreateDoorFrame(root.transform, "Counter Exterior Door", 0f, -16f, true, wall);
            CreateHingedDoor(root.transform, "Counter Exterior Door", 0f, -16f, true, -90f, closedDoor, trim);
            CreateVerticalWall(root.transform, "Counter West Wall", -4.5f, -16f, -7f, wall);
            CreateVerticalWall(root.transform, "Counter East Wall", 4.5f, -16f, -7f, wall);
            CreateHorizontalWall(root.transform, "Counter Hall Wall Left", -4.5f, -1.05f, -7f, wall);
            CreateHorizontalWall(root.transform, "Counter Hall Wall Right", 1.05f, 4.5f, -7f, wall);
            CreateDoorFrame(root.transform, "Counter Hall Door", 0f, -7f, true, wall);
            CreateHingedDoor(root.transform, "Counter Hall Door", 0f, -7f, true, 90f, closedDoor, trim);

            CreateStorageFurniture(root.transform, furniture, equipment, trim);
            CreatePantryFurniture(root.transform, furniture, equipment);
            CreateWorkshopFurniture(root.transform, furniture, equipment, trim);
            CreateCounterFurniture(root.transform, furniture, equipment, trim);
            CreateHiddenRoomFurniture(root.transform, furniture, equipment, trim);
            CreateInteractableFuseBox(root.transform, equipment, trim, closedDoor);

            CreatePlayer(root.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrEmpty(scene.path))
                EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = root;
            Debug.Log("DEAD LETTER reference map generated with storage, pantry, workshop, counter, and hidden room.");
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

        private static void CreateStorageFurniture(Transform parent, Material furniture, Material equipment, Material trim)
        {
            CreateBox(parent, "Storage West Shelves", new Vector3(-3.85f, 0.95f, 12.5f), new Vector3(0.7f, 1.9f, 5.6f), equipment);
            CreateBox(parent, "Storage East Shelves", new Vector3(3.85f, 0.95f, 12.5f), new Vector3(0.7f, 1.9f, 5.6f), equipment);
            CreateBox(parent, "Storage Rear Shelves", new Vector3(0f, 0.95f, 15.25f), new Vector3(4.8f, 1.9f, 0.75f), equipment);
            CreateBox(parent, "Storage Parcel Stack A", new Vector3(-2.35f, 0.65f, 9.8f), new Vector3(1.5f, 1.3f, 1.7f), furniture);
            CreateBox(parent, "Storage Parcel Stack B", new Vector3(2.15f, 0.45f, 10.2f), new Vector3(1.8f, 0.9f, 1.4f), furniture);
            CreateBox(parent, "Storage Cart", new Vector3(0.7f, 0.42f, 13.1f), new Vector3(1.3f, 0.84f, 1.8f), trim);
        }

        private static void CreatePantryFurniture(Transform parent, Material furniture, Material equipment)
        {
            CreateBox(parent, "Pantry Refrigerator", new Vector3(-14.6f, 0.9f, 3f), new Vector3(1.5f, 1.8f, 1.6f), equipment);
            CreateBox(parent, "Pantry Kitchen Counter", new Vector3(-11.3f, 0.75f, 3.8f), new Vector3(4.8f, 1.5f, 0.9f), furniture);
            CreateBox(parent, "Pantry Sofa", new Vector3(-14.2f, 0.45f, -2.5f), new Vector3(1.5f, 0.9f, 3.2f), furniture);
            CreateBox(parent, "Pantry Table", new Vector3(-11.1f, 0.35f, -1.2f), new Vector3(2.5f, 0.7f, 2.2f), furniture);
            CreateBox(parent, "Pantry Bench", new Vector3(-9f, 0.45f, 2.2f), new Vector3(0.75f, 0.9f, 2.6f), equipment);
        }

        private static void CreateWorkshopFurniture(Transform parent, Material furniture, Material equipment, Material trim)
        {
            CreateBox(parent, "Workshop Sorting Table", new Vector3(11.3f, 0.45f, 1.5f), new Vector3(4.2f, 0.9f, 2.1f), furniture);
            CreateBox(parent, "Workshop Monitor", new Vector3(11.3f, 1.25f, 2.35f), new Vector3(1.2f, 0.75f, 0.18f), equipment);
            CreateBox(parent, "Workshop Conveyor", new Vector3(14.6f, 0.4f, -0.5f), new Vector3(1.5f, 0.8f, 5.5f), trim);
            CreateBox(parent, "Workshop Packing Bench", new Vector3(10f, 0.45f, -3.45f), new Vector3(4.6f, 0.9f, 0.8f), furniture);
            CreateBox(parent, "Workshop Parcel Shelves", new Vector3(15.15f, 0.95f, 3.1f), new Vector3(0.7f, 1.9f, 2.2f), equipment);
        }

        private static void CreateCounterFurniture(Transform parent, Material furniture, Material equipment, Material trim)
        {
            CreateBox(parent, "Counter Desk", new Vector3(0f, 0.45f, -10.2f), new Vector3(6.8f, 0.9f, 1.35f), furniture);
            CreateBox(parent, "Counter Monitor", new Vector3(1.4f, 1.15f, -10.65f), new Vector3(1.3f, 0.75f, 0.18f), equipment);
            CreateBox(parent, "Counter Parcel Scale", new Vector3(-1.8f, 1.02f, -10.45f), new Vector3(1.1f, 0.22f, 0.8f), trim);
            CreateBox(parent, "Counter Waiting Bench", new Vector3(0f, 0.42f, -14.4f), new Vector3(4.8f, 0.84f, 0.85f), furniture);
            CreateBox(parent, "Counter Side Cabinet", new Vector3(3.75f, 0.75f, -13f), new Vector3(0.75f, 1.5f, 2.6f), equipment);
        }

        private static void CreateHiddenRoomFurniture(Transform parent, Material furniture, Material equipment, Material trim)
        {
            CreateBox(parent, "Hidden Engine Block", new Vector3(-7.6f, 0.8f, 7.8f), new Vector3(2.5f, 1.6f, 1.8f), equipment);
            CreateBox(parent, "Hidden Pipe Rack", new Vector3(-9.75f, 1.05f, 6.5f), new Vector3(0.65f, 2.1f, 2.4f), trim);
            CreateBox(parent, "Hidden Tool Cabinet", new Vector3(-6.4f, 0.8f, 4.9f), new Vector3(1.8f, 1.6f, 0.7f), furniture);
        }

        private static void CreateInteractableFuseBox(Transform parent, Material equipment, Material trim, Material indicator)
        {
            GameObject panel = CreateBox(parent, "Interactable Fuse Box", new Vector3(-10.18f, 1.35f, 7f), new Vector3(0.72f, 0.75f, 0.32f), equipment);
            // The local front points toward +X so the controls face into the room from its west wall.
            panel.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);

            GameObject indicatorObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Undo.RegisterCreatedObjectUndo(indicatorObject, "Create fuse box indicator");
            indicatorObject.name = "Fuse Box Indicator";
            indicatorObject.transform.SetParent(panel.transform, false);
            indicatorObject.transform.localPosition = new Vector3(0f, 0.22f, -0.17f);
            indicatorObject.transform.localScale = new Vector3(0.15f, 0.15f, 0.08f);
            indicatorObject.GetComponent<MeshRenderer>().sharedMaterial = indicator;
            indicatorObject.GetComponent<Collider>().enabled = false;

            GameObject lever = CreateBox(panel.transform, "Fuse Lever", new Vector3(0f, -0.13f, -0.18f), new Vector3(0.11f, 0.38f, 0.12f), trim);
            lever.transform.localRotation = Quaternion.Euler(0f, 0f, -25f);

            TinyFuseBox interactable = Undo.AddComponent<TinyFuseBox>(panel);
            SerializedObject data = new SerializedObject(interactable);
            data.FindProperty("lever").objectReferenceValue = lever.transform;
            data.FindProperty("indicatorRenderer").objectReferenceValue = indicatorObject.GetComponent<Renderer>();
            data.ApplyModifiedPropertiesWithoutUndo();
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

        private static void CreateHingedDoor(
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

            Vector3 handlePosition = horizontalWall
                ? new Vector3(leafWidth * 0.78f, 1.02f, -0.09f)
                : new Vector3(-0.09f, 1.02f, leafWidth * 0.78f);
            GameObject handle = CreateBox(pivot.transform, name + " Handle", handlePosition, new Vector3(0.11f, 0.11f, 0.11f), handleMaterial);
            handle.isStatic = false;
            handle.GetComponent<Collider>().enabled = false;

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
            doorData.FindProperty("openAngle").floatValue = openAngle;
            doorData.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreateBox(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(box, "Create graybox piece");
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = size;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
            box.isStatic = true;
            return box;
        }

        private static void CreatePlayer(Transform parent)
        {
            GameObject player = new GameObject("Player Controller");
            Undo.RegisterCreatedObjectUndo(player, "Create player controller");
            player.transform.SetParent(parent, false);
            player.transform.position = new Vector3(0f, 0.05f, -12.2f);
            player.transform.rotation = Quaternion.Euler(0f, 0f, 0f);

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
