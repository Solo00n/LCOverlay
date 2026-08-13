using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LCBridgeOverlay
{
    /// <summary>
    /// Баннер победы: появляется после сдачи 3-й квоты (quotaIndex >= 4)
    /// и показывает полную аналитику забега из поля "run" моста:
    /// итоги (время/лут/смерти), по квотам, луны, самые частые монстры,
    /// хроника по дням. Висит до начала новой игры (resetToken), как в HTML.
    /// Таблицы — моноширинным шрифтом с ручным паддингом.
    /// </summary>
    public class VictoryWidget : MonoBehaviour
    {
        private OverlayManager _mgr;
        private GameObject _content;

        public void Init(OverlayManager mgr)
        {
            _mgr = mgr;
            gameObject.SetActive(false);
        }

        public void Show(BridgePayload p, int timerSec)
        {
            BuildContent(p, timerSec);
            gameObject.SetActive(true);
            // содержимое баннера создаётся динамически (после общего ApplyPerspective),
            // поэтому перспективу/наклон навешиваем на него отдельно — иначе аналитика
            // остаётся «плоской», в отличие от остальной панели
            _mgr.AddPerspectiveToTree(transform);
        }

        public void Hide() => gameObject.SetActive(false);

        // Причины смерти приходят из моста по-английски; на русском показываем перевод,
        // имена врагов (тоже возможные «убийцы») оставляем как есть.
        private static readonly Dictionary<string, string> KillerRu = new Dictionary<string, string>
        {
            ["Fall"] = "Падение", ["Drowning"] = "Утопление", ["Suffocation"] = "Удушье",
            ["Fire"] = "Огонь", ["Shock"] = "Ток", ["Crushed"] = "Раздавлен", ["Unknown"] = "Неизвестно",
        };

        private static string LocKiller(string s)
        {
            if (ConfigSettings.RussianActive && s != null && KillerRu.TryGetValue(s.Trim(), out var ru)) return ru;
            return s;
        }

        private void BuildContent(BridgePayload p, int timerSec)
        {
            if (_content != null) Destroy(_content);
            var S = _mgr.Style;

            _content = _mgr.MakeCol(transform, 4f);
            // разделитель сверху
            var div = new GameObject("Divider", typeof(RectTransform));
            div.transform.SetParent(_content.transform, false);
            var im = div.AddComponent<Image>();
            im.color = S.Frame;
            im.raycastTarget = false;
            div.AddComponent<LayoutElement>().preferredHeight = 3f;

            // ---- шапка ----
            var stamp = _mgr.MakeText(_content.transform, Localization.T("vicStamp"), 14f,
                OverlayStyle.FromHex("FFB000"), TextAlignmentOptions.Center, bold: true, big: true);
            var title = _mgr.MakeText(_content.transform, Localization.T("vicTitle"), 34f,
                S.Danger, TextAlignmentOptions.Center, bold: true, big: true);
            var sub = _mgr.MakeText(_content.transform, Localization.T("vicSub"), 11f,
                S.Text, TextAlignmentOptions.Center);

            // ---- итоги: время / лут / смерти ----
            string totals =
                $"{Localization.T("vicTime")} <b>{OverlayManager.FmtTime(timerSec)}</b>   " +
                $"{Localization.T("vicLoot")} <b>${(p != null ? p.shipLoot : 0)}</b>   " +
                $"{Localization.T("vicDeaths")} <b>{(p != null ? p.deaths : 0)}</b>";
            _mgr.MakeText(_content.transform, totals, 13f, OverlayStyle.FromHex("FFB000"), TextAlignmentOptions.Center);

            // 2.8: суммарно ПРОДАНО компании за весь забег (не путать с лутом на планете)
            if (p != null && p.soldLoot > 0)
                _mgr.MakeText(_content.transform,
                    $"{Localization.T("vicSold")} <b>${p.soldLoot}</b>", 13f,
                    OverlayStyle.FromHex("FFB000"), TextAlignmentOptions.Center);

            var run = p?.run;
            if (run == null) return;

            // ---- по квотам ----
            if (run.quotas != null && run.quotas.Length > 0)
            {
                Section(Localization.T("vicQuotas"));
                var sb = new StringBuilder();
                foreach (var q in run.quotas)
                {
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append(("Q" + q.i).PadRight(4))
                      .Append(("$" + q.money).PadLeft(7))
                      .Append(OverlayManager.FmtTime(q.sec).PadLeft(9))
                      .Append(("X" + q.deaths).PadLeft(5));
                }
                Body(sb.ToString());
            }

            // ---- луны ----
            if (run.moons != null && run.moons.Length > 0)
            {
                Section(Localization.T("vicMoons"));
                var sb = new StringBuilder();
                int shown = 0;
                foreach (var m in run.moons)
                {
                    if (shown++ >= 6) break;
                    if (sb.Length > 0) sb.Append('\n');
                    string nm = m.name ?? "?";
                    if (nm.Length > 13) nm = nm.Substring(0, 13);
                    sb.Append(shown == 1 ? "* " : "  ")
                      .Append(nm.PadRight(14))
                      .Append(("$" + m.profit).PadLeft(7))
                      .Append(("x" + m.visits).PadLeft(4));
                }
                Body(sb.ToString());
            }

            // ---- монстры (топ по числу особей) ----
            if (run.monsters != null && run.monsters.Length > 0)
            {
                Section(Localization.T("vicMonsters"));
                var sb = new StringBuilder();
                int shown = 0;
                foreach (var m in run.monsters)
                {
                    if (shown++ >= 8) break;
                    if (sb.Length > 0) sb.Append('\n');
                    string nm = m.name ?? "?";
                    if (nm.Length > 18) nm = nm.Substring(0, 18);
                    sb.Append(nm.PadRight(19)).Append(("x" + m.count).PadLeft(4));
                }
                Body(sb.ToString());
            }

            // ---- хроника по дням ----
            if (run.timeline != null && run.timeline.Length > 0)
            {
                Section(Localization.T("vicTimeline"));
                var days = new SortedDictionary<int, DayInfo>();
                foreach (var raw in run.timeline)
                {
                    var parts = (raw ?? "").Split(new[] { '|' }, 3);
                    if (parts.Length < 3) continue;
                    if (!int.TryParse(parts[0], out int day)) continue;
                    if (!days.TryGetValue(day, out var d)) { d = new DayInfo(); days[day] = d; }
                    switch (parts[1])
                    {
                        case "day": d.Moon = parts[2]; break;
                        case "event": d.Events.Add(parts[2]); break;
                        case "death":
                            // формат "кто@луна" — берём только "кто"
                            int at = parts[2].IndexOf('@');
                            d.Deaths.Add(LocKiller(at > 0 ? parts[2].Substring(0, at) : parts[2]));
                            break;
                    }
                }
                var sb = new StringBuilder();
                foreach (var kv in days)
                {
                    if (sb.Length > 0) sb.Append('\n');
                    var d = kv.Value;
                    sb.Append('D').Append(kv.Key).Append(' ').Append(d.Moon ?? "?");
                    if (d.Events.Count > 0)
                        sb.Append("  <color=#F5D020>").Append(Localization.T("vicEvent")).Append(": ")
                          .Append(OverlayManager.Esc(string.Join(", ", d.Events))).Append("</color>");
                    if (d.Deaths.Count > 0)
                        sb.Append("  <color=#FF5141>").Append(Localization.T("vicDeath")).Append(": ")
                          .Append(OverlayManager.Esc(string.Join(", ", d.Deaths))).Append("</color>");
                    else
                        sb.Append("  <color=#").Append(OverlayStyle.Hex(S.TextDim)).Append('>')
                          .Append(Localization.T("vicNoLosses")).Append("</color>");
                }
                Body(sb.ToString(), 11f);
            }
        }

        private class DayInfo
        {
            public string Moon;
            public readonly List<string> Events = new List<string>();
            public readonly List<string> Deaths = new List<string>();
        }

        private void Section(string title)
        {
            _mgr.MakeText(_content.transform, title, 11f, _mgr.Style.Danger, TextAlignmentOptions.Center, bold: true);
        }

        private void Body(string text, float size = 12f)
        {
            _mgr.MakeText(_content.transform, text, size, _mgr.Style.Text);
        }
    }
}
