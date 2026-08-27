using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace LCBridgeOverlay
{
    /// <summary>
    /// Хост решает, что показывать.
    ///
    /// Правило сообщества Lethal Company: клиентский мод не должен давать
    /// заметного преимущества. Всё, что сообщает игроку то, чего в ванильной игре
    /// он знать не может (монстры на уровне, ловушки, радар за дверью, аппарат,
    /// стоимость лута), теперь разрешает ИМЕННО ХОСТ — и только для лобби целиком.
    ///
    /// Как это работает:
    ///  - хост держит мод → он отвечает на приветствие клиента своей политикой,
    ///    и она одинакова для всех, кто в лобби;
    ///  - хост мода НЕ держит → ответа не будет, и через пару секунд клиент сам
    ///    гасит все такие панели. В одиночной игре игрок всегда сам себе хост,
    ///    поэтому там всё доступно.
    ///
    /// Локальные настройки могут только УБАВИТЬ разрешённое хостом, но не добавить.
    ///
    /// Обмен идёт именованными сообщениями Netcode, без своих NetworkObject и без
    /// сторонних библиотек: ванильный хост их просто не получает.
    /// </summary>
    internal static class OverlayNet
    {
        private const string HelloMsg = "LCBridgeOverlay_hello";
        private const string PolicyMsg = "LCBridgeOverlay_policy";
        private const byte Wire = 1;        // версия протокола
        private const float WaitSeconds = 8f;  // сколько ждём ответ хоста

        public enum Link
        {
            Offline,   // сети нет (главное меню)
            Waiting,   // спросили хоста, ждём ответ
            Granted,   // хост с модом, политика получена
            Denied     // хост не ответил — считаем, что мода у него нет
        }

        public static Link State { get; private set; } = Link.Offline;

        /// <summary>Что именно хост разрешил показывать.</summary>
        public struct HostPolicy
        {
            public bool Monsters, Traps, DoorRadar, Apparatus, Events, Countdown, LootMult, LevelScrap, Interior;
            public bool RequireScan;   // хост может ПРИНУДИТЕЛЬНО требовать скан
            public float DoorRadius;

            public static HostPolicy FromLocalConfig()
            {
                return new HostPolicy
                {
                    Monsters = ConfigSettings.ShowMonsters.Value,
                    Traps = ConfigSettings.ShowTraps.Value,
                    DoorRadar = ConfigSettings.DoorRadar.Value,
                    Apparatus = ConfigSettings.ShowApparatusIcon.Value,
                    Events = ConfigSettings.ShowBrutalEvent.Value,
                    Countdown = ConfigSettings.ShowEndOfDayCountdown.Value,
                    LootMult = ConfigSettings.ShowLootMultiplier.Value,
                    LevelScrap = true,
                    Interior = true,
                    RequireScan = ConfigSettings.RequireScanToShow.Value,
                    DoorRadius = ConfigSettings.DoorRadarRadius.Value,
                };
            }

            /// <summary>Ничего не разрешено: хоста с модом нет.</summary>
            public static HostPolicy Blocked()
            {
                return new HostPolicy { RequireScan = true, DoorRadius = 0f };
            }

            public ushort Bits()
            {
                ushort b = 0;
                if (Monsters) b |= 1 << 0;
                if (Traps) b |= 1 << 1;
                if (DoorRadar) b |= 1 << 2;
                if (Apparatus) b |= 1 << 3;
                if (Events) b |= 1 << 4;
                if (Countdown) b |= 1 << 5;
                if (LootMult) b |= 1 << 6;
                if (LevelScrap) b |= 1 << 7;
                if (RequireScan) b |= 1 << 8;
                if (Interior) b |= 1 << 9;
                return b;
            }

            public static HostPolicy FromBits(ushort b, float radius)
            {
                return new HostPolicy
                {
                    Monsters = (b & (1 << 0)) != 0,
                    Traps = (b & (1 << 1)) != 0,
                    DoorRadar = (b & (1 << 2)) != 0,
                    Apparatus = (b & (1 << 3)) != 0,
                    Events = (b & (1 << 4)) != 0,
                    Countdown = (b & (1 << 5)) != 0,
                    LootMult = (b & (1 << 6)) != 0,
                    LevelScrap = (b & (1 << 7)) != 0,
                    RequireScan = (b & (1 << 8)) != 0,
                    Interior = (b & (1 << 9)) != 0,
                    DoorRadius = radius,
                };
            }
        }

        private static HostPolicy _policy = HostPolicy.Blocked();

        /// <summary>Действующая политика. Пока хост не подтвердил — не разрешено ничего.</summary>
        public static HostPolicy Policy => State == Link.Granted ? _policy : HostPolicy.Blocked();

        private static bool _registered;
        private static float _askedAt;
        private static ushort _lastSentBits;
        private static float _lastSentRadius;
        private static bool _everSent;

        private static NetworkManager NM => NetworkManager.Singleton;

        // ================= жизненный цикл =================

        /// <summary>Регистрация обработчиков. Идемпотентна.</summary>
        public static void Register()
        {
            try
            {
                var nm = NM;
                if (nm == null || nm.CustomMessagingManager == null || _registered) return;

                nm.CustomMessagingManager.RegisterNamedMessageHandler(HelloMsg, OnHello);
                nm.CustomMessagingManager.RegisterNamedMessageHandler(PolicyMsg, OnPolicy);
                _registered = true;
            }
            catch (Exception e) { Plugin.Log?.LogWarning($"[net] регистрация не удалась: {e.Message}"); }
        }

        /// <summary>Локальный игрок в игре: хост разрешает себе сам, клиент спрашивает.</summary>
        public static void OnLocalPlayerReady()
        {
            try
            {
                Register();
                var nm = NM;
                if (nm == null || !nm.IsListening) { State = Link.Offline; return; }

                if (nm.IsServer)
                {
                    // сам себе хост (в том числе одиночная игра)
                    _policy = HostPolicy.FromLocalConfig();
                    State = Link.Granted;
                    _everSent = false;
                    Plugin.Log?.LogInfo("[net] мы хост — панели разрешает наш конфиг, он же уедет клиентам.");
                    return;
                }

                State = Link.Waiting;
                _askedAt = Time.unscaledTime;
                SendHello();
                Plugin.Log?.LogInfo("[net] спросили хоста про мод; ждём ответ.");
            }
            catch (Exception e) { Plugin.Log?.LogWarning($"[net] OnLocalPlayerReady: {e.Message}"); }
        }

        /// <summary>Раз в тик: таймаут ожидания у клиента, рассылка изменений у хоста.</summary>
        public static void Tick()
        {
            try
            {
                var nm = NM;
                if (nm == null || !nm.IsListening) return;

                if (nm.IsServer)
                {
                    _policy = HostPolicy.FromLocalConfig();
                    State = Link.Granted;
                    // конфиг хоста поменялся на лету — донесём до всех
                    ushort bits = _policy.Bits();
                    if (!_everSent || bits != _lastSentBits ||
                        !Mathf.Approximately(_policy.DoorRadius, _lastSentRadius))
                        BroadcastPolicy();
                    return;
                }

                if (State == Link.Waiting && Time.unscaledTime - _askedAt > WaitSeconds)
                {
                    State = Link.Denied;
                    Plugin.Log?.LogInfo("[net] хост не ответил — мода у него нет. " +
                                        "Панели с подсказками выключены: клиентское преимущество в этом сообществе запрещено.");
                }
            }
            catch { }
        }

        /// <summary>Выход из игры.</summary>
        public static void Reset()
        {
            State = Link.Offline;
            _policy = HostPolicy.Blocked();
            _registered = false;
            _everSent = false;
        }

        // ================= обмен =================

        private static void SendHello()
        {
            try
            {
                var nm = NM;
                if (nm == null || nm.CustomMessagingManager == null) return;
                using (var w = new FastBufferWriter(2, Allocator.Temp))
                {
                    w.WriteValueSafe(Wire);
                    nm.CustomMessagingManager.SendNamedMessage(
                        HelloMsg, NetworkManager.ServerClientId, w, NetworkDelivery.ReliableSequenced);
                }
            }
            catch (Exception e) { Plugin.Log?.LogWarning($"[net] приветствие не ушло: {e.Message}"); }
        }

        /// <summary>Клиент поздоровался — отвечаем политикой (только хост).</summary>
        private static void OnHello(ulong sender, FastBufferReader reader)
        {
            try
            {
                var nm = NM;
                if (nm == null || !nm.IsServer) return;
                byte wire = 0;
                if (reader.TryBeginRead(1)) reader.ReadValueSafe(out wire);
                if (wire != Wire)
                {
                    Plugin.Log?.LogWarning($"[net] у клиента {sender} другая версия мода (протокол {wire}, у нас {Wire}).");
                    return;
                }
                _policy = HostPolicy.FromLocalConfig();
                SendPolicyTo(sender);
                Plugin.Log?.LogInfo($"[net] клиенту {sender} отправлена политика хоста.");
            }
            catch (Exception e) { Plugin.Log?.LogWarning($"[net] OnHello: {e.Message}"); }
        }

        /// <summary>Пришла политика хоста (только клиент).</summary>
        private static void OnPolicy(ulong sender, FastBufferReader reader)
        {
            try
            {
                var nm = NM;
                if (nm == null || nm.IsServer) return;
                // принимаем только от сервера, чужие пакеты игнорируем
                if (sender != NetworkManager.ServerClientId) return;

                if (!reader.TryBeginRead(1 + 2 + 4)) return;
                reader.ReadValueSafe(out byte wire);
                if (wire != Wire) return;
                reader.ReadValueSafe(out ushort bits);
                reader.ReadValueSafe(out float radius);

                _policy = HostPolicy.FromBits(bits, radius);
                State = Link.Granted;
                Plugin.Log?.LogInfo($"[net] хост с модом: монстры={_policy.Monsters}, ловушки={_policy.Traps}, " +
                                    $"радар={_policy.DoorRadar}, аппарат={_policy.Apparatus}, только-сканы={_policy.RequireScan}.");
                BridgeTicker.ForceImmediate();
            }
            catch (Exception e) { Plugin.Log?.LogWarning($"[net] OnPolicy: {e.Message}"); }
        }

        private static void SendPolicyTo(ulong clientId)
        {
            try
            {
                var nm = NM;
                if (nm == null || nm.CustomMessagingManager == null || !nm.IsServer) return;
                using (var w = new FastBufferWriter(1 + 2 + 4, Allocator.Temp))
                {
                    w.WriteValueSafe(Wire);
                    w.WriteValueSafe(_policy.Bits());
                    w.WriteValueSafe(_policy.DoorRadius);
                    nm.CustomMessagingManager.SendNamedMessage(
                        PolicyMsg, clientId, w, NetworkDelivery.ReliableSequenced);
                }
            }
            catch (Exception e) { Plugin.Log?.LogWarning($"[net] политика не ушла клиенту {clientId}: {e.Message}"); }
        }

        private static void BroadcastPolicy()
        {
            try
            {
                var nm = NM;
                if (nm == null || nm.CustomMessagingManager == null || !nm.IsServer) return;

                _lastSentBits = _policy.Bits();
                _lastSentRadius = _policy.DoorRadius;
                _everSent = true;

                foreach (var id in nm.ConnectedClientsIds)
                {
                    if (id == NetworkManager.ServerClientId) continue;
                    SendPolicyTo(id);
                }
            }
            catch { }
        }
    }

    /// <summary>
    /// Что оверлею РЕАЛЬНО можно показать: локальная настройка И разрешение хоста.
    /// Выключить у себя можно всегда, включить сверх разрешённого — нет.
    /// </summary>
    internal static class Gate
    {
        private static OverlayNet.HostPolicy P => OverlayNet.Policy;

        public static bool Monsters => ConfigSettings.ShowMonsters.Value && P.Monsters;
        public static bool Traps => ConfigSettings.ShowTraps.Value && P.Traps;
        public static bool DoorRadar => ConfigSettings.DoorRadar.Value && P.DoorRadar;
        public static bool Apparatus => ConfigSettings.ShowApparatusIcon.Value && P.Apparatus;
        public static bool Events => ConfigSettings.ShowBrutalEvent.Value && P.Events;
        public static bool Countdown => ConfigSettings.ShowEndOfDayCountdown.Value && P.Countdown;
        public static bool LootMult => ConfigSettings.ShowLootMultiplier.Value && P.LootMult;
        /// <summary>Сколько лута на уровне и сколько предметов внутри/снаружи — этого игра не сообщает.</summary>
        public static bool LevelLoot => P.LevelScrap;
        public static bool LevelScrap => P.LevelScrap;
        /// <summary>Тип интерьера: в игре узнаётся только при входе.</summary>
        public static bool Interior => P.Interior;

        /// <summary>Радиус радара: не больше, чем разрешил хост.</summary>
        public static float DoorRadius => Mathf.Min(ConfigSettings.DoorRadarRadius.Value, P.DoorRadius);

        /// <summary>Требование скана: хост может включить его принудительно.</summary>
        public static bool RequireScan => ConfigSettings.RequireScanToShow.Value || P.RequireScan;

        /// <summary>Хост без мода — панели с подсказками погашены.</summary>
        public static bool Restricted => OverlayNet.State != OverlayNet.Link.Granted;
    }
}
