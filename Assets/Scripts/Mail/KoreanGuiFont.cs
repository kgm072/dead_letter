using UnityEngine;

namespace DeadLetter.Mail
{
    internal static class KoreanGuiFont
    {
        private const string GalmuriResourcePath = "UI/Fonts/GalmuriMono11/GalmuriMono11";
        private static Font cachedFont;

        public static Font Get()
        {
            if (cachedFont == null)
            {
                cachedFont = Resources.Load<Font>(GalmuriResourcePath);
                if (cachedFont == null)
                {
                    Debug.LogWarning(
                        $"GalmuriMono11을 Resources에서 찾지 못했습니다: {GalmuriResourcePath}. " +
                        "운영체제 한글 폰트로 대체합니다.");
                    cachedFont = Font.CreateDynamicFontFromOSFont(
                        new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" },
                        18);
                }
            }

            return cachedFont;
        }
    }
}
