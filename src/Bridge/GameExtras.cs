using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace LCBridgeOverlay
{
    /// <summary>
    /// Дополнительные данные состояния игры для оверлея v1.5:
    ///  - активные игровые окна (подсказки / сдача квоты / конец дня) и реклама магазина;
    ///  - отсчёт до конца дня;
    ///  - наличие аппарата («лампы») в комплексе;
    ///  - суммарный множитель стоимости лута (погода + ивенты);
    ///  - «виртуальный радар» за дверью комплекса.
    ///
    /// Всё через рефлексию/безопасные проверки: нет мода или поле переименовали —
    /// фича молча выключается, оверлей продолжает работать.
    /// </summary>
    internal static class GameExtras
    {
        // ---------- 2.4 игровые всплывающие окна ----------
        /// <summary>Идёт ли сейчас игровое окно, ради которого стоит убрать оверлей.</summary>
        public static bool PopupActive()
        {
            try
            {
                var hud = HUDManager.Instance;
                if (hud == null) return false;

                // экран статистики конца дня / сдачи квоты
                if (IsAnimatorShowing(hud.endgameStatsAnimator, "display", "displayStats", "showStats")) return true;
                // глобальное уведомление (крупная надпись по центру)
                if (IsAnimatorShowing(hud.globalNotificationAnimator, "display", "displayNotification")) return true;
                // обычная подсказка — пока не истёк её таймер
                float tip = GetFloat(hud, "displayTipTextTimer");
                if (tip > 0.05f) return true;
            }
            catch { }
            return false;
        }

        // ---------- 2.6 реклама магазина ----------
        /// <summary>Идёт ли сейчас реклама (оповещение о скидках).</summary>
        public static bool StoreAdActive()
        {
            try
            {
                var hud = HUDManager.Instance;
                if (hud == null) return false;
                // корутина показа рекламы живёт ровно на время показа
                if (GetObj(hud, "displayAdCoroutine") != null) return true;
                if (IsAnimatorShowing(hud.advertAnimator, "display", "displayAd", "showAd")) return true;
            }
            catch { }
            return false;
        }

        // ---------- 2.12 отсчёт до конца дня ----------
        /// <summary>
        /// Реальных секунд до конца дня (автоотлёта); -1 если неизвестно/не на луне.
        ///
        /// ВАЖНО про единицы: currentDayTime считается в «дневных» единицах и растёт
        /// со скоростью globalTimeSpeedMultiplier, а shipLeaveAutomaticallyTime —
        /// НОРМАЛИЗОВАННЫЙ порог (0..1). Раньше мы вычитали одно из другого напрямую,
        /// получали отрицательное число и всегда возвращали 0 — из-за этого отсчёт
        /// висел на нуле и, как следствие, показывался постоянно.
        /// </summary>
        public static int SecondsToEndOfDay()
        {
            try
            {
                var tod = TimeOfDay.Instance;
                var sor = StartOfRound.Instance;
                if (tod == null || sor == null || !sor.shipHasLanded) return -1;

                float total = tod.totalTime;
                if (total <= 0f) return -1;

                // доля прошедшего дня 0..1
                float norm = tod.normalizedTimeOfDay;
                if (norm < 0f) norm = Mathf.Clamp01(tod.currentDayTime / total);

                // порог автоотлёта: если поле нормализовано (0..1] — берём его,
                // иначе считаем концом дня единицу
                float endNorm = 1f;
                float sl = tod.shipLeaveAutomaticallyTime;
                if (sl > 0f && sl <= 1f) endNorm = sl;

                float leftNorm = endNorm - norm;
                if (leftNorm <= 0f) return 0;

                // сколько РЕАЛЬНЫХ секунд длится весь день
                float speed = Mathf.Max(0.0001f, tod.globalTimeSpeedMultiplier);
                float dayRealSeconds = total / speed;

                int sec = Mathf.RoundToInt(leftNorm * dayRealSeconds);
                return Mathf.Clamp(sec, 0, 24 * 3600);
            }
            catch { return -1; }
        }

        // ---------- 2.14 аппарат («лампа») в комплексе ----------
        /// <summary>true — аппарат ещё на месте (не вынесли из комплекса).</summary>
        public static bool ApparatusInside()
        {
            try
            {
                var arr = UnityEngine.Object.FindObjectsOfType<LungProp>();
                if (arr == null || arr.Length == 0) return false;
                foreach (var lp in arr)
                {
                    if (lp == null) continue;
                    // ещё запитан (не выдернут) ИЛИ физически всё ещё в комплексе и не в руках
                    if (lp.isLungPowered) return true;
                    var g = lp as GrabbableObject;
                    if (g != null && g.isInFactory && !g.isHeld) return true;
                }
            }
            catch { }
            return false;
        }

        // ---------- 2.11 суммарный множитель стоимости лута ----------
        private static bool _multSearched;
        private static FieldInfo _bcmeMulField;
        private static MethodInfo _wrGetCurrent;      // WeatherManager.GetCurrentWeather(level)
        private static PropertyInfo _wrScrapEnabled;  // Settings.ScrapMultipliers
        private static PropertyInfo _weatherScrapProp;// Weather.ScrapValueMultiplier

        /// <summary>
        /// Множитель стоимости лута от ТЕКУЩЕЙ погоды (1 — нет вклада).
        /// Учитывается только если сам WeatherRegistry включил множители.
        /// </summary>
        private static float WeatherScrapMultiplier()
        {
            try
            {
                if (_wrGetCurrent == null) return -1f;

                // мод может отключить множители — тогда погода на цену не влияет
                if (_wrScrapEnabled != null)
                {
                    var on = _wrScrapEnabled.GetValue(null);
                    if (on is bool b && !b) return -1f;
                }

                var sor = StartOfRound.Instance;
                var level = sor != null ? sor.currentLevel : null;
                if (level == null) return -1f;

                var weather = _wrGetCurrent.Invoke(null, new object[] { level });
                if (weather == null) return -1f;

                if (_weatherScrapProp == null || _weatherScrapProp.DeclaringType != weather.GetType())
                    _weatherScrapProp = weather.GetType().GetProperty("ScrapValueMultiplier",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (_weatherScrapProp == null) return -1f;

                var v = _weatherScrapProp.GetValue(weather);
                return v is float f ? f : -1f;
            }
            catch { return -1f; }
        }

        /// <summary>Суммарный множитель стоимости лута (1.0 = без изменений).</summary>
        public static float LootMultiplier()
        {
            float total = 1f;
            try
            {
                if (!_multSearched)
                {
                    _multSearched = true;
                    // BCME хранит текущий множитель стоимости лута в СТАТИЧЕСКОМ ПОЛЕ
                    // Manager.scrapValueMultiplier (метод GetScrapValueMultiplier есть
                    // только у экземпляра LevelProperties — раньше мы искали не там).
                    _bcmeMulField = FindStaticFloatFieldInAssembly("BrutalCompany",
                        new[] { "Manager" }, new[] { "scrapValueMultiplier" });
                    // Погодный множитель живёт в WeatherRegistry: это СВОЙСТВО ЭКЗЕМПЛЯРА
                    // Weather.ScrapValueMultiplier (а не статическое поле, как я искал
                    // раньше). Текущую погоду отдаёт WeatherManager.GetCurrentWeather(level).
                    var wm = GameState.FindTypeByFullName("WeatherRegistry.WeatherManager")
                          ?? GameState.FindTypeFuzzy("WeatherRegistry", new[] { "WeatherManager" });
                    if (wm != null)
                        _wrGetCurrent = wm.GetMethod("GetCurrentWeather",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                            null, new[] { typeof(SelectableLevel) }, null);

                    // включён ли учёт множителей в самом WeatherRegistry
                    var settings = GameState.FindTypeFuzzy("WeatherRegistry", new[] { "Settings" });
                    if (settings != null)
                        _wrScrapEnabled = settings.GetProperty("ScrapMultipliers",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

                    Plugin.Log?.LogInfo($"[loot-mult] BCME-поле={(_bcmeMulField != null ? _bcmeMulField.DeclaringType?.Name + "." + _bcmeMulField.Name : "не найдено")}, " +
                                        $"WeatherRegistry.GetCurrentWeather={(_wrGetCurrent != null ? "OK" : "не найден")}");
                }

                if (_bcmeMulField != null)
                {
                    try
                    {
                        var v = _bcmeMulField.GetValue(null);
                        float f = v is float ff ? ff : -1f;
                        if (f > 0f) total += (f - 1f);
                    }
                    catch { }
                }

                // множитель ТЕКУЩЕЙ погоды (WeatherRegistry/WeatherTweaks)
                float wf = WeatherScrapMultiplier();
                if (wf > 0f) total += (wf - 1f);
            }
            catch { }
            return Mathf.Clamp(total, 0f, 100f);
        }

        /// <summary>Ищет СТАТИЧЕСКОЕ float-поле в указанных классах сборки мода.</summary>
        private static FieldInfo FindStaticFloatFieldInAssembly(string asmContains, string[] typeNames, string[] fieldNames)
        {
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    string an = asm.GetName().Name ?? "";
                    if (an.IndexOf(asmContains, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch (ReflectionTypeLoadException e)
                    {
                        var list = new List<Type>();
                        foreach (var t in e.Types) if (t != null) list.Add(t);
                        types = list.ToArray();
                    }
                    foreach (var t in types)
                    {
                        bool nameOk = false;
                        foreach (var tn in typeNames)
                            if (string.Equals(t.Name, tn, StringComparison.OrdinalIgnoreCase)) { nameOk = true; break; }
                        if (!nameOk) continue;
                        foreach (var fn in fieldNames)
                        {
                            try
                            {
                                var f = t.GetField(fn, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                                if (f != null && f.FieldType == typeof(float)) return f;
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        // ---------- мелкие помощники рефлексии ----------
        private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        /// <summary>Аниматор в одном из «показывающих» состояний (по bool-параметрам).</summary>
        private static bool IsAnimatorShowing(Animator anim, params string[] boolParams)
        {
            try
            {
                if (anim == null || !anim.gameObject.activeInHierarchy) return false;
                foreach (var p in anim.parameters)
                {
                    if (p.type != AnimatorControllerParameterType.Bool) continue;
                    foreach (var want in boolParams)
                        if (string.Equals(p.name, want, StringComparison.OrdinalIgnoreCase) && anim.GetBool(p.nameHash))
                            return true;
                }
            }
            catch { }
            return false;
        }

        private static object GetObj(object o, string name)
        {
            try
            {
                if (o == null) return null;
                var f = o.GetType().GetField(name, F);
                if (f != null) return f.GetValue(o);
                var p = o.GetType().GetProperty(name, F);
                if (p != null && p.CanRead) return p.GetValue(o);
            }
            catch { }
            return null;
        }

        private static float GetFloat(object o, string name)
        {
            var v = GetObj(o, name);
            return v is float f ? f : 0f;
        }

        // ---------- 2.3 «виртуальный радар» за дверью ----------
        /// <summary>
        /// Если игрок стоит у двери комплекса — возвращает позицию ПО ТУ СТОРОНУ этой
        /// двери (точку выхода парной двери) и её тип. Иначе hasDoor = false.
        /// </summary>
        /// <summary>На каком расстоянии от двери считаем, что игрок «стоит у неё».</summary>
        private const float NearDoor = 14f;
        private static int _lastDoorId = int.MinValue;
        private static bool _lastDoorSide;

        public static bool DoorProbe(Vector3 playerPos, out Vector3 otherSide, out bool otherSideIsInside)
        {
            otherSide = Vector3.zero;
            otherSideIsInside = false;
            try
            {
                var doors = UnityEngine.Object.FindObjectsOfType<EntranceTeleport>();
                if (doors == null || doors.Length == 0) return false;

                // ближайшая к игроку дверь. Раньше порог был 6 м и «у двери» почти
                // никогда не срабатывало — берём заметно шире.
                EntranceTeleport near = null;
                float best = NearDoor * NearDoor;
                foreach (var d in doors)
                {
                    if (d == null) continue;
                    // позиция двери — по её точке входа, если она есть
                    Vector3 dp = d.entrancePoint != null ? d.entrancePoint.position : d.transform.position;
                    float sq = (dp - playerPos).sqrMagnitude;
                    if (sq < best) { best = sq; near = d; }
                }
                if (near == null) return false;

                // парная дверь: тот же entranceId, но другая сторона
                foreach (var d in doors)
                {
                    if (d == null || d == near) continue;
                    if (d.entranceId != near.entranceId) continue;
                    if (d.isEntranceToBuilding == near.isEntranceToBuilding) continue;
                    otherSide = d.entrancePoint != null ? d.entrancePoint.position : d.transform.position;
                    // если ЭТА дверь ведёт внутрь здания, то по ту сторону — улица, и наоборот
                    otherSideIsInside = near.isEntranceToBuilding;

                    if (_lastDoorId != near.entranceId || _lastDoorSide != near.isEntranceToBuilding)
                    {
                        _lastDoorId = near.entranceId;
                        _lastDoorSide = near.isEntranceToBuilding;
                        Plugin.Log?.LogInfo($"[door] у двери id={near.entranceId} " +
                            $"({(near.isEntranceToBuilding ? "вход внутрь" : "выход наружу")}), " +
                            $"смотрим за неё в радиусе {ConfigSettings.DoorRadarRadius.Value:0} м.");
                    }
                    return true;
                }
            }
            catch { }
            return false;
        }
    }
}
