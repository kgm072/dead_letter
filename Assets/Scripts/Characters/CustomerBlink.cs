using UnityEngine;

namespace DeadLetter.Characters
{
    /// <summary>
    /// 눈 깜빡임. 눈 감은 텍스처(_EyesClosed)로 잠깐 바꿨다가 되돌린다.
    /// 모델의 모든 재질이 같은 텍스처 한 장(아틀라스)을 쓰므로 렌더러 단위로 교체한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CustomerBlink : MonoBehaviour
    {
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        [SerializeField] private Vector2 intervalRange = new(2.2f, 5.5f);
        [SerializeField, Range(0.04f, 0.3f)] private float closedDuration = 0.11f;
        [SerializeField, Range(0f, 1f)] private float doubleBlinkChance = 0.2f;

        private Renderer[] renderers;
        private Texture openTexture;
        private Texture closedTexture;
        private MaterialPropertyBlock block;
        private float timer;
        private float closedTimer;
        private bool pendingDouble;
        private bool closed;
        private bool rollAllowed = true;

        public void Initialize(Renderer[] targets, Texture open, Texture closedTex)
        {
            renderers = targets;
            openTexture = open;
            closedTexture = closedTex;
            block ??= new MaterialPropertyBlock();
            timer = Random.Range(intervalRange.x, intervalRange.y);
            SetClosed(false);
        }

        private void Update()
        {
            if (renderers == null || closedTexture == null || openTexture == null)
                return;

            if (closed)
            {
                closedTimer -= Time.deltaTime;
                if (closedTimer <= 0f)
                {
                    SetClosed(false);
                    if (pendingDouble)
                    {
                        pendingDouble = false;
                        rollAllowed = false;
                        timer = 0.12f;
                    }
                    else
                    {
                        timer = Random.Range(intervalRange.x, intervalRange.y);
                    }
                }
                return;
            }

            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                closedTimer = closedDuration;
                if (rollAllowed)
                    pendingDouble = Random.value < doubleBlinkChance;
                rollAllowed = true;
                SetClosed(true);
            }
        }

        private void OnDisable()
        {
            if (renderers != null && openTexture != null)
                SetClosed(false);
        }

        private void SetClosed(bool value)
        {
            closed = value;
            Texture tex = value ? closedTexture : openTexture;
            if (tex == null) return;
            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetTexture(BaseMapId, tex);
                block.SetTexture(MainTexId, tex);
                r.SetPropertyBlock(block);
            }
        }
    }
}
