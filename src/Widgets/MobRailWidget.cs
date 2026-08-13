using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LCBridgeOverlay
{
    /// <summary>
    /// Иконки монстров по бортам оверлея (слева — улица, справа — комплекс) и
    /// ловушек снизу — порт логики renderRail / parseMobEntry / canonMonster
    /// из HTML-оверлея:
    ///  - варианты одного базового монстра (обычный / с турелью / slayer)
    ///    складываются в одну «колоду» внахлёст;
    ///  - ToilHead-версии получают свои иконки (toilhead / mantitoil);
    ///  - slayer-версии BCME и жуки-камикадзе перекрашиваются в красный;
    ///  - большая красная цифра — суммарное количество в колоде;
    ///  - мелкая «фоновая» живность (manticoil, locust и т.п.) скрывается.
    /// </summary>
    public class MobRailWidget : MonoBehaviour
    {
        private const float Icon = 42f;
        private const float Overlap = 21f;   // нахлёст вариантов в колоде
        private const float RowStep = 48f;
        private const float TrapDrop = 20f;  // насколько опустить ловушки ниже кромки (чтобы не лезли на ивент)

        private OverlayManager _mgr;
        private RectTransform _left, _right, _traps;
        private string _sigMobs = "", _sigTraps = "";

        private class SwayItem
        {
            public RectTransform Rt;
            public Image Img;                 // для прозрачности по дистанции
            public string GroupKey;           // для поиска дистанции монстра
            public float Speed, Phase, Amp;   // покачивание
            public float Scale;               // целевой масштаб (для «размер по кол-ву»)
            public float Appear;              // 0..1 — анимация появления (поп-ин)
            public float Alpha = 1f;          // текущая (сглаженная) прозрачность
            public Color BaseColor = Color.white; // обычный цвет иконки
            public float HurtFlash;           // 1 → 0, вспышка при получении урона
        }

        private const float HurtFlashTime = 0.45f;
        private static readonly Color HurtColor = new Color(1f, 0.22f, 0.18f, 1f);
        private readonly List<SwayItem> _sway = new List<SwayItem>();

        // ближайшая дистанция до игрока по группе монстров (обновляется каждый пакет,
        // НЕ вызывая пересборку рейки — прозрачность крутим в Update)
        private readonly Dictionary<string, float> _distByGroup = new Dictionary<string, float>();
        // иконка по ключу группы — чтобы запустить вспышку урона без пересборки рейки
        private readonly Dictionary<string, SwayItem> _byGroup = new Dictionary<string, SwayItem>();

        /// <summary>Иконки турелей на нижней рейке — эмиттеры для эффекта стрельбы.</summary>
        public readonly List<RectTransform> TurretIcons = new List<RectTransform>();

        /// <summary>Цвет цифр-счётчиков (меняется по стилю; ставит OverlayManager).</summary>
        public Color CountColor = OverlayStyle.FromHex("FF5141");

        // Стрельба турелей, установленных НА монстрах (ToilHead / MantiToil).
        // Свой эффект на каждую рейку — тогда координаты трассеров совпадают с
        // иконками этой рейки (у трапов эффект отдельный, на своём слое).
        private TrapFireEffect _fireLeft, _fireRight;
        private readonly List<RectTransform> _firingLeft = new List<RectTransform>();
        private readonly List<RectTransform> _firingRight = new List<RectTransform>();

        public void Init(OverlayManager mgr, RectTransform left, RectTransform right, RectTransform traps)
        {
            _mgr = mgr;
            _left = left;
            _right = right;
            _traps = traps;

            _fireLeft = left.gameObject.AddComponent<TrapFireEffect>();
            _fireLeft.Init(left, null, Color.white);
            _fireLeft.Emitters = _firingLeft;

            _fireRight = right.gameObject.AddComponent<TrapFireEffect>();
            _fireRight.Init(right, null, Color.white);
            _fireRight.Emitters = _firingRight;
        }

        private void Update()
        {
            float t = Time.unscaledTime;
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < _sway.Count; i++)
            {
                var s = _sway[i];
                if (s.Rt == null) continue;
                if (s.Appear < 1f) s.Appear = Mathf.MoveTowards(s.Appear, 1f, dt / 0.3f);
                float sc = s.Scale * EaseOutBack(s.Appear);         // появление с «отскоком»
                s.Rt.localScale = new Vector3(sc, sc, 1f);
                s.Rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin((t + s.Phase) * s.Speed) * s.Amp);

                // цвет иконки = прозрачность по дистанции + красная вспышка урона
                if (s.Img != null)
                {
                    float ta = AlphaForGroup(s.GroupKey);
                    s.Alpha = Mathf.Lerp(s.Alpha, ta, 1f - Mathf.Exp(-6f * dt));

                    // 2.13: вспышка затухает сама, чтобы иконка вернулась в обычный вид
                    if (s.HurtFlash > 0f) s.HurtFlash = Mathf.MoveTowards(s.HurtFlash, 0f, dt / HurtFlashTime);

                    var c = Color.Lerp(s.BaseColor, HurtColor, s.HurtFlash);
                    c.a = s.Alpha * s.Appear;
                    s.Img.color = c;
                }
            }
        }

        // near=полностью, far=почти прозрачно. Без ProximityFade — всегда 1.
        private float AlphaForGroup(string groupKey)
        {
            if (!ConfigSettings.ProximityFade.Value || string.IsNullOrEmpty(groupKey)) return 1f;
            if (!_distByGroup.TryGetValue(groupKey, out float d)) return 1f;
            const float near = 6f, far = 34f, minA = 0.28f;
            float k = Mathf.InverseLerp(far, near, d);   // near→1, far→0
            return Mathf.Lerp(minA, 1f, k);
        }

        // ease-out-back: масштаб 0 → ~1.1 → 1 (эффект «выскочил»)
        private static float EaseOutBack(float x)
        {
            if (x >= 1f) return 1f;
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float xm = x - 1f;
            return 1f + c3 * xm * xm * xm + c1 * xm * xm;
        }

        public void SetMobs(string[] outside, string[] inside)
        {
            // дистанции обновляем ВСЕГДА (для живой прозрачности), не пересобирая рейку.
            // ВАЖНО: чистим перед заполнением — иначе копился бы «минимум за всё время»
            // (иконка близко подошедшего монстра навсегда оставалась бы непрозрачной).
            // ВАЖНО: ключи раздельные для улицы и комплекса — иначе одинаковый монстр
            // снаружи и внутри делил бы одну дистанцию и подсвечивался одинаково.
            _distByGroup.Clear();
            UpdateDistances(outside, true);
            UpdateDistances(inside, false);
            // 2.13: вспышки урона — тоже без пересборки рейки
            TriggerHurt(outside, true);
            TriggerHurt(inside, false);

            // сигнатура НЕ зависит ни от порядка, ни от дистанции (мост шлёт список в
            // разном порядке + дистанция меняется каждый тик) → перестраиваем рейку
            // только при реальном изменении СОСТАВА, иначе иконки «дёргались» каждую секунду
            string sig = JoinSorted(outside) + "||" + JoinSorted(inside);
            if (sig == _sigMobs) return;
            _sigMobs = sig;
            RebuildRail(_left, outside, growLeft: true);
            RebuildRail(_right, inside, growLeft: false);
        }

        /// <summary>Ключ подсветки: сторона + группа. Один и тот же монстр снаружи и
        /// внутри — это ДВЕ разные иконки, и подсвечиваться они должны независимо.</summary>
        private static string SideKey(bool outsideRail, string groupKey) =>
            (outsideRail ? "o|" : "i|") + (groupKey ?? "");

        /// <summary>
        /// Зажечь иконку монстра красным ПРЯМО СЕЙЧАС (зовётся из патча HitEnemy).
        /// Имя — как его отдаёт EnemyResolver; сторона — улица/комплекс.
        /// </summary>
        public void FlashMonster(string rawName, bool outside)
        {
            try
            {
                if (!ConfigSettings.DamageFlash.Value || string.IsNullOrEmpty(rawName)) return;
                var d = Parse(rawName);
                if (string.IsNullOrEmpty(d.GroupKey)) return;
                if (_byGroup.TryGetValue(SideKey(outside, d.GroupKey), out var item) &&
                    item != null && item.Rt != null)
                    item.HurtFlash = 1f;
            }
            catch { }
        }

        // монстр с меткой +Hurt → запускаем вспышку у его иконки (рейку не трогаем)
        private void TriggerHurt(string[] arr, bool outsideRail)
        {
            if (arr == null || !ConfigSettings.DamageFlash.Value) return;
            foreach (var raw in arr)
            {
                if (raw == null || raw.IndexOf("+Hurt", StringComparison.OrdinalIgnoreCase) < 0) continue;
                var d = Parse(raw);
                if (string.IsNullOrEmpty(d.GroupKey)) continue;
                if (_byGroup.TryGetValue(SideKey(outsideRail, d.GroupKey), out var item) &&
                    item != null && item.Rt != null)
                    item.HurtFlash = 1f;
            }
        }

        // "@<метры>" в конце записи → в _distByGroup (ключ — сторона + группа)
        private void UpdateDistances(string[] arr, bool outsideRail)
        {
            if (arr == null) return;
            foreach (var raw in arr)
            {
                var d = Parse(raw);
                if (d.Dist < 0f || string.IsNullOrEmpty(d.GroupKey)) continue;
                string k = SideKey(outsideRail, d.GroupKey);
                if (!_distByGroup.TryGetValue(k, out float md) || d.Dist < md)
                    _distByGroup[k] = d.Dist;
            }
        }

        // Из СИГНАТУРЫ рейки убираем всё, что меняется часто: дистанцию и метку урона.
        // Иначе рейка пересобиралась бы при каждом попадании по монстру и иконки бы
        // «дёргались» (пере-появлялись). Вспышку урона запускаем отдельно, без пересборки.
        private static string StripVolatile(string s) =>
            string.IsNullOrEmpty(s) ? s
            : Regex.Replace(Regex.Replace(s, @"\s*@\d+\s*$", ""), @"\+hurt", "", RegexOptions.IgnoreCase);

        public void SetTraps(string[] traps)
        {
            UpdateTrapDistances(traps);   // всегда — для живой прозрачности ловушек
            string sig = JoinSorted(traps);
            if (sig == _sigTraps) return;
            _sigTraps = sig;
            RebuildTraps(traps);
        }

        private void UpdateTrapDistances(string[] traps)
        {
            if (traps == null) return;
            foreach (var raw in traps)
            {
                var d = Parse(raw);
                string icon = TrapIcon(d.Name);
                if (icon == null || d.Dist < 0f) continue;
                if (!_distByGroup.TryGetValue(icon, out float md) || d.Dist < md)
                    _distByGroup[icon] = d.Dist;
            }
        }

        // имя ловушки → ключ иконки
        private static string TrapIcon(string name)
        {
            string n = Norm(name);
            if (n.Contains("turret") || n.Contains("турел")) return "turret";
            if (n.Contains("mine") || n.Contains("мин")) return "landmine";
            if (n.Contains("spike") || n.Contains("шип")) return "spiketrap";
            return null;
        }

        private static string JoinSorted(string[] arr)
        {
            if (arr == null || arr.Length == 0) return "";
            var copy = new string[arr.Length];
            for (int i = 0; i < arr.Length; i++) copy[i] = StripVolatile(arr[i]); // дистанцию и +Hurt — вон из сигнатуры
            Array.Sort(copy, StringComparer.Ordinal);
            return string.Join("|", copy);
        }

        // ==================== разбор записей (порт parseMobEntry) ====================

        private class Desc
        {
            public string Name;
            public int Cnt;
            public bool Turret, Slayer, Kamikaze;
            // состояния из моста (см. MonsterState): зол / развернулся / вырос /
            // атакует / на потолке / застыл / отсканирован
            public bool Aggro, Angry, Adult, Attack, Ceiling, Frozen, Scanned, Firing, Hurt;
            public float Dist = -1f;   // ближайшая дистанция до игрока, м (-1 = нет)
            public string GroupKey, IconKey;
            public int Rank => (Slayer || Kamikaze ? 2 : 0) + (Turret ? 1 : 0);
        }

        private class Group
        {
            public string Key;
            public int Total;
            public readonly List<Desc> Variants = new List<Desc>();
        }

        private static string Norm(string s)
        {
            var sb = new StringBuilder();
            foreach (var c in (s ?? "").ToLowerInvariant())
                if ((c >= 'a' && c <= 'z') || (c >= 'а' && c <= 'я') || c == '+') sb.Append(c);
            return sb.ToString();
        }

        // мелкая «фоновая» живность — не показываем (кроме турельных/slayer-версий)
        private static readonly string[] HideKeys = { "manticoil", "locust", "docile", "vain", "shroud" };

        private static bool Hidden(string name)
        {
            string raw = (name ?? "").ToLowerInvariant();
            if (raw.Contains("+turret") || raw.Contains("slayer")) return false;
            string n = Norm(name);
            foreach (var h in HideKeys)
                if (n.Contains(h)) return true;
            return false;
        }

        // канонизация: все варианты/синонимы → одно базовое имя (порт canonMonster)
        private static string Canon(string name)
        {
            string n = Norm(name);
            if (n.Contains("nut")) return "Nutcracker";
            if (n.Contains("manti")) return "Manticoil";
            if (n.Contains("toil") || n.Contains("spring") || n.Contains("coil")) return "Coil-Head";
            if (n.Contains("hoard") || n.Contains("kamikaz")) return "Hoarding bug";
            return name;
        }

        // имя из игры (enemyType.enemyName) → ключ иконки в res/mobs
        private static string IconFor(string baseName)
        {
            string n = Norm(baseName);
            if (n.Contains("masked") || n.Contains("mimic")) return "masked";
            if (n.Contains("spring") || n.Contains("coil")) return "coil";
            if (n.Contains("nutcracker")) return "nutcracker";
            if (n.Contains("spider")) return "spider";
            if (n.Contains("flowerman") || n.Contains("bracken")) return "bracken";
            if (n.Contains("crawler") || n.Contains("thumper")) return "thumper";
            if (n.Contains("hoard")) return "hoardingbug";
            if (n.Contains("centipede") || n.Contains("snare")) return "snareflea";
            if (n.Contains("jester")) return "jester";
            if (n.Contains("blob") || n.Contains("hygrodere")) return "hygrodere";
            if (n.Contains("girl") || n.Contains("ghost")) return "ghostgirl";
            if (n.Contains("puffer") || n.Contains("spore")) return "sporelizard";
            if (n.Contains("hornet") || n.Contains("butlerbees")) return "maskhornets";
            if (n.Contains("butler")) return "butler";
            if (n.Contains("mouthdog") || n.Contains("eyeless")) return "eyelessdog";
            if (n.Contains("sapsucker")) return "sapsucker";
            if (n.Contains("forest") || n.Contains("giant")) return "forestkeeper";
            if (n.Contains("leviathan")) return "leviathan";
            if (n.Contains("baboon")) return "baboonhawk";
            if (n.Contains("oldbird") || n.Contains("radmech")) return "oldbird";
            if (n.Contains("tulip") || n.Contains("flowersnake")) return "tulip";
            if (n.Contains("bushwolf") || n.Contains("kidnapper") || n.Contains("fox")) return "kidnapper";
            if (n.Contains("barber") || n.Contains("surgeon") || n.Contains("claysurgeon")) return "barber";
            if (n.Contains("maneater") || n.Contains("cavedweller")) return "maneater";
            if (n.Contains("cadaverbloom")) return "cadaverbloom";
            if (n.Contains("cadaver")) return "cadaver";
            if (n.Contains("feiopar")) return "feiopar";
            if (n.Contains("gunkfish") || n.Contains("gunk") || n.Contains("backwater") || n.Contains("stingray")) return "gunkfish";
            if (n.Contains("manticoil")) return "manticoil";
            if (n.Contains("redlocust")) return "redlocust";
            if (n.Contains("lasso")) return "lassoman";
            return null;
        }

        // раз на уникальное имя — чтобы не спамить лог
        private static readonly HashSet<string> _loggedNoIcon = new HashSet<string>();
        private static void LogNoIcon(string raw, string iconKey)
        {
            if (string.IsNullOrEmpty(raw) || !_loggedNoIcon.Add(raw)) return;
            string why = iconKey == null ? "нет маппинга имени → иконки" : $"нет/битый спрайт '{iconKey}'";
            Plugin.Log?.LogInfo($"[no-icon] монстр \"{raw}\" не показан ({why}). Пришли эту строку — добавлю иконку/алиас.");
        }

        private static Desc Parse(string entry)
        {
            string s = entry ?? "";
            // сначала вырезаем дистанцию "@<метры>" в конце
            float dist = -1f;
            var dm = Regex.Match(s, @"\s*@(\d+)\s*$");
            if (dm.Success)
            {
                if (int.TryParse(dm.Groups[1].Value, out int di)) dist = di;
                s = s.Substring(0, dm.Index);
            }
            var m = Regex.Match(s, @"^(.*?)(?:\s+x(\d+))?$");
            string nm = m.Success ? m.Groups[1].Value.Trim() : s;
            int cnt = 1;
            if (m.Success && m.Groups[2].Success) int.TryParse(m.Groups[2].Value, out cnt);

            string low = nm.ToLowerInvariant();
            bool turret = low.Contains("+turret") || low.Contains("toil"); // Toil* = коил с турелью
            bool slayer = low.Contains("slayer");
            bool kamikaze = low.Contains("kamikaz");
            // состояния, которые дописывает MonsterState на стороне моста
            bool aggro   = low.Contains("+aggro");
            bool angry   = low.Contains("+angry");
            bool adult   = low.Contains("+adult");
            bool attack  = low.Contains("+attack");
            bool ceiling = low.Contains("+ceiling");
            bool frozen  = low.Contains("+frozen");
            bool scanned = low.Contains("+scanned");
            bool firing  = low.Contains("+firing");   // турель на монстре ведёт огонь
            bool hurt    = low.Contains("+hurt");     // только что получил урон

            string baseName = Regex.Replace(nm,
                @"\+turret|\+slayer|\+aggro|\+angry|\+adult|\+attack|\+ceiling|\+frozen|\+scanned|\+firing|\+hurt",
                "", RegexOptions.IgnoreCase).Trim();
            baseName = Canon(baseName);
            string groupKey = Norm(baseName);
            if (groupKey.Length == 0) groupKey = low;

            // иконка: коил с турелью → toilhead, manticoil с турелью → mantitoil,
            // камикадзе без своей иконки → обычный жук (перекрасится в красный)
            string icon;
            bool mantiBase = groupKey.Contains("manticoil");
            bool coilBase = !mantiBase && (groupKey.Contains("coil") || groupKey.Contains("spring"));
            if (turret && mantiBase) icon = "mantitoil";
            else if (turret && coilBase) icon = "toilhead";
            else
            {
                icon = IconFor(baseName);
                if (icon == null && kamikaze) icon = "hoardingbug";
            }

            var d = new Desc
            {
                Name = nm,
                Cnt = Math.Max(1, cnt),
                Turret = turret,
                Slayer = slayer,
                Kamikaze = kamikaze,
                Aggro = aggro, Angry = angry, Adult = adult,
                Attack = attack, Ceiling = ceiling, Frozen = frozen, Scanned = scanned, Firing = firing,
                Hurt = hurt,
                Dist = dist,
                GroupKey = groupKey,
                IconKey = icon,
            };
            d.IconKey = StateVariant(icon, d);
            return d;
        }

        /// <summary>
        /// Выбор иконки состояния по приоритету (ТЗ п.3.3), от высшего к низшему:
        ///   ЗОЛ/АТАКУЕТ → агрессия → трансформирован → особое состояние → пассив.
        /// Исключение: у потолочной личинки состояние «на потолке» приоритетнее.
        /// Если спрайта состояния нет — молча остаёмся на базовом.
        /// </summary>
        private static string StateVariant(string icon, Desc d)
        {
            if (string.IsNullOrEmpty(icon)) return icon;
            string want = null;

            if (icon == "snareflea")            want = d.Ceiling ? "snareflea_ceiling" : null; // обратный приоритет
            else if (icon == "jester")          want = d.Angry ? "jester_angry" : null;        // ЗОЛ
            else if (icon == "nutcracker")      want = d.Attack ? "nutcracker_attack" : null;  // АТАКУЕТ
            else if (icon == "hoardingbug")     want = d.Aggro ? "hoardingbug_aggro" : null;   // агрессия
            else if (icon == "maneater")        want = d.Adult ? "maneater_adult" : null;      // трансформация

            if (want == null) return icon;
            return SpriteBank.Get(want) != null ? want : icon;
        }

        // ==================== отрисовка ====================

        private void ClearRail(RectTransform rail)
        {
            _sway.RemoveAll(x => x.Rt == null || x.Rt.IsChildOf(rail));
            // ссылки на уничтоженные иконки убираем, иначе вспышка ушла бы «в никуда»
            var stale = new List<string>();
            foreach (var kv in _byGroup)
                if (kv.Value == null || kv.Value.Rt == null || kv.Value.Rt.IsChildOf(rail)) stale.Add(kv.Key);
            foreach (var k in stale) _byGroup.Remove(k);
            for (int i = rail.childCount - 1; i >= 0; i--)
                Destroy(rail.GetChild(i).gameObject);
        }

        private void RebuildRail(RectTransform rail, string[] list, bool growLeft)
        {
            ClearRail(rail);
            // эмиттеры стрельбы для ЭТОЙ рейки пересобираем вместе с иконками
            var firingList = growLeft ? _firingLeft : _firingRight;
            var fx = growLeft ? _fireLeft : _fireRight;
            firingList.Clear();
            if (fx != null) fx.Firing = false;
            if (list == null || list.Length == 0) return;

            // группировка вариантов одного базового монстра в колоду
            var groups = new List<Group>();
            var byKey = new Dictionary<string, Group>();
            foreach (var raw in list)
            {
                if (Hidden(raw)) continue;
                var d = Parse(raw);
                // ТЗ 3.1: с RequireScanToShow монстр появляется только после сканирования
                if (ConfigSettings.RequireScanToShow.Value && !d.Scanned) continue;
                if (d.IconKey == null || SpriteBank.Get(d.IconKey) == null)
                {
                    // диагностика: какой монстр остался без иконки (имя из игры) —
                    // по логу видно, какой алиас/иконку добавить
                    LogNoIcon(raw, d.IconKey);
                    continue;
                }
                if (!byKey.TryGetValue(d.GroupKey, out var g))
                {
                    g = new Group { Key = d.GroupKey };
                    byKey[d.GroupKey] = g;
                    groups.Add(g);
                }
                var same = g.Variants.Find(v => v.Rank == d.Rank && v.IconKey == d.IconKey);
                if (same != null) same.Cnt += d.Cnt;
                else g.Variants.Add(d);
                g.Total += d.Cnt;
            }
            // 2.10: если у монстра несколько версий (обычный / с турелью / slayer) —
            // показываем только БЛИЖАЙШУЮ к игроку. Смысла подсвечивать дальнего
            // обычного коилхеда, когда рядом стоит версия с турелью, нет.
            if (ConfigSettings.NearestVariantOnly.Value)
            {
                foreach (var g in groups)
                {
                    if (g.Variants.Count < 2) continue;
                    Desc best = null;
                    foreach (var v in g.Variants)
                    {
                        if (best == null) { best = v; continue; }
                        // без дистанции вариант считаем «дальним»
                        float bd = best.Dist < 0f ? float.MaxValue : best.Dist;
                        float vd = v.Dist < 0f ? float.MaxValue : v.Dist;
                        if (vd < bd) best = v;
                    }
                    if (best == null) continue;
                    g.Variants.Clear();
                    g.Variants.Add(best);
                    g.Total = best.Cnt;   // счётчик — по показанной версии
                }
            }

            groups.Sort((a, b) => b.Total.CompareTo(a.Total)); // многочисленные — выше

            bool exp = ConfigSettings.ScaleMonstersByCount.Value;

            float y = 0f; // от верха рейки вниз (обе рейки выровнены по верху)
            foreach (var g in groups)
            {
                // эксперимент: без цифр — чем больше монстров, тем крупнее иконка и сильнее тряска
                float scale = 1f, amp = 5f;
                if (exp)
                {
                    int extra = Mathf.Clamp(g.Total - 1, 0, 8);
                    scale = 1f + extra * 0.10f;   // до 1.8×
                    amp = 5f + extra * 2.2f;      // до ~22°
                }

                g.Variants.Sort((a, b) => a.Rank.CompareTo(b.Rank)); // обычный → турель → slayer
                float x = 0f;
                foreach (var v in g.Variants)
                {
                    // застывший койл (на него кто-то смотрит) — покачивание выключаем
                    float vAmp = v.Frozen ? 0f : amp;
                    // ключ подсветки — со стороной рейки (улица/комплекс раздельно)
                    var irt = MakeIcon(rail, v.IconKey, v.Slayer || v.Kamikaze, g.Key.GetHashCode(),
                                       scale, vAmp, SideKey(growLeft, g.Key), v.Hurt);
                    // первая иконка колоды центром на кромке (x=0), остальные — наружу
                    irt.anchoredPosition = new Vector2(growLeft ? -x : x, y);
                    // у монстра стреляет турель — трассеры полетят из его иконки
                    if (v.Firing) firingList.Add(irt);
                    x += Overlap;
                }
                if (!exp && g.Total > 1)
                {
                    // счётчик — в нижнем ВНЕШНЕМ углу крайней иконки, внахлёст с ней
                    var cntT = _mgr.MakeText(rail, g.Total.ToString(), 26f,
                        CountColor, TextAlignmentOptions.Center, bold: true, big: true);
                    cntT.enableWordWrapping = false;
                    cntT.overflowMode = TextOverflowModes.Overflow;
                    var crt = cntT.rectTransform;
                    crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
                    crt.pivot = new Vector2(0.5f, 0.5f);
                    crt.sizeDelta = new Vector2(34f, 30f);
                    float outerCenterX = growLeft ? -(x - Overlap) : (x - Overlap);
                    float cornerX = outerCenterX + (growLeft ? (-Icon / 2f + 10f) : (Icon / 2f - 10f));
                    float cornerY = y - Icon / 2f + 10f;
                    crt.anchoredPosition = new Vector2(cornerX, cornerY);
                    _mgr.AddPerspective(cntT, false);
                }
                y -= RowStep;
            }

            // включаем стрельбу на этой рейке, если хоть у одного монстра турель ведёт огонь
            if (fx != null) fx.Firing = firingList.Count > 0;
        }

        private void RebuildTraps(string[] traps)
        {
            ClearRail(_traps);
            TurretIcons.Clear();
            if (traps == null || traps.Length == 0) return;

            // имя → количество (по типу ловушки)
            var order = new List<string>();
            var counts = new Dictionary<string, int>();
            foreach (var raw in traps)
            {
                var d = Parse(raw);
                string icon = TrapIcon(d.Name);
                if (icon == null || SpriteBank.Get(icon) == null) continue;
                if (!counts.ContainsKey(icon)) { counts[icon] = 0; order.Add(icon); }
                counts[icon] += d.Cnt;
            }
            if (order.Count == 0) return;

            // тот же эксперимент, что и для монстров: без цифр — чем больше ловушек
            // данного типа, тем крупнее иконка и сильнее тряска
            bool exp = ConfigSettings.ScaleMonstersByCount.Value;

            float step = Icon + 10f;
            float x0 = -(order.Count * step - 10f) / 2f + Icon / 2f;
            for (int i = 0; i < order.Count; i++)
            {
                string icon = order[i];
                int cnt = counts[icon];

                float scale = 1f, amp = 5f;
                if (exp)
                {
                    int extra = Mathf.Clamp(cnt - 1, 0, 8);
                    scale = 1f + extra * 0.10f;   // до 1.8×
                    amp = 5f + extra * 2.2f;      // до ~22°
                }

                // groupKey = ключ иконки → прозрачность по дистанции (как у монстров)
                var irt = MakeIcon(_traps, icon, false, i * 17, scale, amp, icon);
                // чуть НИЖЕ линии-кромки, чтобы верхушка не залезала на надпись ивента
                irt.anchoredPosition = new Vector2(x0 + i * step, -TrapDrop);
                if (icon == "turret") TurretIcons.Add(irt);

                if (!exp && cnt > 1)
                {
                    var cn = _mgr.MakeText(irt.transform, cnt.ToString(), 26f,
                        CountColor, TextAlignmentOptions.Center, bold: true, big: true);
                    cn.enableWordWrapping = false;
                    cn.overflowMode = TextOverflowModes.Overflow;
                    var crt = cn.rectTransform;
                    crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0f);
                    crt.pivot = new Vector2(0.5f, 1f);
                    crt.sizeDelta = new Vector2(48f, 30f);
                    crt.anchoredPosition = new Vector2(0f, 2f);
                    _mgr.AddPerspective(cn, false);
                }
            }
        }

        private RectTransform MakeIcon(RectTransform rail, string iconKey, bool angry, int seed, float scale, float amp, string groupKey = null, bool hurt = false)
        {
            var go = OverlayManager.NewUI("Mob_" + iconKey, rail);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(Icon, Icon);
            rt.localScale = Vector3.zero;   // стартуем с нуля → поп-ин в Update
            var img = go.AddComponent<Image>();
            // slayer/камикадзе (ТЗ 3.4): та же иконка, но с НАЛОЖЕННЫМИ КРОВАВЫМИ
            // ПЯТНАМИ по силуэту — вместо прежней сплошной перекраски в красный.
            Sprite spr = angry ? SpriteBank.GetBloody(iconKey) : null;
            img.sprite = spr != null ? spr : SpriteBank.Get(iconKey);
            img.preserveAspect = true;
            img.raycastTarget = false;
            _mgr.AddPerspective(img, true); // иконка под тем же углом/перспективой (качается → пересчёт)
            var item = new SwayItem
            {
                Rt = rt,
                Img = img,
                GroupKey = groupKey,
                Speed = 2.0f + (Mathf.Abs(seed) % 7) * 0.15f,
                Phase = (Mathf.Abs(seed) % 13) * 0.5f,
                Amp = amp,
                Scale = scale,
                Appear = 0f,
                BaseColor = Color.white,
                HurtFlash = hurt ? 1f : 0f,
            };
            _sway.Add(item);
            _byGroup[groupKey ?? ""] = item;   // чтобы вспышку можно было запустить без пересборки
            return rt;
        }
    }
}
