using HarmonyLib;

namespace LCBridgeOverlay
{
    /// <summary>
    /// Отметка отсканированных врагов — для настройки RequireScanToShow
    /// (монстр появляется в оверлее только после того, как игрок его просканировал).
    ///
    /// HUDManager.AssignNodeToUIElement вызывается, когда узел сканирования реально
    /// повесили на элемент HUD, т.е. игрок этот объект просканировал. Берём у узла
    /// родительский EnemyAI и запоминаем его id (кэш живёт до конца дня).
    /// </summary>
    [HarmonyPatch(typeof(HUDManager), "AssignNodeToUIElement")]
    internal static class Patch_HUDManager_AssignNodeToUIElement
    {
        [HarmonyPostfix]
        public static void Postfix(ScanNodeProperties node)
        {
            try
            {
                if (node == null) return;
                var ai = node.GetComponentInParent<EnemyAI>();
                if (ai != null) MonsterState.MarkScanned(ai.GetInstanceID());
            }
            catch { /* сканирование не должно ронять оверлей */ }
        }
    }
}
