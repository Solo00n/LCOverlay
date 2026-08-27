using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GameNetcodeStuff;
using UnityEngine;

namespace LCBridgeOverlay
{
    /// <summary>
    /// Сбор игрового состояния. Часть данных читается напрямую из игры,
    /// часть (ивент Brutal, погода WeatherTweaks) — через рефлексию, защищённо.
    /// </summary>
    public static class GameState
    {
        // ---- смерти за забег ----
        private static int _deaths = 0;
        private static readonly HashSet<int> _deadThisRound = new HashSet<int>();

        // токен сброса забега: увеличивается при ResetShip (новый сейв/банкротство),
        // оверлей по изменению этого числа сбрасывает таймер
        private static int _resetToken = 0;
        public static int GetResetToken() => _resetToken;

        public static void RegisterDeath(PlayerControllerB p, string killer = null)
        {
            // считаем смерть КАЖДОГО игрока, но защищаемся от повторных вызовов KillPlayer
            // для одного и того же игрока в рамках одного раунда (дня).
            bool counted = false;
            try
            {
                int id = (int)p.playerClientId;
                if (_deadThisRound.Add(id)) { _deaths++; counted = true; }
            }
            catch
            {
                _deaths++; counted = true; // не удалось получить id — считаем на всякий
            }

            // если это дубль-вызов на уже учтённого мертвеца — выходим, чтобы не задвоить квоту
            if (!counted) return;

            // ---- статистика забега: кто убивал ----
            try
            {
                if (!string.IsNullOrEmpty(killer))
                {
                    _killerCounts.TryGetValue(killer, out int c);
                    _killerCounts[killer] = c + 1;
                }
                // привязка смерти к текущему ивенту (для «самого смертоносного ивента»)
                string ev = GetBrutalEvent();
                if (!string.IsNullOrEmpty(ev) && ev != "—")
                {
                    _eventDeaths.TryGetValue(ev, out int ec);
                    _eventDeaths[ev] = ec + 1;
                }
                RunStats.OnDeath(killer);   // +1 к смертям ТЕКУЩЕЙ квоты
            }
            catch { }
        }

        // Вызывается при запуске нового дня (StartGame): чистим набор «уже умерших»,
        // чтобы тот же игрок, погибший в другой день той же квоты, считался снова.
        public static void OnNewRound()
        {
            _deadThisRound.Clear();
        }

        public static void ResetDeaths()
        {
            _deaths = 0;
            _deadThisRound.Clear();
            _resetToken++; // сигнал новой игры → оверлей сбросит таймер
            // обнуляем всю статистику забега
            _killerCounts.Clear();
            _eventDeaths.Clear();
            _monsterSeen.Clear();
            RunStats.ResetRun();
        }

        // Счётчик смертей. При TeamDeaths берём ОБЩЕЕ число смертей команды из
        // статистики игры (gameStats.deaths) — оно считается сервером и включает
        // смерти, которых локальный игрок не видел. Иначе — старый локальный подсчёт.
        // При DeathsOnlyOnLeave показываем «замороженное» значение, обновляя его
        // только при отлёте с луны.
        private static int _deathsShown;      // то, что реально отдаём наружу
        private static int _deathsPending;    // последнее известное значение
        private static bool _wasOnMoonDeaths;

        public static int GetDeaths()
        {
            try
            {
                int live = _deaths;
                if (ConfigSettings.TeamDeaths.Value)
                {
                    var sor = StartOfRound.Instance;
                    if (sor != null && sor.gameStats != null)
                        live = Mathf.Max(sor.gameStats.deaths, 0);
                }
                _deathsPending = live;

                if (!ConfigSettings.DeathsOnlyOnLeave.Value)
                {
                    _deathsShown = live;
                    return _deathsShown;
                }

                // режим «только при отлёте»: фиксируем значение в момент ухода с луны
                bool onMoon = GetOnMoon();
                if (_wasOnMoonDeaths && !onMoon) _deathsShown = _deathsPending;
                _wasOnMoonDeaths = onMoon;
                return _deathsShown;
            }
            catch { return _deaths; }
        }

        // ===================== СТАТИСТИКА ЗАБЕГА =====================
        // кто убивал игрока (имя монстра/причина -> число смертей)
        private static readonly Dictionary<string, int> _killerCounts = new Dictionary<string, int>();
        // какой ивент был активен в момент смертей (имя ивента -> число смертей)
        private static readonly Dictionary<string, int> _eventDeaths = new Dictionary<string, int>();
        // сколько раз монстр был «замечен» на уровне (накопление по тикам, имя -> счётчик)
        private static readonly Dictionary<string, int> _monsterSeen = new Dictionary<string, int>();

        // Вызывается тикером ~раз в секунду: копим, какие монстры встречаются (по присутствию на уровне).
        public static void TickStats()
        {
            try
            {
                // считаем уникальные типы, присутствующие сейчас (по 1 за тик на тип),
                // чтобы «частота встречаемости» = как долго монстр был на уровне
                var seenThisTick = new HashSet<string>();
                foreach (var ai in GetAllLiveEnemies())
                {
                    if (ai == null || ai.isEnemyDead) continue;
                    string name = EnemyResolver.Resolve(ai);   // уточняем (Toil-Head, Manti-Toil…)
                    if (name == null) continue;
                    seenThisTick.Add(name);
                }
                foreach (var n in seenThisTick)
                {
                    _monsterSeen.TryGetValue(n, out int c);
                    _monsterSeen[n] = c + 1;
                }
            }
            catch { }
        }

        private static string TopOf(Dictionary<string, int> dict)
        {
            string best = null; int bestC = 0;
            foreach (var kv in dict) if (kv.Value > bestC) { bestC = kv.Value; best = kv.Key; }
            return best;
        }

        // самый частый убийца (имя), или null
        public static string GetTopKiller() => TopOf(_killerCounts);
        // самый часто встречавшийся монстр (имя), или null
        public static string GetTopMonster() => TopOf(_monsterSeen);
        // самый «смертоносный» ивент (при каком было больше всего смертей), или null
        public static string GetDeadliestEvent() => TopOf(_eventDeaths);

        // ---- живые / всего игроков ----
        public static (int alive, int total) GetCrew()
        {
            try
            {
                var sor = StartOfRound.Instance;
                if (sor == null) return (0, 0);

                int total = 0, alive = 0;
                foreach (var p in sor.allPlayerScripts)
                {
                    if (p == null) continue;
                    // считаем только реально подключённых игроков
                    if (!p.isPlayerControlled && !p.isPlayerDead) continue;
                    total++;
                    if (!p.isPlayerDead) alive++;
                }
                // подчистим: если total получился 0, но мы в игре — хотя бы 1
                if (total == 0) total = 1;
                return (alive, total);
            }
            catch { return (0, 0); }
        }

        // ---- HP локального игрока ----
        public static int GetLocalHealth()
        {
            try
            {
                var p = GameNetworkManager.Instance?.localPlayerController;
                if (p == null) return 0;
                return p.health;
            }
            catch { return 0; }
        }

        // ====================================================================
        //  ИНТЕГРАЦИЯ с MonstersGordion (Solon): его монстры на луне компании
        //  (внутри здания на 71-Gordion) НЕ всегда попадают в
        //  RoundManager.SpawnedEnemies, поэтому дополнительно читаем его список
        //  CompanyMonsterSpawner.Instance._ownedEnemies через рефлексию.
        // ====================================================================
        private static Type _mgSpawnerType;
        private static bool _mgSearched;
        private static FieldInfo _mgOwnedField;
        private static PropertyInfo _mgInstanceProp;
        private static FieldInfo _mgInstanceField;

        private static void EnsureMonstersGordion()
        {
            if (_mgSearched) return;
            _mgSearched = true;
            _mgSpawnerType = FindTypeByFullName("MonstersGordion.CompanyMonsterSpawner")
                          ?? FindTypeFuzzy("MonstersGordion", new[] { "CompanyMonsterSpawner" });
            if (_mgSpawnerType != null)
            {
                const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
                _mgOwnedField = _mgSpawnerType.GetField("_ownedEnemies", F)
                             ?? _mgSpawnerType.GetField("ownedEnemies", F);
                _mgInstanceProp = _mgSpawnerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                _mgInstanceField = _mgSpawnerType.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
                Plugin.Log?.LogInfo($"[reflection] MonstersGordion={_mgSpawnerType.FullName}, _ownedEnemies={(_mgOwnedField != null ? "OK" : "НЕ НАЙДЕНО")}");
            }
            else Plugin.Log?.LogInfo("[reflection] MonstersGordion не найден (не установлен?)");
        }

        // монстры, заспавненные MonstersGordion (или пусто, если мод не стоит)
        public static List<EnemyAI> GetGordionEnemies()
        {
            var list = new List<EnemyAI>();
            try
            {
                EnsureMonstersGordion();
                if (_mgSpawnerType == null || _mgOwnedField == null) return list;
                object inst = _mgInstanceProp?.GetValue(null) ?? _mgInstanceField?.GetValue(null) ?? GetSingletonInstance(_mgSpawnerType);
                if (inst == null) return list;
                var owned = _mgOwnedField.GetValue(inst) as System.Collections.IEnumerable;
                if (owned == null) return list;
                foreach (var o in owned)
                    if (o is EnemyAI ai && ai != null && !ai.isEnemyDead) list.Add(ai);
            }
            catch (Exception e) { Plugin.Log?.LogDebug($"GetGordionEnemies fail: {e.Message}"); }
            return list;
        }

        // все живые монстры локации: RoundManager.SpawnedEnemies + монстры MonstersGordion, без дублей
        public static List<EnemyAI> GetAllLiveEnemies()
        {
            var list = new List<EnemyAI>();
            var seen = new HashSet<int>();
            try
            {
                var rm = RoundManager.Instance;
                if (rm != null && rm.SpawnedEnemies != null)
                    foreach (var ai in rm.SpawnedEnemies)
                        if (ai != null && !ai.isEnemyDead && seen.Add(ai.GetInstanceID())) list.Add(ai);
            }
            catch { }
            foreach (var ai in GetGordionEnemies())
                if (ai != null && !ai.isEnemyDead && seen.Add(ai.GetInstanceID())) list.Add(ai);
            return list;
        }

        // ---- монстры на локации: улица (outside) и комплекс (inside) ----
        // возвращает два списка имён с количеством, например ["Bracken x2", "Thumper"]
        public static (List<string> outside, List<string> inside) GetMonsters()
        {
            var outside = new List<string>();
            var inside = new List<string>();
            try
            {
                // считаем по имени отдельно для улицы и комплекса + ближайшую дистанцию
                var outCount = new Dictionary<string, int>();
                var inCount = new Dictionary<string, int>();
                var outDist = new Dictionary<string, float>();
                var inDist = new Dictionary<string, float>();

                // позиция локального игрока — для «чем ближе, тем менее прозрачно»
                Vector3 me = Vector3.zero; bool haveMe = false;
                try
                {
                    var lp = GameNetworkManager.Instance?.localPlayerController;
                    if (lp != null) { me = lp.transform.position; haveMe = true; }
                }
                catch { }

                // 2.3: если стоим у двери комплекса — «виртуальный радар» по ту сторону.
                // Монстры рядом с этой точкой попадают в оверлей, даже если мы их не видим.
                Vector3 probePos = Vector3.zero; bool haveProbe = false; bool probeInside = false;
                float probeR = 0f;
                if (haveMe && ConfigSettings.DoorRadar.Value)
                {
                    haveProbe = GameExtras.DoorProbe(me, out probePos, out probeInside);
                    probeR = Mathf.Clamp(ConfigSettings.DoorRadarRadius.Value, 5f, 60f);
                }

                foreach (var ai in GetAllLiveEnemies())
                {
                    if (ai == null) continue;
                    if (ai.isEnemyDead) continue;
                    // девочка-призрак видна только тому, кого она выбрала жертвой
                    if (!MonsterState.VisibleToLocal(ai)) continue;

                    string name = "Unknown";
                    try { var r = EnemyResolver.Resolve(ai); if (!string.IsNullOrEmpty(r)) name = r; }
                    catch { }
                    // суффиксы состояния: +Aggro/+Angry/+Adult/+Attack/+Ceiling/+Frozen/+Scanned
                    name += MonsterState.TokensFor(ai);

                    var dict = ai.isOutside ? outCount : inCount;
                    dict.TryGetValue(name, out int c);
                    dict[name] = c + 1;

                    if (haveMe)
                    {
                        float d = Vector3.Distance(me, ai.transform.position);
                        // монстр по ту сторону двери, в радиусе радара — считаем его
                        // «близким», чтобы он подсветился игроку у этой двери
                        if (haveProbe && ai.isOutside != probeInside)
                        {
                            float dp = Vector3.Distance(probePos, ai.transform.position);
                            if (dp <= probeR && dp < d) d = dp;
                        }
                        var dd = ai.isOutside ? outDist : inDist;
                        if (!dd.TryGetValue(name, out float md) || d < md) dd[name] = d;
                    }
                }

                // формат записи: "Name xN @<метры>" (дистанцию оверлей вырезает из
                // сигнатуры рейки, чтобы не пересобирать её каждую секунду)
                foreach (var kv in outCount) outside.Add(Fmt(kv.Key, kv.Value, outDist));
                foreach (var kv in inCount) inside.Add(Fmt(kv.Key, kv.Value, inDist));
                // стабильный порядок → payload не меняется каждую секунду из-за перестановки
                outside.Sort(StringComparer.Ordinal);
                inside.Sort(StringComparer.Ordinal);
            }
            catch (Exception e)
            {
                Plugin.Log?.LogDebug($"GetMonsters fail: {e.Message}");
            }
            return (outside, inside);
        }

        private static string Fmt(string name, int count, Dictionary<string, float> dist)
        {
            string s = count > 1 ? $"{name} x{count}" : name;
            if (dist != null && dist.TryGetValue(name, out float d))
                s += " @" + Mathf.RoundToInt(d);
            return s;
        }

        // ---- ловушки на локации: турели, мины, шипованные потолки ----
        // возвращает список вида ["Turret x2", "Landmine x4", "Spike Trap"].
        // Ловушки — НЕ EnemyAI, это отдельные объекты сцены, ищем их по типам.
        private static Type _grabTurret, _grabMine;
        private static bool _grabSearched;

        public static List<string> GetTraps()
        {
            var result = new List<string>();
            try
            {
                // собираем позиции по человекочитаемому имени (для количества + дистанции)
                var pos = new Dictionary<string, List<Vector3>>();
                void Add(string label, Vector3 p)
                {
                    if (!pos.TryGetValue(label, out var l)) { l = new List<Vector3>(); pos[label] = l; }
                    l.Add(p);
                }
                void CollectType<T>(string label) where T : Component
                {
                    try
                    {
                        var arr = UnityEngine.Object.FindObjectsOfType<T>();
                        if (arr != null) foreach (var o in arr) if (o != null) Add(label, o.transform.position);
                    }
                    catch { }
                }
                // BCME может УНИЧТОЖИТЬ обычную турель/мину и заменить их на
                // «переносные» (GrabbableTurret/GrabbableLandmine : GrabbableObject),
                // из-за чего они пропадали из оверлея. Ловим их по типу (рефлексией).
                void CollectGrabbable(Type t, string label)
                {
                    try
                    {
                        if (t == null) return;
                        var arr = UnityEngine.Object.FindObjectsOfType(t);
                        if (arr == null) return;
                        foreach (var o in arr)
                        {
                            var g = o as GrabbableObject;
                            if (g == null || g.isHeld || g.isHeldByEnemy) continue; // в руках — не ловушка
                            Add(label, g.transform.position);
                        }
                    }
                    catch { }
                }

                if (!_grabSearched)
                {
                    _grabSearched = true;
                    _grabTurret = FindTypeByFullName("BrutalCompanyMinus.Minus.MonoBehaviours.GrabbableTurret");
                    _grabMine = FindTypeByFullName("BrutalCompanyMinus.Minus.MonoBehaviours.GrabbableLandmine");
                }

                CollectType<Turret>("Turret");
                CollectType<Landmine>("Landmine");
                CollectType<SpikeRoofTrap>("Spike Trap");
                CollectGrabbable(_grabTurret, "Turret");
                CollectGrabbable(_grabMine, "Landmine");

                // позиция локального игрока — для «чем ближе, тем менее прозрачно»
                Vector3 me = Vector3.zero; bool haveMe = false;
                try { var lp = GameNetworkManager.Instance?.localPlayerController; if (lp != null) { me = lp.transform.position; haveMe = true; } }
                catch { }

                foreach (var kv in pos)
                {
                    int n = kv.Value.Count;
                    string s = n > 1 ? $"{kv.Key} x{n}" : kv.Key;
                    if (haveMe && n > 0)
                    {
                        float md = float.MaxValue;
                        foreach (var pp in kv.Value) { float d = Vector3.Distance(me, pp); if (d < md) md = d; }
                        if (md < float.MaxValue) s += " @" + Mathf.RoundToInt(md);
                    }
                    result.Add(s);
                }
            }
            catch (Exception e)
            {
                Plugin.Log?.LogDebug($"GetTraps fail: {e.Message}");
            }
            return result;
        }


        // при приземлении корабля и держим до следующего вылета.
        // (значение может догенериться не в первый кадр — ловим первое ненулевое)
        private static int _landedScrap;
        private static bool _wasLanded;
        private static bool _scrapLocked;
        private static float _scrapSettleUntil;   // до какого времени ещё «досматриваем» значение

        // Окно, в течение которого после посадки мы продолжаем принимать БОЛЬШЕЕ значение.
        // Раньше снимок «залипал» на первом же ненулевом числе, и лут, доспавненный
        // ивентами Brutal Company через пару секунд после высадки, в подсчёт не попадал.
        private const float ScrapSettleSeconds = 20f;

        public static int GetLevelScrap()
        {
            try
            {
                var rm = RoundManager.Instance;
                bool landed = GetOnMoon(); // true только на настоящей луне (не Gordion)

                if (landed)
                {
                    if (!_wasLanded)
                    {
                        _wasLanded = true; _scrapLocked = false; _landedScrap = 0;
                        _scrapSettleUntil = Time.unscaledTime + ScrapSettleSeconds;
                    }
                    if (rm != null && !_scrapLocked)
                    {
                        int v = (int)rm.totalScrapValueInLevel;
                        if (v > _landedScrap) _landedScrap = v;   // берём максимум за окно
                        // окно закончилось и что-то поймали — фиксируем
                        if (_landedScrap > 0 && Time.unscaledTime >= _scrapSettleUntil) _scrapLocked = true;
                    }
                }
                else
                {
                    // вылетели / на корабле — сбрасываем снимок
                    _wasLanded = false; _scrapLocked = false; _landedScrap = 0;
                }
                return _landedScrap;
            }
            catch { return _landedScrap; }
        }

        // ====================================================================
        //  ИВЕНТЫ Brutal Company — читаем выбранные на день:
        //  BrutalCompanyMinus.Minus.EventManager.currentEvents (static List<MEvent>)
        //  Это список ивентов текущего дня; чистится при заходе на луну.
        //  Поле Active ненадёжно (часто перекрыто static в самих ивентах),
        //  поэтому берём именно currentEvents и зовём .Name() у каждого.
        // ====================================================================
        private static Type _emType;          // EventManager
        private static FieldInfo _curEventsField; // EventManager.currentEvents
        private static bool _bcSearched;

        // кэш метода Name() по типу ивента — рефлексия только один раз на тип
        private static readonly Dictionary<Type, MethodInfo> _nameMethodCache = new Dictionary<Type, MethodInfo>();
        private static string GetEventName(object ev)
        {
            var t = ev.GetType();
            if (!_nameMethodCache.TryGetValue(t, out var m))
            {
                m = t.GetMethod("Name", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
                _nameMethodCache[t] = m;
            }
            if (m != null)
            {
                try { return m.Invoke(ev, null) as string; } catch { }
            }
            return ExtractName(ev);
        }

        public static string GetBrutalEvent()
        {
            try
            {
                if (!_bcSearched)
                {
                    _bcSearched = true;
                    _emType = FindTypeByFullName("BrutalCompanyMinus.Minus.EventManager")
                           ?? FindTypeFuzzy("BrutalCompany", new[] { "EventManager" });
                    if (_emType != null)
                    {
                        _curEventsField = _emType.GetField("currentEvents",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                        Plugin.Log?.LogInfo($"[reflection] BCMER EventManager={_emType.FullName}, currentEvents field={(_curEventsField != null ? "OK" : "НЕ НАЙДЕНО")}");
                    }
                    else
                    {
                        Plugin.Log?.LogInfo("[reflection] BCMER EventManager не найден (мод выключен?)");
                    }
                }
                var names = new List<string>();

                if (_curEventsField != null)
                {
                    var list = _curEventsField.GetValue(null) as System.Collections.IEnumerable;
                    if (list != null)
                    {
                        foreach (var ev in list)
                        {
                            if (ev == null) continue;
                            string nm = GetEventName(ev);
                            if (!string.IsNullOrEmpty(nm)) names.Add(nm);
                        }
                    }
                }

                // ТА ЖЕ ловушка, что была у плашки ивентов: currentEvents заполняется
                // только у ХОСТА, и у клиентов это поле пустое. Берём клиентский
                // источник (панель BCME, которая синхронизируется всем).
                if (names.Count == 0)
                {
                    foreach (var e in BcmeClientEvents.Get())
                        if (!string.IsNullOrEmpty(e.Name)) names.Add(e.Name);
                }

                if (names.Count == 0) return null;
                return string.Join(", ", names);
            }
            catch (Exception e)
            {
                Plugin.Log?.LogDebug($"GetBrutalEvent fail: {e.Message}");
                return null;
            }
        }

        // ====================================================================
        //  ПОГОДА WeatherTweaks — точно, по исходникам:
        //  WeatherTweaks.Variables.GetCurrentWeather() -> объект WeatherTweaksWeather
        //  у него поле/свойство Name = полная строка ("Eclipsed + Flooded").
        // ====================================================================
        private static Type _wtVarsType;
        private static MethodInfo _wtGetCurrent;
        private static bool _wtSearched;

        public static string GetWeatherTweaksWeather()
        {
            try
            {
                if (!_wtSearched)
                {
                    _wtSearched = true;
                    _wtVarsType = FindTypeByFullName("WeatherTweaks.Variables")
                               ?? FindTypeFuzzy("WeatherTweaks", new[] { "Variables" });
                    if (_wtVarsType != null)
                    {
                        _wtGetCurrent = _wtVarsType.GetMethod("GetCurrentWeather",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                            null, Type.EmptyTypes, null);
                        Plugin.Log?.LogInfo($"[reflection] WeatherTweaks Variables={_wtVarsType.FullName}, GetCurrentWeather={(_wtGetCurrent != null ? "OK" : "НЕ НАЙДЕНО")}");
                    }
                    else
                    {
                        Plugin.Log?.LogInfo("[reflection] WeatherTweaks Variables не найден (мод выключен?)");
                    }
                }
                if (_wtGetCurrent == null) return null;

                var weatherObj = _wtGetCurrent.Invoke(null, null);
                if (weatherObj == null) return null;

                // у WeatherTweaksWeather есть Name
                string nm = ExtractName(weatherObj);
                return string.IsNullOrEmpty(nm) ? null : nm;
            }
            catch (Exception e)
            {
                Plugin.Log?.LogDebug($"GetWeatherTweaks fail: {e.Message}");
                return null;
            }
        }

        // ---- базовая ванильная погода (как у StreamOverlays) — запасной вариант ----
        public static string GetVanillaWeather()
        {
            try
            {
                var sor = StartOfRound.Instance;
                if (sor == null || sor.currentLevel == null) return "None";
                return sor.currentLevel.currentWeather.ToString();
            }
            catch { return "None"; }
        }

        public static string GetMoonName()
        {
            try
            {
                var sor = StartOfRound.Instance;
                if (sor == null || sor.currentLevel == null) return "—";
                return sor.currentLevel.PlanetName;
            }
            catch { return "—"; }
        }

        // true, если корабль сел на луну — ВКЛЮЧАЯ луну компании (Gordion):
        // на компании тоже показываем монстров/интерьер/предметы (по просьбе).
        // ВАЖНО (2.5): shipHasLanded истинно и на луне компании (Gordion), и ложно на
        // орбите. Авто-таймер оверлея гейтится именно по этому флагу — поэтому он
        // корректно ИДЁТ на Gordion и СТОИТ на орбите. Не менять на проверку
        // «настоящей» луны, иначе таймер перестанет идти на Gordion.
        public static bool GetOnMoon()
        {
            try
            {
                var sor = StartOfRound.Instance;
                if (sor == null || sor.currentLevel == null) return false;
                return sor.shipHasLanded;
            }
            catch { return false; }
        }

        // true во время загрузочного экрана / полёта на луну
        public static bool GetLoading()
        {
            try
            {
                var sor = StartOfRound.Instance;
                if (sor == null) return false;
                return sor.travellingToNewLevel;
            }
            catch { return false; }
        }

        // true, если идёт смена (игрок в игре, не в главном меню)
        public static bool GetInGame()
        {
            try
            {
                var sor = StartOfRound.Instance;
                if (sor == null) return false;
                // главное меню — отдельная сцена, где Instance null; на смене/полёте эти флаги активны
                return sor.shipHasLanded || sor.travellingToNewLevel;
            }
            catch { return false; }
        }

        public static int GetDayCount()
        {
            try
            {
                // Сквозной номер дня за ВСЮ компанию (1-based), а не день внутри квоты.
                // gameStats.daysSpent растёт на каждый реальный игровой день и НЕ сбрасывается
                // между квотами — поэтому за 3 квоты по 3 дня получаем сквозные 1..9,
                // и дни разных квот не схлопываются в хронике.
                var sor = StartOfRound.Instance;
                if (sor != null && sor.gameStats != null)
                    return sor.gameStats.daysSpent + 1; // daysSpent 0-based → день 1 на первой высадке
                // запасной вариант, если gameStats недоступен
                var tod = TimeOfDay.Instance;
                if (tod == null) return 1;
                return tod.daysUntilDeadline >= 0 ? (3 - tod.daysUntilDeadline) : 1;
            }
            catch { return 1; }
        }

        // ---- доп. геттеры для RunStats (read-only, защищённо) ----

        // индекс текущей квоты (1,2,3...) — по числу выполненных квот
        public static int GetQuotaIndexSafe()
        {
            try
            {
                var tod = TimeOfDay.Instance;
                if (tod == null) return 1;
                return tod.timesFulfilledQuota + 1;
            }
            catch { return 1; }
        }

        // суммарная стоимость лута, лежащего на корабле (собранный)
        public static int GetShipScrapSafe()
        {
            try
            {
                int sum = 0;
                foreach (var go in UnityEngine.GameObject.FindGameObjectsWithTag("PhysicsProp"))
                {
                    var gi = go.GetComponent<GrabbableObject>();
                    if (gi == null || gi.itemProperties == null || !gi.itemProperties.isScrap) continue;
                    if (gi.isInShipRoom || gi.isInElevator) sum += gi.scrapValue;
                }
                return sum;
            }
            catch { return 0; }
        }

        // Возвращает скрап, ДОСТАВЛЕННЫЙ на корабль, как пары (instanceId -> ценность).
        // По уникальным id RunStats суммирует каждый предмет ОДИН раз → накопительный
        // «собрано за игру», который не падает после продажи компании.
        public static List<KeyValuePair<int,int>> GetShipScrapItems()
        {
            var list = new List<KeyValuePair<int,int>>();
            try
            {
                foreach (var go in UnityEngine.GameObject.FindGameObjectsWithTag("PhysicsProp"))
                {
                    var gi = go.GetComponent<GrabbableObject>();
                    if (gi == null || gi.itemProperties == null || !gi.itemProperties.isScrap) continue;
                    if (gi.isInShipRoom || gi.isInElevator)
                        list.Add(new KeyValuePair<int,int>(gi.GetInstanceID(), gi.scrapValue));
                }
            }
            catch { }
            return list;
        }

        // Живые монстры сейчас на уровне как пары (instanceId -> имя),
        // чтобы считать РЕАЛЬНОЕ число уникальных особей за забег.
        public static List<KeyValuePair<int,string>> GetMonsterInstances()
        {
            var list = new List<KeyValuePair<int,string>>();
            try
            {
                foreach (var ai in GetAllLiveEnemies())
                {
                    if (ai == null || ai.isEnemyDead) continue;
                    string name = EnemyResolver.Resolve(ai);
                    if (name == null) continue;
                    list.Add(new KeyValuePair<int,string>(ai.GetInstanceID(), name));
                }
            }
            catch { }
            return list;
        }

        // сырой список имён монстров (без дедупликации в "xN")
        public static (List<string> outside, List<string> inside) GetMonstersRaw()
        {
            var outside = new List<string>();
            var inside = new List<string>();
            try
            {
                foreach (var ai in GetAllLiveEnemies())
                {
                    if (ai == null || ai.isEnemyDead) continue;
                    string name = EnemyResolver.Resolve(ai);
                    if (name == null) continue;
                    (ai.isOutside ? outside : inside).Add(name);
                }
            }
            catch { }
            return (outside, inside);
        }

        // ====================================================================
        //  ДОП. ДАННЫЕ для внутриигрового оверлея (v1.2+): квота, интерьер,
        //  ульи, счётчики предметов, Old Bird, «игрок на корабле».
        //  Всё read-only и защищённо — как и остальной сбор.
        // ====================================================================

        // квота: цель (profitQuota) и уже сдано компании (quotaFulfilled)
        public static (int quota, int fulfilled) GetQuotaProgress()
        {
            try
            {
                var tod = TimeOfDay.Instance;
                if (tod == null) return (0, 0);
                return ((int)tod.profitQuota, (int)tod.quotaFulfilled);
            }
            catch { return (0, 0); }
        }

        // дней до дедлайна (-1 если неизвестно)
        public static int GetDaysLeft()
        {
            try
            {
                var tod = TimeOfDay.Instance;
                return tod != null ? (int)tod.daysUntilDeadline : -1;
            }
            catch { return -1; }
        }

        // тип интерьера: Facility / Mansion / Mineshaft, для кастомных (LethalLevelLoader)
        // отдаём очищенное имя DungeonFlow — LLL регистрирует свои интерьеры тем же путём
        public static string GetInterior()
        {
            try
            {
                if (!GetOnMoon()) return null;
                var rm = RoundManager.Instance;
                var flow = rm?.dungeonGenerator?.Generator?.DungeonFlow;
                if (flow == null) return null;
                string n = flow.name ?? "";
                if (n.IndexOf("Level1", StringComparison.OrdinalIgnoreCase) >= 0) return "Facility";
                if (n.IndexOf("Level2", StringComparison.OrdinalIgnoreCase) >= 0) return "Mansion";
                if (n.IndexOf("Level3", StringComparison.OrdinalIgnoreCase) >= 0) return "Mineshaft";
                // кастомный интерьер: убираем служебный суффикс "Flow" и обрезаем пробелы
                n = n.Replace("DungeonFlow", "").Replace("Flow", "").Replace("flow", "").Trim();
                return string.IsNullOrEmpty(n) ? null : n;
            }
            catch { return null; }
        }

        // разбор лута на луне ОДНИМ проходом: ульи / предметы в комплексе / предметы снаружи.
        // Ульи в счётчики предметов не входят (по ТЗ). Корабль не считаем.
        public static (int hives, int inside, int outside) GetLootBreakdown()
        {
            int hives = 0, inside = 0, outside = 0;
            try
            {
                if (!GetOnMoon()) return (0, 0, 0);
                foreach (var go in UnityEngine.GameObject.FindGameObjectsWithTag("PhysicsProp"))
                {
                    var gi = go.GetComponent<GrabbableObject>();
                    if (gi == null || gi.itemProperties == null || !gi.itemProperties.isScrap) continue;
                    if (gi.isInShipRoom || gi.isInElevator) continue; // уже на корабле — не считаем
                    if (gi.isHeld || gi.isHeldByEnemy) continue;      // в руках — не считаем
                    string nm = gi.itemProperties.itemName ?? "";
                    if (nm.IndexOf("hive", StringComparison.OrdinalIgnoreCase) >= 0) { hives++; continue; }
                    if (gi.isInFactory) inside++; else outside++;
                }
                // диагностика (не чаще ~5с и при изменении) — чтобы понять классификацию
                if (UnityEngine.Time.time - _lastLootLog > 5f && (inside != _lastInside || outside != _lastOutside))
                {
                    _lastLootLog = UnityEngine.Time.time; _lastInside = inside; _lastOutside = outside;
                    Plugin.Log?.LogInfo($"[loot] внутри={inside} снаружи={outside} ульи={hives}");
                }
            }
            catch { }
            return (hives, inside, outside);
        }
        private static float _lastLootLog; private static int _lastInside = -1, _lastOutside = -1;

        // есть ли на луне живой Old Bird (внутреннее имя RadMech)
        public static bool GetOldBird()
        {
            try
            {
                foreach (var ai in GetAllLiveEnemies())
                {
                    if (ai == null || ai.isEnemyDead) continue;
                    string n = ai.enemyType != null ? (ai.enemyType.enemyName ?? "") : "";
                    if (n.IndexOf("RadMech", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("Old Bird", StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }
            catch { }
            return false;
        }

        // находится ли ЛОКАЛЬНЫЙ игрок физически на корабле (для видимости оверлея)
        public static bool GetOnShip()
        {
            try
            {
                var lp = GameNetworkManager.Instance?.localPlayerController;
                return lp != null && lp.isInHangarShipRoom;
            }
            catch { return false; }
        }

        // находится ли локальный игрок внутри комплекса
        public static bool GetInsideFactorySafe()
        {
            try
            {
                var lp = StartOfRound.Instance?.localPlayerController;
                return lp != null && lp.isInsideFactory;
            }
            catch { return false; }
        }

        // ====================================================================
        //  ВСПОМОГАТЕЛЬНЫЕ методы рефлексии
        // ====================================================================
        internal static Type FindTypeByFullName(string fullName)
        {
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type t = null;
                    try { t = asm.GetType(fullName, false); } catch { }
                    if (t != null) return t;
                }
            }
            catch { }
            return null;
        }

        internal static Type FindTypeFuzzy(string asmNameContains, string[] typeNameCandidates)
        {
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    string an = asm.GetName().Name ?? "";
                    if (an.IndexOf(asmNameContains, StringComparison.OrdinalIgnoreCase) < 0) continue;

                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch (ReflectionTypeLoadException rtle) { types = rtle.Types.Where(t => t != null).ToArray(); }

                    // сначала ищем точные кандидаты
                    foreach (var cand in typeNameCandidates)
                    {
                        var hit = types.FirstOrDefault(t =>
                            string.Equals(t.Name, cand, StringComparison.OrdinalIgnoreCase));
                        if (hit != null)
                        {
                            Plugin.Log?.LogInfo($"[reflection] нашёл тип {hit.FullName} в {an}");
                            return hit;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log?.LogDebug($"FindTypeFuzzy fail: {e.Message}");
            }
            return null;
        }

        private static object ReadStaticMember(Type t, string name)
        {
            const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
            try
            {
                var field = t.GetField(name, F);
                if (field != null)
                {
                    // статическое поле читаем без инстанса
                    if (field.IsStatic) return field.GetValue(null);
                    // инстансное — пробуем найти синглтон .Instance
                    var inst = GetSingletonInstance(t);
                    if (inst != null) return field.GetValue(inst);
                }
                var prop = t.GetProperty(name, F);
                if (prop != null && prop.CanRead)
                {
                    if (prop.GetGetMethod(true)?.IsStatic == true) return prop.GetValue(null);
                    var inst = GetSingletonInstance(t);
                    if (inst != null) return prop.GetValue(inst);
                }
            }
            catch { }
            return null;
        }

        private static object GetSingletonInstance(Type t)
        {
            try
            {
                var instProp = t.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
                            ?? t.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
                if (instProp != null) return instProp.GetValue(null);

                var instField = t.GetField("Instance", BindingFlags.Public | BindingFlags.Static)
                             ?? t.GetField("instance", BindingFlags.Public | BindingFlags.Static);
                if (instField != null) return instField.GetValue(null);
            }
            catch { }
            return null;
        }

        private static string ExtractName(object val)
        {
            if (val == null) return null;
            try
            {
                // если это строка
                if (val is string s) return s;

                // если это enum
                if (val.GetType().IsEnum) return val.ToString();

                // если у объекта есть .Name / .name
                var t = val.GetType();
                var nameProp = t.GetProperty("Name") ?? t.GetProperty("name");
                if (nameProp != null)
                {
                    var nv = nameProp.GetValue(val) as string;
                    if (!string.IsNullOrEmpty(nv)) return nv;
                }
                var nameField = t.GetField("Name") ?? t.GetField("name");
                if (nameField != null)
                {
                    var nv = nameField.GetValue(val) as string;
                    if (!string.IsNullOrEmpty(nv)) return nv;
                }

                // последний шанс — ToString, если он осмысленный (не имя типа)
                var str = val.ToString();
                if (!string.IsNullOrEmpty(str) && str != t.FullName && str != t.Name)
                    return str;
            }
            catch { }
            return null;
        }
    }
}
