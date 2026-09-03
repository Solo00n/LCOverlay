using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using UnityEngine;

namespace LCBridgeOverlay
{
    [BepInPlugin(GUID, NAME, VERSION)]
    // РњРѕСЃС‚ С‚РµРїРµСЂСЊ РІСЃС‚СЂРѕРµРЅ РїСЂСЏРјРѕ СЃСЋРґР° (СЃРј. Bridge/*) вЂ” РѕС‚РґРµР»СЊРЅС‹Р№ РјРѕРґ LCBridge Р±РѕР»СЊС€Рµ РЅРµ РЅСѓР¶РµРЅ.
    // РќРёР¶Рµ вЂ” С‚РѕР»СЊРєРѕ РјСЏРіРєРёРµ Р·Р°РІРёСЃРёРјРѕСЃС‚Рё: Р±РµР· РЅРёС… РѕРІРµСЂР»РµР№ СЂР°Р±РѕС‚Р°РµС‚, РїСЂРѕСЃС‚Рѕ РґР°РЅРЅС‹С… РјРµРЅСЊС€Рµ.
    [BepInDependency("SoftDiamond.BrutalCompanyMinusExtraReborn", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("mrov.WeatherTweaks", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("Timofey.MonstersGordion", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("imabatby.lethallevelloader", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("me.swipez.melonloader.morecompany", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("Timofey.DeviantEnemies", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID = "gdlp.lcbridgeoverlay";
        public const string NAME = "LCBridgeOverlay";
        public const string VERSION = "1.15.6";

        public static Plugin Instance { get; private set; }
        public static ManualLogSource Log { get; private set; }

        private Harmony _harmony;

        // Lethal Company РїСЂРё Р·Р°РіСЂСѓР·РєРµ РіР»Р°РІРЅРѕРіРѕ РјРµРЅСЋ СѓРЅРёС‡С‚РѕР¶Р°РµС‚ РєРѕРјРїРѕРЅРµРЅС‚С‹ РїР»Р°РіРёРЅРѕРІ.
        // РџРѕСЌС‚РѕРјСѓ Рё РѕРІРµСЂР»РµР№, Рё С‚РёРєРµСЂ РјРѕСЃС‚Р° РІРёСЃСЏС‚ РЅР° HideAndDontSave-РѕР±СЉРµРєС‚Р°С…,
        // Р° СЃРµСЂРІРµСЂ РіР°СЃРёС‚СЃСЏ С‚РѕР»СЊРєРѕ РїСЂРё СЂРµР°Р»СЊРЅРѕРј РІС‹С…РѕРґРµ РёР· РёРіСЂС‹.
        private static bool _quitting;
        private static GameObject _tickerGo;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            ConfigSettings.Bind(Config);

            if (!ConfigSettings.Enabled.Value)
            {
                Log.LogInfo($"{NAME} РІС‹РєР»СЋС‡РµРЅ РІ РєРѕРЅС„РёРіРµ (General.Enabled = false).");
                return;
            }

            // Harmony-РїР°С‚С‡Рё: СЂР°Р·РѕРј РїСЂРёРјРµРЅСЏСЋС‚СЃСЏ Рё РїР°С‚С‡Рё UI-РѕРІРµСЂР»РµСЏ (Disconnect),
            // Рё РїР°С‚С‡Рё СЃР±РѕСЂР° РґР°РЅРЅС‹С… РІСЃС‚СЂРѕРµРЅРЅРѕРіРѕ РјРѕСЃС‚Р° (PlayerControllerB / StartOfRound).
            try
            {
                _harmony = new Harmony(GUID);
                // ПОШТУЧНО, а не PatchAll: одна несовпавшая сигнатура роняла
                // весь PatchAll, и мод оставался вообще без патчей — именно так
                // пропала вся сетевая часть, ивенты и вспышки урона.
                int ok = 0, bad = 0;
                foreach (var t in System.Reflection.Assembly.GetExecutingAssembly().GetTypes())
                {
                    try
                    {
                        if (t.GetCustomAttributes(typeof(HarmonyPatch), true).Length == 0) continue;
                        _harmony.CreateClassProcessor(t).Patch();
                        ok++;
                    }
                    catch (Exception pe)
                    {
                        bad++;
                        Log.LogError($"патч {t.Name} не применён: {pe.Message}");
                    }
                }
                Log.LogInfo($"Harmony: применено классов {ok}, с ошибкой {bad}.");
                Log.LogInfo("Harmony-РїР°С‚С‡Рё РїСЂРёРјРµРЅРµРЅС‹.");
            }
            catch (Exception e)
            {
                Log.LogWarning($"РќРµ СѓРґР°Р»РѕСЃСЊ РїСЂРёРјРµРЅРёС‚СЊ Harmony-РїР°С‚С‡Рё: {e.Message}");
            }

            // РћС‚РґРµР»СЊРЅРѕ, С‡РµСЂРµР· СЂРµС„Р»РµРєСЃРёСЋ (РјСЏРіРєР°СЏ Р·Р°РІРёСЃРёРјРѕСЃС‚СЊ): РїР°С‚С‡РёРј Р°РЅРѕРЅСЃ РёРІРµРЅС‚РѕРІ BCME,
            // С‡С‚РѕР±С‹ РљР›РР•РќРўР« С‚РѕР¶Рµ РІРёРґРµР»Рё РёРІРµРЅС‚С‹ (Сѓ РЅРёС… EventManager.currentEvents РїСѓСЃС‚).
            TryPatchBcmeTips();

            // --- РІСЃС‚СЂРѕРµРЅРЅС‹Р№ РјРѕСЃС‚: WebSocket-СЃРµСЂРІРµСЂ + С‚РёРєРµСЂ СЃР±РѕСЂР° СЃРѕСЃС‚РѕСЏРЅРёСЏ ---
            // Мост наружу поднимаем ТОЛЬКО если игрок сам его включил: без этого
            // мод не открывает ни одного порта. Внутриигровому оверлею мост не нужен.
            if (ConfigSettings.WebSocketEnabled.Value)
            {
                int port = ConfigSettings.Port.Value;
                try
                {
                    BridgeServer.Start(port);
                    Log.LogInfo($"WebSocket-мост включён в конфиге: слушаю ws://127.0.0.1:{port} (только этот компьютер).");
                }
                catch (Exception e)
                {
                    Log.LogError($"Не удалось поднять мост на порту {port} (возможно, порт занят): {e.Message}");
                }
            }
            else
            {
                Log.LogInfo("WebSocket-мост выключен (по умолчанию) — порты не открываются. Включается в конфиге: [WebSocket] Enabled.");
            }
            // образец схемы локации рядом с конфигом — чтобы её можно было править
            try { MapLayout.WriteTemplateIfMissing(); } catch { }
            EnsureTicker();
            Application.quitting += OnApplicationQuitting;

            CreateOverlay();

            Log.LogInfo($"{NAME} v{VERSION} РіРѕС‚РѕРІ (РјРѕСЃС‚ РІСЃС‚СЂРѕРµРЅ, РѕС‚РґРµР»СЊРЅС‹Р№ LCBridge РЅРµ С‚СЂРµР±СѓРµС‚СЃСЏ).");
        }

        /// <summary>
        /// РџР°С‚С‡РёС‚ Net.DisplayTipClientRpc РёР· BCME (РјСЏРіРєР°СЏ Р·Р°РІРёСЃРёРјРѕСЃС‚СЊ, С‡РµСЂРµР· СЂРµС„Р»РµРєСЃРёСЋ),
        /// С‡С‚РѕР±С‹ Р»РѕРІРёС‚СЊ Р°РЅРѕРЅСЃС‹ РёРІРµРЅС‚РѕРІ РЅР° РєР»РёРµРЅС‚Р°С…. Р•СЃР»Рё BCME РЅРµС‚ РёР»Рё СЃРёРіРЅР°С‚СѓСЂР° РґСЂСѓРіР°СЏ вЂ”
        /// С‚РёС…Рѕ РїСЂРѕРїСѓСЃРєР°РµРј, РѕРІРµСЂР»РµР№ РїСЂРѕРґРѕР»Р¶Р°РµС‚ СЂР°Р±РѕС‚Р°С‚СЊ.
        /// </summary>
        private void TryPatchBcmeTips()
        {
            try
            {
                var netType = AccessTools.TypeByName("BrutalCompanyMinus.Net");
                if (netType == null) { Log.LogInfo("BCME.Net РЅРµ РЅР°Р№РґРµРЅ вЂ” Р»РѕРІР»СЏ РёРІРµРЅС‚РѕРІ РЅР° РєР»РёРµРЅС‚Рµ РїСЂРѕРїСѓС‰РµРЅР°."); return; }
                var target = AccessTools.Method(netType, "DisplayTipClientRpc");
                if (target == null) { Log.LogWarning("BCME.Net.DisplayTipClientRpc РЅРµ РЅР°Р№РґРµРЅ вЂ” СЃРёРіРЅР°С‚СѓСЂР° РёР·РјРµРЅРёР»Р°СЃСЊ?"); return; }
                var postfix = new HarmonyMethod(typeof(BcmeClientEvents).GetMethod(
                    nameof(BcmeClientEvents.OnDisplayTip),
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static));
                _harmony.Patch(target, postfix: postfix);
                Log.LogInfo("BCME.Net.DisplayTipClientRpc РїСЂРѕРїР°С‚С‡РµРЅ вЂ” РєР»РёРµРЅС‚С‹ Р±СѓРґСѓС‚ РІРёРґРµС‚СЊ РёРІРµРЅС‚С‹.");
            }
            catch (Exception e)
            {
                Log.LogWarning($"РќРµ СѓРґР°Р»РѕСЃСЊ РїСЂРѕРїР°С‚С‡РёС‚СЊ BCME-Р°РЅРѕРЅСЃС‹ РёРІРµРЅС‚РѕРІ: {e.Message}");
            }
        }

        /// <summary>РўРёРєРµСЂ СЃРѕР±РёСЂР°РµС‚ СЃРѕСЃС‚РѕСЏРЅРёРµ СЂР°Р· РІ СЃРµРєСѓРЅРґСѓ Рё (Р°) СЂР°Р·РґР°С‘С‚ РїРѕ WebSocket
        /// HTML-РѕРІРµСЂР»РµСЋ, (Р±) РѕС‚РґР°С‘С‚ РЅР°РїСЂСЏРјСѓСЋ РІРЅСѓС‚СЂРёРёРіСЂРѕРІРѕРјСѓ РѕРІРµСЂР»РµСЋ. Р–РёРІС‘С‚ РЅР°
        /// РЅРµСѓР±РёРІР°РµРјРѕРј РѕР±СЉРµРєС‚Рµ, РїРµСЂРµР¶РёРІР°РµС‚ Р·Р°С‡РёСЃС‚РєСѓ СЃС†РµРЅС‹ РіР»Р°РІРЅРѕРіРѕ РјРµРЅСЋ.</summary>
        private static void EnsureTicker()
        {
            if (_tickerGo != null) return;
            _tickerGo = new GameObject("LCBridgeTicker") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(_tickerGo);
            _tickerGo.AddComponent<BridgeTicker>();
        }

        /// <summary>
        /// РЎРѕР·РґР°С‘С‚ РѕР±СЉРµРєС‚-РґРѕР»РіРѕР¶РёС‚РµР»СЊ СЃ РѕРІРµСЂР»РµРµРј. Р’РђР–РќРћ: HideAndDontSave вЂ”
        /// Lethal Company РїСЂРё Р·Р°РіСЂСѓР·РєРµ РіР»Р°РІРЅРѕРіРѕ РјРµРЅСЋ СѓРЅРёС‡С‚РѕР¶Р°РµС‚ В«РїРѕСЃС‚РѕСЂРѕРЅРЅРёРµВ»
        /// DontDestroyOnLoad-РѕР±СЉРµРєС‚С‹ Р±РµР· СЌС‚РѕРіРѕ С„Р»Р°РіР°.
        /// </summary>
        private static void CreateOverlay()
        {
            var go = new GameObject("LCBridgeOverlay") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<OverlayManager>();
        }

        // СЃС‚РѕСЂРѕР¶РµРІРѕР№ С‚Р°Р№РјРµСЂ: РµСЃР»Рё РѕР±СЉРµРєС‚ РѕРІРµСЂР»РµСЏ РёР»Рё С‚РёРєРµСЂ РІСЃС‘ Р¶Рµ СѓРЅРёС‡С‚РѕР¶РёР»Рё вЂ”
        // РїРµСЂРµСЃРѕР·РґР°С‘Рј. Plugin-РєРѕРјРїРѕРЅРµРЅС‚ BepInEx Р¶РёРІС‘С‚ РІСЃРµРіРґР°, РїРѕСЌС‚РѕРјСѓ Update РЅР°РґС‘Р¶РµРЅ.
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
                Log.LogWarning("РћР±СЉРµРєС‚ РѕРІРµСЂР»РµСЏ Р±С‹Р» СѓРЅРёС‡С‚РѕР¶РµРЅ вЂ” РїРµСЂРµСЃРѕР·РґР°СЋ.");
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
            // СЃРјРµРЅР° СЃС†РµРЅС‹ вЂ” РќР• РІС‹С…РѕРґ РёР· РёРіСЂС‹: РјРѕСЃС‚ Рё С‚РёРєРµСЂ РїСЂРѕРґРѕР»Р¶Р°СЋС‚ СЂР°Р±РѕС‚Р°С‚СЊ
            if (!_quitting)
            {
                Log?.LogInfo("РљРѕРјРїРѕРЅРµРЅС‚ LCBridgeOverlay СѓРЅРёС‡С‚РѕР¶РµРЅ СЃРјРµРЅРѕР№ СЃС†РµРЅС‹ вЂ” РјРѕСЃС‚ Рё С‚РёРєРµСЂ РїСЂРѕРґРѕР»Р¶Р°СЋС‚ СЂР°Р±РѕС‚Р°С‚СЊ.");
                return;
            }
            try { BridgeServer.Stop(); } catch { }
            try { _harmony?.UnpatchSelf(); } catch { }
        }
    }
}

