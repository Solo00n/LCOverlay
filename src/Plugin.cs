using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using UnityEngine;

namespace LCBridgeOverlay
{
    [BepInPlugin(GUID, NAME, VERSION)]
    // Мост теперь встроен прямо сюда (см. Bridge/*) — отдельный мод LCBridge больше не нужен.
    // Ниже — только мягкие зависимости: без них оверлей работает, просто данных меньше.
    [BepInDependency("SoftDiamond.BrutalCompanyMinusExtraReborn", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("mrov.WeatherTweaks", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("Timofey.MonstersGordion", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("imabatby.lethallevelloader", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("me.swipez.melonloader.morecompany", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID = "gdlp.lcbridgeoverlay";
        public const string NAME = "LCBridgeOverlay";
        public const string VERSION = "1.4.0";

        public static Plugin Instance { get; private set; }
        public static ManualLogSource Log { get; private set; }

        private Harmony _harmony;

        // Lethal Company при загрузке главного меню уничтожает компоненты плагинов.
        // Поэтому и оверлей, и тикер моста висят на HideAndDontSave-объектах,
        // а сервер гасится только при реальном выходе из игры.
        private static bool _quitting;
        private static GameObject _tickerGo;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            ConfigSettings.Bind(Config);

            if (!ConfigSettings.Enabled.Value)
            {
                Log.LogInfo($"{NAME} выключен в конфиге (General.Enabled = false).");
                return;
            }

            // Harmony-патчи: разом применяются и патчи UI-оверлея (Disconnect),
            // и патчи сбора данных встроенного моста (PlayerControllerB / StartOfRound).
            try
            {
                _harmony = new Harmony(GUID);
                _harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());
                Log.LogInfo("Harmony-патчи применены.");
            }
            catch (Exception e)
            {
                Log.LogWarning($"Не удалось применить Harmony-патчи: {e.Message}");
            }

            // Отдельно, через рефлексию (мягкая зависимость): патчим анонс ивентов BCME,
            // чтобы КЛИЕНТЫ тоже видели ивенты (у них EventManager.currentEvents пуст).
            TryPatchBcmeTips();

            // --- встроенный мост: WebSocket-сервер + тикер сбора состояния ---
            int port = ConfigSettings.Port.Value;
            try
            {
                BridgeServer.Start(port);
                Log.LogInfo($"Встроенный мост запущен на ws://localhost:{port} (для HTML-оверлея/OBS).");
            }
            catch (Exception e)
            {
                // Частая причина — рядом ещё стоит старый мод LCBridge и держит порт.
                Log.LogError($"Не удалось поднять мост на порту {port} (возможно, порт занят — удали старый мод LCBridge): {e.Message}");
            }
            EnsureTicker();
            Application.quitting += OnApplicationQuitting;

            CreateOverlay();

            Log.LogInfo($"{NAME} v{VERSION} готов (мост встроен, отдельный LCBridge не требуется).");
        }

        /// <summary>
        /// Патчит Net.DisplayTipClientRpc из BCME (мягкая зависимость, через рефлексию),
        /// чтобы ловить анонсы ивентов на клиентах. Если BCME нет или сигнатура другая —
        /// тихо пропускаем, оверлей продолжает работать.
        /// </summary>
        private void TryPatchBcmeTips()
        {
            try
            {
                var netType = AccessTools.TypeByName("BrutalCompanyMinus.Net");
                if (netType == null) { Log.LogInfo("BCME.Net не найден — ловля ивентов на клиенте пропущена."); return; }
                var target = AccessTools.Method(netType, "DisplayTipClientRpc");
                if (target == null) { Log.LogWarning("BCME.Net.DisplayTipClientRpc не найден — сигнатура изменилась?"); return; }
                var postfix = new HarmonyMethod(typeof(BcmeClientEvents).GetMethod(
                    nameof(BcmeClientEvents.OnDisplayTip),
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static));
                _harmony.Patch(target, postfix: postfix);
                Log.LogInfo("BCME.Net.DisplayTipClientRpc пропатчен — клиенты будут видеть ивенты.");
            }
            catch (Exception e)
            {
                Log.LogWarning($"Не удалось пропатчить BCME-анонсы ивентов: {e.Message}");
            }
        }

        /// <summary>Тикер собирает состояние раз в секунду и (а) раздаёт по WebSocket
        /// HTML-оверлею, (б) отдаёт напрямую внутриигровому оверлею. Живёт на
        /// неубиваемом объекте, переживает зачистку сцены главного меню.</summary>
        private static void EnsureTicker()
        {
            if (_tickerGo != null) return;
            _tickerGo = new GameObject("LCBridgeTicker") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(_tickerGo);
            _tickerGo.AddComponent<BridgeTicker>();
        }

        /// <summary>
        /// Создаёт объект-долгожитель с оверлеем. ВАЖНО: HideAndDontSave —
        /// Lethal Company при загрузке главного меню уничтожает «посторонние»
        /// DontDestroyOnLoad-объекты без этого флага.
        /// </summary>
        private static void CreateOverlay()
        {
            var go = new GameObject("LCBridgeOverlay") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<OverlayManager>();
        }

        // сторожевой таймер: если объект оверлея или тикер всё же уничтожили —
        // пересоздаём. Plugin-компонент BepInEx живёт всегда, поэтому Update надёжен.
        private float _watchdogT;

        private void Update()
        {
            if (!ConfigSettings.Enabled.Value) return;
            _watchdogT += Time.unscaledDeltaTime;
            if (_watchdogT < 3f) return;
            _watchdogT = 0f;
            if (_tickerGo == null) EnsureTicker();
            if (OverlayManager.Instance == null)
            {
                Log.LogWarning("Объект оверлея был уничтожен — пересоздаю.");
                CreateOverlay();
            }
        }

        private static void OnApplicationQuitting()
        {
            _quitting = true;
            try { BridgeServer.Stop(); } catch { }
        }

        private void OnDestroy()
        {
            // смена сцены — НЕ выход из игры: мост и тикер продолжают работать
            if (!_quitting)
            {
                Log?.LogInfo("Компонент LCBridgeOverlay уничтожен сменой сцены — мост и тикер продолжают работать.");
                return;
            }
            try { BridgeServer.Stop(); } catch { }
            try { _harmony?.UnpatchSelf(); } catch { }
        }
    }
}
