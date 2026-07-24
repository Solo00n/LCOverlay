using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace LCBridgeOverlay
{
    /// <summary>
    /// Минимальный WebSocket-сервер (RFC 6455) без внешних зависимостей.
    /// Только то, что нужно: рукопожатие + отправка текстовых кадров серверкой клиенту.
    /// Входящие сообщения от клиента не обрабатываем — нам нужен только односторонний поток данных в оверлей.
    /// </summary>
    public static class BridgeServer
    {
        private static TcpListener _listener;
        private static Thread _acceptThread;
        private static volatile bool _running;
        private static readonly List<TcpClient> _clients = new List<TcpClient>();
        private static readonly object _lock = new object();

        // последний разосланный payload: отдаём его новому клиенту сразу после
        // рукопожатия, чтобы оверлей не ждал первого ИЗМЕНЕНИЯ данных
        // (в меню/на орбите payload может не меняться долго)
        private static volatile string _lastPayload;

        public static void Start(int port)
        {
            if (_running) return;
            _running = true;

            _listener = new TcpListener(IPAddress.Loopback, port);
            _listener.Start();

            _acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "LCBridge-Accept" };
            _acceptThread.Start();
        }

        public static void Stop()
        {
            _running = false;
            try { _listener?.Stop(); } catch { }
            lock (_lock)
            {
                foreach (var c in _clients)
                {
                    try { c.Close(); } catch { }
                }
                _clients.Clear();
            }
        }

        private static void AcceptLoop()
        {
            while (_running)
            {
                try
                {
                    var client = _listener.AcceptTcpClient();
                    if (Handshake(client))
                    {
                        // сразу шлём последнее состояние, чтобы клиент не ждал изменения
                        var last = _lastPayload;
                        if (last != null)
                        {
                            try
                            {
                                var frame = EncodeTextFrame(last);
                                client.GetStream().Write(frame, 0, frame.Length);
                            }
                            catch { }
                        }
                        lock (_lock) { _clients.Add(client); }
                        Plugin.Log?.LogInfo("Оверлей подключился к мосту.");
                    }
                    else
                    {
                        try { client.Close(); } catch { }
                    }
                }
                catch
                {
                    // listener остановлен или ошибка — выходим, если не работаем
                    if (!_running) break;
                }
            }
        }

        private static bool Handshake(TcpClient client)
        {
            try
            {
                var stream = client.GetStream();
                // читаем заголовки запроса
                var buffer = new byte[4096];
                int read = stream.Read(buffer, 0, buffer.Length);
                if (read <= 0) return false;
                string request = Encoding.UTF8.GetString(buffer, 0, read);

                // ищем Sec-WebSocket-Key
                string key = null;
                foreach (var line in request.Split(new[] { "\r\n" }, StringSplitOptions.None))
                {
                    if (line.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase))
                    {
                        key = line.Substring("Sec-WebSocket-Key:".Length).Trim();
                        break;
                    }
                }
                if (key == null) return false;

                string accept;
                using (var sha1 = SHA1.Create())
                {
                    var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"));
                    accept = Convert.ToBase64String(hash);
                }

                string response =
                    "HTTP/1.1 101 Switching Protocols\r\n" +
                    "Upgrade: websocket\r\n" +
                    "Connection: Upgrade\r\n" +
                    "Sec-WebSocket-Accept: " + accept + "\r\n\r\n";
                var respBytes = Encoding.UTF8.GetBytes(response);
                stream.Write(respBytes, 0, respBytes.Length);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Разослать текстовое сообщение всем подключённым клиентам.</summary>
        public static void Broadcast(string text)
        {
            _lastPayload = text;
            byte[] frame = EncodeTextFrame(text);
            List<TcpClient> dead = null;

            lock (_lock)
            {
                foreach (var c in _clients)
                {
                    try
                    {
                        if (!c.Connected) { (dead ??= new List<TcpClient>()).Add(c); continue; }
                        var s = c.GetStream();
                        s.Write(frame, 0, frame.Length);
                    }
                    catch
                    {
                        (dead ??= new List<TcpClient>()).Add(c);
                    }
                }
                if (dead != null)
                {
                    foreach (var d in dead) { _clients.Remove(d); try { d.Close(); } catch { } }
                }
            }
        }

        /// <summary>Кадрирование одного текстового кадра (FIN=1, opcode=0x1), сервер не маскирует.</summary>
        private static byte[] EncodeTextFrame(string text)
        {
            byte[] payload = Encoding.UTF8.GetBytes(text);
            int len = payload.Length;

            byte[] header;
            if (len <= 125)
            {
                header = new byte[2];
                header[1] = (byte)len;
            }
            else if (len <= ushort.MaxValue)
            {
                header = new byte[4];
                header[1] = 126;
                header[2] = (byte)((len >> 8) & 0xFF);
                header[3] = (byte)(len & 0xFF);
            }
            else
            {
                header = new byte[10];
                header[1] = 127;
                for (int i = 0; i < 8; i++)
                    header[9 - i] = (byte)((len >> (8 * i)) & 0xFF);
            }
            header[0] = 0x81; // FIN + text opcode

            var frame = new byte[header.Length + payload.Length];
            Buffer.BlockCopy(header, 0, frame, 0, header.Length);
            Buffer.BlockCopy(payload, 0, frame, header.Length, payload.Length);
            return frame;
        }
    }
}
