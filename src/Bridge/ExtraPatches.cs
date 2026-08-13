using System.Collections.Generic;
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
    /// </summary>
    [HarmonyPatch(typeof(EnemyAI), "HitEnemy")]
    internal static class Patch_EnemyAI_HitEnemy
    {
        [HarmonyPostfix]
        public static void Postfix(EnemyAI __instance, int force)
        {
            try
            {
                if (__instance == null || force <= 0) return;
                MonsterState.MarkHurt(__instance.GetInstanceID());
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
            try { RunSnapshot.OnLeverPulled(); }
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
    }
}
