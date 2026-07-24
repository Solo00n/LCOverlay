using System;
using UnityEngine;

namespace LCBridgeOverlay
{
    /// <summary>
    /// Пакет данных от моста LCBridge (тот же JSON, что читает HTML-оверлей).
    /// Парсится через JsonUtility, поэтому имена полей строго совпадают с JSON.
    /// </summary>
    [Serializable]
    public class BridgePayload
    {
        public string type;

        public int deaths;
        public int alive;
        public int total;
        public int health;

        public string moonName;
        public string weatherFull;
        public string brutalEvent;

        public bool onMoon;
        public bool loading;
        public bool inGame;

        public int resetToken;
        public int levelScrap;

        public string topKiller;
        public string topMonster;
        public string deadliestEvent;

        public string[] monstersOutside;
        public string[] monstersInside;
        public string[] traps;

        // ---- аналитика забега (для баннера победы) ----
        public RunInfo run;

        // ---- поля LCBridge v1.2+ (если мост старый — остаются значения по умолчанию) ----
        public int quotaValue;
        public int quotaFulfilled;
        public int shipLoot;
        public int quotaIndex = 1;
        public int dayCount = 1;
        public int daysLeft = -1;
        public string interiorType;
        public int beehiveCount;
        public int itemsInside;
        public int itemsOutside;
        public bool hasOldBird;
        public bool onShip;
    }

    [Serializable]
    public class RunInfo
    {
        public RunQuota[] quotas;
        public RunMoon[] moons;
        public RunMonster[] monsters;
        public int peak;
        public int inside;    // секунд в комплексе
        public int outside;   // секунд на улице
        public int runSec;
        public string[] timeline; // строки "day|type|text"
    }

    [Serializable]
    public class RunQuota
    {
        public int i;       // номер квоты 1..3
        public int money;   // заработано за квоту
        public int sec;     // секунд в квоте
        public int deaths;  // смертей в квоте
    }

    [Serializable]
    public class RunMoon
    {
        public string name;
        public int visits;
        public int profit;
        public int sec;
    }

    [Serializable]
    public class RunMonster
    {
        public string name;
        public int count;
        public int sec;
    }

    /// <summary>
    /// Парсинг JSON моста и хранение текущего состояния.
    /// </summary>
    public static class DataParser
    {
        /// <summary>Последний успешно разобранный пакет (null до первого).</summary>
        public static BridgePayload Current { get; private set; }

        // --- локальный «почтовый ящик» встроенного моста ---
        // После слияния LCBridge в оверлей данные не идут по сети, а передаются
        // напрямую из BridgeTicker в OverlayManager. Ящик потокобезопасен на
        // всякий случай, хотя оба живут в главном потоке Unity.
        private static string _pendingLocal;
        private static readonly object _localLock = new object();

        /// <summary>Пульс встроенного моста (Time.unscaledTime последнего тика).
        /// По нему глаз-индикатор понимает, что мост «жив».</summary>
        public static float Heartbeat = -999f;

        /// <summary>Тикер кладёт сюда свежий JSON (только при изменении состояния).</summary>
        public static void PushLocal(string json)
        {
            lock (_localLock) { _pendingLocal = json; }
        }

        /// <summary>OverlayManager забирает отложенный JSON (или null).</summary>
        public static string TakeLocal()
        {
            lock (_localLock) { var s = _pendingLocal; _pendingLocal = null; return s; }
        }

        /// <summary>
        /// Разобрать входящий JSON. Возвращает true, если это валидный пакет
        /// моста (type == "bridge") и состояние обновлено.
        /// </summary>
        public static bool TryParse(string json)
        {
            if (string.IsNullOrEmpty(json)) return false;
            BridgePayload p;
            try { p = JsonUtility.FromJson<BridgePayload>(json); }
            catch { return false; }
            if (p == null || p.type != "bridge") return false;
            Current = p;
            return true;
        }

        public static void Clear() { Current = null; }
    }
}
