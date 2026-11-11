using TMPro;
using UnityEngine;

namespace DeadLetter.UI
{
    [CreateAssetMenu(fileName = "DeadLetterUITheme", menuName = "DEAD LETTER/UI Theme")]
    public sealed class DeadLetterUITheme : ScriptableObject
    {
        [Header("Typography")]
        [SerializeField] private TMP_FontAsset primaryFont;
        [SerializeField, Min(1)] private int titleSize = 36;
        [SerializeField, Min(1)] private int headingSize = 24;
        [SerializeField, Min(1)] private int bodySize = 18;
        [SerializeField, Min(1)] private int captionSize = 16;

        [Header("Palette")]
        [SerializeField] private Color background = new(0.018f, 0.018f, 0.02f, 1f);
        [SerializeField] private Color secondaryBackground = new(0.12f, 0.075f, 0.045f, 1f);
        [SerializeField] private Color primaryText = new(0.95f, 0.91f, 0.82f, 1f);
        [SerializeField] private Color mutedText = new(0.62f, 0.59f, 0.54f, 1f);
        [SerializeField] private Color accent = new(0.82f, 0.36f, 0.12f, 1f);
        [SerializeField] private Color monitor = new(0.18f, 0.42f, 0.27f, 1f);
        [SerializeField] private Color success = new(0.34f, 0.66f, 0.43f, 1f);
        [SerializeField] private Color danger = new(0.78f, 0.21f, 0.16f, 1f);

        public TMP_FontAsset PrimaryFont => primaryFont;
        public int TitleSize => titleSize;
        public int HeadingSize => headingSize;
        public int BodySize => bodySize;
        public int CaptionSize => captionSize;
        public Color Background => background;
        public Color SecondaryBackground => secondaryBackground;
        public Color PrimaryText => primaryText;
        public Color MutedText => mutedText;
        public Color Accent => accent;
        public Color Monitor => monitor;
        public Color Success => success;
        public Color Danger => danger;
    }
}
