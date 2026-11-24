using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace HartEngine
{
    /// <summary>Resultado de uma transação: a resposta do instrumento ou o motivo da falha.</summary>
    public sealed class HartResult
    {
        public HartFrame Response { get; set; }
        public string Error { get; set; }
        public bool Ok => Response != null;
    }

    /// <summary>Instrumento encontrado numa varredura.</summary>
    public sealed class HartScanEntry
    {
        public HartIdentity Identity { get; set; }
        /// <summary>Endereço de polling (null para sub-dispositivos de gateway/multiplexador).</summary>
        public byte? PollingAddress { get; set; }
        public HartSubDevice SubDevice { get; set; }
        public HartAddress UniqueAddress => Identity?.UniqueAddress ?? SubDevice?.UniqueAddress;
        public string Tag => Identity?.Tag ?? SubDevice?.LongTag ?? "";
    }

    /// <summary>
    /// Mestre HART sobre um transporte: monta os quadros, repete em falha, trata respostas atrasadas
    /// (WirelessHART) e serializa o acesso ao meio. Não interpreta comandos além do necessário para
    /// identificar instrumentos (0, 13, 74 e 84 — todos de leitura).
    /// </summary>
    public sealed class HartMaster : IDisposable
    {
        private readonly object _lock = new object();
        private readonly HartLinkSettings _s;

        public HartMaster(IHartTransport transport, HartLinkSettings settings, HartCommLog log)
        {
            Transport = transport;
            _s = settings.Clone();
            Log = log ?? new HartCommLog();
        }

        public IHartTransport Transport { get; }
        public HartCommLog Log { get; }
        public HartLinkSettings Settings => _s;
        public int Transactions { get; private set; }
        public int Failures { get; private set; }

        public void Open() { lock (_lock) Transport.Open(); }
        public void Close() { lock (_lock) Transport.Close(); }
        public void Dispose() => Close();

        /// <summary>
        /// Envia um comando e espera a resposta. Repete <see cref="HartLinkSettings.RetryCount"/> vezes em silêncio
        /// ou erro de comunicação; em "ocupado" (32) e resposta atrasada (33/34) reenvia o mesmo pedido até concluir.
        /// </summary>
        public HartResult Send(HartAddress address, byte command, byte[] data, CancellationToken ct = default(CancellationToken),
                               int? preambles = null, int? retries = null, int? timeoutMs = null)
        {
            var req = HartFrame.Request(address, _s.PrimaryMaster, command, data);
            int pre = Math.Max(_s.PreambleCount, preambles ?? 0);
            int tries = 1 + Math.Max(0, retries ?? _s.RetryCount);
            int timeout = timeoutMs ?? _s.EffectiveResponseTimeoutMs;
            var drClock = Stopwatch.StartNew();
            string lastError = "sem resposta";
            lock (_lock)
            {
                if (!Transport.IsOpen)
                {
                    try { Transport.Open(); }
                    catch (HartTransportException ex) { Failures++; return new HartResult { Error = ex.Message }; }
                }
                for (int attempt = 1; attempt <= tries; attempt++)
                {
                    ct.ThrowIfCancellationRequested();
                    Transactions++;
                    HartFrame resp;
                    try { resp = Transport.Transact(req, pre, timeout, f => Matches(req, f), ct); }
                    catch (HartTransportException ex) { Failures++; return new HartResult { Error = ex.Message }; }
                    if (resp == null)
                    {
                        Failures++;
                        lastError = $"sem resposta em {timeout} ms";
                        if (attempt < tries) Log.Info($"cmd {command}: {lastError}; tentativa {attempt + 1} de {tries}");
                        continue;
                    }
                    if (resp.IsCommunicationError)
                    {
                        Failures++;
                        lastError = $"erro de comunicação informado pelo instrumento (0x{resp.ResponseCode:X2})";
                        if (attempt < tries) { Log.Info($"cmd {command}: {lastError}; repetindo"); continue; }
                        return new HartResult { Response = resp };
                    }
                    byte rc = resp.ResponseCode;
                    bool delayed = rc == HartResponseCodes.DelayedResponseInitiated || rc == HartResponseCodes.DelayedResponseRunning;
                    if ((delayed || rc == HartResponseCodes.Busy) && drClock.ElapsedMilliseconds < _s.DelayedResponseTimeoutMs)
                    {
                        // Resposta atrasada / ocupado: o mestre reenvia o mesmo pedido até receber o resultado.
                        attempt--;
                        Log.Info($"cmd {command}: {(rc == HartResponseCodes.Busy ? "instrumento ocupado" : "resposta atrasada em andamento")}; reenviando");
                        Thread.Sleep(delayed ? 1000 : 200);
                        continue;
                    }
                    return new HartResult { Response = resp };
                }
            }
            return new HartResult { Error = lastError };
        }

        /// <summary>A resposta é do instrumento endereçado, para o mesmo comando e o mesmo mestre.</summary>
        public static bool Matches(HartFrame req, HartFrame resp)
        {
            if (resp.Type != HartFrameType.Ack || resp.Command != req.Command || resp.LongAddress != req.LongAddress) return false;
            if (resp.PrimaryMaster != req.PrimaryMaster) return false;
            if (!req.LongAddress) return (resp.AddressBytes[0] & 0x3F) == (req.AddressBytes[0] & 0x3F);
            if ((resp.AddressBytes[0] & 0x3F) != (req.AddressBytes[0] & 0x3F)) return false;
            for (int i = 1; i < 5; i++) if (resp.AddressBytes[i] != req.AddressBytes[i]) return false;
            return true;
        }

        /// <summary>Comando 0 no endereço dado (curto ou longo). Retorna null se ninguém respondeu.</summary>
        public HartIdentity Identify(HartAddress address, CancellationToken ct, out string error, int? retries = null, int? timeoutMs = null)
        {
            var r = Send(address, 0, new byte[0], ct, retries: retries, timeoutMs: timeoutMs);
            if (!r.Ok) { error = r.Error; return null; }
            if (r.Response.IsCommunicationError || r.Response.ResponseCode != 0)
            {
                error = $"comando 0 respondeu com código {r.Response.ResponseCode}";
                return null;
            }
            var id = HartIdentity.FromCommand0(r.Response.Payload);
            error = id == null ? "resposta do comando 0 curta demais" : null;
            if (id != null && !address.IsLong) id.PollingAddress = address.PollingAddress;
            return id;
        }

        /// <summary>Tag curto (comando 13, packed ASCII).</summary>
        public string ReadTag(HartAddress unique, CancellationToken ct)
        {
            var r = Send(unique, 13, new byte[0], ct, retries: 1);
            if (!r.Ok || r.Response.ResponseCode != 0 || r.Response.Payload.Length < 6) return "";
            return HartText.UnpackAscii(r.Response.Payload, 0, 6);
        }

        /// <summary>
        /// Sub-dispositivos de um sistema de E/S (gateway WirelessHART, multiplexador): comando 74 para a quantidade
        /// e 84 para cada um. Lista vazia se o instrumento não é um sistema de E/S.
        /// </summary>
        public List<HartSubDevice> ReadSubDevices(HartAddress ioSystem, CancellationToken ct)
        {
            var list = new List<HartSubDevice>();
            var r74 = Send(ioSystem, 74, new byte[0], ct, retries: 1);
            if (!r74.Ok || r74.Response.ResponseCode != 0 || r74.Response.Payload.Length < 5) return list;
            int count = (r74.Response.Payload[3] << 8) | r74.Response.Payload[4]; // inclui o próprio sistema de E/S (índice 0)
            Log.Info($"sistema de E/S com {count - 1} sub-dispositivo(s)");
            for (int i = 1; i < count && i < 1000; i++)
            {
                ct.ThrowIfCancellationRequested();
                var r84 = Send(ioSystem, 84, new[] { (byte)(i >> 8), (byte)i }, ct, retries: 1);
                if (!r84.Ok || r84.Response.ResponseCode != 0) continue;
                var d = HartSubDevice.FromCommand84(r84.Response.Payload);
                if (d != null) list.Add(d);
            }
            return list;
        }

        /// <summary>Varredura dos endereços de polling (comando 0, uma tentativa cada) + sub-dispositivos.</summary>
        public List<HartScanEntry> Scan(int first, int last, CancellationToken ct, Action<int, string> progress = null)
        {
            var found = new List<HartScanEntry>();
            int total = last - first + 1;
            for (int a = first; a <= last; a++)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Invoke((a - first) * 100 / total, $"Endereço {a}");
                var id = Identify(HartAddress.Short((byte)a), ct, out _, retries: 0);
                if (id == null) continue;
                id.Tag = ReadTag(id.UniqueAddress, ct);
                Log.Info($"varredura: endereço {a}: {id}");
                found.Add(new HartScanEntry { Identity = id, PollingAddress = (byte)a });
                if (_s.ScanSubDevices)
                    foreach (var sd in ReadSubDevices(id.UniqueAddress, ct))
                    {
                        Log.Info($"varredura: sub-dispositivo {sd.Index} '{sd.LongTag}' tipo {sd.ExpandedDeviceType:X4} id {sd.DeviceId:X6}");
                        found.Add(new HartScanEntry { SubDevice = sd });
                    }
            }
            progress?.Invoke(100, "Concluído");
            return found;
        }
    }
}
