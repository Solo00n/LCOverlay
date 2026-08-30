using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace LCBridgeOverlay
{
    /// <summary>
    /// Кто уже просканирован — общий на всё лобби.
    ///
    /// Зачем отдельный реестр: раньше сканы помнились по GetInstanceID(), а этот
    /// номер свой у каждой машины, поэтому поделиться им было нельзя в принципе.
    /// Здесь ключ — NetworkObjectId: он одинаков у всех, кто в лобби, так что скан
    /// одного игрока честно доходит до остальных.
    ///
    /// Работает и для монстров, и для ловушек (турели, мины, шипы — тоже сетевые
    /// объекты).
    /// </summary>
    internal static class ScanRegistry
    {
        private static readonly HashSet<ulong> _ids = new HashSet<ulong>();
        private static readonly List<ulong> _pending = new List<ulong>();  // ещё не отправленные хосту

        /// <summary>Набор менялся — хосту стоит разослать его заново.</summary>
        public static bool Dirty { get; set; }

        public static bool Has(ulong id) => id != 0UL && _ids.Contains(id);

        public static int Count => _ids.Count;

        /// <summary>Отметить просканированным. local=true — это наш скан, его надо разослать.</summary>
        public static void Mark(ulong id, bool local)
        {
            if (id == 0UL) return;
            if (!_ids.Add(id)) return;
            Dirty = true;
            if (local) _pending.Add(id);
        }

        /// <summary>Влить набор, пришедший от хоста.</summary>
        public static void Merge(ulong[] ids)
        {
            if (ids == null) return;
            foreach (var id in ids) if (id != 0UL) _ids.Add(id);
        }

        /// <summary>Полностью заменить набор (авторитетный список хоста).</summary>
        public static void Replace(ulong[] ids)
        {
            _ids.Clear();
            Merge(ids);
        }

        public static ulong[] Snapshot()
        {
            var arr = new ulong[_ids.Count];
            _ids.CopyTo(arr);
            return arr;
        }

        /// <summary>Забрать наши свежие сканы для отправки хосту.</summary>
        public static ulong[] TakePending()
        {
            if (_pending.Count == 0) return null;
            var arr = _pending.ToArray();
            _pending.Clear();
            return arr;
        }

        public static void Clear()
        {
            _ids.Clear();
            _scannable.Clear();
            _pending.Clear();
            Dirty = true;
        }

        /// <summary>Сетевой номер объекта, или 0 если его нет.</summary>
        public static ulong IdOf(Component c)
        {
            try
            {
                if (c == null) return 0UL;
                var no = c.GetComponentInParent<NetworkObject>();
                if (no == null) no = c.GetComponentInChildren<NetworkObject>();
                return no != null && no.IsSpawned ? no.NetworkObjectId : 0UL;
            }
            catch { return 0UL; }
        }

        /// <summary>
        /// Есть ли у объекта узел сканирования вообще. Узлы висят на префабах, а не
        /// в коде, поэтому проверяем в рантайме: если сканировать нечего, требовать
        /// скан нельзя — иначе ловушка не показалась бы уже никогда.
        /// </summary>
        public static bool Scannable(Component c)
        {
            try
            {
                if (c == null) return false;
                int id = c.GetInstanceID();
                if (_scannable.TryGetValue(id, out bool has)) return has;
                has = c.GetComponentInChildren<ScanNodeProperties>(true) != null
                   || c.GetComponentInParent<ScanNodeProperties>() != null;
                _scannable[id] = has;
                return has;
            }
            catch { return false; }
        }

        private static readonly Dictionary<int, bool> _scannable = new Dictionary<int, bool>();

        /// <summary>Просканирован ли этот объект (по его сетевому номеру).</summary>
        public static bool HasFor(Component c) => Has(IdOf(c));

        /// <summary>Отметить объект просканированным нами.</summary>
        public static void MarkLocal(Component c) => Mark(IdOf(c), true);
    }
}
