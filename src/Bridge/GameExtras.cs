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
        /// <summary>Секунд до автоматического отлёта корабля; -1 если неизвестно/не на луне.</summary>
        public static int SecondsToEndOfDay()
        {
            try
            {
                var tod = TimeOfDay.Instance;
                var sor = StartOfRound.Instance;
                if (tod == null || sor == null || !sor.shipHasLanded) return -1;
                if (tod.globalTimeAtEndOfDay <= 0f) return -1;

                // нормализованное время суток -> сколько ещё «игровых» секунд до конца дня
                float left = (tod.shipLeaveAutomaticallyTime > 0f
                                ? tod.shipLeaveAutomaticallyTime
                                : tod.globalTimeAtEndOfDay) - tod.currentDayTime;
                if (left <= 0f) return 0;
                // currentDayTime идёт в «ускоренном» времени — переводим в реальные секунды
                float speed = Mathf.Max(0.0001f, tod.globalTimeSpeedMultiplier);
                int sec = Mathf.RoundToInt(left / speed);
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
        private static MethodInfo _bcmeScrapValueMul;
        private static Type _wtType;

        /// <summary>Суммарный множитель стоимости лута (1.0 = без изменений).</summary>
        public static float LootMultiplier()
        {
            float total = 1f;
            try
            {
                if (!_multSearched)
                {
                    _multSearched = true;
                    // BCME: статический метод множителя стоимости лута
                    var mgr = GameState.FindTypeFuzzy("BrutalCompanyMinus", new[] { "Manager" });
                    if (mgr != null)
                        _bcmeScrapValueMul = mgr.GetMethod("GetScrapValueMultiplier",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    _wtType = GameState.FindTypeFuzzy("WeatherTweaks", new[] { "Variables", "WeatherManager" });
                    Plugin.Log?.LogInfo($"[loot-mult] BCME={( _bcmeScrapValueMul != null ? "OK" : "нет")}, " +
                                        $"WeatherTweaks={(_wtType != null ? "OK" : "нет")}");
                }

                if (_bcmeScrapValueMul != null)
                {
                    try
                    {
                        var v = _bcmeScrapValueMul.Invoke(null, null);
                        if (v is float f && f > 0f) total += (f - 1f);
                    }
                    catch { }
                }

                // погодный множитель ищем среди статических полей/свойств WeatherTweaks
                if (_wtType != null)
                {
                    float wf = ReadStaticFloat(_wtType, "ScrapValueMultiplier", "scrapValueMultiplier",
                                                        "ScrapMultiplier", "scrapMultiplier");
                    if (wf > 0f) total += (wf - 1f);
                }
            }
            catch { }
            return Mathf.Clamp(total, 0f, 100f);
        }

        private static float ReadStaticFloat(Type t, params string[] names)
        {
            const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            foreach (var n in names)
            {
                try
                {
                    var f = t.GetField(n, F);
                    if (f != null && f.FieldType == typeof(float)) return (float)f.GetValue(null);
                    var p = t.GetProperty(n, F);
                    if (p != null && p.PropertyType == typeof(float)) return (float)p.GetValue(null);
                }
                catch { }
            }
            return -1f;
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
        public static bool DoorProbe(Vector3 playerPos, out Vector3 otherSide, out bool otherSideIsInside)
        {
            otherSide = Vector3.zero;
            otherSideIsInside = false;
            try
            {
                var doors = UnityEngine.Object.FindObjectsOfType<EntranceTeleport>();
                if (doors == null || doors.Length == 0) return false;

                // ближайшая к игроку дверь в пределах 6 м — «мы у неё стоим»
                EntranceTeleport near = null;
                float best = 6f * 6f;
                foreach (var d in doors)
                {
                    if (d == null) continue;
                    float sq = (d.transform.position - playerPos).sqrMagnitude;
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
                    return true;
                }
            }
            catch { }
            return false;
        }
    }
}
