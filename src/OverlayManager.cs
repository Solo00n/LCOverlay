using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LCBridgeOverlay
{
    /// <summary>
    /// Управление оверлеем: Canvas (Screen Space - Overlay), позиционирование
    /// у правого края по центру, анимации появления (скольжение + fade, ~0.3с,
    /// ease-out), видимость и обновление виджетов. Данные приходят ТОЛЬКО из
    /// WebSocket LCBridge (раз в секунду) — перерисовка только при новом пакете.
    /// </summary>
    public class OverlayManager : MonoBehaviour
    {
        public static OverlayManager Instance { get; private set; }

        private const float PanelWidth = 340f;
        private const float SlideTime = 0.3f;
        private const float RailReserve = 92f; // место справа под рейку внутренних мобов (иконки + счётчики)
        private const float TiltDeg = 1f;      // наклон панели под HUD игры (база, не настраивается)

        private Canvas _canvas;
        private bool _dirty;

        internal OverlayStyle Style => S;
        private OverlayStyle S;

        // --- UI ---
        private RectTransform _root;
        private CanvasGroup _group;
        private Image _bgImage;
        private readonly List<Image> _frameImages = new List<Image>();
        private readonly List<PixbitFlicker> _pixbits = new List<PixbitFlicker>();
        private readonly List<TextMeshProUGUI> _allTexts = new List<TextMeshProUGUI>();
        private readonly List<TextMeshProUGUI> _bigTexts = new List<TextMeshProUGUI>();

        private TMP_FontAsset _fontBody, _fontBig;
        private TMP_FontAsset _dynFont;
        private bool _fontsResolved;
        private float _fontRetryT;
        private static readonly Dictionary<string, TMP_FontAsset> _osFontCache =
            new Dictionary<string, TMP_FontAsset>();

        private GameObject _headerGo, _headerDivider, _locationGo, _quotaGo, _dayDeathsGo, _tickerGo;
        private TextMeshProUGUI _timerText;
        private EyeWidget _topEye;     // глаз-индикатор связи сверху (закрывается при уходе с корабля)
        private TextMeshProUGUI _moonText, _interiorText, _itemsText, _oldBirdText;
        private Image _lampImg;                       // 2.14: иконка аппарата у интерьера
        private TextMeshProUGUI _multText;            // 2.11: суммарный множитель лута
        private TextMeshProUGUI _endOfDayText;        // 2.12: отсчёт до конца дня
        private readonly Image[] _qtabBgs = new Image[3];
        private readonly TextMeshProUGUI[] _qtabTexts = new TextMeshProUGUI[3];
        private TextMeshProUGUI _lootQuotaText, _barText;
        private GameObject _onPlanetGo;
        private TextMeshProUGUI _onPlanetVal;
        private RectTransform _barFill;
        private Image _barFillImg;
        private TextMeshProUGUI _dayText, _deathsText;

        // рейки монстров/ловушек и мини-плашка ивента
        private MobRailWidget _mobRail;
        private TrapFireEffect _trapFire;
        private EventPlateWidget _eventPlate;
        private RectTransform _eventPlateRt, _trapRailRt, _trapFxRt;
        private TextMeshProUGUI _eventText;

        private TickerWidget _ticker;
        private VictoryWidget _victory;

        // --- видимость / анимация ---
        private bool _userHidden;
        private float _vis;

        // --- затухание при неподвижной камере (параметры — в конфиге) ---
        private Quaternion _lastCamRot = Quaternion.identity;
        private float _idleT;
        private float _idleAlpha = 1f;
        private const float IdleFadeTime = 0.8f;  // скорость самого затухания (сек)

        // --- покачивание панели вслед за камерой (синергия с Camera Overhaul) ---
        private float _swayRoll;         // текущий доп-наклон панели, град (сглажен)
        private Vector2 _swayPos;        // текущий доп-сдвиг панели, px (сглажен)
        private Vector3 _lastCamFwd = Vector3.forward;
        private const float SwayRollFactor = 0.45f;  // доля крена камеры → в наклон панели
        private const float SwayMaxRoll = 5f;         // макс. доп-наклон, град
        private const float SwayDriftMax = 10f;       // макс. сдвиг «отставания», px
        private const float SwayRotSpeed = 9f;        // скорость догоняния наклона
        private const float SwayPosSpeed = 7f;        // скорость догоняния сдвига

        // --- таймер ---
        private float _timerSec;
        private bool _timerRunning;
        private bool? _prevWantRun;   // фронт для авто-таймера (onMoon && !loading)
        private int? _prevResetToken;
        private int? _lastQuotaIndex;

        private List<BcmerEvents.EventInfo> _events = new List<BcmerEvents.EventInfo>();

        // диагностика
        private bool _loggedFirstPacket, _loggedParseFail;
        private bool? _loggedOnShip;

        private static readonly string[] TurretEventKeys =
        {
            "turret", "турел", "berserk", "mobile", "everywhere",
            "toilhead", "toil", "hell", "quad", "artillery", "артилл"
        };

        private void Awake()
        {
            Instance = this;
            S = ConfigSettings.LegacyStyleActive ? OverlayStyle.Legacy() : OverlayStyle.Game();
            BuildUi();
            // Мост встроен в этот же мод (см. Bridge/*): данные приходят напрямую
            // через DataParser, без WebSocket-клиента. Если объект оверлея пересоздан
            // сторожком — сразу подхватываем уже собранное состояние.
            if (DataParser.Current != null) OnPayload(DataParser.Current);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            string json = DataParser.TakeLocal();
            if (json != null)
            {
                if (DataParser.TryParse(json))
                {
                    if (!_loggedFirstPacket)
                    {
                        _loggedFirstPacket = true;
                        var c = DataParser.Current;
                        Plugin.Log?.LogInfo($"Первый пакет моста разобран: moon={c.moonName}, onShip={c.onShip}, inGame={c.inGame}, quota={c.shipLoot}/{c.quotaValue}");
                    }
                    OnPayload(DataParser.Current);
                }
                else if (!_loggedParseFail)
                {
                    _loggedParseFail = true;
                    Plugin.Log?.LogWarning("Не удалось разобрать пакет моста (начало): " +
                        json.Substring(0, Math.Min(300, json.Length)));
                }
            }

            HandleInput();

            if (_timerRunning) _timerSec += Time.unscaledDeltaTime;

            if (!_fontsResolved)
            {
                _fontRetryT += Time.unscaledDeltaTime;
                if (_fontRetryT >= 1f)
                {
                    _fontRetryT = 0f;
                    EnsureFonts();
                    if (_fontsResolved) _dirty = true;
                }
            }

            UpdateVisibility(Time.unscaledDeltaTime);
            PositionTrapRail();

            // таймер обновляем КАЖДЫЙ кадр — с миллисекундами
            if (_root.gameObject.activeSelf && ConfigSettings.ShowTimer.Value)
            {
                _timerText.text = FmtTimeMs(_timerSec);
                _timerText.color = _timerRunning ? Color.white : new Color(1f, 1f, 1f, 0.6f);
            }

            if (_dirty)
            {
                _dirty = false;
                EnsureFonts();
                Refresh();
            }

            // перспектива запечена под старый размер панели — при изменении высоты
            // (баннер победы и т.п.) пересчитываем, иначе Q1/Q2/Q3 «уезжают» из рамок
            RewarpOnResize();
        }

        public void NotifyDisconnectedFromGame()
        {
            DataParser.Clear();
            _timerRunning = false;
            _prevWantRun = null;
            _victory?.Hide();
            _dirty = true;
        }

        // ==================== данные ====================

        private void OnPayload(BridgePayload p)
        {
            _dirty = true;

            if (_loggedOnShip != p.onShip)
            {
                _loggedOnShip = p.onShip;
                Plugin.Log?.LogInfo($"onShip={p.onShip} → панель {(p.onShip ? "разрешена" : "будет скрыта")} (AlwaysVisible={ConfigSettings.AlwaysVisible.Value})");
            }

            if (_prevResetToken != null && p.resetToken != _prevResetToken)
            {
                _timerSec = 0f;
                _timerRunning = false;
                _prevWantRun = null;
                _lastQuotaIndex = null;
                _marksShown = -1;      // метки троек квот пересоберутся
                _victory?.Hide();
            }
            _prevResetToken = p.resetToken;

            // 2.9: забег завершён (eject/банкротство) — держим аналитику прошлого забега
            // на экране, пока не дёрнут рычаг. Скрываем, как только рычаг дёрнули.
            if (ConfigSettings.ShowVictoryBanner.Value)
            {
                if (RunSnapshot.ShowLastRun && !_showingLastRun)
                {
                    _showingLastRun = true;
                    _victory?.Show(p, (int)_timerSec);
                }
                else if (!RunSnapshot.ShowLastRun && _showingLastRun)
                {
                    _showingLastRun = false;
                    _victory?.Hide();
                }
            }

            // Авто-таймер: идёт только пока мы РЕАЛЬНО на луне (высадка), не в орбите
            // и не на загрузке. Раньше гейтом был inGame (= shipHasLanded ||
            // travellingToNewLevel), из-за чего таймер продолжал тикать на орбите.
            // Теперь по фронту onMoon&&!loading запускаем/останавливаем (чтобы ручная
            // пауза не сбрасывалась каждый тик), и ЖЁСТКО гасим, когда мы не на луне.
            if (ConfigSettings.AutoTimer.Value)
            {
                bool wantRun = p.onMoon && !p.loading;
                if (wantRun != _prevWantRun)
                {
                    _timerRunning = wantRun;
                    _prevWantRun = wantRun;
                }
                if (!p.onMoon) _timerRunning = false; // орбита/корабль/меню — стоп
            }

            _events = BcmerEvents.GetEvents();
            if (_events.Count == 0 && !string.IsNullOrEmpty(p.brutalEvent))
            {
                foreach (var raw in p.brutalEvent.Split(','))
                {
                    string nm = raw.Trim();
                    if (nm.Length == 0) continue;
                    if (ConfigSettings.RussianActive) nm = EventTranslate.ToRu(nm);
                    _events.Add(new BcmerEvents.EventInfo { Name = nm, ColorHex = "#FFFFFF" });
                }
            }

            int qi = Mathf.Max(1, p.quotaIndex);
            // 2.1: аналитику показываем в конце КАЖДОЙ тройки квот (3, 6, 9, ...),
            // а не только после первой. Данные в RunStats копятся за весь забег,
            // поэтому баннер всегда накопительный.
            if (_lastQuotaIndex != null && qi > _lastQuotaIndex &&
                ConfigSettings.ShowVictoryBanner.Value)
            {
                int triplesNow = (qi - 1) / 3;
                int triplesWas = (_lastQuotaIndex.Value - 1) / 3;
                if (triplesNow > triplesWas && triplesNow >= 1)
                    _victory.Show(p, (int)_timerSec);
            }
            _lastQuotaIndex = qi;
        }

        // ==================== ввод ====================

        private void HandleInput()
        {
            if (IsTypingChat()) return;

            if (KeyPressed(ConfigSettings.ToggleKeyParsed))
            {
                // во время рекламы магазина показ заблокирован (2.6)
                if (_adBlocked) return;
                var p = DataParser.Current;
                bool onShip = p != null && p.onShip;
                if (ConfigSettings.AlwaysVisible.Value || onShip)
                    _userHidden = !_userHidden;
            }
            if (KeyPressed(ConfigSettings.TimerPauseKeyParsed))
                _timerRunning = !_timerRunning;
            if (KeyPressed(ConfigSettings.TimerResetKeyParsed))
                _timerSec = 0f;
        }

        private static bool IsTypingChat()
        {
            try
            {
                var lp = GameNetworkManager.Instance?.localPlayerController;
                return lp != null && lp.isTypingChat;
            }
            catch { return false; }
        }

        private static bool KeyPressed(Key k)
        {
            if (k == Key.None) return false;
            var kb = Keyboard.current;
            if (kb == null) return false;
            try { return kb[k].wasPressedThisFrame; } catch { return false; }
        }

        // ==================== видимость ====================

        // Плавное «уведение в фон» при паузе (Esc). Меню LC рисуется в режиме
        // Screen Space - Camera, а наш канвас — Screen Space - Overlay, который в
        // Unity ВСЕГДА поверх камерных канвасов, какой sortingOrder ни ставь.
        // Поэтому уйти строго ПОД меню нельзя — вместо этого приглушаем оверлей,
        // чтобы он оставался на заднем плане и не мешал меню.
        private const float PauseDimAlpha = 0.06f;
        private float _pauseFade; // 0 = обычный, 1 = приглушён под паузу
        private bool _adBlocked;      // идёт реклама магазина — ручной показ заблокирован (2.6)
        private bool _showingLastRun; // 2.9: на экране аналитика прошлого забега

        // ---- 2.1: метки пройденных троек квот ----
        private GameObject _marksGo;      // контейнер рядов меток
        private int _marksShown = -1;     // сколько меток уже нарисовано
        private const int MarksPerRow = 15;   // сколько влезает в ряд
        private const int MarksMax = 100;     // дальше счёт не ведём
        private const float MarkW = 12f, MarkH = 6f;

        /// <summary>
        /// Перестроить ряды меток: одна метка = одна пройденная тройка квот.
        /// В ряду MarksPerRow штук; после первого ряда метки меняют цвет.
        /// </summary>
        private void RebuildQuotaMarks(int triples)
        {
            if (_marksGo == null) return;
            triples = Mathf.Clamp(triples, 0, MarksMax);
            if (triples == _marksShown) return;
            _marksShown = triples;

            for (int i = _marksGo.transform.childCount - 1; i >= 0; i--)
                Destroy(_marksGo.transform.GetChild(i).gameObject);

            _marksGo.SetActive(triples > 0);
            if (triples <= 0) return;

            GameObject row = null;
            for (int i = 0; i < triples; i++)
            {
                if (i % MarksPerRow == 0)
                {
                    row = Row(_marksGo.transform, 3f);
                    row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
                    row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
                }
                var m = NewUI("Mark", row.transform);
                // после первого ряда — другой цвет (вышли за «стандартные» 15 троек)
                var col = i < MarksPerRow ? S.Danger : OverlayStyle.FromHex("FFB000");
                AddImage(m, col);
                var le = m.AddComponent<LayoutElement>();
                le.preferredWidth = MarkW; le.minWidth = MarkW;
                le.preferredHeight = MarkH; le.minHeight = MarkH;
                AddPerspective(m.GetComponent<Image>(), false);
            }
        }

        private void UpdateVisibility(float dt)
        {
            var p = DataParser.Current;
            bool connected = DataParser.Current != null && (Time.unscaledTime - DataParser.Heartbeat) < 5f;
            bool inSave = false, paused = false, spectating = false, leaving = false;
            try
            {
                var gnm = GameNetworkManager.Instance;
                var lp = gnm != null ? gnm.localPlayerController : null;
                inSave = lp != null;
                // мёртв и наблюдает за живыми (спектатор) — оверлей должен оставаться виден,
                // хотя игрок уже не «на корабле»
                spectating = lp != null && lp.isPlayerDead;
                if (lp != null && lp.quickMenuManager != null)
                    paused = lp.quickMenuManager.isMenuOpen;
                leaving = ShipLeavingNow();
            }
            catch { }
            // при паузе оверлей НЕ прячем — он остаётся на игровом плане, но уходит
            // ПОД меню эскейпа. Раньше вычисляли sortingOrder меню и ставили на 1 ниже,
            // но это было ненадёжно (меню иногда читалось не тем канвасом) → оверлей
            // вылезал поверх. Теперь при паузе уводим канвас на заведомо низкий порядок,
            // ниже любого игрового UI/меню; вне паузы — обратно наверх.
            if (_canvas != null) _canvas.sortingOrder = paused ? -1000 : 500;

            UpdateIdleFade(dt);

            // 2.4 / 2.6: игровые окна и реклама магазина. Реклама ЖЁСТЧЕ — пока она
            // идёт, оверлей нельзя вернуть клавишей (см. HandleInput).
            bool popup = ConfigSettings.HideOnPopups.Value && p != null && p.popupActive;
            _adBlocked = ConfigSettings.HideOnStoreAd.Value && p != null && p.storeAdActive;

            bool allowed = ConfigSettings.Enabled.Value && inSave && !leaving && !popup && !_adBlocked &&
                (ConfigSettings.AlwaysVisible.Value || (connected && (spectating || (p != null && p.onShip))));
            bool target = allowed && !_userHidden;

            // глаз-индикатор смыкается при уходе с корабля
            _topEye?.SetOpen(target ? 1f : 0f);

            _vis = Mathf.Clamp01(_vis + (target ? 1f : -1f) * (dt / SlideTime));
            float e = EaseOutCubic(_vis);

            float hiddenX = PanelWidth * ConfigSettings.Scale.Value + 200f;
            // отступ справа: не меньше RailReserve, чтобы правая рейка мобов помещалась
            float shownX = -Mathf.Max(ConfigSettings.RightOffsetPx.Value, RailReserve);
            // покачивание вслед за камерой (наклон + «отставание» панели)
            UpdateCameraSway(dt, e);
            _root.anchoredPosition = new Vector2(Mathf.Lerp(hiddenX, shownX, e), 0f) + _swayPos;
            // плавно приглушаем на паузе (см. PauseDimAlpha) — уходит в фон под меню Esc
            _pauseFade = Mathf.MoveTowards(_pauseFade, paused ? 1f : 0f, dt / 0.2f);
            float pauseMul = Mathf.Lerp(1f, PauseDimAlpha, _pauseFade);
            _group.alpha = e * _idleAlpha * pauseMul; // + затухание при неподвижной камере

            bool anyVisible = _vis > 0.001f;
            if (_root.gameObject.activeSelf != anyVisible)
            {
                _root.gameObject.SetActive(anyVisible);
                Plugin.Log?.LogInfo(anyVisible ? "Оверлей появляется." : "Оверлей скрыт.");
                if (anyVisible) _dirty = true;
            }
        }

        private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

        // Прячем оверлей ТОЛЬКО во время ВЗЛЁТА с луны (moon→orbit) — когда игра
        // показывает по центру экрана инфу, пока снова не разрешат дёрнуть рычаг.
        // ВАЖНО: рычаг дёргают ДВАЖДЫ — чтобы приземлиться (скрывать НЕ нужно) и
        // чтобы улететь (нужно). Взлёт уникально помечается shipIsLeaving; посадка
        // идёт через travellingToNewLevel и shipIsLeaving НЕ ставит. Поэтому держим
        // фазу взлёта отдельным флагом от старта shipIsLeaving до готовности рычага.
        private static StartMatchLever _lever;
        private bool _takingOff;
        private bool ShipLeavingNow()
        {
            try
            {
                var sor = StartOfRound.Instance;
                if (sor == null) { _takingOff = false; return false; }

                if (sor.shipIsLeaving) _takingOff = true;               // взлетаем с луны
                if (sor.shipHasLanded || sor.travellingToNewLevel)
                    _takingOff = false;                                 // сели / летим на новую луну — не взлёт

                if (!_takingOff) return false;

                // держим скрытым, пока снова не разрешат дёрнуть рычаг отлёта
                if (_lever == null) _lever = UnityEngine.Object.FindObjectOfType<StartMatchLever>();
                bool leverReady = _lever != null && _lever.triggerScript != null && _lever.triggerScript.interactable;
                if (leverReady) { _takingOff = false; return false; }
                return true;
            }
            catch { return false; }
        }

        // затухание оверлея, когда камера долго неподвижна
        private void UpdateIdleFade(float dt)
        {
            if (!ConfigSettings.FadeWhenIdle.Value) { _idleAlpha = 1f; return; }
            float moved = 999f;
            try
            {
                var lp = GameNetworkManager.Instance?.localPlayerController;
                var cam = lp != null ? lp.gameplayCamera : null;
                if (cam == null && Camera.main != null) cam = Camera.main;
                if (cam != null)
                {
                    moved = Quaternion.Angle(cam.transform.rotation, _lastCamRot);
                    _lastCamRot = cam.transform.rotation;
                }
            }
            catch { }
            if (moved > 0.4f) _idleT = 0f;          // камера движется → сбрасываем
            else _idleT += dt;
            float targetA = _idleT > ConfigSettings.IdleFadeSeconds.Value
                ? Mathf.Clamp01(ConfigSettings.IdleMinOpacity.Value) : 1f;
            _idleAlpha = Mathf.MoveTowards(_idleAlpha, targetA, dt / IdleFadeTime);
        }

        /// <summary>
        /// Покачивание панели вслед за камерой — синергия с Camera Overhaul и любыми
        /// модами, добавляющими динамический крен/наклон камеры (игровые меню так же
        /// «плывут» под неё, а наш ScreenSpaceOverlay стоит намертво).
        ///
        /// Читаем НАКЛОН самой камеры (независимо от того, чем он вызван):
        ///   roll = крен вокруг forward относительно горизонта (0 в ваниле);
        /// + небольшое «отставание» панели от вращения камеры. Оба сигнала сглажены
        /// и гаснут, когда камера стоит. Масштаб — CameraSwayStrength и видимость e.
        /// </summary>
        private void UpdateCameraSway(float dt, float e)
        {
            float strength = ConfigSettings.CameraSway.Value
                ? Mathf.Clamp(ConfigSettings.CameraSwayStrength.Value, 0f, 2f) : 0f;

            float rollTarget = 0f;
            Vector2 driftTarget = Vector2.zero;

            if (strength > 0f && e > 0.001f)
            {
                try
                {
                    var lp = GameNetworkManager.Instance?.localPlayerController;
                    var cam = lp != null ? lp.gameplayCamera : null;
                    if (cam == null && Camera.main != null) cam = Camera.main;
                    if (cam != null)
                    {
                        var tr = cam.transform;
                        Vector3 fwd = tr.forward;

                        // крен камеры относительно горизонта (что и делает Camera Overhaul)
                        Vector3 levelUp = Vector3.up - fwd * Vector3.Dot(Vector3.up, fwd);
                        if (levelUp.sqrMagnitude > 1e-4f)
                        {
                            float roll = Vector3.SignedAngle(levelUp.normalized, tr.up, fwd);
                            rollTarget = Mathf.Clamp(roll * SwayRollFactor, -SwayMaxRoll, SwayMaxRoll);
                        }

                        // «отставание» панели от поворота камеры (горизонт. и вертик.)
                        Vector3 d = fwd - _lastCamFwd;
                        Vector2 drift = new Vector2(
                            -Vector3.Dot(d, tr.right) * 900f,
                             Vector3.Dot(d, tr.up) * 900f);
                        driftTarget = Vector2.ClampMagnitude(drift, SwayDriftMax);
                        _lastCamFwd = fwd;
                    }
                }
                catch { }

                rollTarget *= strength * e;
                driftTarget *= strength * e;
            }

            _swayRoll = Mathf.Lerp(_swayRoll, rollTarget, 1f - Mathf.Exp(-SwayRotSpeed * dt));
            _swayPos = Vector2.Lerp(_swayPos, driftTarget, 1f - Mathf.Exp(-SwayPosSpeed * dt));

            // наклон панели = база (TiltDeg) + доп-крен от камеры
            _root.localRotation = Quaternion.Euler(0f, 0f, TiltDeg + _swayRoll);
        }

        // ==================== отрисовка ====================

        private void Refresh()
        {
            if (!_root.gameObject.activeSelf) return;
            var p = DataParser.Current;
            bool connected = DataParser.Current != null && (Time.unscaledTime - DataParser.Heartbeat) < 5f;
            bool onMoon = p != null && p.onMoon;

            bool showPanel = ConfigSettings.ShowPanel.Value;
            _bgImage.enabled = showPanel;
            foreach (var f in _frameImages) if (f != null) f.enabled = showPanel;
            foreach (var pb in _pixbits) if (pb != null) pb.Master = showPanel;
            _topEye.gameObject.SetActive(showPanel);
            _headerGo.SetActive(showPanel || ConfigSettings.ShowTimer.Value);
            _headerDivider.SetActive(showPanel);
            _timerText.transform.parent.gameObject.SetActive(ConfigSettings.ShowTimer.Value);
            _locationGo.SetActive(ConfigSettings.ShowLocation.Value);
            _quotaGo.SetActive(ConfigSettings.ShowQuota.Value);
            _dayDeathsGo.SetActive(ConfigSettings.ShowDayDeaths.Value);
            if (!ConfigSettings.ShowVictoryBanner.Value) _victory.Hide();

            // глаз-индикатор сверху: связь есть — свои цвета (рисунок), нет связи — притушен серым
            _topEye.Img.color = connected ? Color.white : new Color(0.5f, 0.5f, 0.5f, 1f);

            _timerText.text = FmtTime((int)_timerSec);
            _timerText.color = _timerRunning ? Color.white : new Color(1f, 1f, 1f, 0.6f);

            // ---- локация ----
            string moon = (p == null || string.IsNullOrEmpty(p.moonName)) ? "- -" : p.moonName;
            string weather = p != null ? ColorizeWeather(p.weatherFull) : "";
            _moonText.text = weather.Length > 0
                ? $"{Esc(moon.ToUpperInvariant())} <color=#{OverlayStyle.Hex(S.TextDim)}>//</color> {weather}"
                : Esc(moon.ToUpperInvariant());

            bool hasInterior = onMoon && !string.IsNullOrEmpty(p.interiorType);
            _interiorText.gameObject.SetActive(hasInterior);
            if (hasInterior)
                _interiorText.text = $"{Localization.T("interior")}: <b>{Esc(p.interiorType.ToUpperInvariant())}</b>";

            _itemsText.gameObject.SetActive(onMoon);
            if (onMoon)
                _itemsText.text = $"{Localization.T("items")}: {Localization.T("in")} <b>{p.itemsInside}</b> / " +
                                  $"{Localization.T("out")} <b>{p.itemsOutside}</b> / " +
                                  $"{Localization.T("hives")} <b>{p.beehiveCount}</b>";

            _oldBirdText.gameObject.SetActive(onMoon && p.hasOldBird);

            // 2.14: лампа (аппарат) — пока он в комплексе
            if (_lampImg != null)
                _lampImg.gameObject.SetActive(ConfigSettings.ShowApparatusIcon.Value &&
                                              hasInterior && p.apparatusInside &&
                                              _lampImg.sprite != null);

            // 2.11: суммарный множитель стоимости лута (показываем, если он не 1.0)
            if (_multText != null)
            {
                bool showMult = ConfigSettings.ShowLootMultiplier.Value && p.lootMultiplier > 0f &&
                                Mathf.Abs(p.lootMultiplier - 1f) > 0.01f;
                _multText.gameObject.SetActive(showMult);
                if (showMult)
                    _multText.text = $"{Localization.T("mult")} <b>x{p.lootMultiplier:0.##}</b>";
            }

            // 2.12: отсчёт до конца дня — последние 10 секунд
            if (_endOfDayText != null)
            {
                bool showEod = ConfigSettings.ShowEndOfDayCountdown.Value &&
                               onMoon && p.endOfDaySec >= 0 && p.endOfDaySec <= 10;
                _endOfDayText.gameObject.SetActive(showEod);
                if (showEod)
                    _endOfDayText.text = $"{Localization.T("endOfDay")} <b>{p.endOfDaySec}</b>";
            }

            // ---- квота ----
            int qi = p != null ? Mathf.Max(1, p.quotaIndex) : 1;
            // 2.1: блок показывает ТЕКУЩУЮ тройку. Пройденные тройки уходят в метки,
            // а подписи сдвигаются: Q1..Q3 → Q4..Q6 → ... (до Q28..Q30 и дальше).
            int triples = (qi - 1) / 3;                 // сколько троек уже пройдено
            int baseQ = triples * 3;                    // с какой квоты начинается текущая тройка
            int inTriple = qi - baseQ;                  // позиция внутри тройки: 1..3
            for (int i = 0; i < 3; i++)
            {
                bool done = i < inTriple - 1;
                bool active = !done && i == inTriple - 1;
                _qtabBgs[i].color = done ? S.Frame
                    : active ? OverlayStyle.WithA(S.Frame, S.LegacyCorners ? 0.10f : 0.22f)
                    : new Color(1f, 1f, 1f, S.LegacyCorners ? 0f : 0.06f);
                _qtabTexts[i].color = done ? (S.LegacyCorners ? Color.white : Color.black)
                    : active ? S.Accent : S.TextDim;
                string want = "Q" + (baseQ + i + 1);
                if (_qtabTexts[i].text != want) _qtabTexts[i].text = want;
            }
            RebuildQuotaMarks(triples);
            int quota = p != null ? Mathf.Max(0, p.quotaValue) : 0;
            int loot = p != null ? Mathf.Max(0, p.shipLoot) : 0;
            _lootQuotaText.text = $"<b>{loot}</b><color=#{OverlayStyle.Hex(S.TextDim)}>/</color>{quota}";
            float pct = quota > 0 ? (float)loot / quota : 0f;
            _barFill.anchorMax = new Vector2(Mathf.Clamp01(pct), 1f);
            _barFillImg.color = pct >= 1f ? S.Accent : OverlayStyle.WithA(S.Accent, 0.7f);
            _barFillImg.SetVerticesDirty(); // пересчёт перспективы под новую ширину заполнения
            // текст на полосе убран (без процентов/«есть»)

            bool showPlanet = onMoon && p.levelScrap > 0;
            _onPlanetGo.SetActive(showPlanet);
            if (showPlanet)
                _onPlanetVal.text = "$" + p.levelScrap;

            // ---- день и смерти ----
            int day = p != null ? p.dayCount : 1;
            int deaths = p != null ? p.deaths : 0;
            string daysLeft = (p != null && p.daysLeft >= 0)
                ? $" <size=55%><color=#{OverlayStyle.Hex(S.TextDim)}>({p.daysLeft} {Localization.T("left")})</color></size>" : "";
            _dayText.text = day + daysLeft;
            _deathsText.text = deaths.ToString();

            // ---- монстры (иконки по бортам) + ловушки снизу ----
            bool showMon = ConfigSettings.ShowMonsters.Value;
            _mobRail.SetMobs(showMon ? p?.monstersOutside : null, showMon ? p?.monstersInside : null);
            var traps = ConfigSettings.ShowTraps.Value ? p?.traps : null;
            _mobRail.SetTraps(traps);
            _trapFire.Firing = ConfigSettings.ShowTraps.Value && traps != null && traps.Length > 0 &&
                               onMoon && HasTurretTrap(traps) && TurretEventActive();

            // ---- мини-плашка ивента (выпадает на луне) ----
            RefreshEventPlate(onMoon);

            // ---- тикер ----
            RefreshTicker(p, connected, moon, qi, day, deaths);
        }

        private void RefreshEventPlate(bool onMoon)
        {
            bool cfgOn = ConfigSettings.ShowBrutalEvent.Value;
            bool show = cfgOn && onMoon && (_events.Count > 0 || BcmerEvents.BcmePresent());
            _eventPlate.SetVisible(show);
            if (!show) return;

            if (_events.Count == 0)
            {
                _eventText.text = $"<color=#{OverlayStyle.Hex(S.TextDim)}>{Localization.T("noData")}</color>";
                return;
            }

            var list = _events;
            if (!ConfigSettings.ShowAllEvents.Value && list.Count > 1)
                list = list.GetRange(0, 1);

            var sb = new StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append("<color=#").Append(OverlayStyle.Hex(S.TextDim)).Append(">, </color>");
                sb.Append("<color=").Append(list[i].ColorHex).Append('>')
                  .Append(Esc(list[i].Name.ToUpperInvariant()))
                  .Append("</color>");
            }
            _eventText.text = sb.ToString();
        }

        private void RefreshTicker(BridgePayload p, bool connected, string moon, int qi, int day, int deaths)
        {
            bool on = ConfigSettings.ShowTicker.Value;
            _tickerGo.SetActive(on);
            if (!on) return;

            string wx = p != null && !string.IsNullOrEmpty(p.weatherFull) ? p.weatherFull : "-";
            string crew = p != null && p.total > 0 ? $"{p.alive}/{p.total}" : "-";
            var sb = new StringBuilder(160);
            sb.Append(Localization.T("crew")).Append(": ").Append(crew)
              .Append(" // ").Append(Localization.T("tMoon")).Append(": ").Append(moon.ToUpperInvariant())
              .Append(" // ").Append(Localization.T("tWx")).Append(": ").Append(wx.ToUpperInvariant())
              .Append(" // ").Append(Localization.T("day")).Append(' ').Append(day)
              .Append(" // ").Append(Localization.T("tQuota")).Append(' ').Append(qi)
              .Append(" // ").Append(Localization.T("deaths")).Append(' ').Append(deaths);
            if (_events.Count > 0)
                sb.Append(" // ").Append(Localization.T("tEvent")).Append(": ")
                  .Append(_events[0].Name.ToUpperInvariant());
            if (!connected)
                sb.Append(" // ").Append(Localization.T("offline"));
            sb.Append(" // ").Append(Localization.T("tObjective")).Append(" //");
            _ticker.SetContent(sb.ToString());
        }

        private static bool HasTurretTrap(string[] traps)
        {
            if (traps == null) return false;
            foreach (var t in traps)
                if (t != null && t.IndexOf("turret", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }

        private bool TurretEventActive()
        {
            foreach (var ev in _events)
            {
                string n = (ev.Name ?? "").ToLowerInvariant();
                foreach (var key in TurretEventKeys)
                    if (n.Contains(key)) return true;
            }
            return false;
        }

        // ==================== хелперы отображения ====================

        private static readonly (string key, string hex)[] WxColors =
        {
            ("eclips", "#FF3A2E"),
            ("storm",  "#FFCF3A"),
            ("rain",   "#5FB6E6"),
            ("flood",  "#3FA9C9"),
            ("fog",    "#C8C2B0"),
            ("dust",   "#D9A05A"),
        };

        private string ColorizeWeather(string w)
        {
            if (string.IsNullOrEmpty(w) || w.Equals("None", StringComparison.OrdinalIgnoreCase)) return "";
            var parts = w.Split('+');
            var sb = new StringBuilder();
            foreach (var part in parts)
            {
                string raw = part.Trim();
                if (raw.Length == 0) continue;
                string low = raw.ToLowerInvariant();
                string hex = null;
                foreach (var (key, h) in WxColors)
                    if (low.Contains(key)) { hex = h; break; }
                if (sb.Length > 0) sb.Append($" <color=#{OverlayStyle.Hex(S.TextDim)}>+</color> ");
                if (hex != null) sb.Append("<color=").Append(hex).Append('>').Append(Esc(raw.ToUpperInvariant())).Append("</color>");
                else sb.Append(Esc(raw.ToUpperInvariant()));
            }
            return sb.ToString();
        }

        public static string Esc(string s)
        {
            return (s ?? "").Replace("<", "<noparse><</noparse>");
        }

        public static string FmtTime(int s)
        {
            if (s < 0) s = 0;
            int h = s / 3600, m = (s % 3600) / 60, sec = s % 60;
            return h > 0 ? $"{h:00}:{m:00}:{sec:00}" : $"{m:00}:{sec:00}";
        }

        /// <summary>Время с миллисекундами: mm:ss.mmm (или h:mm:ss.mmm).</summary>
        public static string FmtTimeMs(float s)
        {
            if (s < 0f) s = 0f;
            int total = (int)s;
            int h = total / 3600, m = (total % 3600) / 60, sec = total % 60;
            int ms = (int)((s - total) * 1000f);
            if (ms > 999) ms = 999;
            return h > 0 ? $"{h}:{m:00}:{sec:00}.{ms:000}" : $"{m:00}:{sec:00}.{ms:000}";
        }

        // ==================== шрифт ====================

        private void EnsureFonts()
        {
            if (_fontsResolved) return;
            try
            {
                TMP_FontAsset body = null, big = null;
                if (ConfigSettings.RussianActive)
                {
                    // для русского — чистый системный шрифт с кириллицей (Arial и т.п.);
                    // шрифт RTLC берём ТОЛЬКО если он явно так назван (иначе можно
                    // случайно схватить декоративный шрифт «языка стола зачарований»)
                    if (_dynFont == null) _dynFont = CreateDynamicCyrillicFont();
                    var cyr = _dynFont;
                    var rtlc = FindRtlcFont();
                    if (rtlc != null) cyr = rtlc;
                    body = big = cyr;
                }
                else
                {
                    var hud = HUDManager.Instance;
                    TMP_FontAsset game = (hud != null && hud.chatText != null) ? hud.chatText.font : null;
                    body = TryOsFont("Pixelify Sans") ?? game;
                    big = TryOsFont("Jersey 10") ?? game;
                }
                if (body == null && big == null) return;
                if (body == null) body = big;
                if (big == null) big = body;
                _fontBody = body;
                _fontBig = big;
                _fontsResolved = true;
                foreach (var t in _allTexts) if (t != null) t.font = body;
                foreach (var t in _bigTexts) if (t != null) t.font = big;
                Plugin.Log?.LogInfo($"Шрифты оверлея: заголовки={big.name}, текст={body.name}");
            }
            catch { }
        }

        /// <summary>Только явный шрифт RTLC с кириллицей (по имени). Иначе null → системный.</summary>
        private static TMP_FontAsset FindRtlcFont()
        {
            try
            {
                var all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                foreach (var f in all)
                {
                    if (f == null) continue;
                    string n = (f.name ?? "").ToLowerInvariant();
                    if (n.IndexOf("rtlc", System.StringComparison.Ordinal) < 0) continue;
                    bool hasCyr;
                    try { hasCyr = f.HasCharacter('Я') && f.HasCharacter('а'); }
                    catch { hasCyr = false; }
                    if (hasCyr) return f;
                }
            }
            catch { }
            return null;
        }

        private static TMP_FontAsset TryOsFont(string name)
        {
            if (_osFontCache.TryGetValue(name, out var cached)) return cached;
            TMP_FontAsset asset = null;
            try
            {
                var installed = Font.GetOSInstalledFontNames();
                bool has = false;
                if (installed != null)
                    foreach (var n in installed)
                        if (string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) { has = true; break; }
                if (has)
                {
                    var f = Font.CreateDynamicFontFromOSFont(name, 32);
                    if (f != null) asset = TMP_FontAsset.CreateFontAsset(f);
                }
            }
            catch { }
            _osFontCache[name] = asset;
            return asset;
        }

        private void ApplyBootstrapFont()
        {
            try
            {
                var any = TMP_Settings.defaultFontAsset;
                if (any == null)
                {
                    var all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                    if (all != null && all.Length > 0) any = all[0];
                }
                if (any != null)
                {
                    foreach (var t in _allTexts) if (t != null) t.font = any;
                    Plugin.Log?.LogInfo($"Временный шрифт оверлея: {any.name}");
                }
                else Plugin.Log?.LogWarning("Не найден ни один TMP-шрифт.");
            }
            catch { }
        }

        private static TMP_FontAsset CreateDynamicCyrillicFont()
        {
            // Arial/Segoe/Verdana гарантированно содержат кириллицу в Windows
            string[] names = { "Arial", "Segoe UI", "Verdana", "Tahoma", "Consolas" };
            foreach (var nm in names)
            {
                try
                {
                    var os = Font.CreateDynamicFontFromOSFont(nm, 28);
                    if (os == null) continue;
                    var fa = TMP_FontAsset.CreateFontAsset(os);
                    if (fa != null)
                    {
                        Plugin.Log?.LogInfo($"Русский шрифт оверлея: {nm}");
                        return fa;
                    }
                }
                catch { }
            }
            try
            {
                var os = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                      ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
                if (os != null) return TMP_FontAsset.CreateFontAsset(os);
            }
            catch { }
            return null;
        }

        // ==================== построение UI ====================

        private void BuildUi()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 500;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            var rootGo = NewUI("Root", transform);
            _root = (RectTransform)rootGo.transform;
            _root.anchorMin = _root.anchorMax = new Vector2(1f, 0.5f);
            _root.pivot = new Vector2(1f, 0.5f);
            _root.sizeDelta = new Vector2(PanelWidth, 100f);
            _root.localScale = Vector3.one * Mathf.Clamp(ConfigSettings.Scale.Value, 0.5f, 2f);
            // наклон под перспективу игрового HUD (шлема) — база оверлея, всегда
            _root.localRotation = Quaternion.Euler(0f, 0f, TiltDeg);

            _group = rootGo.AddComponent<CanvasGroup>();
            _group.interactable = false;
            _group.blocksRaycasts = false;
            _group.alpha = 0f;

            _bgImage = AddImage(rootGo, S.Bg);

            var v = rootGo.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(15, 15, 13, 11);
            v.spacing = 9f;
            v.childAlignment = TextAnchor.UpperLeft;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            var fit = rootGo.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            BuildHeader(rootGo.transform);
            BuildLocation(rootGo.transform);
            BuildQuota(rootGo.transform);
            BuildDayDeaths(rootGo.transform);
            BuildVictory(rootGo.transform);
            BuildTicker(rootGo.transform);

            BuildFrame(rootGo);
            BuildScanlines(rootGo);           // едва заметные горизонтальные полосы (CRT)
            BuildRails();                     // рейки монстров/ловушек по бортам
            BuildEventPlate();                // мини-плашка ивента снизу

            ApplyBootstrapFont();
            ApplyPerspective();
            _root.gameObject.SetActive(false);
        }

        // навешивает эффект перспективы на все графики панели (кроме рейков — они за краем)
        private void ApplyPerspective()
        {
            float s = ConfigSettings.PerspectiveStrength.Value;
            if (s <= 0f) return;
            var graphics = _root.GetComponentsInChildren<Graphic>(true);
            foreach (var g in graphics)
            {
                if (g == null) continue;
                // глаз-индикатор: искажаем ПЕРСПЕКТИВОЙ, но с пересчётом каждый кадр
                // (он масштабируется при закрытии, иначе меш-варп «съезжал»)
                if (_topEye != null && g.transform.IsChildOf(_topEye.transform))
                {
                    var ew = g.gameObject.AddComponent<PerspectiveWarp>();
                    ew.Panel = _root; ew.Width = PanelWidth; ew.Strength = s; ew.Continuous = true;
                    g.SetVerticesDirty();
                    continue;
                }

                bool ticker = _tickerGo != null && g.transform.IsChildOf(_tickerGo.transform);
                // рамки/уголки/пиксели сдвигаются при изменении размера панели, но их
                // меш не перегенерируется → перспектива устаёт. Пересчитываем каждый кадр.
                string nm = g.gameObject.name;
                bool frame = nm == "Corner" || nm == "Bracket" || nm == "Edge" || nm == "Pixbit";
                AddPerspective(g, ticker || frame);
            }
        }

        /// <summary>Навесить перспективу на графику (для панели и для рейков мобов).</summary>
        internal void AddPerspective(Graphic g, bool continuous)
        {
            float s = ConfigSettings.PerspectiveStrength.Value;
            if (s <= 0f || g == null) return;
            if (g is TMPro.TextMeshProUGUI)
            {
                var tw = g.gameObject.AddComponent<TMPPerspective>();
                tw.Panel = _root; tw.Width = PanelWidth; tw.Strength = s; tw.Continuous = continuous;
            }
            else
            {
                var w = g.gameObject.AddComponent<PerspectiveWarp>();
                w.Panel = _root; w.Width = PanelWidth; w.Strength = s; w.Continuous = continuous;
            }
            g.SetVerticesDirty();
        }

        /// <summary>
        /// Навесить перспективу на все графики поддерева, у которых её ещё нет.
        /// Нужно для ДИНАМИЧЕСКОГО контента (баннер победы), который создаётся уже
        /// после общего ApplyPerspective — иначе он остаётся плоским.
        /// </summary>
        internal void AddPerspectiveToTree(Transform root)
        {
            if (ConfigSettings.PerspectiveStrength.Value <= 0f || root == null) return;
            foreach (var g in root.GetComponentsInChildren<Graphic>(true))
            {
                if (g == null) continue;
                if (g.GetComponent<PerspectiveWarp>() != null || g.GetComponent<TMPPerspective>() != null) continue;
                AddPerspective(g, false);
            }
            _lastPanelH = -1f; // заставить перепривязать перспективу под новый размер панели
        }

        // Панель имеет пивот по центру (y=0.5): при изменении высоты (появился баннер
        // победы, скрылся блок и т.п.) ВСЕ элементы сдвигаются, а запечённая перспектива
        // остаётся от старой позиции → метки Q1/Q2/Q3 «уезжают» из рамок. Поэтому при
        // изменении высоты панели пересчитываем перспективу у всех элементов.
        private float _lastPanelH = -1f;

        private void RewarpOnResize()
        {
            if (ConfigSettings.PerspectiveStrength.Value <= 0f || _root == null) return;
            float h = _root.rect.height;
            if (Mathf.Abs(h - _lastPanelH) < 0.5f) return;
            _lastPanelH = h;
            foreach (var t in _root.GetComponentsInChildren<TMPro.TMP_Text>(true))
                if (t != null && t.GetComponent<TMPPerspective>() != null) t.ForceMeshUpdate();
            foreach (var g in _root.GetComponentsInChildren<Graphic>(true))
                if (g != null && !(g is TMPro.TextMeshProUGUI) && g.GetComponent<PerspectiveWarp>() != null)
                    g.SetVerticesDirty();
        }

        private static Texture2D _scanTex;
        /// <summary>Горизонтальные полосы (CRT/LSD-эффект как в ванильных меню).</summary>
        private void BuildScanlines(GameObject panel)
        {
            if (!ConfigSettings.Scanlines.Value) return;
            if (_scanTex == null)
            {
                // 1×3: тёмная строка + 2 прозрачные → полоса каждые 3px
                _scanTex = new Texture2D(1, 3, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
                _scanTex.SetPixels32(new Color32[]
                {
                    new Color32(0, 0, 0, 160), new Color32(0, 0, 0, 0), new Color32(0, 0, 0, 0),
                });
                _scanTex.Apply();
            }
            var go = NewUI("Scanlines", panel.transform);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            var img = go.AddComponent<RawImage>();
            img.texture = _scanTex;
            img.raycastTarget = false;
            img.color = new Color(1f, 1f, 1f, 0.5f);
            var uv = go.AddComponent<ScanlineUV>();
            uv.Img = img; uv.LinePx = 3f;
            go.transform.SetAsLastSibling();
        }

        // ---- «потёртости» рамок (grunge), как в ванильных меню ----
        private static Sprite _grunge;
        private static Sprite Grunge()
        {
            if (_grunge != null) return _grunge;
            const int N = 32;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float n = Mathf.PerlinNoise(x * 0.35f + 3.1f, y * 0.35f + 7.7f);
                    float n2 = Mathf.PerlinNoise(x * 0.9f + 11f, y * 0.9f + 2f);
                    byte a = 255;
                    if (n < 0.30f) a = 30;        // сколы (почти прозрачные)
                    else if (n2 < 0.28f) a = 140; // потёртости (полупрозрачные)
                    px[y * N + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px); tex.Apply();
            _grunge = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 1f);
            return _grunge;
        }

        private void BuildHeader(Transform parent)
        {
            _headerGo = Row(parent, 8f);
            // распорки слева и справа от глаза → он по центру пустого места между
            // левой рамкой и таймером (в потоке шапки → наклон/перспектива как у всего)
            var spacerL = NewUI("SpacerL", _headerGo.transform);
            Flexible(spacerL, 1f);
            _topEye = EclipseSun.BuildInlineEye(_headerGo.transform, 60f, 46f);
            var spacerR = NewUI("SpacerR", _headerGo.transform);
            Flexible(spacerR, 1f);

            // таймер: сплошная заливка цвета стиля (Legacy красная / Game синяя),
            // белые цифры, ровно по центру коробки
            var box = NewUI("TimerBox", _headerGo.transform);
            AddImage(box, S.Frame);
            // ФИКСИРОВАННАЯ ширина коробки: миллисекунды меняются каждый кадр и без
            // этого перекраивали бы шапку (таймер/глаз мигали «как перезагрузка»)
            var boxLE = box.AddComponent<LayoutElement>();
            boxLE.preferredWidth = 205f; boxLE.minWidth = 205f;
            var hl = box.AddComponent<HorizontalLayoutGroup>();
            // ассиметрия сверху/снизу компенсирует пустое место под цифрами (descent)
            hl.padding = new RectOffset(12, 12, 9, 3);
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = false;
            hl.childAlignment = TextAnchor.MiddleCenter;
            _timerText = MakeText(box.transform, "00:00", 28f, Color.white, TextAlignmentOptions.Center, big: true);
            _timerText.enableWordWrapping = false;        // цифры миллисекунд не переносятся вниз
            _timerText.overflowMode = TextOverflowModes.Overflow;

            _headerDivider = NewUI("Divider", parent);
            _headerDivider.AddComponent<LayoutElement>().preferredHeight = 2f;
            if (S.LegacyCorners)
            {
                for (float x = 0f; x < 312f; x += 10f)
                {
                    var dash = NewUI("Dash", _headerDivider.transform);
                    var drt = (RectTransform)dash.transform;
                    drt.anchorMin = drt.anchorMax = new Vector2(0f, 0.5f);
                    drt.pivot = new Vector2(0f, 0.5f);
                    drt.sizeDelta = new Vector2(6f, 2f);
                    drt.anchoredPosition = new Vector2(x, 0f);
                    AddImage(dash, OverlayStyle.WithA(S.Frame, 0.30f));
                }
            }
            else AddImage(_headerDivider, OverlayStyle.WithA(S.Frame, 0.5f));
        }

        private void BuildLocation(Transform parent)
        {
            _locationGo = Col(parent, 2f);
            MakeText(_locationGo.transform, Localization.T("location"), 13f, S.TextDim, bold: true);
            _moonText = MakeText(_locationGo.transform, "- -", 26f, S.Text, big: true);

            // 2.14: строка интерьера + иконка лампы (аппарата), пока он в комплексе
            var intRow = Row(_locationGo.transform, 5f);
            intRow.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
            _interiorText = MakeText(intRow.transform, "", 15f, S.Text);
            var lampGo = NewUI("Lamp", intRow.transform);
            _lampImg = lampGo.AddComponent<Image>();
            _lampImg.sprite = SpriteBank.Get("apparatus");
            _lampImg.preserveAspect = true;
            _lampImg.raycastTarget = false;
            var lampLe = lampGo.AddComponent<LayoutElement>();
            lampLe.preferredWidth = 22f; lampLe.minWidth = 22f;
            lampLe.preferredHeight = 16f; lampLe.minHeight = 16f;
            AddPerspective(_lampImg, false);
            lampGo.SetActive(false);

            _itemsText = MakeText(_locationGo.transform, "", 14f, S.TextDim);
            _oldBirdText = MakeText(_locationGo.transform, Localization.T("oldBird"), 17f, S.Danger, bold: true);
            _oldBirdText.gameObject.SetActive(false);
        }

        private void BuildQuota(Transform parent)
        {
            _quotaGo = Col(parent, 4f);
            MakeText(_quotaGo.transform, Localization.T("deadline"), 13f, S.TextDim, bold: true);

            var tabsRow = Row(_quotaGo.transform, 6f);
            tabsRow.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            for (int i = 0; i < 3; i++)
            {
                var cell = NewUI("QTab" + (i + 1), tabsRow.transform);
                _qtabBgs[i] = AddImage(cell, new Color(1f, 1f, 1f, 0.06f));
                var cle = cell.AddComponent<LayoutElement>();
                cle.flexibleWidth = 1f;
                cle.preferredHeight = 30f;
                if (S.LegacyCorners) AddBorder(cell, S.FrameDim, 2f);
                var t = MakeText(cell.transform, "Q" + (i + 1), 20f, S.TextDim, TextAlignmentOptions.Center, big: true);
                StretchInto(t.rectTransform);
                _qtabTexts[i] = t;
            }

            // 2.1: контейнер под ряды меток пройденных троек квот (заполняется в Refresh)
            _marksGo = Col(_quotaGo.transform, 3f);
            _marksGo.name = "QuotaMarks";
            _marksGo.SetActive(false);

            var lootRow = Row(_quotaGo.transform, 8f);
            var lbl = MakeText(lootRow.transform, Localization.T("lootQuota"), 14f, S.TextDim, bold: true);
            Flexible(lbl.gameObject, 1f);
            _lootQuotaText = MakeText(lootRow.transform, "0/0", 26f, S.Text, TextAlignmentOptions.Right, big: true);

            var bar = NewUI("Bar", _quotaGo.transform);
            AddImage(bar, OverlayStyle.WithA(S.Frame, S.LegacyCorners ? 0.05f : 0.15f));
            bar.AddComponent<LayoutElement>().preferredHeight = 18f;
            if (S.LegacyCorners) AddBorder(bar, S.Frame, 2f);
            var fillGo = NewUI("Fill", bar.transform);
            _barFill = (RectTransform)fillGo.transform;
            _barFill.anchorMin = Vector2.zero;
            _barFill.anchorMax = new Vector2(0f, 1f);
            _barFill.offsetMin = Vector2.zero;
            _barFill.offsetMax = Vector2.zero;
            _barFillImg = AddImage(fillGo, S.LegacyCorners ? S.Frame : OverlayStyle.WithA(S.Accent, 0.7f));
            _barText = MakeText(bar.transform, "", 13f, Color.white, TextAlignmentOptions.Center, bold: true);
            StretchInto(_barText.rectTransform);
            _barText.gameObject.SetActive(false); // без надписей на полосе (проценты/«есть» убраны)

            _onPlanetGo = Row(_quotaGo.transform, 6f);
            var opBorder = NewUI("LBorder", _onPlanetGo.transform);
            var ople = opBorder.AddComponent<LayoutElement>();
            ople.preferredWidth = 3f;
            ople.preferredHeight = 20f;
            AddImage(opBorder, S.Frame);
            var opLabel = MakeText(_onPlanetGo.transform, Localization.T("onPlanet"), 11f, S.TextDim, bold: true);
            Flexible(opLabel.gameObject, 1f);
            _onPlanetVal = MakeText(_onPlanetGo.transform, "$0", 20f, S.Text, TextAlignmentOptions.Right, big: true);
            _onPlanetGo.SetActive(false);

            // 2.11: суммарный множитель стоимости лута (погода + ивенты) одним числом
            _multText = MakeText(_quotaGo.transform, "", 13f, OverlayStyle.FromHex("FFB000"),
                                 TextAlignmentOptions.Right, bold: true);
            _multText.gameObject.SetActive(false);

            // 2.12: отсчёт до конца дня — крупно и заметно, появляется за 10 секунд
            _endOfDayText = MakeText(_quotaGo.transform, "", 20f, S.Danger,
                                     TextAlignmentOptions.Center, bold: true, big: true);
            _endOfDayText.gameObject.SetActive(false);
        }

        private void BuildDayDeaths(Transform parent)
        {
            _dayDeathsGo = Row(parent, 8f);

            TextMeshProUGUI MakeCell(string label, Color valColor)
            {
                var cell = Col(_dayDeathsGo.transform, 1f);
                var vlg = cell.GetComponent<VerticalLayoutGroup>();
                vlg.padding = new RectOffset(8, 8, 6, 7);
                vlg.childAlignment = TextAnchor.MiddleCenter;   // центрируем содержимое ячейки
                Flexible(cell, 1f);
                // рамка ячейки: Legacy — красная, Game — тонкая синяя (без заливки-затемнения)
                AddBorder(cell, S.LegacyCorners ? S.Frame : S.FrameDim, S.LegacyCorners ? 2f : 1f);
                MakeText(cell.transform, label, 12f, S.TextDim, TextAlignmentOptions.Center, bold: true);
                return MakeText(cell.transform, "0", 26f, valColor, TextAlignmentOptions.Center, big: true);
            }

            _dayText = MakeCell(Localization.T("day"), S.Text);
            _deathsText = MakeCell(Localization.T("deaths"), S.Danger);
        }

        private void BuildVictory(Transform parent)
        {
            var go = Col(parent, 4f);
            go.name = "Victory";
            _victory = go.AddComponent<VictoryWidget>();
            _victory.Init(this);
        }

        private void BuildTicker(Transform parent)
        {
            _tickerGo = NewUI("Ticker", parent);
            _tickerGo.AddComponent<LayoutElement>().preferredHeight = 20f;
            // Mask (стенсил) вместо RectMask2D — RectMask2D ломается при наклоне панели
            var maskImg = _tickerGo.AddComponent<Image>();
            maskImg.color = new Color(1f, 1f, 1f, 0.004f);
            maskImg.raycastTarget = false;
            var mask = _tickerGo.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var line = NewUI("Line", _tickerGo.transform);
            var lrt = (RectTransform)line.transform;
            lrt.anchorMin = new Vector2(0f, 1f);
            lrt.anchorMax = new Vector2(1f, 1f);
            lrt.pivot = new Vector2(0.5f, 1f);
            lrt.sizeDelta = new Vector2(0f, 2f);
            lrt.anchoredPosition = Vector2.zero;
            AddImage(line, OverlayStyle.WithA(S.Frame, 0.6f));

            var trackGo = NewUI("Track", _tickerGo.transform);
            var track = (RectTransform)trackGo.transform;
            track.anchorMin = new Vector2(0f, 0f);
            track.anchorMax = new Vector2(0f, 1f);
            track.pivot = new Vector2(0f, 0.5f);
            track.sizeDelta = new Vector2(4000f, 0f);
            track.anchoredPosition = Vector2.zero;

            var c1 = MakeTickerCopy(track);
            var c2 = MakeTickerCopy(track);

            _ticker = _tickerGo.AddComponent<TickerWidget>();
            _ticker.Init(track, c1, c2);
        }

        private TextMeshProUGUI MakeTickerCopy(Transform track)
        {
            var t = MakeText(track, "", 13f, S.TextDim);
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(400f, 18f);
            return t;
        }

        // ---- рейки монстров/ловушек (иконки снаружи панели) ----
        private void BuildRails()
        {
            RectTransform Rail(string name, Vector2 anchor, Vector2 pivot, Vector2 pos)
            {
                var go = NewUI(name, _root);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = anchor;
                rt.pivot = pivot;
                rt.sizeDelta = Vector2.zero;
                rt.anchoredPosition = pos;
                go.AddComponent<LayoutElement>().ignoreLayout = true;
                return rt;
            }

            // рейки на кромке панели (x=0, центр иконок на границе с фоном), обе от
            // ОДНОГО верха вниз — левая (улица) и правая (комплекс) выровнены по вертикали
            var left = Rail("MobLeft", new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -64f));
            var right = Rail("MobRight", new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -64f));
            // трап-рейка — по НИЖНЕЙ кромке: pivot по центру, иконки центрируются прямо
            // на линии (как монстры на боковых кромках), позицию ставит PositionTrapRail
            var trap = Rail("TrapRail", new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero);
            // ОТДЕЛЬНЫЙ слой для трассеров: идентичен трап-рейке по трансформу, но НЕ
            // очищается при перестройке ловушек — иначе пул трассеров уничтожался
            // вместе с иконками (это и вызывало ошибки при «стреляющих» ивентах).
            var trapFx = Rail("TrapFx", new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero);
            _trapRailRt = trap;
            _trapFxRt = trapFx;

            var host = NewUI("Rails", _root);
            host.AddComponent<LayoutElement>().ignoreLayout = true;
            _mobRail = host.AddComponent<MobRailWidget>();
            _mobRail.Init(this, left, right, trap);
            _mobRail.CountColor = S.Frame; // цифры-счётчики цвета стиля

            _trapFire = trapFx.gameObject.AddComponent<TrapFireEffect>();
            _trapFire.Init(trapFx, null, S.Accent);
            _trapFire.Emitters = _mobRail.TurretIcons;
        }

        // трап-рейка центрируется на нижней ЛИНИИ-кромке (граница фона оверлея и
        // экрана), как монстры на боковых кромках. Без ивента — на кромке панели;
        // при раскрытой плашке ивента линия опускается на её низ — рейка следует.
        private void PositionTrapRail()
        {
            if (_trapRailRt == null) return;
            float y = 0f; // на самой кромке панели
            if (_eventPlate != null && _eventPlate.gameObject.activeSelf && _eventPlateRt != null)
                y = _eventPlateRt.rect.height * _eventPlate.Progress; // на низ фона ивента
            _trapRailRt.anchoredPosition = new Vector2(0f, -y);
            if (_trapFxRt != null) _trapFxRt.anchoredPosition = new Vector2(0f, -y); // слой трассеров вслед за рейкой
        }

        // ---- мини-плашка ивента (выпадающее окно снизу) ----
        private void BuildEventPlate()
        {
            var go = NewUI("EventPlate", _root);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(PanelWidth - 24f, 56f);
            rt.anchoredPosition = new Vector2(0f, -14f); // с небольшим отступом от основного блока
            _eventPlateRt = rt;
            go.AddComponent<LayoutElement>().ignoreLayout = true;

            AddImage(go, S.Bg); // фон как у основного блока

            var v = go.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(16, 16, 9, 11);
            v.spacing = 2f;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            var fit = go.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            MakeText(go.transform, Localization.T("brutalEvent"), 13f, S.Danger, bold: true);
            _eventText = MakeText(go.transform, "", 22f, S.Text, big: true);

            AddStyleFrame(go, false); // та же рамка (уголки/скобки), что у основного блока

            _eventPlate = go.AddComponent<EventPlateWidget>();
            _eventPlate.Init(rt);
        }

        // ==================== рамки ====================

        /// <summary>Рамка панели: пиксельные уголки+пиксели (Legacy) или синие скобки (Game).</summary>
        private void BuildFrame(GameObject rootGo) => AddStyleFrame(rootGo, true);

        /// <summary>Тот же стиль рамки для любого блока (панель и плашка ивента).</summary>
        private void AddStyleFrame(GameObject host, bool full)
        {
            if (S.LegacyCorners)
            {
                AddCorner(host, new Vector2(0f, 1f));
                AddCorner(host, new Vector2(1f, 1f));
                AddCorner(host, new Vector2(0f, 0f));
                AddCorner(host, new Vector2(1f, 0f));
            }
            else AddBrackets(host, S.Frame);
            AddPixbits(host, full);
        }

        private void AddCorner(GameObject rootGo, Vector2 corner)
        {
            float sx = corner.x == 0f ? 1f : -1f;
            float sy = corner.y == 0f ? 1f : -1f;
            // уголки почти вплотную к фону (вынос 2px вместо 6), толщина 4
            AddCornerBar(rootGo, corner, new Vector2(26f, 4f), new Vector2(-sx * 2f, -sy * 2f));
            AddCornerBar(rootGo, corner, new Vector2(4f, 26f), new Vector2(-sx * 2f, -sy * 2f));
        }

        private void AddCornerBar(GameObject rootGo, Vector2 corner, Vector2 size, Vector2 offset)
        {
            var go = NewUI("Corner", rootGo.transform);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = corner;
            rt.pivot = corner;
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;
            var img = AddImage(go, S.Frame);
            img.sprite = Grunge(); img.type = Image.Type.Simple; // потёртости
            _frameImages.Add(img);
            go.AddComponent<LayoutElement>().ignoreLayout = true;
        }

        /// <summary>Синие уголковые «скобки» по углам (стиль игрового чата).</summary>
        private void AddBrackets(GameObject host, Color c)
        {
            const float len = 20f, th = 3f, off = 0f; // скобки вплотную к фону
            void Bracket(Vector2 corner)
            {
                float sx = corner.x == 0f ? 1f : -1f;
                float sy = corner.y == 0f ? 1f : -1f;
                Bar(host, corner, new Vector2(len, th), new Vector2(sx * off, sy * off), c);
                Bar(host, corner, new Vector2(th, len), new Vector2(sx * off, sy * off), c);
            }
            Bracket(new Vector2(0f, 1f));
            Bracket(new Vector2(1f, 1f));
            Bracket(new Vector2(0f, 0f));
            Bracket(new Vector2(1f, 0f));
        }

        private void Bar(GameObject host, Vector2 corner, Vector2 size, Vector2 offset, Color c)
        {
            var go = NewUI("Bracket", host.transform);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = corner;
            rt.pivot = corner;
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;
            var img = AddImage(go, c);
            img.sprite = Grunge(); img.type = Image.Type.Simple;
            _frameImages.Add(img);
            go.AddComponent<LayoutElement>().ignoreLayout = true;
        }

        private void AddBorder(GameObject go, Color c, float t)
        {
            MakeEdge(go, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, t), c);
            MakeEdge(go, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, t), c);
            MakeEdge(go, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(t, 0f), c);
            MakeEdge(go, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(t, 0f), c);
        }

        private static Image MakeEdge(GameObject parent, Vector2 aMin, Vector2 aMax, Vector2 thickness, Color c)
        {
            var go = NewUI("Edge", parent.transform);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = new Vector2(aMin.x == aMax.x ? aMin.x : 0.5f, aMin.y == aMax.y ? aMin.y : 0.5f);
            rt.sizeDelta = thickness;
            rt.anchoredPosition = Vector2.zero;
            var img = AddImage(go, c);
            img.sprite = Grunge(); img.type = Image.Type.Simple; // потёртости на всех рамках-линиях
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            return img;
        }

        /// <summary>
        /// Мерцающие «пиксели» точно на линиях рамки. Линии рамки проходят по
        /// центру уголковых полос — в 3.5px снаружи каждой кромки панели
        /// (полоса толщиной 5, вынесена на 6 → центр 6-2.5=3.5). Пиксели ставим
        /// на ту же координату, чтобы они были ровно напротив рамки.
        /// </summary>
        private void AddPixbits(GameObject rootGo, bool full)
        {
            const float E = 0f; // на кромке панели (рамка теперь вплотную)

            void Bit(Vector2 anchor, Vector2 pos, float period, float phase)
            {
                var go = NewUI("Pixbit", rootGo.transform);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = anchor;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(5f, 5f);
                rt.anchoredPosition = pos;
                var img = AddImage(go, S.Frame);
                go.AddComponent<LayoutElement>().ignoreLayout = true;
                var fl = go.AddComponent<PixbitFlicker>();
                fl.Init(img, period, phase);
                _pixbits.Add(fl);
            }

            // верхняя кромка (y = +E), продолжение уголков вправо/влево
            Bit(new Vector2(0f, 1f), new Vector2(32f, E), 1.1f, 0f);
            Bit(new Vector2(0f, 1f), new Vector2(42f, E), 1.5f, 0.4f);
            Bit(new Vector2(1f, 1f), new Vector2(-32f, E), 0.7f, 0.15f);
            Bit(new Vector2(1f, 1f), new Vector2(-42f, E), 0.9f, 0.6f);
            // нижняя кромка (y = -E)
            Bit(new Vector2(0f, 0f), new Vector2(32f, -E), 0.9f, 0.6f);
            Bit(new Vector2(0f, 0f), new Vector2(42f, -E), 0.7f, 0.15f);
            Bit(new Vector2(1f, 0f), new Vector2(-32f, -E), 1.1f, 0f);
            Bit(new Vector2(1f, 0f), new Vector2(-42f, -E), 1.5f, 0.4f);
            if (!full) return; // у мелких блоков (плашка ивента) — только уголки сверху/снизу
            // левая кромка (x = -E)
            Bit(new Vector2(0f, 1f), new Vector2(-E, -32f), 0.7f, 0.15f);
            Bit(new Vector2(0f, 0f), new Vector2(-E, 32f), 0.9f, 0.6f);
            // правая кромка (x = +E)
            Bit(new Vector2(1f, 1f), new Vector2(E, -32f), 1.5f, 0.4f);
            Bit(new Vector2(1f, 0f), new Vector2(E, 32f), 1.1f, 0f);
        }

        // ---- фабрика элементов ----

        internal static GameObject NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Image AddImage(GameObject go, Color c)
        {
            var im = go.AddComponent<Image>();
            im.color = c;
            im.raycastTarget = false;
            return im;
        }

        private GameObject Row(Transform parent, float spacing)
        {
            var go = NewUI("Row", parent);
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            return go;
        }

        private GameObject Col(Transform parent, float spacing)
        {
            var go = NewUI("Col", parent);
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childAlignment = TextAnchor.UpperLeft;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return go;
        }

        internal GameObject MakeCol(Transform parent, float spacing) => Col(parent, spacing);

        private static void Flexible(GameObject go, float w)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = w;
        }

        private static void StretchInto(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        }

        internal TextMeshProUGUI MakeText(Transform parent, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Left, bool bold = false, bool big = false)
        {
            var go = NewUI("Text", parent);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.enableWordWrapping = true;
            t.richText = true;
            t.raycastTarget = false;
            t.fontStyle = FontStyles.Bold;  // жирнее (ru и en) — параметр bold больше не нужен
            _ = bold;
            var f = big ? (_fontBig ?? _fontBody) : _fontBody;
            if (f != null) t.font = f;
            _allTexts.Add(t);
            if (big) _bigTexts.Add(t);
            return t;
        }
    }
}
