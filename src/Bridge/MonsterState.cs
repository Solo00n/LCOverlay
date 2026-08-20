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
                        // 0 — ходит закрытый, 1 — заводится, 2 — вылез и гонится
                        if (state >= 2) tok += "+Angry";
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
                    if (IsScannedByBestiary(ai) || _scanned.Contains(ai.GetInstanceID())) tok += "+Scanned";
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

        private static bool GetBool(object o, params string[] names)
        {
            var v = GetObj(o, names);
            return v is bool && (bool)v;
        }
    }
}
