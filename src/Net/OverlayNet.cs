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
        private const string StateMsg  = "LCBridgeOverlay_state";   // таймер/ивенты от хоста
        private const string ScanMsg   = "LCBridgeOverlay_scan";    // кто кого просканировал
        private const byte Wire = 2;        // версия протокола (2: в состоянии появились смерти)
        private const float WaitSeconds = 8f;   // сколько ждём ответ хоста
        private const float RetrySeconds = 10f; // и как часто пробуем снова

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
            public bool ResetScans;    // и требовать пересканировать всё каждый день
            public bool ShareScans;    // и разрешать/запрещать обмен сканами
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
                    ResetScans = ConfigSettings.ResetScansEachDay.Value,
                    ShareScans = ConfigSettings.ShareScans.Value,
                    DoorRadius = ConfigSettings.DoorRadarRadius.Value,
                };
            }

            /// <summary>Ничего не разрешено: хоста с модом нет.</summary>
            public static HostPolicy Blocked()
            {
                return new HostPolicy { RequireScan = true, ResetScans = false, ShareScans = false, DoorRadius = 0f };
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
                if (ResetScans) b |= 1 << 10;
                if (ShareScans) b |= 1 << 11;
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
                    ResetScans = (b & (1 << 10)) != 0,
                    ShareScans = (b & (1 << 11)) != 0,
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

        // ---- то, что приехало от хоста (у клиента) ----
        private static float _hostTimerSec;
        private static bool _hostTimerRunning;
        private static float _hostTimerAt;      // когда пакет получен, для плавного хода
        private static int _hostResetToken;
        private static string _hostEvents;
        private static int _hostDeaths;
        private static float _hostStateAt = -999f;
        private static NetworkManager _registeredOn;

        /// <summary>Мы клиент и хост с модом отвечает.</summary>
        public static bool HasHostState =>
            State == Link.Granted && NM != null && !NM.IsServer &&
            Time.unscaledTime - _hostStateAt < 15f;

        /// <summary>Таймер хоста, доведённый до текущего момента.</summary>
        public static float HostTimerSec =>
            _hostTimerSec + (_hostTimerRunning ? Time.unscaledTime - _hostTimerAt : 0f);
        public static bool HostTimerRunning => _hostTimerRunning;
        public static int HostResetToken => _hostResetToken;
        public static string HostEvents => _hostEvents;
        public static int HostDeaths => _hostDeaths;

        private static NetworkManager NM => NetworkManager.Singleton;

        // ================= жизненный цикл =================

        /// <summary>Регистрация обработчиков. Идемпотентна.</summary>
        public static void Register()
        {
            try
            {
                var nm = NM;
                if (nm == null || nm.CustomMessagingManager == null) return;
                // NetworkManager пересоздаётся при перезаходе в сейв: если просто
                // помнить «уже регистрировались», обработчики повиснут на старом
                // объекте и у клиента всё замолкнет.
                if (_registered && ReferenceEquals(_registeredOn, nm)) return;
                _registeredOn = nm;

                nm.CustomMessagingManager.RegisterNamedMessageHandler(HelloMsg, OnHello);
                nm.CustomMessagingManager.RegisterNamedMessageHandler(PolicyMsg, OnPolicy);
                nm.CustomMessagingManager.RegisterNamedMessageHandler(StateMsg, OnState);
                nm.CustomMessagingManager.RegisterNamedMessageHandler(ScanMsg, OnScan);
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
                    BroadcastState();   // таймер, токен сброса и ивенты — всем
                    // свои сканы хост тоже кладёт в общий реестр через патч скана;
                    // раздаём набор, когда он изменился
                    ScanRegistry.TakePending();
                    if (ScanRegistry.Dirty)
                    {
                        ScanRegistry.Dirty = false;
                        if (Gate.ShareScans) BroadcastScans();   // обмен разрешён хостом
                    }
                    return;
                }

                // ---- мы клиент ----
                if (State == Link.Waiting && Time.unscaledTime - _askedAt > WaitSeconds)
                {
                    State = Link.Denied;
                    Plugin.Log?.LogInfo("[net] хост не ответил — мода у него нет. " +
                                        "Панели с подсказками выключены: клиентское преимущество в этом сообществе запрещено.");
                }

                // наши свежие сканы — хосту, он разошлёт их остальным
                if (State == Link.Granted)
                {
                    var mine = ScanRegistry.TakePending();
                    // если обмен запрещён, свои сканы наружу не отдаём вовсе
                    if (mine != null && Gate.ShareScans) SendScans(mine, NetworkManager.ServerClientId);
                }

                // Периодически спрашиваем заново. Это чинит перезаход хоста в сейв:
                // без повтора клиент навсегда оставался с Denied и пустым оверлеем.
                if (State != Link.Granted && Time.unscaledTime - _askedAt > RetrySeconds)
                {
                    _askedAt = Time.unscaledTime;
                    SendHello();
                }
                // связь с хостом пропала (он перезашёл) — вернёмся к ожиданию
                else if (State == Link.Granted && Time.unscaledTime - _hostStateAt > 15f)
                {
                    Plugin.Log?.LogInfo("[net] хост замолчал — спрашиваем заново.");
                    State = Link.Waiting;
                    _askedAt = Time.unscaledTime;
                    SendHello();
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
            _registeredOn = null;
            _everSent = false;
            _hostStateAt = -999f;
            _hostEvents = null;
            _hostTimerSec = 0f;
            _hostTimerRunning = false;
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
                if (ConfigSettings.ShareScans.Value)
                    SendScans(ScanRegistry.Snapshot(), sender);   // и что уже просканировано
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

        // ================= общие сканы =================

        /// <summary>
        /// Кто-то просканировал монстра или ловушку — это должно быть видно всем.
        /// Клиент шлёт хосту свои свежие сканы, хост — сводный набор всем остальным.
        ///
        /// Ключ — NetworkObjectId, он одинаков у всех в лобби. Сообщение только
        /// ДОБАВЛЯЕТ: очистку на новый день каждая сторона делает у себя сама
        /// (по разрешённой хостом настройке), поэтому гонок «стёрли только что
        /// добавленное» здесь не возникает.
        /// </summary>
        private static void SendScans(ulong[] ids, ulong target)
        {
            try
            {
                var nm = NM;
                if (nm == null || nm.CustomMessagingManager == null || ids == null || ids.Length == 0) return;
                if (ids.Length > 400) return;   // защита от абсурдного размера

                using (var w = new FastBufferWriter(4 + ids.Length * 8, Allocator.Temp, 8192))
                {
                    w.WriteValueSafe(Wire);
                    w.WriteValueSafe(ids.Length);
                    foreach (var id in ids) w.WriteValueSafe(id);
                    nm.CustomMessagingManager.SendNamedMessage(ScanMsg, target, w, NetworkDelivery.ReliableFragmentedSequenced);
                }
            }
            catch (Exception e) { Plugin.Log?.LogWarning($"[net] сканы не ушли: {e.Message}"); }
        }

        private static void BroadcastScans()
        {
            var nm = NM;
            if (nm == null || !nm.IsServer || nm.ConnectedClientsIds == null) return;
            var all = ScanRegistry.Snapshot();
            if (all.Length == 0) return;
            foreach (var id in nm.ConnectedClientsIds)
            {
                if (id == NetworkManager.ServerClientId) continue;
                SendScans(all, id);
            }
        }

        private static void OnScan(ulong sender, FastBufferReader reader)
        {
            try
            {
                var nm = NM;
                if (nm == null) return;
                reader.ReadValueSafe(out byte wire);
                if (wire != Wire) return;
                reader.ReadValueSafe(out int n);
                if (n < 0 || n > 400) return;
                if (!Gate.ShareScans) return;   // обмен запрещён — чужие сканы не принимаем

                var ids = new ulong[n];
                for (int i = 0; i < n; i++) reader.ReadValueSafe(out ids[i]);

                if (nm.IsServer)
                {
                    // клиент прислал свои сканы — принимаем и раздадим остальным
                    int before = ScanRegistry.Count;
                    ScanRegistry.Merge(ids);
                    if (ScanRegistry.Count != before)
                    {
                        ScanRegistry.Dirty = true;
                        Plugin.Log?.LogInfo($"[scan] игрок {sender} поделился сканами (+{ScanRegistry.Count - before}).");
                    }
                }
                else
                {
                    if (sender != NetworkManager.ServerClientId) return;
                    ScanRegistry.Merge(ids);
                }
            }
            catch (Exception e) { Plugin.Log?.LogWarning($"[net] OnScan: {e.Message}"); }
        }

        // ================= состояние забега (таймер / ивенты) =================

        /// <summary>
        /// Хост раз в тик рассылает то, что должно совпадать у ВСЕХ: ход таймера,
        /// токен сброса и имена активных ивентов. Раньше таймер шёл у каждого свой
        /// (считался локально от своей посадки), а имена ивентов клиент пытался
        /// вычитать из панели BCME — там их нет.
        /// </summary>
        private static void BroadcastState()
        {
            try
            {
                var nm = NM;
                if (nm == null || nm.CustomMessagingManager == null || !nm.IsServer) return;
                if (nm.ConnectedClientsIds == null || nm.ConnectedClientsIds.Count <= 1) return;

                float sec = 0f; bool running = false;
                var om = OverlayManager.Instance;
                if (om != null) { sec = om.TimerSeconds; running = om.TimerRunning; }

                int deaths = GameState.GetDeaths();
                string ev = GameState.GetBrutalEvent() ?? "";
                if (ev.Length > 900) ev = ev.Substring(0, 900);

                byte flags = (byte)(running ? 1 : 0);
                int token = GameState.GetResetToken();

                using (var w = new FastBufferWriter(1024, Allocator.Temp, 4096))
                {
                    w.WriteValueSafe(Wire);
                    w.WriteValueSafe(flags);
                    w.WriteValueSafe(sec);
                    w.WriteValueSafe(token);
                    w.WriteValueSafe(deaths);
                    w.WriteValueSafe(ev);
                    foreach (var id in nm.ConnectedClientsIds)
                    {
                        if (id == NetworkManager.ServerClientId) continue;
                        nm.CustomMessagingManager.SendNamedMessage(StateMsg, id, w, NetworkDelivery.ReliableSequenced);
                    }
                }
            }
            catch (Exception e) { Plugin.Log?.LogWarning($"[net] состояние не ушло: {e.Message}"); }
        }

        /// <summary>Пришло состояние от хоста (только клиент).</summary>
        private static void OnState(ulong sender, FastBufferReader reader)
        {
            try
            {
                var nm = NM;
                if (nm == null || nm.IsServer) return;
                if (sender != NetworkManager.ServerClientId) return;

                reader.ReadValueSafe(out byte wire);
                if (wire != Wire) return;
                reader.ReadValueSafe(out byte flags);
                reader.ReadValueSafe(out float sec);
                reader.ReadValueSafe(out int token);
                reader.ReadValueSafe(out int deaths);
                reader.ReadValueSafe(out string ev);

                _hostTimerRunning = (flags & 1) != 0;
                _hostTimerSec = sec;
                _hostTimerAt = Time.unscaledTime;
                _hostResetToken = token;
                _hostDeaths = deaths;
                _hostEvents = string.IsNullOrEmpty(ev) ? null : ev;
                _hostStateAt = Time.unscaledTime;

                // хост есть и он с модом — значит панели разрешены
                if (State != Link.Granted) State = Link.Granted;
            }
            catch (Exception e) { Plugin.Log?.LogWarning($"[net] OnState: {e.Message}"); }
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

        /// <summary>Делиться ли сканами с отрядом — тоже решает хост.</summary>
        public static bool ShareScans =>
            OverlayNet.State == OverlayNet.Link.Granted
                ? P.ShareScans
                : ConfigSettings.ShareScans.Value;

        /// <summary>Забывать сканы каждый день — решает хост, чтобы лобби не разъезжалось.</summary>
        public static bool ResetScansDaily =>
            OverlayNet.State == OverlayNet.Link.Granted
                ? P.ResetScans
                : ConfigSettings.ResetScansEachDay.Value;

        /// <summary>Хост без мода — панели с подсказками погашены.</summary>
        public static bool Restricted => OverlayNet.State != OverlayNet.Link.Granted;
    }
}
