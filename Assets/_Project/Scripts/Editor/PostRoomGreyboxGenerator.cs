#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadLetter.Editor
{
    /// <summary>Creates a non-gameplay greybox scene for early level layout reviews.</summary>
    internal static class PostRoomGreyboxGenerator
    {
        private const string ScenePath = "Assets/_Project/Scenes/PostRoom.unity";
        private const string MaterialsPath = "Assets/_Project/Materials/";

        [MenuItem("DEAD LETTER/Generate Post Room Greybox")]
        private static void Generate()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("PostRoom_Greybox");

            var environment = CreateGroup("Environment", root.transform);
            var workstations = CreateGroup("Workstations", root.transform);
            var mailBins = CreateGroup("MailBins", root.transform);

            var floor = GetMaterial("Greybox_Floor", new Color(0.16f, 0.17f, 0.19f));
            var wall = GetMaterial("Greybox_Wall", new Color(0.29f, 0.31f, 0.34f));
            var furniture = GetMaterial("Greybox_Furniture", new Color(0.19f, 0.24f, 0.29f));
            var accent = GetMaterial("Greybox_Accent", new Color(0.48f, 0.17f, 0.13f));

            CreateBlock("Floor", environment.transform, new Vector3(0f, -0.25f, 0f), new Vector3(24f, 0.5f, 18f), floor);
            CreateBlock("BackWall", environment.transform, new Vector3(0f, 2f, 8.75f), new Vector3(24f, 4f, 0.5f), wall);
            CreateBlock("LeftWall", environment.transform, new Vector3(-11.75f, 2f, 0f), new Vector3(0.5f, 4f, 18f), wall);
            CreateBlock("RightWall", environment.transform, new Vector3(11.75f, 2f, 0f), new Vector3(0.5f, 4f, 18f), wall);
            CreateBlock("FrontWall_Left", environment.transform, new Vector3(-7f, 2f, -8.75f), new Vector3(10f, 4f, 0.5f), wall);
            CreateBlock("FrontWall_Right", environment.transform, new Vector3(7f, 2f, -8.75f), new Vector3(10f, 4f, 0.5f), wall);
            CreateBlock("EntranceHeader", environment.transform, new Vector3(0f, 3.25f, -8.75f), new Vector3(4f, 1.5f, 0.5f), wall);

            CreateBlock("ReceptionDesk", workstations.transform, new Vector3(-5.5f, 0.7f, 4.75f), new Vector3(5.5f, 1.4f, 1.4f), furniture);
            CreateBlock("ProcessingDesk", workstations.transform, new Vector3(1.5f, 0.7f, 1.25f), new Vector3(5f, 1.4f, 2f), furniture);
            CreateBlock("Terminal", workstations.transform, new Vector3(1.5f, 1.75f, 1.55f), new Vector3(1.4f, 1.1f, 0.25f), accent);
            CreateBlock("StampStation", workstations.transform, new Vector3(6.5f, 0.6f, 1.25f), new Vector3(2.5f, 1.2f, 2f), furniture);
            CreateBlock("StampPress", workstations.transform, new Vector3(6.5f, 1.55f, 1.25f), new Vector3(0.6f, 0.7f, 0.6f), accent);

            var countries = new[] { "Aster", "Boreal", "Cinder", "Dawn" };
            for (var index = 0; index < countries.Length; index++)
            {
                var x = -7.5f + index * 5f;
                CreateBlock($"MailBin_{countries[index]}", mailBins.transform, new Vector3(x, 1.15f, 6.9f), new Vector3(3.2f, 2.3f, 1.2f), furniture);
                CreateLabel(countries[index], mailBins.transform, new Vector3(x, 2.6f, 6.25f));
            }

            var playerStart = new GameObject("PlayerStart");
            playerStart.transform.SetParent(root.transform);
            playerStart.transform.SetPositionAndRotation(new Vector3(0f, 0f, -6.5f), Quaternion.identity);

            CreateLighting(root.transform);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            Debug.Log($"Created DEAD LETTER post-room greybox at {ScenePath}.");
        }

        private static GameObject CreateGroup(string name, Transform parent)
        {
            var group = new GameObject(name);
            group.transform.SetParent(parent);
            return group;
        }

        private static void CreateBlock(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent);
            block.transform.SetPositionAndRotation(position, Quaternion.identity);
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void CreateLabel(string text, Transform parent, Vector3 position)
        {
            var label = new GameObject($"Label_{text}");
            label.transform.SetParent(parent);
            label.transform.SetPositionAndRotation(position, Quaternion.identity);
            var textMesh = label.AddComponent<TextMesh>();
            textMesh.text = text;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.characterSize = 0.25f;
            textMesh.fontSize = 48;
            textMesh.color = Color.white;
        }

        private static void CreateLighting(Transform parent)
        {
            var light = new GameObject("Directional Light");
            light.transform.SetParent(parent);
            light.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
            var directional = light.AddComponent<Light>();
            directional.type = LightType.Directional;
            directional.intensity = 1.1f;

            var camera = new GameObject("Preview Camera");
            camera.transform.SetParent(parent);
            camera.transform.SetPositionAndRotation(new Vector3(0f, 12f, -14f), Quaternion.Euler(32f, 0f, 0f));
            var previewCamera = camera.AddComponent<Camera>();
            previewCamera.tag = "MainCamera";
        }

        private static Material GetMaterial(string name, Color color)
        {
            var path = $"{MaterialsPath}{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
#endif
