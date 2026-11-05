using System;
using System.Collections.Generic;
using DeadLetter.Interaction;
using DeadLetter.Mail;
using DeadLetter.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadLetter.Editor
{
    public static class GrayboxMapValidator
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string RootName = "DEAD LETTER - Graybox";
        private const float Tolerance = 0.015f;

        [MenuItem("Tools/DEAD LETTER/Validate Map Layout")]
        public static void ValidateMapLayout()
        {
            List<string> failures = new List<string>();
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                failures.Add($"Active scene must be {ScenePath}, but was {scene.path}.");

            GameObject root = GameObject.Find(RootName);
            if (root == null)
            {
                failures.Add($"Missing map root: {RootName}.");
                Finish(failures);
                return;
            }

            CheckBox(root.transform, "Storage Floor", new Vector3(0f, -0.1f, 11.5f), new Vector3(9f, 0.2f, 9f), failures);
            CheckBox(root.transform, "Pantry Floor", new Vector3(-11.5f, -0.1f, 0f), new Vector3(9f, 0.2f, 9f), failures);
            CheckBox(root.transform, "Workshop Floor", new Vector3(11.5f, -0.1f, 0f), new Vector3(9f, 0.2f, 9f), failures);
            CheckBox(root.transform, "Counter Floor", new Vector3(0f, -0.1f, -11.5f), new Vector3(9f, 0.2f, 9f), failures);
            CheckBox(root.transform, "East-West Hall Floor", new Vector3(0f, -0.1f, 0f), new Vector3(14f, 0.2f, 3.6f), failures);
            CheckBox(root.transform, "North-South Hall Floor", new Vector3(0f, -0.1f, 0f), new Vector3(3.6f, 0.2f, 14f), failures);
            CheckBox(root.transform, "Hidden Room Floor", new Vector3(-7.6f, -0.1f, 7f), new Vector3(5.6f, 0.2f, 5.2f), failures);
            CheckBox(root.transform, "Hidden Connector Floor", new Vector3(-3.3f, -0.1f, 5.7f), new Vector3(3f, 0.2f, 1.9f), failures);
            CheckBox(root.transform, "Counter Exterior Approach Floor", new Vector3(0f, -0.1f, -17.55f), new Vector3(2.8f, 0.2f, 3.1f), failures);

            CheckPosition(root.transform, "Storage Hall Door Pivot", new Vector3(-0.51f, 0.01f, 7f), failures);
            CheckPosition(root.transform, "Pantry Hall Door Pivot", new Vector3(-7f, 0.01f, -0.51f), failures);
            CheckPosition(root.transform, "Workshop Hall Door Pivot", new Vector3(7f, 0.01f, -0.51f), failures);
            CheckPosition(root.transform, "Counter Hall Door Pivot", new Vector3(-0.51f, 0.01f, -7f), failures);
            CheckPosition(root.transform, "Counter Exterior Door Pivot", new Vector3(-0.51f, 0.01f, -16f), failures);
            CheckPosition(root.transform, "Hidden Room Door Pivot", new Vector3(-1.8f, 0.01f, 5.19f), failures);

            Transform player = Require(root.transform, "Player Controller", failures);
            if (player != null)
            {
                CheckVector("Player start", player.position, new Vector3(0f, 0.05f, -10.1f), failures);
                if (player.GetComponent<CharacterController>() == null ||
                    player.GetComponent<FirstPersonController>() == null ||
                    player.GetComponent<FirstPersonInteractor>() == null)
                    failures.Add("Player movement, collision, or interaction component is missing.");
                else if (Mathf.Abs(player.GetComponent<FirstPersonInteractor>().InteractionDistance - 3f) > Tolerance)
                    failures.Add("Player interaction distance is not 3 metres.");
            }

            LetterTaskState taskState = root.GetComponentInChildren<LetterTaskState>(true);
            LetterWorkstation workstation = FindActiveComponent<LetterWorkstation>(root);
            MailDropBox mailDropBox = root.GetComponentInChildren<MailDropBox>(true);
            TestCustomerNPC customer = root.GetComponentInChildren<TestCustomerNPC>(true);
            DaySleepSofa sofa = FindActiveComponent<DaySleepSofa>(root);
            TinyFuseBox fuseBox = root.GetComponentInChildren<TinyFuseBox>(true);

            RequireComponent(taskState, "Letter task state", failures);
            CheckReference(workstation, "taskState", taskState, "Letter workstation task reference", failures);
            CheckReference(mailDropBox, "taskState", taskState, "Mail drop task reference", failures);
            CheckReference(sofa, "taskState", taskState, "Sleep sofa task reference", failures);
            CheckObjectReference(sofa, "dayCycle", "Sleep sofa day-cycle reference", failures);
            RequireComponent(fuseBox, "Fuse box interaction", failures);

            ValidateProductionArt(root.transform, failures);

            if (customer == null)
            {
                failures.Add("Test customer NPC is missing.");
            }
            else
            {
                CheckReference(customer, "taskState", taskState, "Customer task reference", failures);
                CheckObjectReference(customer, "entranceDoor", "Customer entrance door reference", failures);
                SerializedProperty waypoints = new SerializedObject(customer).FindProperty("waypoints");
                if (waypoints == null || waypoints.arraySize != 3)
                    failures.Add("Customer route must contain exactly three waypoints.");
                else
                {
                    for (int index = 0; index < waypoints.arraySize; index++)
                    {
                        if (waypoints.GetArrayElementAtIndex(index).objectReferenceValue == null)
                            failures.Add($"Customer waypoint {index + 1} is not connected.");
                    }
                }
            }

            HingedDoor[] doors = root.GetComponentsInChildren<HingedDoor>(true);
            if (doors.Length < 7)
                failures.Add("Expected seven hinged doors in the map.");
            foreach (HingedDoor door in doors)
                ValidateDoor(door, failures);

            Physics.SyncTransforms();
            CheckPlayerClearance(root.transform, player, "Central hall", new Vector3(0f, 0f, 0f), failures);
            CheckPlayerClearance(root.transform, player, "Storage route", new Vector3(0f, 0f, 7.6f), failures);
            CheckPlayerClearance(root.transform, player, "Pantry route", new Vector3(-8.3f, 0f, 0f), failures);
            CheckPlayerClearance(root.transform, player, "Workshop route", new Vector3(8.3f, 0f, 0f), failures);
            CheckPlayerClearance(root.transform, player, "Counter route", new Vector3(0f, 0f, -10.1f), failures);
            CheckPlayerClearance(root.transform, player, "Hidden-room route", new Vector3(-5.35f, 0f, 7f), failures);

            Finish(failures);
        }

        [MenuItem("Tools/DEAD LETTER/Validate Door Interaction (Play Mode)")]
        public static void ValidateDoorInteractionRuntime()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Enter Play Mode before running the door interaction smoke test.");
                return;
            }

            List<string> failures = new List<string>();
            GameObject root = GameObject.Find(RootName);
            GameObject player = root != null ? root.transform.Find("Player Controller")?.gameObject : null;
            if (root == null || player == null)
            {
                failures.Add("Map root or player is missing in Play Mode.");
                Finish(failures);
                return;
            }

            System.Reflection.FieldInfo openState = typeof(HingedDoor).GetField(
                "isOpen",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (openState == null)
            {
                failures.Add("HingedDoor open-state field could not be inspected.");
                Finish(failures);
                return;
            }

            foreach (HingedDoor door in root.GetComponentsInChildren<HingedDoor>(true))
            {
                if ((bool)openState.GetValue(door))
                    door.CloseDoor();

                door.Interact(player);
                if (!(bool)openState.GetValue(door))
                    failures.Add($"{door.name} did not open through IInteractable.Interact.");

                door.Interact(player);
                if ((bool)openState.GetValue(door))
                    failures.Add($"{door.name} did not close through IInteractable.Interact.");
            }

            Finish(failures);
            Debug.Log("Door interaction smoke test passed: all seven doors opened and closed through the shared F/right-click interaction path.");
        }

        private static void CheckPlayerClearance(
            Transform root,
            Transform player,
            string label,
            Vector3 floorPosition,
            List<string> failures)
        {
            Vector3 bottom = floorPosition + Vector3.up * 0.35f;
            Vector3 top = floorPosition + Vector3.up * 1.55f;
            Collider[] overlaps = Physics.OverlapCapsule(bottom, top, 0.3f, ~0, QueryTriggerInteraction.Ignore);
            foreach (Collider overlap in overlaps)
            {
                if (player != null && overlap.transform.IsChildOf(player))
                    continue;
                if (!overlap.transform.IsChildOf(root))
                    continue;

                failures.Add($"{label} player clearance is blocked by {overlap.name}.");
                return;
            }
        }

        private static T FindActiveComponent<T>(GameObject root) where T : Component
        {
            foreach (T component in root.GetComponentsInChildren<T>(true))
            {
                if (component.gameObject.activeInHierarchy)
                    return component;
            }
            return null;
        }

        private static void ValidateProductionArt(Transform root, List<string> failures)
        {
            Transform artRoot = Require(root, "Production Room Art", failures);
            if (artRoot == null)
                return;

            string[] roomRoots =
            {
                "Counter Production Art",
                "Workshop Production Art",
                "Pantry Production Art",
                "Hidden Room Production Art",
                "Storage Production Art"
            };
            foreach (string roomName in roomRoots)
            {
                Transform room = Require(artRoot, roomName, failures);
                if (room == null || room.GetComponentsInChildren<Renderer>(true).Length == 0)
                    failures.Add($"{roomName} has no production renderers.");
            }

            Renderer[] renderers = artRoot.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null)
                    {
                        failures.Add($"{renderer.name} has a missing material.");
                        continue;
                    }
                    if (material.shader == null || material.shader.name != "Universal Render Pipeline/Lit")
                        failures.Add($"{renderer.name} is not using URP/Lit.");
                    if (!material.HasProperty("_BaseMap") || material.GetTexture("_BaseMap") == null)
                        failures.Add($"{renderer.name} has no Base Map texture.");
                }
            }

            if (artRoot.GetComponentsInChildren<Collider>(true).Length < 10)
                failures.Add("Production furniture colliders are missing or incomplete.");

            ArtInteractionMarker[] markers = artRoot.GetComponentsInChildren<ArtInteractionMarker>(true);
            CheckMarker(markers, ArtInteractionRole.LetterWorkstation, failures);
            CheckMarker(markers, ArtInteractionRole.Stamp, failures);
            CheckMarker(markers, ArtInteractionRole.Tape, failures);
            CheckMarker(markers, ArtInteractionRole.VoiceRecorder, failures);
            CheckMarker(markers, ArtInteractionRole.DaySofa, failures);
            CheckMarker(markers, ArtInteractionRole.Note, failures);

            Transform storage = artRoot.Find("Storage Production Art");
            if (storage != null && storage.childCount != 1)
                failures.Add($"Storage production art expected one correctly sized shelf set, got {storage.childCount}.");

            CheckColliderSize(artRoot, "Collider - 창구-책상", 2.4f, 1.05f, 1f, failures);
            CheckColliderSize(artRoot, "Collider - 작업실-책상", 1.8f, 0.78f, 0.85f, failures);
            CheckColliderSize(artRoot, "Collider - 작업실-콘베이어 밸트", 4f, 0.75f, 1.1f, failures);
            CheckColliderSize(artRoot, "Collider - 탕비실-냉장고", 0.9f, 1.85f, 0.8f, failures);
            CheckColliderSize(artRoot, "Collider - 탕비실-선빈", 2.8f, 2.05f, 0.45f, failures);
            CheckColliderSize(artRoot, "Collider - 탕비실-소파", 2f, 0.9f, 0.9f, failures);
            CheckColliderSize(artRoot, "Collider - 탕비실-탁자", 1.8f, 0.75f, 0.85f, failures);
            CheckColliderSize(artRoot, "Collider - 히든룸-책상", 1.7f, 0.75f, 0.8f, failures);
            CheckColliderSize(artRoot, "Collider - 칭고", 7.2f, 2.1f, 5.4f, failures);
        }

        private static void ValidateDoor(HingedDoor door, List<string> failures)
        {
            Transform leaf = null;
            foreach (Transform child in door.transform)
            {
                if (child.name.EndsWith(" Leaf", StringComparison.Ordinal))
                {
                    leaf = child;
                    break;
                }
            }

            if (leaf == null)
            {
                failures.Add($"{door.name} is missing its door leaf.");
                return;
            }

            Vector3 size = leaf.localScale;
            float width = Mathf.Max(size.x, size.z);
            float thickness = Mathf.Min(size.x, size.z);
            if (Mathf.Abs(width - 1.02f) > Tolerance || Mathf.Abs(size.y - 2.13f) > Tolerance || Mathf.Abs(thickness - 0.05f) > Tolerance)
                failures.Add($"{door.name} leaf must be 1.02 x 2.13 x 0.05 metres, got {size}.");

            string baseName = door.name.EndsWith(" Pivot", StringComparison.Ordinal)
                ? door.name.Substring(0, door.name.Length - " Pivot".Length)
                : door.name;
            string[] panelBases =
            {
                baseName + " Front Upper Panel",
                baseName + " Front Lower Panel",
                baseName + " Back Upper Panel",
                baseName + " Back Lower Panel"
            };
            foreach (string panelBase in panelBases)
            {
                foreach (string rail in new[] { " Top", " Bottom", " Near", " Far" })
                {
                    string detail = panelBase + rail;
                    if (door.transform.Find(detail) == null)
                        failures.Add($"{door.name} is missing double-sided detail: {detail}.");
                }
            }

            string[] requiredDetails =
            {
                baseName + " Front Handle",
                baseName + " Back Handle"
            };
            foreach (string detail in requiredDetails)
            {
                if (door.transform.Find(detail) == null)
                    failures.Add($"{door.name} is missing double-sided detail: {detail}.");
            }
        }

        private static void CheckColliderSize(
            Transform root,
            string name,
            float expectedLongSide,
            float expectedHeight,
            float expectedShortSide,
            List<string> failures)
        {
            foreach (BoxCollider collider in root.GetComponentsInChildren<BoxCollider>(true))
            {
                if (collider.name != name)
                    continue;

                float longSide = Mathf.Max(collider.size.x, collider.size.z);
                float shortSide = Mathf.Min(collider.size.x, collider.size.z);
                if (Mathf.Abs(longSide - expectedLongSide) > 0.08f ||
                    Mathf.Abs(collider.size.y - expectedHeight) > 0.08f ||
                    Mathf.Abs(shortSide - expectedShortSide) > 0.08f)
                {
                    failures.Add($"{name} expected {expectedLongSide:F2} x {expectedHeight:F2} x {expectedShortSide:F2}m, got {collider.size}.");
                }
                return;
            }
            failures.Add($"Missing sized furniture collider: {name}.");
        }

        private static void CheckMarker(ArtInteractionMarker[] markers, ArtInteractionRole role, List<string> failures)
        {
            foreach (ArtInteractionMarker marker in markers)
            {
                if (marker.Role == role)
                    return;
            }
            failures.Add($"Missing art interaction marker: {role}.");
        }

        private static void CheckBox(Transform root, string name, Vector3 position, Vector3 scale, List<string> failures)
        {
            Transform target = Require(root, name, failures);
            if (target == null)
                return;

            CheckVector(name + " position", target.localPosition, position, failures);
            CheckVector(name + " scale", target.localScale, scale, failures);
        }

        private static void CheckPosition(Transform root, string name, Vector3 position, List<string> failures)
        {
            Transform target = Require(root, name, failures);
            if (target != null)
                CheckVector(name + " position", target.localPosition, position, failures);
        }

        private static Transform Require(Transform root, string name, List<string> failures)
        {
            Transform target = root.Find(name);
            if (target == null)
                failures.Add($"Missing object: {name}.");
            return target;
        }

        private static void RequireComponent(Component component, string label, List<string> failures)
        {
            if (component == null)
                failures.Add(label + " is missing.");
        }

        private static void CheckReference(Component component, string propertyName, UnityEngine.Object expected, string label, List<string> failures)
        {
            if (component == null)
            {
                failures.Add(label + " component is missing.");
                return;
            }

            SerializedProperty property = new SerializedObject(component).FindProperty(propertyName);
            if (property == null || property.objectReferenceValue != expected)
                failures.Add(label + " is disconnected.");
        }

        private static void CheckObjectReference(Component component, string propertyName, string label, List<string> failures)
        {
            if (component == null)
            {
                failures.Add(label + " component is missing.");
                return;
            }

            SerializedProperty property = new SerializedObject(component).FindProperty(propertyName);
            if (property == null || property.objectReferenceValue == null)
                failures.Add(label + " is disconnected.");
        }

        private static void CheckVector(string label, Vector3 actual, Vector3 expected, List<string> failures)
        {
            CheckVector(label, actual, expected, failures, Tolerance);
        }

        private static void CheckVector(string label, Vector3 actual, Vector3 expected, List<string> failures, float tolerance)
        {
            if (Vector3.Distance(actual, expected) > tolerance)
                failures.Add($"{label} expected {expected}, got {actual}.");
        }

        private static void Finish(List<string> failures)
        {
            if (failures.Count > 0)
                throw new InvalidOperationException("Map validation failed:\n- " + string.Join("\n- ", failures));

            Debug.Log("Map validation passed: compact dimensions, doors, player, NPC route, production FBX art, URP materials, colliders, and gameplay references are intact.");
        }
    }
}
