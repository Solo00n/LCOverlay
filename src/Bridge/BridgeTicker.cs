using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace LCBridgeOverlay
{
    /// <summary>
    /// Раз в секунду собирает состояние и рассылает JSON всем подключённым оверлеям.
    /// </summary>
    public class BridgeTicker : MonoBehaviour
    {
        private float _timer;
        private const float Interval = 1f;

        private string _lastPayload;
        private int _lastMobCount = -1;

        // Внешний сигнал «собери и разошли состояние ПРЯМО сейчас» — чтобы при
        // посадке/готовности уровня все данные (ивент, интерьер, монстры) появились
        // мгновенно и одним пакетом, а не подхватывались в течение секунды.
        private static volatile bool _forceNow;
        public static void ForceImmediate() { _forceNow = true; }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_forceNow) { _forceNow = false; _timer = Interval; } // тикнуть в этом кадре
            if (_timer < Interval) return;
            _timer = 0f;

            // пульс встроенного моста — обновляем каждый тик, даже если данные не менялись
            // (иначе глаз-индикатор в меню/на орбите ложно погаснет)
            DataParser.Heartbeat = Time.unscaledTime;

            // копим статистику забега (встреченные монстры) — независимо от отправки
            GameState.TickStats();
            RunStats.Tick();
            // состояния монстров (зол / трансформирован / на потолке / застыл).
            // Внутри свой интервал ~0.5с, чтобы не дёргать рефлексию каждый кадр.
            OverlayNet.Tick();
            MonsterState.Tick(GameState.GetAllLiveEnemies());

            string json = BuildJson();
            // шлём только при изменении, чтобы не спамить
            if (json != _lastPayload)
            {
                _lastPayload = json;
                // наружу — только если игрок сам включил мост (иначе сокет и не открыт)
                if (BridgeServer.IsRunning) BridgeServer.Broadcast(json);
                DataParser.PushLocal(json);         // → внутриигровой оверлей, напрямую
            }
        }

        private string BuildJson()
        {
            var (alive, total) = GameState.GetCrew();
            int deaths = GameState.GetDeaths();
            int hp = GameState.GetLocalHealth();
            string moon = GameState.GetMoonName();

            // погода: приоритет — WeatherTweaks (комбо), иначе ванильная
            string wt = GameState.GetWeatherTweaksWeather();
            string weather = !string.IsNullOrEmpty(wt) ? wt : GameState.GetVanillaWeather();

            // Всё, что даёт подсказку, собираем ТОЛЬКО с разрешения хоста. Гасим здесь,
            // у источника, а не при отрисовке: иначе данные всё равно утекли бы наружу
            // через WebSocket-мост в браузерный оверлей.
            string bevent = Gate.Events ? GameState.GetBrutalEvent() : null;
            var (outside, inside) = Gate.Monsters
                ? GameState.GetMonsters()
                : (new List<string>(), new List<string>());
            var traps = Gate.Traps ? GameState.GetTraps() : new List<string>();
            bool onMoon = GameState.GetOnMoon();
            bool loading = GameState.GetLoading();
            bool inGame = GameState.GetInGame();
            int resetToken = GameState.GetResetToken();
            int levelScrap = Gate.LevelScrap ? GameState.GetLevelScrap() : 0;
            // v1.2+: данные для внутриигрового оверлея (LCBridgeOverlay)
            var (quotaValue, quotaFulfilled) = GameState.GetQuotaProgress();
            int shipLoot = GameState.GetShipScrapSafe();
            int quotaIndex = GameState.GetQuotaIndexSafe();
            int dayCount = GameState.GetDayCount();
            int daysLeft = GameState.GetDaysLeft();
            string interior = Gate.Interior ? GameState.GetInterior() : null;
            var (beeHives, itemsInside, itemsOutside) = Gate.LevelLoot
                ? GameState.GetLootBreakdown()
                : (0, 0, 0);
            bool oldBird = GameState.GetOldBird();
            bool onShip = GameState.GetOnShip();
            string topKiller = GameState.GetTopKiller();
            string topMonster = GameState.GetTopMonster();
            string deadliestEvent = GameState.GetDeadliestEvent();
            // v1.5: новые аддитивные поля
            bool popup = GameExtras.PopupActive();
            bool storeAd = GameExtras.StoreAdActive();
            int endOfDaySec = Gate.Countdown ? GameExtras.SecondsToEndOfDay() : -1;
            bool apparatus = Gate.Apparatus && GameExtras.ApparatusInside();
            float lootMult = Gate.LootMult ? GameExtras.LootMultiplier() : 1f;
            int soldLoot = RunStats.SoldTotal;

            // диагностика: логируем счётчики при изменении, чтобы видеть что мост реально находит
            int totalMobs = outside.Count + inside.Count;
            if (totalMobs != _lastMobCount)
            {
                _lastMobCount = totalMobs;
                Plugin.Log?.LogInfo($"[monsters] улица={outside.Count} ({string.Join(",", outside)}) | комплекс={inside.Count} ({string.Join(",", inside)})");
            }

            var sb = new StringBuilder();
            sb.Append('{');
            sb.Append("\"type\":\"bridge\",");
            sb.Append("\"deaths\":").Append(deaths).Append(',');
            sb.Append("\"alive\":").Append(alive).Append(',');
            sb.Append("\"total\":").Append(total).Append(',');
            sb.Append("\"health\":").Append(hp).Append(',');
            sb.Append("\"moonName\":").Append(JsonStr(moon)).Append(',');
            sb.Append("\"weatherFull\":").Append(JsonStr(weather)).Append(',');
            sb.Append("\"brutalEvent\":").Append(JsonStr(bevent ?? "")).Append(',');
            sb.Append("\"onMoon\":").Append(onMoon ? "true" : "false").Append(',');
            sb.Append("\"loading\":").Append(loading ? "true" : "false").Append(',');
            sb.Append("\"inGame\":").Append(inGame ? "true" : "false").Append(',');
            sb.Append("\"resetToken\":").Append(resetToken).Append(',');
            sb.Append("\"levelScrap\":").Append(levelScrap).Append(',');
            // v1.2+: аддитивные поля (старые оверлеи их просто игнорируют)
            sb.Append("\"quotaValue\":").Append(quotaValue).Append(',');
            sb.Append("\"quotaFulfilled\":").Append(quotaFulfilled).Append(',');
            sb.Append("\"shipLoot\":").Append(shipLoot).Append(',');
            sb.Append("\"quotaIndex\":").Append(quotaIndex).Append(',');
            sb.Append("\"dayCount\":").Append(dayCount).Append(',');
            sb.Append("\"daysLeft\":").Append(daysLeft).Append(',');
            sb.Append("\"interiorType\":").Append(JsonStr(interior ?? "")).Append(',');
            sb.Append("\"beehiveCount\":").Append(beeHives).Append(',');
            sb.Append("\"itemsInside\":").Append(itemsInside).Append(',');
            sb.Append("\"itemsOutside\":").Append(itemsOutside).Append(',');
            sb.Append("\"hasOldBird\":").Append(oldBird ? "true" : "false").Append(',');
            sb.Append("\"onShip\":").Append(onShip ? "true" : "false").Append(',');
            // v1.5: аддитивные поля
            sb.Append("\"popupActive\":").Append(popup ? "true" : "false").Append(',');
            sb.Append("\"storeAdActive\":").Append(storeAd ? "true" : "false").Append(',');
            sb.Append("\"endOfDaySec\":").Append(endOfDaySec).Append(',');
            sb.Append("\"apparatusInside\":").Append(apparatus ? "true" : "false").Append(',');
            sb.Append("\"lootMultiplier\":").Append(lootMult.ToString("0.##", CultureInfo.InvariantCulture)).Append(',');
            sb.Append("\"soldLoot\":").Append(soldLoot).Append(',');
            sb.Append("\"topKiller\":").Append(JsonStr(topKiller ?? "")).Append(',');
            sb.Append("\"topMonster\":").Append(JsonStr(topMonster ?? "")).Append(',');
            sb.Append("\"deadliestEvent\":").Append(JsonStr(deadliestEvent ?? "")).Append(',');
            sb.Append("\"monstersOutside\":").Append(JsonArr(outside)).Append(',');
            sb.Append("\"monstersInside\":").Append(JsonArr(inside)).Append(',');
            sb.Append("\"traps\":").Append(JsonArr(traps)).Append(',');
            sb.Append("\"run\":").Append(RunStats.ToJson());
            sb.Append('}');
            return sb.ToString();
        }

        private static string JsonArr(System.Collections.Generic.List<string> items)
        {
            var sb = new StringBuilder();
            sb.Append('[');
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(JsonStr(items[i]));
            }
            sb.Append(']');
            return sb.ToString();
        }

        private static string JsonStr(string s)
        {
            if (s == null) return "\"\"";
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
