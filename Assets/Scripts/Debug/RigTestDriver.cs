using UnityEngine;

namespace DeadLetter.Debugging
{
    public sealed class RigTestDriver : MonoBehaviour
    {
        [SerializeField] private Transform testBone;
        [SerializeField] private bool animateBone;
        [SerializeField, TextArea] private string rigSummary;

        private Quaternion initialLocalRotation;

        public void Configure(Transform bone, bool shouldAnimateBone, string summary)
        {
            testBone = bone;
            animateBone = shouldAnimateBone;
            rigSummary = summary;
            if (testBone != null)
                initialLocalRotation = testBone.localRotation;
        }

        private void Awake()
        {
            if (testBone != null)
                initialLocalRotation = testBone.localRotation;
        }

        private void Update()
        {
            if (!animateBone || testBone == null)
                return;

            float angle = Mathf.Sin(Time.time * 1.8f) * 18f;
            testBone.localRotation = initialLocalRotation * Quaternion.Euler(0f, 0f, angle);
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(20f, 20f, 520f, 118f), string.Empty);
            GUI.Label(new Rect(36f, 32f, 480f, 24f), "RIG TEST - texture intentionally omitted");
            GUI.Label(new Rect(36f, 58f, 480f, 66f), rigSummary ?? "No rig report available.");
        }
    }
}
