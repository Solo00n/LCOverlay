using HarmonyLib;

namespace LCBridgeOverlay
{
    [HarmonyPatch(typeof(StartOfRound))]
    public static class StartOfRound_Patches
    {
        private static int _lastScanClearDay = int.MinValue;

        /// <summary>Перезаход в сейв: номер дня может повториться, забываем отметку.</summary>
        public static void ForgetScanClearDay() => _lastScanClearDay = int.MinValue;

        // Конец забега (eject / банкротство / новый файл). 2.9: НЕ стираем статистику
        // сразу — иначе после eject игрок не успевает увидеть итоги. Замораживаем
        // аналитику прошлого забега и показываем её до тех пор, пока не дёрнут рычаг
        // (см. RunSnapshot / Patch_StartMatchLever_PullLever).
        [HarmonyPatch("ResetShip")]
        [HarmonyPostfix]
        public static void OnResetShip()
        {
            RunSnapshot.CaptureRunEnd();
        }

        // Загрузка настроек сейва: если это НОВАЯ игра (день 0 / ничего не отработано) —
        // тоже фиксируем итоги прошлого забега, а обнуление произойдёт по рычагу.
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
                // НОВЫЙ сейв — полный сброс (а не заморозка итогов): иначе таймер
                // оверлея оставался со значениями прошлой игры.
                if (freshSave)
                    RunSnapshot.ResetForNewSave();
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
            // Новый день: если включено, забываем все сканы — монстров и ловушки
            // придётся просвечивать заново, и знать заранее, что на луне, нельзя.
            try
            {
                // StartGame прилетает ДВАЖДЫ за высадку (в логе две записи подряд), и
                // второй раз мог бы стереть уже просканированное. Чистим строго один
                // раз на день — по номеру дня.
                if (Gate.ResetScansDaily)
                {
                    var sor = StartOfRound.Instance;
                    int day = (sor != null && sor.gameStats != null) ? sor.gameStats.daysSpent : -1;
                    if (day != _lastScanClearDay)
                    {
                        _lastScanClearDay = day;
                        ScanRegistry.Clear();
                        Plugin.Log?.LogInfo($"[scan] новый день ({day}) — сканы сброшены.");
                    }
                }
            }
            catch { }

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

        // Посадка: сразу собираем и рассылаем полный пакет (не ждём 1-сек тик) —
        // чтобы оверлей показал всё мгновенно, а не поэтапно.
        [HarmonyPatch("OnShipLandedMiscEvents")]
        [HarmonyPostfix]
        public static void OnLanded()
        {
            BridgeTicker.ForceImmediate();
        }
    }

    // Уровень (интерьер) полностью сгенерирован у клиента — тип комплекса и монстры
    // уже известны: форсируем мгновенную отправку состояния.
    [HarmonyPatch(typeof(RoundManager), "FinishGeneratingNewLevelClientRpc")]
    internal static class Patch_RoundManager_LevelReady
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            BridgeTicker.ForceImmediate();
        }
    }
}
