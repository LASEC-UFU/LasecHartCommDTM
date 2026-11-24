using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace HartEngine
{
    /// <summary>
    /// HART-IP (FieldComm Group, porta 5094, UDP ou TCP) — o acesso padrão a gateways WirelessHART,
    /// multiplexadores HART-IP e instrumentos com HART-IP nativo.
    ///
    /// Mensagem = cabeçalho de 8 bytes + corpo:
    ///   versão (1) | tipo (0 pedido, 1 resposta, 2 publicação, 3 erro, 15 NAK) | id (0 iniciar sessão,
    ///   1 fechar sessão, 2 keep-alive, 3 PDU HART) | status | sequência (2) | tamanho total (2).
    /// O corpo de um PDU HART é o quadro token-passing a partir do delimitador, sem preâmbulos.
    /// Iniciar sessão: tipo de mestre (1 = primário) + tempo de inatividade em ms (4 bytes).
    /// </summary>
    public sealed class HartIpTransport : IHartTransport
    {
        public const int DefaultPort = 5094;
        private const byte Version = 1;
        private const byte TypeRequest = 0, TypeResponse = 1, TypePublish = 2, TypeError = 3, TypeNak = 15;
        private const byte IdSessionInitiate = 0, IdSessionClose = 1, IdKeepAlive = 2, IdPdu = 3;

        private readonly HartLinkSettings _s;
        private readonly HartCommLog _log;
        private readonly bool _tcp;
        private readonly object _io = new object();
        private UdpClient _udp;
        private IPEndPoint _server;
        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private ushort _seq;
        private Timer _keepAlive;
        private long _lastActivity;
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        public HartIpTransport(HartLinkSettings settings, HartCommLog log, bool useTcp)
        {
            _s = settings.Clone();
            _log = log ?? new HartCommLog();
            _tcp = useTcp;
        }

        public string Description => _s.Describe();
        public bool IsOpen { get; private set; }
        /// <summary>Tempo de inatividade aceito pelo servidor na abertura da sessão.</summary>
        public uint NegotiatedInactivityMs { get; private set; }

        public void Open()
        {
            if (IsOpen) return;
            IPAddress ip = Resolve(_s.HostAddress);
            _server = new IPEndPoint(ip, _s.HostPort);
            try
            {
                if (_tcp)
                {
                    _tcpClient = new TcpClient(ip.AddressFamily) { NoDelay = true };
                    var ar = _tcpClient.BeginConnect(ip, _s.HostPort, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(_s.EffectiveResponseTimeoutMs)) throw new TimeoutException("tempo esgotado na conexão TCP");
                    _tcpClient.EndConnect(ar);
                    _stream = _tcpClient.GetStream();
                }
                else
                {
                    _udp = new UdpClient(ip.AddressFamily);
                }

                uint inactivity = (uint)Math.Max(10000, _s.InactivityTimeoutMs);
                var body = new byte[5];
                body[0] = (byte)(_s.PrimaryMaster ? 1 : 0);
                body[1] = (byte)(inactivity >> 24); body[2] = (byte)(inactivity >> 16); body[3] = (byte)(inactivity >> 8); body[4] = (byte)inactivity;
                var resp = Exchange(IdSessionInitiate, body, _s.EffectiveResponseTimeoutMs, CancellationToken.None, rebindUdp: true);
                if (resp == null) throw new TimeoutException("o servidor não respondeu ao início de sessão");
                if (resp.Type != TypeResponse) throw new IOException($"início de sessão recusado (tipo {resp.Type}, status {resp.Status})");
                // Status 0 = sucesso; outros valores podem indicar que o servidor ajustou o tempo de inatividade.
                NegotiatedInactivityMs = resp.Body.Length >= 5 ? (uint)((resp.Body[1] << 24) | (resp.Body[2] << 16) | (resp.Body[3] << 8) | resp.Body[4]) : inactivity;
                if (resp.Status != 0) _log.Info($"HART-IP: início de sessão com status {resp.Status} (inatividade aceita: {NegotiatedInactivityMs} ms)");
            }
            catch (Exception ex)
            {
                CloseSockets();
                throw new HartTransportException($"HART-IP {(_tcp ? "TCP" : "UDP")} {_s.HostAddress}:{_s.HostPort}: {ex.Message}", ex);
            }
            IsOpen = true;
            Touch();
            int period = (int)Math.Max(2000, Math.Min(NegotiatedInactivityMs, 600000) / 3);
            _keepAlive = new Timer(_ => KeepAlive(), null, period, period);
            _log.Info($"Sessão HART-IP aberta: {Description} (servidor {_server})");
        }

        public void Close()
        {
            _keepAlive?.Dispose();
            _keepAlive = null;
            if (IsOpen)
            {
                lock (_io)
                {
                    try { Send(TypeRequest, IdSessionClose, new byte[0]); } catch { }
                }
                _log.Info("Sessão HART-IP fechada");
            }
            IsOpen = false;
            CloseSockets();
        }

        public void Dispose() => Close();

        public HartFrame Transact(HartFrame request, int preambles, int responseTimeoutMs, Func<HartFrame, bool> accept, CancellationToken ct)
        {
            if (!IsOpen) throw new HartTransportException("Sessão HART-IP fechada.");
            var pdu = request.Encode();
            _log.Tx(pdu, $"cmd {request.Command,3} → {request.Address} (HART-IP)");
            Message resp;
            lock (_io) resp = Exchange(IdPdu, pdu, responseTimeoutMs, ct, rebindUdp: false);
            if (resp == null) return null;
            if (resp.Type == TypeNak || resp.Type == TypeError)
            {
                _log.Error($"HART-IP: servidor respondeu {(resp.Type == TypeNak ? "NAK" : "erro")} status {resp.Status}");
                return null;
            }
            var f = HartFrame.Decode(resp.Body);
            if (f == null) { _log.Error($"HART-IP: PDU inválido ({Hex.Of(resp.Body)})"); return null; }
            if (f.Type != HartFrameType.Ack || !accept(f)) { _log.Rx(resp.Body, "resposta de outro endereço/comando (descartada)"); return null; }
            _log.Rx(resp.Body, $"cmd {f.Command,3} ← rc {f.ResponseCode} st {f.DeviceStatus:X2}");
            return f;
        }

        // ------------------------------------------------------------ mensagens

        private sealed class Message
        {
            public byte Type, Id, Status;
            public ushort Seq;
            public byte[] Body;
        }

        private void Touch() => Interlocked.Exchange(ref _lastActivity, _clock.ElapsedMilliseconds);

        private void KeepAlive()
        {
            if (!IsOpen || _clock.ElapsedMilliseconds - Interlocked.Read(ref _lastActivity) < NegotiatedInactivityMs / 3) return;
            if (!Monitor.TryEnter(_io)) return; // transação em andamento já mantém a sessão
            try
            {
                var r = Exchange(IdKeepAlive, new byte[0], _s.EffectiveResponseTimeoutMs, CancellationToken.None, rebindUdp: false);
                if (r == null) _log.Error("HART-IP: keep-alive sem resposta");
            }
            catch (Exception ex) { _log.Error("HART-IP: keep-alive falhou: " + ex.Message); }
            finally { Monitor.Exit(_io); }
        }

        private ushort Send(byte type, byte id, byte[] body)
        {
            ushort seq = unchecked(++_seq);
            int len = 8 + body.Length;
            var m = new byte[len];
            m[0] = Version; m[1] = type; m[2] = id; m[3] = 0;
            m[4] = (byte)(seq >> 8); m[5] = (byte)seq;
            m[6] = (byte)(len >> 8); m[7] = (byte)len;
            Buffer.BlockCopy(body, 0, m, 8, body.Length);
            if (_tcp) _stream.Write(m, 0, m.Length);
            else _udp.Send(m, m.Length, _server);
            Touch();
            return seq;
        }

        /// <summary>Envia um pedido e espera a resposta com o mesmo número de sequência.</summary>
        private Message Exchange(byte id, byte[] body, int timeoutMs, CancellationToken ct, bool rebindUdp)
        {
            ushort seq = Send(TypeRequest, id, body);
            var sw = Stopwatch.StartNew();
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                int left = timeoutMs - (int)sw.ElapsedMilliseconds;
                if (left <= 0) return null;
                var m = Receive(Math.Min(left, 200), rebindUdp);
                if (m == null) continue;
                Touch();
                if (m.Type == TypePublish) { _log.Rx(m.Body, "HART-IP publicação (ignorada)"); continue; }
                if (m.Seq != seq || m.Id != id) { _log.Info($"HART-IP: mensagem fora de sequência descartada (id {m.Id} seq {m.Seq})"); continue; }
                return m;
            }
        }

        private Message Receive(int timeoutMs, bool rebindUdp)
        {
            byte[] raw;
            if (_tcp)
            {
                _tcpClient.ReceiveTimeout = timeoutMs;
                if (!_stream.DataAvailable)
                {
                    var sw = Stopwatch.StartNew();
                    while (!_stream.DataAvailable && sw.ElapsedMilliseconds < timeoutMs) Thread.Sleep(2);
                    if (!_stream.DataAvailable) return null;
                }
                var hdr = ReadExact(8);
                int len = (hdr[6] << 8) | hdr[7];
                if (hdr[0] < 1 || len < 8) throw new IOException("cabeçalho HART-IP inválido: " + Hex.Of(hdr));
                raw = hdr.Concat(ReadExact(len - 8)).ToArray();
            }
            else
            {
                if (_udp.Available == 0)
                {
                    if (!_udp.Client.Poll(timeoutMs * 1000, SelectMode.SelectRead)) return null;
                }
                var from = new IPEndPoint(IPAddress.Any, 0);
                try { raw = _udp.Receive(ref from); }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset) { return null; } // ICMP porta inalcançável
                if (!from.Address.Equals(_server.Address)) return null;
                // Servidores UDP costumam responder ao início de sessão a partir de uma porta nova, usada daí em diante.
                if (rebindUdp && from.Port != _server.Port)
                {
                    _log.Info($"HART-IP: sessão continua na porta {from.Port} do servidor");
                    _server = from;
                }
                if (raw.Length < 8) return null;
            }
            return new Message
            {
                Type = raw[1], Id = raw[2], Status = raw[3],
                Seq = (ushort)((raw[4] << 8) | raw[5]),
                Body = raw.Skip(8).Take(Math.Max(0, ((raw[6] << 8) | raw[7]) - 8)).ToArray(),
            };
        }

        private byte[] ReadExact(int n)
        {
            var b = new byte[n];
            int got = 0;
            while (got < n)
            {
                int r = _stream.Read(b, got, n - got);
                if (r <= 0) throw new IOException("conexão HART-IP encerrada pelo servidor");
                got += r;
            }
            return b;
        }

        private void CloseSockets()
        {
            try { _stream?.Dispose(); } catch { }
            try { _tcpClient?.Close(); } catch { }
            try { _udp?.Close(); } catch { }
            _stream = null; _tcpClient = null; _udp = null;
        }

        private static IPAddress Resolve(string host)
        {
            if (IPAddress.TryParse(host?.Trim(), out var ip)) return ip;
            try
            {
                var all = Dns.GetHostAddresses(host.Trim());
                return all.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork) ?? all.First();
            }
            catch (Exception ex) { throw new HartTransportException($"Endereço '{host}' não encontrado: {ex.Message}", ex); }
        }
    }

    /// <summary>
    /// Ponte UDP simples (ex.: módulo Wi-Fi ligado a um modem HART): o quadro da linha, com preâmbulos, vai
    /// inteiro num datagrama para HostAddress:HostPort; a resposta volta em um ou mais datagramas.
    /// </summary>
    public sealed class UdpBridgeTransport : IHartTransport
    {
        private readonly HartLinkSettings _s;
        private readonly HartCommLog _log;
        private UdpClient _udp;
        private IPEndPoint _remote;

        public UdpBridgeTransport(HartLinkSettings settings, HartCommLog log)
        {
            _s = settings.Clone();
            _log = log ?? new HartCommLog();
        }

        public string Description => _s.Describe();
        public bool IsOpen => _udp != null;

        public void Open()
        {
            if (IsOpen) return;
            if (!IPAddress.TryParse(_s.HostAddress?.Trim(), out var ip))
            {
                try { ip = Dns.GetHostAddresses(_s.HostAddress.Trim()).First(a => a.AddressFamily == AddressFamily.InterNetwork); }
                catch (Exception ex) { throw new HartTransportException($"Endereço '{_s.HostAddress}' não encontrado: {ex.Message}", ex); }
            }
            _remote = new IPEndPoint(ip, _s.HostPort);
            _udp = new UdpClient(ip.AddressFamily);
            _log.Info($"Ponte UDP: {_remote}");
        }

        public void Close() { _udp?.Close(); _udp = null; }
        public void Dispose() => Close();

        public HartFrame Transact(HartFrame request, int preambles, int responseTimeoutMs, Func<HartFrame, bool> accept, CancellationToken ct)
        {
            if (!IsOpen) throw new HartTransportException("Ponte UDP fechada.");
            while (_udp.Available > 0) { var e = new IPEndPoint(IPAddress.Any, 0); _udp.Receive(ref e); }
            var raw = request.EncodeWithPreambles(preambles);
            _udp.Send(raw, raw.Length, _remote);
            _log.Tx(request.Encode(), $"cmd {request.Command,3} → {request.Address} (UDP)");
            var parser = new HartFrameParser(minPreambles: 0);
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < responseTimeoutMs)
            {
                ct.ThrowIfCancellationRequested();
                if (!_udp.Client.Poll(50 * 1000, SelectMode.SelectRead)) continue;
                var from = new IPEndPoint(IPAddress.Any, 0);
                byte[] d;
                try { d = _udp.Receive(ref from); } catch (SocketException) { continue; }
                parser.Append(d, d.Length);
                HartFrame f;
                while ((f = parser.TryRead()) != null)
                {
                    if (f.Type != HartFrameType.Ack) continue;
                    if (accept(f)) { _log.Rx(f.Encode(), $"cmd {f.Command,3} ← rc {f.ResponseCode} st {f.DeviceStatus:X2}"); return f; }
                }
            }
            return null;
        }
    }
}
