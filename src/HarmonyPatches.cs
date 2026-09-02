using HarmonyLib;

namespace LCBridgeOverlay
{
    /// <summary>
    /// Патчи Harmony (HarmonyX из состава BepInEx). Сбор данных остаётся в
    /// LCBridge — здесь только мгновенная реакция на игровые события, чтобы
    /// не ждать следующего пакета моста (он приходит раз в секунду).
    /// Каждый патч безопасен: тело в try/catch не нужно, Postfix ничего
    /// не меняет в игре.
    /// </summary>
    [HarmonyPatch(typeof(GameNetworkManager), "Disconnect")]
    internal static class Patch_GameNetworkManager_Disconnect
    {
        // выход в главное меню / отключение от сервера → оверлей сразу
        // плавно исчезает и сбрасывает данные (по ТЗ п.11)
        private static void Postfix()
        {
            OverlayManager.Instance?.NotifyDisconnectedFromGame();
            OverlayNet.Reset();
        }
    }

    /// <summary>
    /// Перезаход в сейв: оверлей должен начинать «с чистого листа», иначе на новом
    /// заходе оставались метки квот, таймер и состояние прошлой сессии.
    /// StartOfRound.Start вызывается при каждой загрузке корабля.
    /// </summary>
    [HarmonyPatch(typeof(StartOfRound), "Start")]
    internal static class Patch_StartOfRound_Start
    {
        private static void Postfix()
        {
            try
            {
                OverlayNet.Register();
                MapImages.Forget();     // подхватить поправленные слои схемы
                MonsterState.Reset();
                ScanRegistry.Clear();
                StartOfRound_Patches.ForgetScanClearDay();
                BcmeClientEvents.Clear();
                OverlayManager.Instance?.NotifyEnteredSave();
                BridgeTicker.ForceImmediate();
            }
            catch { }
        }
    }
}
