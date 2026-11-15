using UnityEngine;

namespace DeadLetter.Interaction
{
    [ExecuteAlways]
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ArtColliderFollower : MonoBehaviour
    {
        [SerializeField] private Renderer targetRenderer;

        private BoxCollider cachedCollider;

        public Renderer TargetRenderer => targetRenderer;
        public BoxCollider BoundCollider => ResolveCollider();

        private void OnEnable()
        {
            SyncNow();
        }

        private void OnValidate()
        {
            SyncNow();
        }

        private void Update()
        {
            SyncNow();
        }

        public void Configure(Renderer renderer)
        {
            targetRenderer = renderer;
            SyncNow();
        }

        public bool SyncNow()
        {
            BoxCollider box = ResolveCollider();
            Transform parent = transform.parent;
            if (targetRenderer == null || box == null || parent == null)
                return false;

            Bounds rendererBounds = targetRenderer.localBounds;
            Vector3 min = rendererBounds.min;
            Vector3 max = rendererBounds.max;
            Bounds boundsInParent = new Bounds(
                parent.InverseTransformPoint(targetRenderer.transform.TransformPoint(min)),
                Vector3.zero);

            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int z = 0; z < 2; z++)
                    {
                        Vector3 corner = new Vector3(
                            x == 0 ? min.x : max.x,
                            y == 0 ? min.y : max.y,
                            z == 0 ? min.z : max.z);
                        boundsInParent.Encapsulate(
                            parent.InverseTransformPoint(targetRenderer.transform.TransformPoint(corner)));
                    }
                }
            }

            bool changed = false;
            changed |= SetIfChanged(transform.localPosition, boundsInParent.center, value => transform.localPosition = value);
            changed |= SetIfChanged(transform.localRotation, Quaternion.identity, value => transform.localRotation = value);
            changed |= SetIfChanged(transform.localScale, Vector3.one, value => transform.localScale = value);
            changed |= SetIfChanged(box.center, Vector3.zero, value => box.center = value);
            changed |= SetIfChanged(box.size, boundsInParent.size, value => box.size = value);

            // Physics can otherwise keep the previous broadphase position until the next
            // simulation step. Sync only when something moved so a same-frame raycast sees
            // the collider at the renderer's current position.
            if (changed && Application.isPlaying)
                Physics.SyncTransforms();
            return true;
        }

        private BoxCollider ResolveCollider()
        {
            if (cachedCollider == null)
                cachedCollider = GetComponent<BoxCollider>();
            return cachedCollider;
        }

        private static bool SetIfChanged(Vector3 current, Vector3 desired, System.Action<Vector3> setter)
        {
            if ((current - desired).sqrMagnitude <= 0.00000001f)
                return false;

            setter(desired);
            return true;
        }

        private static bool SetIfChanged(Quaternion current, Quaternion desired, System.Action<Quaternion> setter)
        {
            if (Quaternion.Angle(current, desired) <= 0.001f)
                return false;

            setter(desired);
            return true;
        }
    }
}
