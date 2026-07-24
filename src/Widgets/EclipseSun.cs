using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace LCBridgeOverlay
{
    /// <summary>
    /// «Глаз» — эмблема оверлея и индикатор связи с мостом. Берётся из рисунка
    /// пользователя (res/eye.png — белый силуэт + альфа, тинтуется в любой цвет
    /// через Image.color). Если ресурс не найден — рисуется процедурно.
    /// Тинт: оранжевый (акцент) при связи, серый — без связи. Умеет «закрываться»
    /// (EyeWidget) при выходе с корабля.
    /// </summary>
    public static class EclipseSun
    {
        private static Sprite _sprite;

        public static Sprite Get()
        {
            if (_sprite != null) return _sprite;
            _sprite = LoadEmbedded() ?? Procedural();
            return _sprite;
        }

        private static Sprite LoadEmbedded()
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                var stream = asm.GetManifestResourceStream("LCBridgeOverlay.res.eye.png");
                if (stream == null) return null;
                byte[] bytes;
                using (var ms = new MemoryStream()) { stream.CopyTo(ms); bytes = ms.ToArray(); }
                stream.Dispose();
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
                if (!ImageConversion.LoadImage(tex, bytes)) return null;
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
            catch { return null; }
        }

        // запасной процедурный глаз (если ресурс не подхватился)
        private static Sprite Procedural()
        {
            const int SS = 4, W = 60, H = 34;
            int bw = W * SS, bh = H * SS;
            float cx = (bw - 1) / 2f, cy = (bh - 1) / 2f;
            float rx = 27f * SS, ry = 14.5f * SS, lidTh = 2.4f * SS;
            float irisR = 9.5f * SS, irisTh = 2.4f * SS, pupilR = 4.2f * SS;
            var big = new float[bw * bh];
            for (int y = 0; y < bh; y++)
                for (int x = 0; x < bw; x++)
                {
                    float dx = x - cx, dy = y - cy, nx = dx / rx;
                    bool on = false;
                    if (Mathf.Abs(nx) <= 1f)
                    {
                        float lid = ry * (1f - nx * nx);
                        if (Mathf.Abs(dy - lid) < lidTh || Mathf.Abs(dy + lid) < lidTh) on = true;
                    }
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d < pupilR) on = true;
                    else if (Mathf.Abs(d - irisR) < irisTh) on = true;
                    if (on) big[y * bw + x] = 1f;
                }
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float sum = 0f;
                    for (int sy = 0; sy < SS; sy++)
                        for (int sx = 0; sx < SS; sx++)
                            sum += big[(y * SS + sy) * bw + (x * SS + sx)];
                    byte a = (byte)Mathf.Clamp(Mathf.RoundToInt(sum / (SS * SS) * 255f), 0, 255);
                    px[y * W + x] = new Color32(255, 255, 255, a);
                }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Глаз-индикатор в шапке (на месте логотипа): участвует в layout,
        /// умеет «закрываться» (EyeWidget). Возвращает EyeWidget (у него .Img для тинта).</summary>
        public static EyeWidget BuildInlineEye(Transform parent, float w, float h)
        {
            var go = new GameObject("EyeLogo", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            var img = go.AddComponent<Image>();
            img.sprite = Get();
            img.raycastTarget = false;
            img.preserveAspect = true;
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = w;
            le.preferredHeight = h;
            var wdg = go.AddComponent<EyeWidget>();
            wdg.Init(rt, img);
            return wdg;
        }

        /// <summary>Глаз-индикатор в левом верхнем углу панели (место логотипа),
        /// абсолютное положение (не зависит от layout/перспективы), с анимацией закрытия.</summary>
        public static EyeWidget BuildCornerEye(RectTransform panel)
        {
            var go = new GameObject("EyeLogo", typeof(RectTransform));
            go.transform.SetParent(panel, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(44f, 32f);
            rt.anchoredPosition = new Vector2(15f, -12f);
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            var img = go.AddComponent<Image>();
            img.sprite = Get();
            img.raycastTarget = false;
            img.preserveAspect = true;
            var w = go.AddComponent<EyeWidget>();
            w.Init(rt, img);
            return w;
        }

        /// <summary>Глаз-индикатор сверху по центру (половина над панелью), с анимацией закрытия.</summary>
        public static EyeWidget BuildOverlay(RectTransform panel)
        {
            var go = new GameObject("EyeTop", typeof(RectTransform));
            go.transform.SetParent(panel, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(52f, 52f);
            rt.anchoredPosition = new Vector2(0f, 8f);
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            var img = go.AddComponent<Image>();
            img.sprite = Get();
            img.raycastTarget = false;
            img.preserveAspect = true;
            go.transform.SetAsFirstSibling();
            var w = go.AddComponent<EyeWidget>();
            w.Init(rt, img);
            return w;
        }
    }
}
