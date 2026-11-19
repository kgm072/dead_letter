using System.Collections;
using UnityEngine;

namespace DeadLetter.Mail
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TestCustomerNPC))]
    public sealed class Day3MascotBalloonSequence : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float lidOpenDuration = 0.7f;
        [SerializeField, Min(0.5f)] private float balloonRiseDuration = 3.2f;
        [SerializeField, Min(0.2f)] private float balloonRiseHeight = 1.7f;

        private TestCustomerNPC customer;
        private DayCycleManager dayCycle;
        private Transform visualRoot;
        private Transform leftLid;
        private Transform rightLid;
        private Transform balloon;
        private Transform balloonString;
        private readonly Vector3 balloonFinalScale = new(0.32f, 0.42f, 0.32f);
        private Material boxMaterial;
        private Material balloonMaterial;
        private Coroutine sequenceRoutine;
        private bool hasPlayed;

        public bool IsPlaying => sequenceRoutine != null;
        public bool HasPlayed => hasPlayed;

        private void Awake()
        {
            ResolveReferences();
            EnsureVisuals();
            HideVisuals();
        }

        private void OnEnable()
        {
            ResolveReferences();
            customer.DialogueEventReached -= HandleDialogueEvent;
            customer.DialogueEventReached += HandleDialogueEvent;
        }

        private void OnDisable()
        {
            if (customer != null)
            {
                customer.DialogueEventReached -= HandleDialogueEvent;
                if (customer.DialogueEventGateActive)
                    customer.SetDialogueEventGate(false);
            }

            if (sequenceRoutine != null)
                StopCoroutine(sequenceRoutine);
            sequenceRoutine = null;
            hasPlayed = false;
            HideVisuals();
        }

        private void OnDestroy()
        {
            if (visualRoot != null)
            {
                if (Application.isPlaying)
                    Destroy(visualRoot.gameObject);
                else
                    DestroyImmediate(visualRoot.gameObject);
            }
            DestroyMaterial(boxMaterial);
            DestroyMaterial(balloonMaterial);
        }

        private void HandleDialogueEvent(string eventId)
        {
            TryStartForDialogueEvent(eventId);
        }

        public bool TryStartForDialogueEvent(string eventId)
        {
            if (eventId != CustomerDialogueCatalog.DayThreeRedBalloonEvent || hasPlayed || IsPlaying)
                return false;

            ResolveReferences();
            Day2RequestRuntimeState request = dayCycle != null ? dayCycle.CurrentDayThreeRequest : null;
            if (request == null || request.Definition.RequestId != Day3RequestCatalog.MascotRequestId ||
                !request.DeliveredToCustomer)
                return false;

            customer.SetDialogueEventGate(true);
            sequenceRoutine = StartCoroutine(PlaySequence());
            return true;
        }

        private IEnumerator PlaySequence()
        {
            EnsureVisuals();
            visualRoot.gameObject.SetActive(true);
            PlaceRevealInFrontOfCustomer();

            leftLid.localRotation = Quaternion.identity;
            rightLid.localRotation = Quaternion.identity;
            balloon.localPosition = new Vector3(0f, 0.24f, 0f);
            balloon.localScale = balloonFinalScale * 0.08f;
            balloonString.gameObject.SetActive(false);

            float elapsed = 0f;
            while (elapsed < lidOpenDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / lidOpenDuration));
                leftLid.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, 105f, progress));
                rightLid.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, -105f, progress));
                yield return null;
            }

            balloonString.gameObject.SetActive(true);
            Vector3 start = new(0f, 0.24f, 0f);
            Vector3 end = start + Vector3.up * balloonRiseHeight;
            elapsed = 0f;
            while (elapsed < balloonRiseDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / balloonRiseDuration);
                float eased = Mathf.SmoothStep(0f, 1f, normalized);
                Vector3 position = Vector3.Lerp(start, end, eased);
                position.x += Mathf.Sin(normalized * Mathf.PI * 3f) * 0.035f;
                balloon.localPosition = position;
                balloon.localScale = Vector3.Lerp(
                    balloonFinalScale * 0.08f,
                    balloonFinalScale,
                    Mathf.Clamp01(normalized * 3f));
                UpdateString(position);
                yield return null;
            }

            hasPlayed = true;
            sequenceRoutine = null;
            customer.SetDialogueEventGate(false);
        }

        private void ResolveReferences()
        {
            customer ??= GetComponent<TestCustomerNPC>();
            dayCycle ??= FindAnyObjectByType<DayCycleManager>();
        }

        private void PlaceRevealInFrontOfCustomer()
        {
            Camera playerCamera = Camera.main;
            Vector3 towardPlayer = playerCamera != null
                ? playerCamera.transform.position - transform.position
                : -transform.forward;
            towardPlayer.y = 0f;
            if (towardPlayer.sqrMagnitude <= 0.0001f)
                towardPlayer = -transform.forward;
            towardPlayer.Normalize();
            Vector3 counterSide = Vector3.Cross(Vector3.up, towardPlayer).normalized;

            // Offset the parcel beside the counter monitor; a centered parcel is
            // fully occluded by the existing monitor from the first-person view.
            visualRoot.position = transform.position + towardPlayer * 0.78f +
                                  counterSide * 0.82f + Vector3.up * 0.82f;
            visualRoot.rotation = Quaternion.LookRotation(towardPlayer, Vector3.up);
        }

        private void EnsureVisuals()
        {
            if (visualRoot != null)
                return;

            GameObject rootObject = new("Day 3 Mascot Parcel Reveal");
            visualRoot = rootObject.transform;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            boxMaterial = new Material(shader) { name = "Runtime Unknown Cargo Box" };
            balloonMaterial = new Material(shader) { name = "Runtime Red Balloon" };
            SetMaterialColor(boxMaterial, new Color(0.24f, 0.12f, 0.065f, 1f));
            SetMaterialColor(balloonMaterial, new Color(0.58f, 0.015f, 0.02f, 1f));

            CreatePrimitive("Sealed Cargo Box", visualRoot, new Vector3(0f, 0.13f, 0f),
                new Vector3(0.64f, 0.26f, 0.48f), boxMaterial);
            leftLid = CreatePrimitive("Left Lid", visualRoot, new Vector3(-0.165f, 0.29f, 0f),
                new Vector3(0.31f, 0.045f, 0.48f), boxMaterial);
            rightLid = CreatePrimitive("Right Lid", visualRoot, new Vector3(0.165f, 0.29f, 0f),
                new Vector3(0.31f, 0.045f, 0.48f), boxMaterial);

            balloon = CreatePrimitive("Red Balloon", visualRoot, new Vector3(0f, 0.24f, 0f),
                balloonFinalScale, balloonMaterial, PrimitiveType.Sphere);
            balloonString = CreatePrimitive("Balloon String", visualRoot, Vector3.zero,
                new Vector3(0.008f, 0.25f, 0.008f), boxMaterial, PrimitiveType.Cylinder);
        }

        private static Transform CreatePrimitive(
            string objectName,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            PrimitiveType primitiveType = PrimitiveType.Cube)
        {
            GameObject instance = GameObject.CreatePrimitive(primitiveType);
            instance.name = objectName;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = localPosition;
            instance.transform.localScale = localScale;
            if (instance.TryGetComponent(out Collider collider))
                Destroy(collider);
            instance.GetComponent<Renderer>().sharedMaterial = material;
            return instance.transform;
        }

        private void UpdateString(Vector3 balloonPosition)
        {
            Vector3 boxTop = new(0f, 0.3f, 0f);
            Vector3 center = (boxTop + balloonPosition) * 0.5f;
            float length = Mathf.Max(0.02f, balloonPosition.y - boxTop.y);
            balloonString.localPosition = center;
            balloonString.localScale = new Vector3(0.008f, length * 0.5f, 0.008f);
        }

        private void HideVisuals()
        {
            if (visualRoot != null)
                visualRoot.gameObject.SetActive(false);
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
        }

        private static void DestroyMaterial(Material material)
        {
            if (material == null)
                return;
            if (Application.isPlaying)
                Destroy(material);
            else
                DestroyImmediate(material);
        }
    }
}
