using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LCBridgeOverlay
{
    /// <summary>
    /// Расширенная аналитика забега для турнирной страницы.
    /// Копится в течение всех 3 квот, сбрасывается на новой игре (ResetRun).
    /// Сериализуется в компактный JSON-объект (поле "run" в payload моста).
    ///
    /// Всё построено поверх read-only геттеров GameState — ничего не пишем в игру.
    /// </summary>
    public static class RunStats
    {
        // ---- по-квотные данные ----
        public class QuotaSlice
        {
            public int index;          // 1..3
            public int scrapStart;     // лут на корабле в начале квоты (для дельты)
            public int scrapEnd;       // лут на конец
            public int seconds;        // секунд в этой квоте
            public int deaths;         // смертей в этой квоте
        }
        private static readonly List<QuotaSlice> _quotas = new List<QuotaSlice>();
        private static QuotaSlice _curQuota;
        private static int _lastQuotaIndex = -1;

        // ---- луны: имя -> {визиты, заработок, секунды} ----
        private class MoonStat { public int visits; public int profit; public int seconds; }
        private static readonly Dictionary<string, MoonStat> _moons = new Dictionary<string, MoonStat>();
        private static string _curMoon;
        private static int _moonScrapStart;

        // ---- монстры: имя -> "монстро-секунды" присутствия ----
        private static readonly Dictionary<string, int> _monsterTime = new Dictionary<string, int>();
        // монстры: имя -> РЕАЛЬНОЕ число уникальных особей (по instanceID)
        private static readonly Dictionary<string, int> _monsterCount = new Dictionary<string, int>();
        // уже учтённые особи (instanceID), чтобы не считать одну дважды
        private static readonly HashSet<int> _seenMonsterIds = new HashSet<int>();
        // пик одновременно на уровне
        private static int _peakMonsters;

        // ---- накопительный лут: каждый доставленный предмет учитывается ОДИН раз ----
        // instanceID предмета -> его ценность; сумма = собрано за игру (не падает после продаж)
        private static readonly Dictionary<int,int> _collectedScrap = new Dictionary<int,int>();

        // ---- время внутри комплекса / снаружи (секунды) ----
        private static int _secInside, _secOutside;

        // ---- таймлайн событий: список строк "day|type|text" ----
        private static readonly List<string> _timeline = new List<string>();
        private static int _lastDayLogged = -1;
        private static string _lastEventLogged;

        // общий таймер забега (секунды), считаем сами по тикам в игре
        private static int _runSeconds;

        public static void ResetRun()
        {
            _quotas.Clear(); _curQuota = null; _lastQuotaIndex = -1;
            _moons.Clear(); _curMoon = null; _moonScrapStart = 0;
            _monsterTime.Clear(); _peakMonsters = 0;
            _monsterCount.Clear(); _seenMonsterIds.Clear();
            _collectedScrap.Clear();
            _secInside = 0; _secOutside = 0;
            _timeline.Clear(); _lastDayLogged = -1; _lastEventLogged = null;
            _runSeconds = 0;
        }

        // регистрируется из RegisterDeath, чтобы таймлайн знал контекст смерти
        public static void OnDeath(string killer)
        {
            try
            {
                int day = GameState.GetDayCount();
                string moon = GameState.GetMoonName();
                string who = string.IsNullOrEmpty(killer) ? "?" : killer;
                _timeline.Add($"{day}|death|{who}@{moon}");
                if (_curQuota != null) _curQuota.deaths++;
            }
            catch { }
        }

        /// <summary>Вызывается тикером ~раз в секунду.</summary>
        public static void Tick()
        {
            try
            {
                bool inGame = GameState.GetInGame();
                if (!inGame) return;
                _runSeconds++;

                int day = GameState.GetDayCount();
                int qi = GameState.GetQuotaIndexSafe(); // 1..3+
                bool onMoon = GameState.GetOnMoon();
                string moon = GameState.GetMoonName();
                string ev = GameState.GetBrutalEvent();

                // --- НАКОПИТЕЛЬНЫЙ лут: учитываем каждый доставленный предмет один раз ---
                // (после продажи предметы исчезают, но в _collectedScrap остаются → сумма не падает)
                foreach (var kv in GameState.GetShipScrapItems())
                    if (!_collectedScrap.ContainsKey(kv.Key)) _collectedScrap[kv.Key] = kv.Value;
                int cumScrap = 0; foreach (var v in _collectedScrap.Values) cumScrap += v;

                // --- по квотам (money = прирост НАКОПЛЕННОГО за период квоты) ---
                if (qi != _lastQuotaIndex)
                {
                    if (_curQuota != null) { _curQuota.scrapEnd = cumScrap; _quotas.Add(_curQuota); }
                    _curQuota = new QuotaSlice { index = qi, scrapStart = cumScrap, scrapEnd = cumScrap, seconds = 0, deaths = 0 };
                    _lastQuotaIndex = qi;
                }
                if (_curQuota != null) { _curQuota.seconds++; _curQuota.scrapEnd = cumScrap; }

                // --- луны (profit = накопительный прирост за все визиты на эту луну) ---
                if (onMoon && !string.IsNullOrEmpty(moon))
                {
                    if (_curMoon != moon)
                    {
                        _curMoon = moon; _moonScrapStart = cumScrap;
                        if (!_moons.ContainsKey(moon)) _moons[moon] = new MoonStat();
                        _moons[moon].visits++;
                    }
                    var ms = _moons[moon];
                    ms.seconds++;
                    // накопительно: добавляем прирост с прошлого тика, не теряя при продаже
                    int gained = cumScrap - _moonScrapStart;
                    if (gained > 0) { ms.profit += gained; _moonScrapStart = cumScrap; }
                }
                else { _curMoon = null; }

                // --- монстры: время присутствия + РЕАЛЬНОЕ число уникальных особей ---
                var monNow = GameState.GetMonsterInstances();
                foreach (var kv in monNow)
                {
                    Add(_monsterTime, kv.Value);                 // монстро-секунды (для длительности)
                    if (_seenMonsterIds.Add(kv.Key))             // новая особь → +1 к счётчику
                        Add(_monsterCount, kv.Value);
                }
                int totalNow = monNow.Count;
                if (totalNow > _peakMonsters) _peakMonsters = totalNow;
                if (onMoon)
                {
                    if (GameState.GetInsideFactorySafe()) _secInside++;
                    else _secOutside++;
                }

                // --- таймлайн: запись дня привязана к РЕАЛЬНОЙ посадке на луну ---
                // фиксируем день только когда корабль на луне и её название известно.
                // так мгновенный отлёт (без высадки) не создаёт пустую запись,
                // а сквозной day (daysSpent) гарантирует уникальность каждого дня 1..9.
                if (onMoon && !string.IsNullOrEmpty(moon) && moon != "—" && day > 0 && day != _lastDayLogged)
                {
                    _lastDayLogged = day;
                    _timeline.Add($"{day}|day|{moon}");
                }
                if (!string.IsNullOrEmpty(ev) && ev != "—" && ev != _lastEventLogged)
                {
                    _lastEventLogged = ev;
                    _timeline.Add($"{day}|event|{ev}");
                }
            }
            catch { }
        }

        private static void Add(Dictionary<string,int> d, string k)
        {
            if (string.IsNullOrEmpty(k)) return;
            d.TryGetValue(k, out int c); d[k] = c + 1;
        }

        // ---- сериализация в JSON ----
        public static string ToJson()
        {
            try
            {
                var sb = new StringBuilder();
                sb.Append('{');

                // квоты
                sb.Append("\"quotas\":[");
                var all = new List<QuotaSlice>(_quotas);
                if (_curQuota != null) all.Add(_curQuota);
                for (int i = 0; i < all.Count; i++)
                {
                    var q = all[i];
                    if (i > 0) sb.Append(',');
                    int earned = Math.Max(0, q.scrapEnd - q.scrapStart);
                    sb.Append('{')
                      .Append("\"i\":").Append(q.index).Append(',')
                      .Append("\"money\":").Append(earned).Append(',')
                      .Append("\"sec\":").Append(q.seconds).Append(',')
                      .Append("\"deaths\":").Append(q.deaths)
                      .Append('}');
                }
                sb.Append("],");

                // луны
                sb.Append("\"moons\":[");
                int mi = 0;
                foreach (var kv in _moons.OrderByDescending(x => x.Value.profit))
                {
                    if (mi++ > 0) sb.Append(',');
                    sb.Append('{')
                      .Append("\"name\":").Append(JsonStr(kv.Key)).Append(',')
                      .Append("\"visits\":").Append(kv.Value.visits).Append(',')
                      .Append("\"profit\":").Append(kv.Value.profit).Append(',')
                      .Append("\"sec\":").Append(kv.Value.seconds)
                      .Append('}');
                }
                sb.Append("],");

                // монстры (топ по РЕАЛЬНОМУ числу особей; sec оставляем для длительности)
                sb.Append("\"monsters\":[");
                int ci = 0;
                foreach (var kv in _monsterCount.OrderByDescending(x => x.Value).Take(20))
                {
                    if (ci++ > 0) sb.Append(',');
                    _monsterTime.TryGetValue(kv.Key, out int secs);
                    sb.Append('{')
                      .Append("\"name\":").Append(JsonStr(kv.Key)).Append(',')
                      .Append("\"count\":").Append(kv.Value).Append(',')
                      .Append("\"sec\":").Append(secs)
                      .Append('}');
                }
                sb.Append("],");

                // пик / внутри / снаружи / общее время
                sb.Append("\"peak\":").Append(_peakMonsters).Append(',');
                sb.Append("\"inside\":").Append(_secInside).Append(',');
                sb.Append("\"outside\":").Append(_secOutside).Append(',');
                sb.Append("\"runSec\":").Append(_runSeconds).Append(',');

                // таймлайн
                sb.Append("\"timeline\":[");
                for (int i = 0; i < _timeline.Count && i < 120; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(JsonStr(_timeline[i]));
                }
                sb.Append(']');

                sb.Append('}');
                return sb.ToString();
            }
            catch { return "{}"; }
        }

        private static string JsonStr(string s)
        {
            if (s == null) return "\"\"";
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c == '\n' || c == '\r') sb.Append(' ');
                else sb.Append(c);
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
