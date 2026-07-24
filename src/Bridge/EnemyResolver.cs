using System;
using System.Collections.Generic;
using UnityEngine;

namespace LCBridgeOverlay
{
    /// <summary>
    /// Превращает врага (EnemyAI) в УТОЧНЁННОЕ имя для оверлея.
    ///
    /// Зачем: моды ToilHead / BrutalCompanyMinusExtraReborn навешивают турель на
    /// обычных Coil-Head (enemyName = "Spring") и Manticoil, не меняя их enemyType.
    /// Базовый ai.enemyType.enemyName поэтому НЕ различает Toil-Head/Manti-Toil,
    /// и оверлей не может дать им свою иконку. Здесь мы добавляем суффикс.
    ///
    /// ВАЖНО: никаких прямых ссылок на сборки модов — только рефлексия по имени
    /// компонента. Если ToilHead не установлен, всё просто работает как раньше.
    /// </summary>
    internal static class EnemyResolver
    {
        // имя возвращается в формате, который ждёт оверлей (см. MOB_ALIAS в HTML).
        // ToilHead вешает на врага компонент-контроллер. Реальные имена (из лога мода):
        //   ToilHeadController      -> Coil-Head + турель        => "<base>+Turret"
        //   MantiToilController     -> Manticoil + турель         => "<base>+Turret"
        //   ToilSlayerController    -> Coil-Head slayer + турель  => "<base>+Turret+Slayer"
        //   MantiSlayerController   -> Manticoil slayer + турель  => "<base>+Turret+Slayer"
        // Если у врага уже своё уникальное имя (Nutslayer и т.п.) — мод задаёт его сам.

        public static string Resolve(object enemyAiObj)
        {
            string baseName = GetBaseName(enemyAiObj);
            if (string.IsNullOrEmpty(baseName)) return null;

            try
            {
                int kind = TurretKind(enemyAiObj); // 0 нет, 1 турель, 2 турель+slayer
                if (kind == 2) return baseName + "+Turret+Slayer";
                if (kind == 1) return baseName + "+Turret";
            }
            catch { /* рефлексия не должна ронять тик */ }

            return baseName;
        }

        private static string GetBaseName(object enemyAiObj)
        {
            try
            {
                var ai = enemyAiObj as EnemyAI;
                if (ai != null && ai.enemyType != null && !string.IsNullOrEmpty(ai.enemyType.enemyName))
                    return ai.enemyType.enemyName;
            }
            catch { }
            return null;
        }

        // маркеры контроллеров ToilHead (по имени типа, без ссылки на сборку мода)
        private static readonly string[] _turretMarkers =
            { "ToilHeadController", "MantiToilController", "TurretHeadController" };
        private static readonly string[] _slayerMarkers =
            { "ToilSlayerController", "MantiSlayerController", "SlayerController" };

        /// <summary>
        /// 0 — нет турели; 1 — турель; 2 — турель slayer-версии.
        /// Ищем компонент-контроллер ToilHead на враге или его детях по имени типа.
        /// </summary>
        private static int TurretKind(object enemyAiObj)
        {
            var mb = enemyAiObj as MonoBehaviour;
            if (mb == null) return 0;

            var comps = mb.GetComponentsInChildren<Component>(true);
            if (comps == null) return 0;

            int kind = 0;
            foreach (var c in comps)
            {
                if (c == null) continue;
                string tn = c.GetType().Name;
                if (string.IsNullOrEmpty(tn)) continue;
                foreach (var s in _slayerMarkers)
                    if (tn.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0) return 2; // slayer приоритетнее
                if (kind == 0)
                    foreach (var t in _turretMarkers)
                        if (tn.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0) { kind = 1; break; }
            }
            return kind;
        }
    }
}
