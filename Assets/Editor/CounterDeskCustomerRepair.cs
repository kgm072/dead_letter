using DeadLetter.Interaction;
using DeadLetter.Mail;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadLetter.Editor
{
    public static class CounterDeskCustomerRepair
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        // The FBX combines the desk, monitor, keyboard, mouse and cup in one renderer.
        // A 3.2 m service counter reads naturally in the 12 m counter room while retaining
        // a normal work-surface height and enough clearance on both sides.
        private static readonly Vector3 DeskTargetSize = new(3.2f, 1.35f, 1.05f);
        private static readonly Vector3 DeskTargetCenter = new(0f, 0.675f, -11.75f);
        private static readonly Vector3 ClockTargetSize = new(0.26f, 0.14f, 0.12f);
        private static readonly Vector3 RecorderTargetSize = new(0.28f, 0.18f, 0.16f);

        [MenuItem("Tools/DEAD LETTER/Repair/Counter Desk and Customer Facing")]
        public static void Apply()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject artRoot = GameObject.Find("Counter Production Art");
            TestCustomerNPC customer = Object.FindAnyObjectByType<TestCustomerNPC>(FindObjectsInactive.Include);
            GameObject player = GameObject.Find("Player Controller");
            if (artRoot == null || customer == null || player == null)
            {
                Debug.LogError("Counter repair could not find the counter art, customer, or player.");
                return;
            }

            Renderer desk = FindRenderer(artRoot.transform, "창구-책상");
            if (desk == null || desk.transform.parent == null)
            {
                Debug.LogError("Counter repair could not find the counter desk renderer and size wrapper.");
                return;
            }

            Transform sizeWrapper = desk.transform.parent;
            Undo.RecordObject(sizeWrapper, "Fit counter desk");
            FitRenderer(sizeWrapper, desk, DeskTargetSize, DeskTargetCenter);

            Renderer clock = FindRenderer(artRoot.transform, "창구-시계");
            Renderer recorder = FindRenderer(artRoot.transform, "창구-녹음기");
            if (clock != null && clock.transform.parent != null)
            {
                Undo.RecordObject(clock.transform.parent, "Fit counter clock");
                FitRenderer(clock.transform.parent, clock, ClockTargetSize, new Vector3(0.95f, 0.88f, -11.55f));
            }
            if (recorder != null && recorder.transform.parent != null)
            {
                Undo.RecordObject(recorder.transform.parent, "Fit counter recorder");
                FitRenderer(recorder.transform.parent, recorder, RecorderTargetSize, new Vector3(1.28f, 0.90f, -11.55f));
            }

            foreach (ArtColliderFollower follower in artRoot.GetComponentsInChildren<ArtColliderFollower>(true))
            {
                Undo.RecordObject(follower.transform, "Align counter collider");
                follower.SyncNow();
                EditorUtility.SetDirty(follower);
                if (follower.BoundCollider != null)
                    EditorUtility.SetDirty(follower.BoundCollider);
            }

            SerializedObject serializedCustomer = new(customer);
            SerializedProperty lookTarget = serializedCustomer.FindProperty("waitingLookTarget");
            SerializedProperty yawOffset = serializedCustomer.FindProperty("modelForwardYawOffset");
            lookTarget.objectReferenceValue = player.transform;
            yawOffset.floatValue = 180f;
            serializedCustomer.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(customer);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Counter repair complete. Desk bounds={desk.bounds.size:F3}, center={desk.bounds.center:F3}; clock={clock?.bounds.size:F3}, recorder={recorder?.bounds.size:F3}; customer model yaw offset=180, look target={player.name}.");
        }

        [MenuItem("Tools/DEAD LETTER/Validate/Counter Desk and Customer Facing")]
        public static void Validate()
        {
            GameObject artRoot = GameObject.Find("Counter Production Art");
            TestCustomerNPC customer = Object.FindAnyObjectByType<TestCustomerNPC>(FindObjectsInactive.Include);
            Renderer desk = artRoot == null ? null : FindRenderer(artRoot.transform, "창구-책상");
            Renderer clock = artRoot == null ? null : FindRenderer(artRoot.transform, "창구-시계");
            Renderer recorder = artRoot == null ? null : FindRenderer(artRoot.transform, "창구-녹음기");
            ArtColliderFollower deskFollower = FindDeskFollower(artRoot, desk);

            bool deskSizeValid = desk != null && Approximately(desk.bounds.size, DeskTargetSize, 0.04f);
            bool deskPositionValid = desk != null && Approximately(desk.bounds.center, DeskTargetCenter, 0.04f);
            bool colliderValid = desk != null && deskFollower != null && deskFollower.BoundCollider != null &&
                                 Approximately(deskFollower.BoundCollider.bounds.center, desk.bounds.center, 0.02f) &&
                                 Approximately(deskFollower.BoundCollider.bounds.size, desk.bounds.size, 0.02f);
            bool propsValid = clock != null && recorder != null &&
                              Approximately(clock.bounds.size, ClockTargetSize, 0.02f) &&
                              Approximately(clock.bounds.center, new Vector3(0.95f, 0.88f, -11.55f), 0.02f) &&
                              Approximately(recorder.bounds.size, RecorderTargetSize, 0.02f) &&
                              Approximately(recorder.bounds.center, new Vector3(1.28f, 0.90f, -11.55f), 0.02f);

            SerializedObject serializedCustomer = customer == null ? null : new SerializedObject(customer);
            Transform lookTarget = serializedCustomer?.FindProperty("waitingLookTarget").objectReferenceValue as Transform;
            float yawOffset = serializedCustomer?.FindProperty("modelForwardYawOffset").floatValue ?? 0f;
            bool customerValid = customer != null && lookTarget != null && Mathf.Abs(Mathf.DeltaAngle(yawOffset, 180f)) < 0.1f;

            if (deskSizeValid && deskPositionValid && colliderValid && propsValid && customerValid)
                Debug.Log("Counter/customer validation passed: desk proportions, desktop props and collider match; customer forward correction and counter look target are configured.");
            else
                Debug.LogError($"Counter/customer validation failed. deskSize={deskSizeValid}, deskPosition={deskPositionValid}, props={propsValid}, collider={colliderValid}, customerFacing={customerValid}");
        }

        private static void FitRenderer(Transform wrapper, Renderer renderer, Vector3 targetSize, Vector3 targetCenter)
        {
            for (int iteration = 0; iteration < 3; iteration++)
            {
                Vector3 current = renderer.bounds.size;
                if (current.x <= 0.0001f || current.y <= 0.0001f || current.z <= 0.0001f)
                    break;

                wrapper.localScale = Vector3.Scale(wrapper.localScale, new Vector3(
                    targetSize.x / current.x,
                    targetSize.y / current.y,
                    targetSize.z / current.z));
            }

            wrapper.position += targetCenter - renderer.bounds.center;
        }

        private static Renderer FindRenderer(Transform root, string exactName)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                if (renderer.gameObject.name == exactName)
                    return renderer;
            return null;
        }

        private static ArtColliderFollower FindDeskFollower(GameObject root, Renderer desk)
        {
            if (root == null || desk == null)
                return null;
            foreach (ArtColliderFollower follower in root.GetComponentsInChildren<ArtColliderFollower>(true))
                if (follower.TargetRenderer == desk)
                    return follower;
            return null;
        }

        private static bool Approximately(Vector3 a, Vector3 b, float tolerance)
        {
            return Mathf.Abs(a.x - b.x) <= tolerance &&
                   Mathf.Abs(a.y - b.y) <= tolerance &&
                   Mathf.Abs(a.z - b.z) <= tolerance;
        }
    }
}
