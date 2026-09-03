using System;
using System.Collections.Generic;
using System.Reflection;
using GameNetcodeStuff;
using UnityEngine;

namespace LCBridgeOverlay
{
    /// <summary>
    /// Определяет СОСТОЯНИЕ каждого живого врага (зол / трансформирован / на потолке /
    /// застыл и т.п.) и отдаёт его как суффиксы-токены к имени монстра — в том же
    /// стиле, что уже используется для "+Turret"/"+Slayer" (см. EnemyResolver).
    ///
    /// Токены: +Aggro +Angry +Adult +Attack +Ceiling +Frozen +Scanned
    ///
    /// ВАЖНО:
    ///  - всё через рефлексию по именам полей: у разных версий игры/модов поля
    ///    отличаются, и жёсткая ссылка сломала бы мод. Ничего не бросает наружу;
    ///  - пересчёт по таймеру (~0.5 с), а не каждый кадр (требование по нагрузке);
    ///  - результат кэшируется по instanceID врага.
    /// </summary>
    internal static class MonsterState
    {
        private const float Interval = 0.5f;
        private static float _next;

        private static readonly Dictionary<int, string> _tokens = new Dictionary<int, string>();
        // враг -> игрок, которого он «преследует» (для девочки-призрака)
        private static readonly Dictionary<int, int> _hauntTarget = new Dictionary<int, int>();
        // отсканированные враги (кэш, требование 4.3)
        private static readonly HashSet<int> _scanned = new HashSet<int>();

        private static bool _loggedOnce;

        /// <summary>Сбросить на новый день/раунд.</summary>
        public static void Reset()
        {
            _tokens.Clear();
            _hauntTarget.Clear();
            _scanned.Clear();
            _scanIdCache.Clear();
            _hurt.Clear();
            _deviant.Clear();
            _windMax.Clear();
            _terminal = null;
        }

        // 2.13: недавно получившие урон (id -> момент времени).
        // ВАЖНО: состояния пересчитываются из тикера моста, то есть раз в СЕКУНДУ.
        // При коротком удержании (0.6 с) метка успевала истечь до того, как её
        // прочитают, и вспышка не срабатывала вообще. Держим дольше секунды и
        // дополнительно просим тикер отправить пакет немедленно.
        private const float HurtHold = 1.6f;
        private static readonly Dictionary<int, float> _hurt = new Dictionary<int, float>();

        /// <summary>Враг получил урон (зовётся из патча EnemyAI.HitEnemy).</summary>
        public static void MarkHurt(int instanceId)
        {
            try
            {
                _hurt[instanceId] = Time.unscaledTime;
                _next = 0f;                      // пробить собственный интервал пересчёта
                BridgeTicker.ForceImmediate();   // показать вспышку сразу, не ждать тик
            }
            catch { }
        }

        // Бестиарий игры: Terminal.scannedEnemyIDs хранит creatureScanID всех
        // существ, которых игрок уже отсканировал. Это надёжнее ловли узла скана.
        private static Terminal _terminal;
        private static readonly Dictionary<int, int> _scanIdCache = new Dictionary<int, int>();

        private static bool IsScannedByBestiary(EnemyAI ai)
        {
            try
            {
                if (_terminal == null) _terminal = UnityEngine.Object.FindObjectOfType<Terminal>();
                if (_terminal == null || _terminal.scannedEnemyIDs == null) return false;
                int id = ai.GetInstanceID();
                int scanId;
                if (!_scanIdCache.TryGetValue(id, out scanId))
                {
                    scanId = -1;
                    var node = ai.GetComponentInChildren<ScanNodeProperties>(true);
                    if (node != null) scanId = node.creatureScanID;
                    _scanIdCache[id] = scanId;
                }
                return scanId >= 0 && _terminal.scannedEnemyIDs.Contains(scanId);
            }
            catch { return false; }
        }

        // ---- опрос сканера ----
        private static System.Reflection.FieldInfo _scanNodesField;
        private static bool _scanFieldSearched;
        private static int _scanDiag;

        // GoodItemScan полностью заменяет ванильное сканирование
        private static bool _gisSearched;
        private static System.Reflection.FieldInfo _gisScannerField, _gisActiveNodes;
        private static System.Reflection.PropertyInfo _gisNodeProp;

        /// <summary>
        /// Что игрок просветил сканером ПРЯМО СЕЙЧАС.
        ///
        /// История граблей, чтобы не наступить снова:
        ///  1) патч на HUDManager.AssignNodeToUIElement не срабатывал — до него не
        ///     доходит управление;
        ///  2) чтение HUDManager.scanNodes падало: поле приватное, а публицирован
        ///     только эталон для компиляции. Теперь через рефлексию;
        ///  3) но и этого мало: мод GoodItemScan ВЫЧИЩАЕТ ванильные scanNodes и
        ///     scanElements и ведёт свой список. Поэтому спрашиваем оба источника.
        /// </summary>
        /// <summary>
        /// Опрос сканера. Зовётся каждый кадр, но реально работает 10 раз в секунду:
        /// узел скана живёт секунды, так что этого с запасом, а рефлексию каждый
        /// кадр гонять незачем.
        /// </summary>
        private static float _pollNext;
        public static void PollScannerNow()
        {
            if (Time.unscaledTime < _pollNext) return;
            _pollNext = Time.unscaledTime + 0.1f;
            PollScanner();
        }

        private static void PollScanner()
        {
            var nodes = new List<ScanNodeProperties>();
            CollectVanilla(nodes);
            CollectGoodItemScan(nodes);
            if (nodes.Count == 0) return;

            if (_scanDiag < 3)
            {
                _scanDiag++;
                Plugin.Log?.LogInfo($"[scan] сканер показывает узлов: {nodes.Count}");
            }

            foreach (var node in nodes)
            {
                if (node == null) continue;

                var ai = node.GetComponentInParent<EnemyAI>();
                if (ai != null)
                {
                    if (_scanned.Add(ai.GetInstanceID()))
                    {
                        ScanRegistry.MarkLocal(ai);
                        // и в память по планетам: на этой луне такой вид уже встречался
                        try { SeenRegistry.Remember(ai.enemyType != null ? ai.enemyType.enemyName : null); } catch { }
                        Plugin.Log?.LogInfo($"[scan] отсканирован {ai.GetType().Name} (\"{node.headerText}\").");
                    }
                    continue;
                }

                // ловушки: в режиме скана они тоже открываются сканером
                Component trap = node.GetComponentInParent<Turret>();
                if (trap == null) trap = node.GetComponentInParent<Landmine>();
                if (trap == null) trap = node.GetComponentInParent<SpikeRoofTrap>();
                if (trap != null && !ScanRegistry.HasFor(trap))
                {
                    ScanRegistry.MarkLocal(trap);
                    Plugin.Log?.LogInfo($"[scan] отсканирована ловушка {trap.GetType().Name}.");
                }
            }
        }

        /// <summary>Ванильный сканер: HUDManager.scanNodes (приватное поле).</summary>
        private static void CollectVanilla(List<ScanNodeProperties> outp)
        {
            try
            {
                var hud = HUDManager.Instance;
                if (hud == null) return;

                if (!_scanFieldSearched)
                {
                    _scanFieldSearched = true;
                    _scanNodesField = typeof(HUDManager).GetField("scanNodes",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.Instance);
                    Plugin.Log?.LogInfo($"[scan] ванильное поле scanNodes: {(_scanNodesField != null ? "найдено" : "НЕ НАЙДЕНО")}");
                }
                if (_scanNodesField == null) return;

                var dict = _scanNodesField.GetValue(hud) as System.Collections.IDictionary;
                if (dict == null || dict.Count == 0) return;
                foreach (var v in dict.Values) if (v is ScanNodeProperties n) outp.Add(n);
            }
            catch (Exception e)
            {
                if (_scanDiag < 6) { _scanDiag++; Plugin.Log?.LogWarning($"[scan] ванильный сканер: {e.GetType().Name}: {e.Message}"); }
            }
        }

        /// <summary>
        /// GoodItemScan: GoodItemScan.scanner.activeNodes — набор ScannedNode,
        /// у каждого свойство ScanNodeProperties. Мягкая зависимость, только рефлексия.
        /// </summary>
        private static void CollectGoodItemScan(List<ScanNodeProperties> outp)
        {
            try
            {
                if (!_gisSearched)
                {
                    _gisSearched = true;
                    const System.Reflection.BindingFlags SF =
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Static;
                    const System.Reflection.BindingFlags IF =
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance;

                    var plug = GameState.FindTypeByFullName("GoodItemScan.GoodItemScan")
                            ?? GameState.FindTypeFuzzy("GoodItemScan", new[] { "GoodItemScan" });
                    if (plug != null) _gisScannerField = plug.GetField("scanner", SF);

                    var scannerType = GameState.FindTypeByFullName("GoodItemScan.Scanner")
                                   ?? GameState.FindTypeFuzzy("GoodItemScan", new[] { "Scanner" });
                    if (scannerType != null) _gisActiveNodes = scannerType.GetField("activeNodes", IF);

                    var nodeType = GameState.FindTypeByFullName("GoodItemScan.ScannedNode")
                                ?? GameState.FindTypeFuzzy("GoodItemScan", new[] { "ScannedNode" });
                    if (nodeType != null) _gisNodeProp = nodeType.GetProperty("ScanNodeProperties", IF);

                    if (_gisScannerField != null)
                        Plugin.Log?.LogInfo($"[scan] GoodItemScan найден: scanner={(_gisScannerField != null ? "OK" : "нет")}, " +
                                            $"activeNodes={(_gisActiveNodes != null ? "OK" : "нет")}, " +
                                            $"ScanNodeProperties={(_gisNodeProp != null ? "OK" : "нет")}");
                }

                if (_gisScannerField == null || _gisActiveNodes == null || _gisNodeProp == null) return;

                var scanner = _gisScannerField.GetValue(null);
                if (scanner == null) return;
                var set = _gisActiveNodes.GetValue(scanner) as System.Collections.IEnumerable;
                if (set == null) return;

                foreach (var sn in set)
                {
                    if (sn == null) continue;
                    if (_gisNodeProp.GetValue(sn) is ScanNodeProperties n) outp.Add(n);
                }
            }
            catch (Exception e)
            {
                if (_scanDiag < 6) { _scanDiag++; Plugin.Log?.LogWarning($"[scan] GoodItemScan: {e.GetType().Name}: {e.Message}"); }
            }
        }

        /// <summary>Отметить врага отсканированным (зовётся из патча сканера).</summary>
        public static void MarkScanned(int instanceId) { _scanned.Add(instanceId); }
        public static bool IsScanned(int instanceId) { return _scanned.Contains(instanceId); }

        /// <summary>Суффикс состояния для врага ("" если нечего добавить).</summary>
        public static string TokensFor(EnemyAI ai)
        {
            if (ai == null) return "";
            string t;
            return _tokens.TryGetValue(ai.GetInstanceID(), out t) ? t : "";
        }

        /// <summary>
        /// Девочка-призрак видна только своей жертве. true — показывать этого врага
        /// локальному игроку.
        /// </summary>
        public static bool VisibleToLocal(EnemyAI ai)
        {
            try
            {
                if (ai == null) return true;
                int id = ai.GetInstanceID();
                int target;
                if (!_hauntTarget.TryGetValue(id, out target)) return true; // не «преследователь»
                var local = GameNetworkManager.Instance != null ? GameNetworkManager.Instance.localPlayerController : null;
                if (local == null) return false;
                return target == local.GetInstanceID();
            }
            catch { return true; }
        }

        /// <summary>Пересчёт состояний (вызывается из BridgeTicker раз в секунду,
        /// но не чаще Interval).</summary>
        public static void Tick(List<EnemyAI> enemies)
        {
            try
            {
                if (Time.unscaledTime < _next) return;
                _next = Time.unscaledTime + Interval;

                if (enemies == null) return;

                _tokens.Clear();
                _hauntTarget.Clear();

                foreach (var ai in enemies)
                {
                    if (ai == null || ai.isEnemyDead) continue;
                    string type = ai.GetType().Name ?? "";
                    string tok = "";

                    int state = 0;
                    try { state = ai.currentBehaviourStateIndex; } catch { }

                    // ---- по типам ----
                    if (type.IndexOf("Hoarder", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // ТОЛЬКО реально злой. Раньше брали currentBehaviourStateIndex > 0,
                        // но состояние 1 у жука — «несу вещь в гнездо», а не агрессия,
                        // из-за чего злая иконка появлялась почти всегда.
                        if (GetBool(ai, "isAngry") || GetBool(ai, "inChase")) tok += "+Aggro";
                    }
                    else if (type.IndexOf("Jester", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // 0 — ходит закрытый, 1 — заводится (крутит ручку), 2 — вылез и гонится
                        int jid = ai.GetInstanceID();
                        if (state >= 2)
                        {
                            tok += "+Angry";
                            _windMax.Remove(jid);
                        }
                        else if (state == 1)
                        {
                            // popUpTimer тикает ВНИЗ до хлопка. Стартовое значение
                            // рандомное, поэтому запоминаем максимум, который увидели,
                            // и считаем от него долю завода 0..1.
                            float cur = GetFloat(ai, "popUpTimer");
                            if (!_windMax.TryGetValue(jid, out float max) || cur > max)
                            { max = cur; _windMax[jid] = max; }
                            float prog = max > 0.01f ? Mathf.Clamp01(1f - cur / max) : 0f;
                            // уровень 0..9 — чем ближе к хлопку, тем сильнее тряска
                            tok += "+w" + Mathf.Clamp(Mathf.RoundToInt(prog * 9f), 0, 9);
                        }
                        else _windMax.Remove(jid);
                    }
                    else if (type.IndexOf("CaveDweller", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             type.IndexOf("Maneater", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // у CaveDwellerAI нет флага «взрослый» — есть отдельный объект
                        // модели взрослого: активен => трансформация произошла
                        bool adult = false;
                        var cont = GetObj(ai, "adultContainer") as GameObject;
                        if (cont != null) adult = cont.activeSelf;
                        if (!adult) adult = GetBool(ai, "adultMode", "grownUp", "isAdult");
                        if (adult) tok += "+Adult";
                    }
                    else if (type.IndexOf("Nutcracker", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // целится / развернул корпус на игрока — это и есть «атакует»
                        if (GetBool(ai, "aimingGun") || GetBool(ai, "isInspecting") ||
                            GetBool(ai, "torsoTurning")) tok += "+Attack";
                    }
                    else if (type.IndexOf("Centipede", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             type.IndexOf("SnareFlea", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        bool ceiling = GetBool(ai, "clingingToCeiling", "onCeiling", "hangingOnCeiling");
                        if (ceiling) tok += "+Ceiling";
                    }
                    else if (type.IndexOf("SpringMan", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             type.IndexOf("Coil", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // застыл = на него кто-то смотрит (хватает одного игрока)
                        bool stopped = GetBool(ai, "hasStopped", "stoppingMovement");
                        if (!stopped) stopped = AnyPlayerLookingAt(ai.transform);
                        if (stopped) tok += "+Frozen";
                    }
                    else if (type.IndexOf("DressGirl", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             type.IndexOf("GhostGirl", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var pl = GetObj(ai, "hauntingPlayer", "targetPlayer", "hauntedPlayer") as PlayerControllerB;
                        if (pl != null) _hauntTarget[ai.GetInstanceID()] = pl.GetInstanceID();
                    }

                    // турель НА монстре (ToilHead / MantiToil): если она стреляет —
                    // оверлей рисует от этой иконки трассеры
                    if (IsTurretFiring(ai)) tok += "+Firing";

                    // девиант (мод DeviantEnemies) — иконка перевернётся вверх ногами
                    if (IsDeviant(ai)) tok += "+Deviant";

                    // недавно получил урон — иконка вспыхнет красным (2.13)
                    if (ConfigSettings.DamageFlash.Value &&
                        _hurt.TryGetValue(ai.GetInstanceID(), out float ht) &&
                        Time.unscaledTime - ht <= HurtHold) tok += "+Hurt";

                    // отсканирован: по бестиарию игры ИЛИ пойман нашим патчем узла скана
                    // скан засчитан, если: его видел бестиарий игры (кроме режима
                    // «забывать каждый день» — там прошлые открытия не в счёт),
                    // либо это наш скан, либо кто-то из лобби уже отсканировал
                    bool byBestiary = !Gate.ResetScansDaily && IsScannedByBestiary(ai);
                    // память по планетам: этот вид тут уже видели раньше
                    bool byMemory = false;
                    try { byMemory = SeenRegistry.Knows(ai.enemyType != null ? ai.enemyType.enemyName : null); } catch { }
                    if (byBestiary || byMemory || _scanned.Contains(ai.GetInstanceID()) || ScanRegistry.HasFor(ai))
                        tok += "+Scanned";
                    if (tok.Length > 0) _tokens[ai.GetInstanceID()] = tok;
                }

                if (!_loggedOnce && _tokens.Count > 0)
                {
                    _loggedOnce = true;
                    var sb = new System.Text.StringBuilder();
                    foreach (var kv in _tokens) { sb.Append(kv.Value).Append(' '); }
                    Plugin.Log?.LogInfo("[states] первые состояния: " + sb);
                }
            }
            catch (Exception e) { Plugin.Log?.LogDebug("MonsterState.Tick: " + e.Message); }
        }

        // ---------- девиант (мод DeviantEnemies) ----------
        // Мод вешает на объект врага свой компонент-маркер. Ищем его ПО ИМЕНИ ТИПА,
        // без ссылки на сборку мода: нет мода — просто никогда не находим.
        //
        // ВАЖНО: кэшируем ТОЛЬКО положительный результат. Маркер появляется не в тот
        // же кадр, что и сам враг (хост прокатывает роль при спавне, клиенту флаг
        // приходит по сети), поэтому запомненное «не девиант» намертво отключало бы
        // переворот иконки. Девиантом враг, наоборот, перестать быть не может.
        private static readonly HashSet<int> _deviant = new HashSet<int>();

        // джестер: стартовое значение popUpTimer, чтобы посчитать долю завода
        private static readonly Dictionary<int, float> _windMax = new Dictionary<int, float>();

        private static bool IsDeviant(EnemyAI ai)
        {
            try
            {
                int id = ai.GetInstanceID();
                if (_deviant.Contains(id)) return true;

                // маркер живёт на объекте врага; на всякий случай смотрим и детей
                var comps = ai.GetComponentsInChildren<Component>(true);
                if (comps == null) return false;
                foreach (var c in comps)
                {
                    if (c == null) continue;
                    if (!string.Equals(c.GetType().Name, "DeviantMarker", StringComparison.Ordinal)) continue;
                    _deviant.Add(id);
                    Plugin.Log?.LogInfo($"[deviant] {ai.GetType().Name} помечен как девиант — иконка перевёрнута.");
                    BridgeTicker.ForceImmediate();   // показать переворот сразу
                    return true;
                }
            }
            catch { }
            return false;
        }

        // ---------- турель на монстре стреляет? ----------
        // Компонент турели ищем по ИМЕНИ типа (ToilHead может ставить свой),
        // режим читаем рефлексией — без жёсткой ссылки на игру/мод.
        private static bool IsTurretFiring(EnemyAI ai)
        {
            try
            {
                var comps = ai.GetComponentsInChildren<Component>(true);
                if (comps == null) return false;
                foreach (var c in comps)
                {
                    if (c == null) continue;
                    if (c.GetType().Name.IndexOf("Turret", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    var mode = GetObj(c, "turretMode", "mode", "currentMode");
                    if (mode == null) continue;
                    string s = mode.ToString();
                    if (s.IndexOf("Fir", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        s.IndexOf("Berserk", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                }
            }
            catch { }
            return false;
        }

        // ---------- взгляд на коила ----------
        private static bool AnyPlayerLookingAt(Transform t)
        {
            try
            {
                if (t == null) return false;
                var sor = StartOfRound.Instance;
                if (sor == null || sor.allPlayerScripts == null) return false;
                foreach (var p in sor.allPlayerScripts)
                {
                    if (p == null || !p.isPlayerControlled || p.isPlayerDead) continue;
                    var cam = p.gameplayCamera;
                    if (cam == null) continue;
                    Vector3 dir = t.position + Vector3.up * 1.2f - cam.transform.position;
                    float dist = dir.magnitude;
                    if (dist > 40f) continue;
                    if (Vector3.Dot(cam.transform.forward, dir.normalized) > 0.86f) return true;
                }
            }
            catch { }
            return false;
        }

        // ---------- мелкая рефлексия с кэшем ----------
        private static readonly Dictionary<string, MemberInfo> _members = new Dictionary<string, MemberInfo>();
        private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        private static MemberInfo FindMember(Type t, string[] names)
        {
            string key = t.FullName + "|" + string.Join(",", names);
            MemberInfo mi;
            if (_members.TryGetValue(key, out mi)) return mi;
            foreach (var n in names)
            {
                var f = t.GetField(n, F); if (f != null) { _members[key] = f; return f; }
                var p = t.GetProperty(n, F); if (p != null && p.CanRead) { _members[key] = p; return p; }
            }
            _members[key] = null;
            return null;
        }

        private static object GetObj(object o, params string[] names)
        {
            try
            {
                if (o == null) return null;
                var mi = FindMember(o.GetType(), names);
                var f = mi as FieldInfo; if (f != null) return f.GetValue(o);
                var p = mi as PropertyInfo; if (p != null) return p.GetValue(o);
            }
            catch { }
            return null;
        }

        private static float GetFloat(object o, params string[] names)
        {
            var v = GetObj(o, names);
            return v is float f ? f : 0f;
        }

        private static bool GetBool(object o, params string[] names)
        {
            var v = GetObj(o, names);
            return v is bool && (bool)v;
        }
    }
}
