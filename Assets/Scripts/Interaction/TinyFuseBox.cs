using UnityEngine;

namespace DeadLetter.Interaction
{
    public sealed class TinyFuseBox : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform lever;
        [SerializeField] private Renderer indicatorRenderer;
        [SerializeField] private Color poweredColor = new Color(0.12f, 0.85f, 0.22f);
        [SerializeField] private Color unpoweredColor = new Color(0.9f, 0.08f, 0.025f);

        private bool powered;

        private void Awake()
        {
            UpdateVisuals();
        }

        public void Interact(GameObject interactor)
        {
            powered = !powered;
            UpdateVisuals();
            Debug.Log(powered ? "Fuse box switched on." : "Fuse box switched off.", this);
        }

        private void UpdateVisuals()
        {
            if (lever != null)
                lever.localRotation = Quaternion.Euler(0f, 0f, powered ? 25f : -25f);

            if (indicatorRenderer != null)
            {
                Material material = indicatorRenderer.material;
                Color color = powered ? poweredColor : unpoweredColor;
                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", color);
                else if (material.HasProperty("_Color"))
                    material.SetColor("_Color", color);

                if (material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", color * 1.5f);
                }
            }
        }
    }
}
