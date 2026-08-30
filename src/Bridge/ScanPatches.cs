using HarmonyLib;
using UnityEngine;

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

                // 1) обычно узел сканирования — ребёнок объекта врага
                var ai = node.GetComponentInParent<EnemyAI>();

                // 2) если нет (узел вынесен отдельно) — берём ближайшего живого врага
                //    к позиции узла в небольшом радиусе
                if (ai == null) ai = NearestEnemy(node.transform.position, 8f);

                if (ai != null)
                {
                    MonsterState.MarkScanned(ai.GetInstanceID());
                    ScanRegistry.MarkLocal(ai);            // и в общий реестр — для остальных игроков
                    if (_logged.Add(ai.GetInstanceID()))
                        Plugin.Log?.LogInfo($"[scan] отсканирован {ai.GetType().Name} (\"{node.headerText}\") — покажем в оверлее.");
                    return;
                }

                // Ловушки при RequireScanToShow тоже должны требовать скана: турель,
                // мина и шипы — обычные сетевые объекты, помечаем их так же.
                Component trap = node.GetComponentInParent<Turret>();
                if (trap == null) trap = node.GetComponentInParent<Landmine>();
                if (trap == null) trap = node.GetComponentInParent<SpikeRoofTrap>();
                if (trap != null)
                {
                    ScanRegistry.MarkLocal(trap);
                    if (_logged.Add(trap.GetInstanceID()))
                        Plugin.Log?.LogInfo($"[scan] отсканирована ловушка {trap.GetType().Name} — покажем в оверлее.");
                }
            }
            catch { /* сканирование не должно ронять оверлей */ }
        }

        private static readonly System.Collections.Generic.HashSet<int> _logged =
            new System.Collections.Generic.HashSet<int>();

        private static EnemyAI NearestEnemy(Vector3 pos, float maxDist)
        {
            try
            {
                var rm = RoundManager.Instance;
                if (rm == null || rm.SpawnedEnemies == null) return null;
                EnemyAI best = null;
                float bestD = maxDist * maxDist;
                foreach (var ai in rm.SpawnedEnemies)
                {
                    if (ai == null || ai.isEnemyDead) continue;
                    float d = (ai.transform.position - pos).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = ai; }
                }
                return best;
            }
            catch { return null; }
        }
    }
}
