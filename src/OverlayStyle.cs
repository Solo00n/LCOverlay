using UnityEngine;

namespace LCBridgeOverlay
{
    /// <summary>
    /// Палитра и режим рамок оверлея. Два пресета:
    ///  - Legacy — старый дизайн HTML-оверлея (пиксельный гранж, красные
    ///    уголки-«скобки», мерцающие пиксели, оранжевый текст, тёмный фон);
    ///  - Game   — под внутриигровой чат: почти прозрачный фон, синие уголковые
    ///    «скобки» (как маркеры чата на скрине), оранжевые цифры «как часы».
    /// </summary>
    public class OverlayStyle
    {
        public Color Bg;          // фон панели
        public Color Accent;      // главный акцент (логотип, цифры, «часы»)
        public Color AccentDim;   // приглушённый акцент
        public Color Frame;       // рамка/уголки/разделители
        public Color FrameDim;    // приглушённая рамка (неактивные табы)
        public Color Text;        // основной текст
        public Color TextDim;     // вторичный текст
        public Color Danger;      // предупреждения (Old Bird, смерти)

        public bool LegacyCorners; // пиксельные уголки + мерцающие пиксели + красные боксы (Legacy)
        public bool BlueBrackets;  // синие уголковые «скобки» без фона (Game)

        public static OverlayStyle Legacy() => new OverlayStyle
        {
            Bg = new Color(20f / 255f, 6f / 255f, 6f / 255f, 0.62f),
            Accent = FromHex("FF7A1A"),
            AccentDim = FromHex("C25910"),
            Frame = FromHex("FF3A2E"),
            FrameDim = FromHex("C43022"),
            Text = FromHex("FF7A1A"),
            TextDim = FromHex("C25910"),
            Danger = FromHex("FF5141"),
            LegacyCorners = true,
            BlueBrackets = false,
        };

        // «как игровой чат»: БЕЗ фона, синие скобки по углам
        public static OverlayStyle Game() => new OverlayStyle
        {
            Bg = new Color(0f, 0f, 0f, 0f),   // полностью прозрачный (без затемнения)
            Accent = FromHex("FF9A3D"),
            AccentDim = FromHex("B96A20"),
            Frame = new Color(0.43f, 0.44f, 0.86f, 0.85f),   // индиго-синие скобки чата
            FrameDim = new Color(0.43f, 0.44f, 0.86f, 0.45f),
            Text = FromHex("E6E6F2"),
            TextDim = FromHex("9BA0C4"),
            Danger = FromHex("FF5C5C"),
            LegacyCorners = false,
            BlueBrackets = true,
        };

        public static Color FromHex(string hex)
        {
            return ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : Color.white;
        }

        public static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        public static Color WithA(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
