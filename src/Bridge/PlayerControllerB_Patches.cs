using HarmonyLib;
using GameNetcodeStuff;
using UnityEngine;

namespace LCBridgeOverlay
{
    /// <summary>
    /// Считаем реальные смерти и пытаемся определить «убийцу» (ближайшего врага).
    /// </summary>
    [HarmonyPatch(typeof(PlayerControllerB))]
    public static class PlayerControllerB_Patches
    {
        [HarmonyPatch("KillPlayer")]
        [HarmonyPostfix]
        public static void OnKillPlayer(PlayerControllerB __instance, CauseOfDeath causeOfDeath)
        {
            if (__instance != null && __instance.isPlayerDead)
            {
                string killer = ResolveKiller(__instance, causeOfDeath);
                GameState.RegisterDeath(__instance, killer);
            }
        }

        // Кто/что убило: для смертей от врага — имя ближайшего живого врага; иначе обобщённая причина.
        private static string ResolveKiller(PlayerControllerB player, CauseOfDeath cause)
        {
            try
            {
                // Причины смерти храним по-английски (нейтрально, как имена врагов);
                // на русский их переводит уже оверлей при показе (VictoryWidget).
                switch (cause)
                {
                    case CauseOfDeath.Gravity:       return "Fall";
                    case CauseOfDeath.Drowning:      return "Drowning";
                    case CauseOfDeath.Suffocation:   return "Suffocation";
                    case CauseOfDeath.Burning:       return "Fire";
                    case CauseOfDeath.Electrocution: return "Shock";
                    case CauseOfDeath.Crushing:      return "Crushed";
                }

                var rm = RoundManager.Instance;
                if (rm == null || rm.SpawnedEnemies == null) return "Unknown";
                Vector3 pos = player.transform.position;
                float best = 25f;
                string name = null;
                foreach (var ai in rm.SpawnedEnemies)
                {
                    if (ai == null || ai.isEnemyDead) continue;
                    float d = Vector3.Distance(pos, ai.transform.position);
                    if (d < best)
                    {
                        best = d;
                        name = EnemyResolver.Resolve(ai);
                    }
                }
                return string.IsNullOrEmpty(name) ? "Unknown" : name;
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}
