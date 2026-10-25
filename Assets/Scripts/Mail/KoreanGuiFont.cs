using UnityEngine;

namespace DeadLetter.Mail
{
    internal static class KoreanGuiFont
    {
        private static Font cachedFont;

        public static Font Get()
        {
            if (cachedFont == null)
            {
                cachedFont = Font.CreateDynamicFontFromOSFont(
                    new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" },
                    18);
            }

            return cachedFont;
        }
    }
}
