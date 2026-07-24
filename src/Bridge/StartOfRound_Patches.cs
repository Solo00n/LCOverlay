using HarmonyLib;

namespace LCBridgeOverlay
{
    [HarmonyPatch(typeof(StartOfRound))]
    public static class StartOfRound_Patches
    {
        // Полный сброс компании (новый файл / банкротство) — обнуляем всю статистику забега.
        [HarmonyPatch("ResetShip")]
        [HarmonyPostfix]
        public static void OnResetShip()
        {
            GameState.ResetDeaths(); // обнуляет смерти + RunStats + bump resetToken
        }

        // Загрузка настроек сейва: если это НОВАЯ игра (день 0 / ничего не отработано) —
        // гарантированно сбрасываем всё, чтобы не копить статистику со старого файла.
        [HarmonyPatch("SetTimeAndPlanetToSavedSettings")]
        [HarmonyPostfix]
        public static void OnLoadSavedSettings()
        {
            try
            {
                var sor = StartOfRound.Instance;
                if (sor == null) return;
                bool freshSave =
                    (sor.gameStats != null && sor.gameStats.daysSpent <= 0) ||
                    (TimeOfDay.Instance != null && TimeOfDay.Instance.timesFulfilledQuota <= 0
                        && TimeOfDay.Instance.daysUntilDeadline >= 3);
                if (freshSave)
                    GameState.ResetDeaths();
            }
            catch { }
        }

        // Запуск дня (вылет) — статистику НЕ сбрасываем (копим за весь забег),
        // но чистим набор «умерших в этом раунде», чтобы повторная смерть игрока
        // в другой день засчитывалась как новая.
        [HarmonyPatch("StartGame")]
        [HarmonyPostfix]
        public static void OnStartGame()
        {
            GameState.OnNewRound();
        }

        // Перед стартом нового дня чистим пойманные на клиенте анонсы ивентов BCME —
        // ивенты нового дня придут после посадки через Net.DisplayTipClientRpc.
        [HarmonyPatch("StartGame")]
        [HarmonyPrefix]
        public static void BeforeStartGame()
        {
            BcmeClientEvents.Clear();
            MonsterState.Reset();   // состояния и кэш сканирования — на новый день
        }
    }
}
