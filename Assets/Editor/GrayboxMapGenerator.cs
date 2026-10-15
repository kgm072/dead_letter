using DeadLetter.Interaction;
using DeadLetter.Player;
using DeadLetter.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace DeadLetter.Editor
{
    public static class GrayboxMapGenerator
    {
        private const string RootName = "DEAD LETTER - Graybox";
        private const float WallHeight = 2.7f;
        private const float WallThickness = 0.18f;
        private const float DoorWidth = 1.9f;
        private const float DoorHeight = 2.2f;

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

            // Longer cross-shaped hall, with the four rooms placed at meaningful walking distance.
            CreateBox(root.transform, "Central Hall Floor", new Vector3(0f, -0.1f, 0f), new Vector3(12f, 0.2f, 3.6f), floor);
            CreateBox(root.transform, "North-South Hall Floor", new Vector3(0f, -0.1f, 0f), new Vector3(3.6f, 0.2f, 33f), floor);
            CreateBox(root.transform, "Central Hall Ceiling", new Vector3(0f, 2.89f, 0f), new Vector3(12f, 0.18f, 3.6f), ceiling);
            CreateBox(root.transform, "North-South Hall Ceiling", new Vector3(0f, 2.89f, 0f), new Vector3(3.6f, 0.18f, 33f), ceiling);
            CreateBox(root.transform, "West Spur Floor", new Vector3(-3.9f, -0.1f, 0f), new Vector3(4.2f, 0.2f, 3.6f), floor);
            CreateBox(root.transform, "West Spur Ceiling", new Vector3(-3.9f, 2.89f, 0f), new Vector3(4.2f, 0.18f, 3.6f), ceiling);
            CreateBox(root.transform, "East Spur Floor", new Vector3(3.9f, -0.1f, 0f), new Vector3(4.2f, 0.2f, 3.6f), floor);
            CreateBox(root.transform, "East Spur Ceiling", new Vector3(3.9f, 2.89f, 0f), new Vector3(4.2f, 0.18f, 3.6f), ceiling);
            CreateVerticalWall(root.transform, "West Corridor Wall Upper", -1.8f, 1.8f, 12.35f, wall);
            CreateVerticalWall(root.transform, "West Corridor Wall Lower", -1.8f, -12.35f, -1.8f, wall);
            CreateVerticalWall(root.transform, "East Corridor Wall Upper", 1.8f, 1.8f, 12.35f, wall);
            CreateVerticalWall(root.transform, "East Corridor Wall Lower", 1.8f, -12.35f, -1.8f, wall);
            CreateHorizontalWall(root.transform, "West Spur North Wall", -6f, -1.8f, 1.8f, wall);
            CreateHorizontalWall(root.transform, "West Spur South Wall", -6f, -1.8f, -1.8f, wall);
            CreateHorizontalWall(root.transform, "East Spur North Wall", 1.8f, 6f, 1.8f, wall);
            CreateHorizontalWall(root.transform, "East Spur South Wall", 1.8f, 6f, -1.8f, wall);

            // Main office / counter room, north.
            CreateRoomFloor(root.transform, "Counter Room", 0f, 16.85f, 9f, 9f, floor);
            CreateBox(root.transform, "Counter Room Ceiling", new Vector3(0f, 2.89f, 16.85f), new Vector3(9f, 0.18f, 9f), ceiling);
            CreateHorizontalWall(root.transform, "Counter North Wall Left", -4.5f, -1.05f, 21.35f, wall);
            CreateHorizontalWall(root.transform, "Counter North Wall Right", 1.05f, 4.5f, 21.35f, wall);
            CreateDoorFrame(root.transform, "Counter Exterior Door Frame", 0f, 21.35f, true, wall);
            CreateSwingDoor(root.transform, "Counter Exterior Door", 0f, 21.35f, true, closedDoor);
            CreateVerticalWall(root.transform, "Counter West Wall", -4.5f, 12.35f, 21.35f, wall);
            CreateVerticalWall(root.transform, "Counter East Wall", 4.5f, 12.35f, 21.35f, wall);
            CreateHorizontalWall(root.transform, "Counter Hall Wall Left", -4.5f, -1.05f, 12.35f, wall);
            CreateHorizontalWall(root.transform, "Counter Hall Wall Right", 1.05f, 4.5f, 12.35f, wall);
            CreateDoorFrame(root.transform, "Counter Hall Door Frame", 0f, 12.35f, true, wall);
            CreateSwingDoor(root.transform, "Counter Hall Door", 0f, 12.35f, true, closedDoor);

            // Pantry and staff lounge, west.
            CreateRoomFloor(root.transform, "Pantry", -10.5f, 0f, 9f, 9f, floor);
            CreateBox(root.transform, "Pantry Ceiling", new Vector3(-10.5f, 2.89f, 0f), new Vector3(9f, 0.18f, 9f), ceiling);
            CreateVerticalWall(root.transform, "Pantry West Wall", -15f, -4.5f, 4.5f, wall);
            CreateHorizontalWall(root.transform, "Pantry North Wall", -15f, -6f, 4.5f, wall);
            CreateHorizontalWall(root.transform, "Pantry South Wall", -15f, -6f, -4.5f, wall);
            CreateVerticalWall(root.transform, "Pantry Hall Wall Upper", -6f, 1.15f, 4.5f, wall);
            CreateVerticalWall(root.transform, "Pantry Hall Wall Lower", -6f, -4.5f, -1.15f, wall);
            CreateDoorFrame(root.transform, "Pantry Hall Door", -6f, 0f, false, wall);
            CreateSwingDoor(root.transform, "Pantry Hall Door", -6f, 0f, false, closedDoor);

            // The hidden room is physically attached to the east side of the hall.
            // Its doorway is now a normal interactive door, ready for later lock-state rules.
            CreateRoomFloor(root.transform, "Hidden Room", 10.5f, 0f, 9f, 9f, floor);
            CreateBox(root.transform, "Hidden Room Ceiling", new Vector3(10.5f, 2.89f, 0f), new Vector3(9f, 0.18f, 9f), ceiling);
            CreateVerticalWall(root.transform, "Hidden East Wall", 15f, -4.5f, 4.5f, wall);
            CreateHorizontalWall(root.transform, "Hidden North Wall", 6f, 15f, 4.5f, wall);
            CreateHorizontalWall(root.transform, "Hidden South Wall", 6f, 15f, -4.5f, wall);
            CreateVerticalWall(root.transform, "Hidden West Wall Upper", 6f, 1.15f, 4.5f, wall);
            CreateVerticalWall(root.transform, "Hidden West Wall Lower", 6f, -4.5f, -1.15f, wall);
            CreateDoorFrame(root.transform, "Hidden Room Door Frame", 6f, 0f, false, wall);
            CreateSwingDoor(root.transform, "Hidden Room Door", 6f, 0f, false, closedDoor);
            // Keep the plate flush against the corridor-facing surface of the sealed door.
            CreateBox(root.transform, "Hidden Door Warning Plate", new Vector3(5.935f, 1.7f, 0f), new Vector3(0.02f, 0.28f, 0.8f), trim);

            // Mail processing room, south.
            CreateRoomFloor(root.transform, "Mail Processing Room", 0f, -16.85f, 9f, 9f, floor);
            CreateBox(root.transform, "Mail Room Ceiling", new Vector3(0f, 2.89f, -16.85f), new Vector3(9f, 0.18f, 9f), ceiling);
            CreateHorizontalWall(root.transform, "Mail South Wall Left", -4.5f, -1.05f, -21.35f, wall);
            CreateHorizontalWall(root.transform, "Mail South Wall Right", 1.05f, 4.5f, -21.35f, wall);
            CreateDoorFrame(root.transform, "Mail Exterior Door Frame", 0f, -21.35f, true, wall);
            CreateSwingDoor(root.transform, "Mail Exterior Door", 0f, -21.35f, true, closedDoor);
            CreateVerticalWall(root.transform, "Mail West Wall", -4.5f, -21.35f, -12.35f, wall);
            CreateVerticalWall(root.transform, "Mail East Wall", 4.5f, -21.35f, -12.35f, wall);
            CreateHorizontalWall(root.transform, "Mail Hall Wall Left", -4.5f, -1.05f, -12.35f, wall);
            CreateHorizontalWall(root.transform, "Mail Hall Wall Right", 1.05f, 4.5f, -12.35f, wall);
            CreateDoorFrame(root.transform, "Mail Hall Door Frame", 0f, -12.35f, true, wall);
            CreateSwingDoor(root.transform, "Mail Hall Door", 0f, -12.35f, true, closedDoor);

            CreateCounterFurniture(root.transform, furniture, equipment, trim);
            CreatePantryFurniture(root.transform, furniture, equipment);
            CreateMailFurniture(root.transform, furniture, equipment, trim);
            CreateInteractableFuseBox(root.transform, equipment, trim, closedDoor);

            CreatePlayer(root.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrEmpty(scene.path))
                EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = root;
            Debug.Log("DEAD LETTER horror restaurant map generated. Right-click the wall fuse box to toggle it.");
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
            CreatePointLight(parent, "Office Sickly Light", new Vector3(0f, 2.35f, 16.8f), new Color(0.65f, 0.78f, 0.48f), 6f, 8f);
            CreatePointLight(parent, "Pantry Red Light", new Vector3(-10.5f, 2.3f, 0f), new Color(0.9f, 0.16f, 0.12f), 5f, 7.5f);
            CreatePointLight(parent, "Mail Room Warm Light", new Vector3(0f, 2.3f, -16.8f), new Color(1f, 0.4f, 0.16f), 5.5f, 7.5f);
            CreatePointLight(parent, "Sealed Room Red Glow", new Vector3(10.5f, 2.2f, 0f), new Color(0.85f, 0.08f, 0.1f), 4f, 7f);
        }

        private static void CreateLightFixtures(Transform parent, Material glow)
        {
            CreateBox(parent, "Hall Ceiling Fixture", new Vector3(0f, 2.62f, 0f), new Vector3(0.55f, 0.08f, 1.1f), glow);
            CreateBox(parent, "Office Ceiling Fixture", new Vector3(0f, 2.62f, 16.8f), new Vector3(0.55f, 0.08f, 1.1f), glow);
            CreateBox(parent, "Pantry Ceiling Fixture", new Vector3(-10.5f, 2.62f, 0f), new Vector3(0.55f, 0.08f, 1.1f), glow);
            CreateBox(parent, "Mail Ceiling Fixture", new Vector3(0f, 2.62f, -16.8f), new Vector3(0.55f, 0.08f, 1.1f), glow);
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

        private static void CreateCounterFurniture(Transform parent, Material furniture, Material equipment, Material trim)
        {
            CreateBox(parent, "Counter Desk", new Vector3(0f, 0.45f, 16.8f), new Vector3(5.8f, 0.9f, 2.4f), furniture);
            CreateBox(parent, "Counter Monitor", new Vector3(0f, 1.15f, 17.65f), new Vector3(1.7f, 0.75f, 0.18f), equipment);
            CreateBox(parent, "Counter Cashier", new Vector3(-3.75f, 0.75f, 16.8f), new Vector3(0.55f, 1.5f, 2.1f), equipment);
            CreateBox(parent, "Counter Clock", new Vector3(4.35f, 1f, 17f), new Vector3(0.16f, 1.7f, 0.8f), trim);
        }

        private static void CreatePantryFurniture(Transform parent, Material furniture, Material equipment)
        {
            CreateBox(parent, "Pantry Refrigerator", new Vector3(-13.3f, 0.9f, 3f), new Vector3(1.5f, 1.8f, 1.6f), equipment);
            CreateBox(parent, "Pantry Shelf", new Vector3(-9.5f, 0.75f, 3.1f), new Vector3(4.6f, 1.5f, 1f), furniture);
            CreateBox(parent, "Pantry Sofa", new Vector3(-13.2f, 0.45f, -1.4f), new Vector3(1.5f, 0.9f, 3.8f), furniture);
            CreateBox(parent, "Pantry Table", new Vector3(-9.5f, 0.35f, -1.1f), new Vector3(2.4f, 0.7f, 3.1f), furniture);
        }

        private static void CreateMailFurniture(Transform parent, Material furniture, Material equipment, Material trim)
        {
            CreateBox(parent, "Mail Sorting Table", new Vector3(2.7f, 0.45f, -16.8f), new Vector3(1.8f, 0.9f, 5.6f), furniture);
            CreateBox(parent, "Mail Monitor", new Vector3(2.7f, 1.25f, -14.8f), new Vector3(0.8f, 1.5f, 0.18f), equipment);
            CreateBox(parent, "Mail Conveyor", new Vector3(-1.8f, 0.4f, -18.3f), new Vector3(2.2f, 0.8f, 3.3f), trim);
        }

        private static void CreateInteractableFuseBox(Transform parent, Material equipment, Material trim, Material indicator)
        {
            GameObject panel = CreateBox(parent, "Interactable Fuse Box", new Vector3(-4.25f, 1.35f, 15.4f), new Vector3(0.72f, 0.75f, 0.32f), equipment);
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

        private static void CreateSwingDoor(Transform parent, string name, float x, float z, bool horizontalWall, Material material)
        {
            float panelWidth = DoorWidth - 0.2f;
            GameObject hinge = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(hinge, "Create interactive door");
            hinge.transform.SetParent(parent, false);
            hinge.transform.localPosition = horizontalWall
                ? new Vector3(x - panelWidth * 0.5f, 0f, z)
                : new Vector3(x, 0f, z - panelWidth * 0.5f);

            Vector3 panelPosition = horizontalWall
                ? new Vector3(panelWidth * 0.5f, DoorHeight * 0.5f, 0f)
                : new Vector3(0f, DoorHeight * 0.5f, panelWidth * 0.5f);
            Vector3 panelSize = horizontalWall
                ? new Vector3(panelWidth, DoorHeight, 0.12f)
                : new Vector3(0.12f, DoorHeight, panelWidth);

            GameObject panel = CreateBox(hinge.transform, name + " Panel", panelPosition, panelSize, material);
            panel.isStatic = false;
            hinge.AddComponent<SwingDoor>();
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
            player.transform.position = new Vector3(-3.3f, 0.05f, 14.5f);
            player.transform.rotation = Quaternion.Euler(0f, -44f, 0f);

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

            player.AddComponent<PauseMenuController>();
        }

        private static void DisableTemplateCamerasAndPostProcessing()
        {
            foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                camera.enabled = false;
                if (camera.gameObject.CompareTag("MainCamera"))
                    camera.gameObject.tag = "Untagged";
            }

            foreach (AudioListener listener in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                listener.enabled = false;

            foreach (Volume volume in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
                volume.enabled = false;

            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
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
