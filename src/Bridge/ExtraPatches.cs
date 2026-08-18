using System.Collections.Generic;
using System.Reflection;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace LCBridgeOverlay
{
    /// <summary>
    /// 2.8 — считаем стоимость лута, проданного компании.
    /// Прилавок продаёт предметы через SellAndDisplayItemProfits; берём сумму
    /// со стойки в момент продажи.
    /// </summary>
    [HarmonyPatch(typeof(DepositItemsDesk), "SellAndDisplayItemProfits")]
    internal static class Patch_DepositItemsDesk_Sell
    {
        [HarmonyPrefix]
        public static void Prefix(int profit, int total)
        {
            try
            {
                // profit — сколько денег принесла партия (уже с учётом ставки компании)
                if (profit > 0) RunStats.AddSold(profit);
                else if (total > 0) RunStats.AddSold(total);
                Plugin.Log?.LogInfo($"[sold] продано на {(profit > 0 ? profit : total)} (всего за забег: {RunStats.SoldTotal})");
            }
            catch { }
        }
    }

    /// <summary>
    /// 2.13 — монстр получил урон: помечаем его, чтобы иконка коротко вспыхнула красным.
    ///
    /// ВАЖНО: HitEnemy — виртуальный метод, и 21 монстр в игре его ПЕРЕОПРЕДЕЛЯЕТ
    /// (Bracken, Thumper, Hoarder, Nutcracker, Masked и др.). Патч только на базовый
    /// EnemyAI.HitEnemy их не перехватывал — поэтому вспышка почти никогда не
    /// срабатывала. Патчим базовый метод И все переопределения.
    /// </summary>
    [HarmonyPatch]
    internal static class Patch_EnemyAI_HitEnemy
    {
        [HarmonyTargetMethods]
        internal static IEnumerable<MethodBase> Targets()
        {
            var list = new List<MethodBase>();
            const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic |
                                   BindingFlags.Instance | BindingFlags.DeclaredOnly;
            var baseM = AccessTools.Method(typeof(EnemyAI), "HitEnemy");
            if (baseM != null) list.Add(baseM);
            try
            {
                foreach (var t in typeof(EnemyAI).Assembly.GetTypes())
                {
                    if (t == null || !t.IsSubclassOf(typeof(EnemyAI))) continue;
                    MethodInfo m = null;
                    try { m = t.GetMethod("HitEnemy", F); } catch { }
                    if (m != null && !m.IsAbstract) list.Add(m);
                }
            }
            catch { }
            Plugin.Log?.LogInfo($"[hit] патчим HitEnemy: {list.Count} методов (база + переопределения).");
            return list;
        }

        [HarmonyPostfix]
        public static void Postfix(EnemyAI __instance, int force)
        {
            try
            {
                if (__instance == null || force <= 0) return;
                MonsterState.MarkHurt(__instance.GetInstanceID());

                // ПРЯМАЯ подсветка: коллектор и UI живут в одном процессе, поэтому не
                // ждём кругосветку через пакет моста (он идёт раз в секунду и метка
                // могла не дожить) — зажигаем иконку сразу.
                string nm = null;
                try { nm = EnemyResolver.Resolve(__instance); } catch { }
                if (!string.IsNullOrEmpty(nm))
                    OverlayManager.Instance?.FlashMonster(nm, __instance.isOutside);
            }
            catch { }
        }
    }

    /// <summary>
    /// 2.9 — сброс статистики забега ТОЛЬКО по рычагу.
    /// Раньше сброс был завязан на ResetShip/загрузку сейва, из-за чего после eject
    /// аналитика прошлого забега пропадала ещё до того, как игрок её увидел.
    /// </summary>
    [HarmonyPatch(typeof(StartMatchLever), "PullLever")]
    internal static class Patch_StartMatchLever_PullLever
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            try
            {
                RunSnapshot.OnLeverPulled();
                // 2.1/2.9: игрок решил играть дальше — панель возвращается к обычному
                // размеру, аналитика убирается с экрана.
                OverlayManager.Instance?.HideAnalytics();
            }
            catch { }
        }
    }

    /// <summary>
    /// Хранит «замороженную» аналитику прошлого забега (2.9).
    /// Живёт только в рамках запуска игры — на диск ничего не пишем.
    /// </summary>
    internal static class RunSnapshot
    {
        /// <summary>JSON прошлого забега (или null). Показывается, пока не дёрнут рычаг.</summary>
        public static string LastRunJson { get; private set; }
        public static bool ShowLastRun { get; private set; }

        /// <summary>Забег завершён (eject / банкротство) — запоминаем итоги.</summary>
        public static void CaptureRunEnd()
        {
            try
            {
                LastRunJson = RunStats.ToJson();
                ShowLastRun = true;
                Plugin.Log?.LogInfo("[run] забег завершён — аналитика сохранена до следующего вылета.");
            }
            catch { }
        }

        /// <summary>Дёрнули рычаг — начинаем новый забег, сохранённое прячем.</summary>
        public static void OnLeverPulled()
        {
            if (!ShowLastRun && LastRunJson == null) return;
            ShowLastRun = false;
            LastRunJson = null;
            RunStats.ResetRun();
            GameState.ResetDeaths();
            Plugin.Log?.LogInfo("[run] рычаг — статистика забега сброшена, начинаем новый.");
        }

        /// <summary>
        /// Создан/загружен ПУСТОЙ сейв — это не «продолжение просмотра итогов», а
        /// полностью новая игра: чистим всё, включая замороженную аналитику.
        /// Важно: ResetDeaths поднимает resetToken, по которому оверлей обнуляет таймер.
        /// </summary>
        public static void ResetForNewSave()
        {
            ShowLastRun = false;
            LastRunJson = null;
            GameState.ResetDeaths();   // + RunStats.ResetRun() + bump resetToken
            Plugin.Log?.LogInfo("[run] новый сейв — статистика и таймер обнулены.");
        }
    }
}
