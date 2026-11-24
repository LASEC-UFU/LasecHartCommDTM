using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Xml;
using HartEngine;
using Jigfdt.Fdt100;

namespace LasecHartCommDTM
{
    /// <summary>
    /// Canal de comunicação HART do Comm DTM (IFdtChannel + IFdtCommunication + IFdtChannelSubTopology).
    /// Os DTMs de instrumento se conectam a ele (ConnectRequest/TransactionRequest/DisconnectRequest, FDT 1.2 §4.5.3);
    /// cada pedido vira uma transação do mestre HART, executada numa thread de E/S, e a resposta volta assíncrona na
    /// thread do DTM.
    /// </summary>
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public sealed class HartChannel : StandardOleMarshalObject, IFdtChannel, IFdtCommunication, IFdtChannelSubTopology
    {
        /// <summary>Identificador do canal dentro do DTM.</summary>
        public const string ChannelId = "HART";

        /// <summary>§4.5.1.2: o caminho do canal começa com o systemTag da instância: &lt;systemTag&gt;/&lt;id&gt;.</summary>
        public string ChannelPath => string.IsNullOrEmpty(_owner.SystemTag) ? ChannelId : _owner.SystemTag + "/" + ChannelId;

        private sealed class Connection
        {
            public string CommRef;
            public IFdtCommunicationEvents Callback;
            public string ClientTag;
            public HartAddress Address;
            public byte PollingAddress;
            public int Preambles;
            public volatile int DelayMs;
        }

        private readonly CommDtm _owner;
        private readonly object _sync = new object();
        private readonly Dictionary<string, Connection> _connections = new Dictionary<string, Connection>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _children = new List<string>();
        private readonly BlockingCollection<Action> _jobs = new BlockingCollection<Action>();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private Thread _worker;
        private HartMaster _master; // só na thread de E/S
        private volatile bool _closed;

        internal HartChannel(CommDtm owner)
        {
            _owner = owner;
        }

        /// <summary>A thread de E/S só existe a partir do primeiro pedido (o Frame cria DTMs só para ler o catálogo).</summary>
        private void StartWorker()
        {
            lock (_sync)
            {
                if (_worker != null) return;
                _worker = new Thread(WorkerLoop) { IsBackground = true, Name = "LasecHartCommDTM E/S" };
                _worker.Start();
            }
        }

        internal HartCommLog Log => _owner.CommLog;
        internal int ConnectionCount { get { lock (_sync) return _connections.Count; } }
        internal IReadOnlyList<string> Children { get { lock (_sync) return _children.ToList(); } }
        internal bool LinkOpen => _master?.Transport.IsOpen == true;

        // ================================================================ IFdtChannel

        public string GetChannelPath() => ChannelPath;

        public string GetChannelParameters(string parameterPath, string protocolId) => DtmXml.ChannelParameters(ChannelId);

        /// <summary>O canal é configurado pela tela do DTM (data set do DTM), não por parâmetros de canal.</summary>
        public bool SetChannelParameters(string parameterPath, string protocolId, string xmlDocument) => false;

        // ================================================================ IFdtCommunication

        public string GetSupportedProtocols() => DtmXml.SupportedProtocols();

        public bool ConnectRequest(IFdtCommunicationEvents callBack, string invokeId, string protocolId, string fieldbusFrame)
        {
            if (_closed || callBack == null) return false;
            if (!string.Equals((protocolId ?? "").Trim().Trim('{', '}'), DtmXml.HartProtocolId, StringComparison.OrdinalIgnoreCase))
            {
                Log.Error($"ConnectRequest recusado: protocolo {protocolId} não é HART");
                return false;
            }
            XmlElement req;
            try { req = DtmXml.Elements(DtmXml.Load(fieldbusFrame), "ConnectRequest").FirstOrDefault(); }
            catch (Exception ex) { Log.Error("ConnectRequest com XML inválido: " + ex.Message); return false; }
            if (req == null) { Log.Error("ConnectRequest sem o elemento ConnectRequest"); return false; }

            string tag = DtmXml.Attr(req, "tag") ?? "";
            int hintPreambles = DtmXml.Int(DtmXml.Attr(req, "preambleCount"), 0);
            var sa = DtmXml.Elements(req, "ShortAddress").FirstOrDefault();
            byte polling = (byte)Math.Max(0, Math.Min(63, DtmXml.Int(DtmXml.Attr(sa, "shortAddress"), 0)));
            HartAddress expected = null;
            var la = DtmXml.Elements(req, "LongAddress").FirstOrDefault();
            if (la != null)
                expected = HartAddress.Long(new[]
                {
                    (byte)DtmXml.Int(la.GetAttribute("manufacturerId"), 0), (byte)DtmXml.Int(la.GetAttribute("deviceTypeId"), 0),
                    (byte)DtmXml.Int(la.GetAttribute("address1"), 0), (byte)DtmXml.Int(la.GetAttribute("address2"), 0),
                    (byte)DtmXml.Int(la.GetAttribute("address3"), 0),
                });

            Log.Info($"ConnectRequest de '{tag}': polling {polling}{(expected != null ? ", " + expected : "")}");
            Enqueue(() => DoConnect(callBack, invokeId, tag, polling, expected, hintPreambles));
            return true;
        }

        private void DoConnect(IFdtCommunicationEvents callBack, string invokeId, string tag, byte polling, HartAddress expected, int hintPreambles)
        {
            string response;
            Connection conn = null;
            string error;
            if (!EnsureLink(out error))
                response = DtmXml.CommunicationError(tag, "noConnection", error);
            else
            {
                HartIdentity id = null;
                if (expected != null) id = _master.Identify(expected, _cts.Token, out error);
                if (id == null && (expected == null || !_owner.Settings.IsNetwork))
                    id = _master.Identify(HartAddress.Short(polling), _cts.Token, out error);

                if (id == null)
                    response = DtmXml.CommunicationError(tag, "noConnection",
                        $"Nenhum instrumento respondeu {(expected != null ? "no endereço " + expected + " nem " : "")}no endereço de polling {polling} ({error}).");
                else if (expected != null && !id.UniqueAddress.Equals(expected))
                    response = DtmXml.CommunicationError(tag, "noConnection",
                        $"O instrumento encontrado ({id.UniqueAddress}) não é o esperado pelo DTM ({expected}).");
                else
                {
                    conn = new Connection
                    {
                        CommRef = Guid.NewGuid().ToString("D").ToUpperInvariant(),
                        Callback = callBack,
                        ClientTag = tag,
                        Address = id.UniqueAddress,
                        PollingAddress = polling,
                        Preambles = Math.Max(_owner.Settings.PreambleCount, Math.Max(hintPreambles, id.RequestPreambles)),
                    };
                    lock (_sync) _connections[conn.CommRef] = conn;
                    Log.Info($"Conectado '{tag}': {id} (referência {conn.CommRef})");
                    response = DtmXml.ConnectResponse(tag, conn.Preambles, _owner.Settings.PrimaryMaster, conn.CommRef, id.UniqueAddress, polling);
                }
            }
            if (conn == null) { Log.Error($"Conexão de '{tag}' falhou"); CloseLinkIfIdle(); }
            _owner.Post(() =>
            {
                if (callBack is IFdtCommunicationEvents2 c2) c2.OnConnectResponse2(invokeId, DtmXml.ParentInformation(), response);
                else callBack.OnConnectResponse(invokeId, response);
            });
        }

        public bool DisconnectRequest(string invokeId, string fieldbusFrame)
        {
            string commRef = CommRefOf(fieldbusFrame, "DisconnectRequest");
            Connection conn;
            lock (_sync)
            {
                if (commRef == null || !_connections.TryGetValue(commRef, out conn)) return false;
                _connections.Remove(commRef); // pedidos pendentes desta conexão são descartados
            }
            Log.Info($"Desconectado '{conn.ClientTag}' ({commRef})");
            // A resposta vai antes de retornar, como no HART Communication da CodeWrights: há DTMs de instrumento
            // (ex.: Smar LD301) que esperam OnDisconnectResponse dentro de PrepareToReleaseCommunication sem atender
            // chamadas COM de fora — uma resposta postada depois só chegaria quando eles já desistiram.
            try { conn.Callback.OnDisconnectResponse(invokeId, DtmXml.DisconnectResponse(commRef)); }
            catch (Exception ex) { Log.Error("OnDisconnectResponse: " + ex.Message); }
            Enqueue(CloseLinkIfIdle);
            return true;
        }

        public bool TransactionRequest(string invokeId, string fieldbusFrame)
        {
            XmlElement req;
            try { req = DtmXml.Elements(DtmXml.Load(fieldbusFrame), "DataExchangeRequest").FirstOrDefault(); }
            catch (Exception ex) { Log.Error("TransactionRequest com XML inválido: " + ex.Message); return false; }
            string commRef = req?.GetAttribute("communicationReference");
            Connection conn;
            lock (_sync)
                if (req == null || string.IsNullOrEmpty(commRef) || !_connections.TryGetValue(commRef, out conn)) return false;
            int cmd = DtmXml.Int(req.GetAttribute("commandNumber"), -1);
            if (cmd < 0 || cmd > 255) return false;
            byte[] data;
            try { data = Hex.Parse(DtmXml.Attr(DtmXml.Elements(req, "CommunicationData").FirstOrDefault(), "byteArray")); }
            catch { return false; }
            Enqueue(() => DoTransaction(conn, invokeId, (byte)cmd, data));
            return true;
        }

        private void DoTransaction(Connection conn, string invokeId, byte cmd, byte[] data)
        {
            if (!IsActive(conn)) return;
            string response;
            string error;
            if (!EnsureLink(out error))
                response = DtmXml.CommunicationError(conn.ClientTag, "noConnection", error);
            else
            {
                var r = _master.Send(conn.Address, cmd, data, _cts.Token, preambles: conn.Preambles);
                response = r.Ok
                    ? DtmXml.DataExchangeResponse(conn.CommRef, cmd, r.Response)
                    : DtmXml.CommunicationError(conn.ClientTag, "timeout", $"Comando {cmd}: {r.Error}");
            }
            if (conn.DelayMs > 0) Thread.Sleep(conn.DelayMs);
            if (!IsActive(conn)) return;
            _owner.Post(() => { if (IsActive(conn)) conn.Callback.OnTransactionResponse(invokeId, response); });
        }

        public void Abort(string fieldbusFrame)
        {
            string commRef = null;
            try { commRef = CommRefOf(fieldbusFrame, "Abort"); } catch { }
            lock (_sync)
            {
                if (string.IsNullOrEmpty(commRef)) _connections.Clear();
                else _connections.Remove(commRef);
            }
            Log.Info("Abort " + (string.IsNullOrEmpty(commRef) ? "(todas as conexões)" : commRef));
            Enqueue(CloseLinkIfIdle);
        }

        public bool SequenceBegin(string fieldbusFrame)
        {
            try
            {
                var e = DtmXml.Elements(DtmXml.Load(fieldbusFrame), "SequenceBegin").FirstOrDefault();
                string commRef = e?.GetAttribute("communicationReference");
                lock (_sync)
                    if (commRef != null && _connections.TryGetValue(commRef, out var c)) { c.DelayMs = Math.Max(0, DtmXml.Int(e.GetAttribute("delayTime"), 0)); return true; }
            }
            catch { }
            return false;
        }

        /// <summary>Os pedidos já são executados em ordem, um por vez: o início da sequência não muda nada.</summary>
        public bool SequenceStart(string fieldbusFrame) => true;

        public bool SequenceEnd(string fieldbusFrame)
        {
            try
            {
                string commRef = CommRefOf(fieldbusFrame, "SequenceEnd");
                lock (_sync) if (commRef != null && _connections.TryGetValue(commRef, out var c)) c.DelayMs = 0;
            }
            catch { }
            return true;
        }

        // ================================================================ IFdtChannelSubTopology

        /// <summary>Varredura FDT 1.2: resultado por IDtmEvents::OnScanResponse (DTMTopologyScanSchema).</summary>
        public bool ScanRequest(string invokeId)
        {
            if (_closed) return false;
            Log.Info("ScanRequest do Frame");
            Scan((found, error) =>
            {
                string xml = DtmXml.TopologyScan(found ?? new List<HartScanEntry>());
                _owner.Post(() =>
                {
                    _owner.Events?.OnProgress(_owner.SystemTag, "", 100, false);
                    if (error != null) _owner.Events?.OnErrorMessage(_owner.SystemTag, error);
                    _owner.Events?.OnScanResponse(invokeId, xml);
                });
            }, (pct, text) => _owner.Post(() => _owner.Events?.OnProgress(_owner.SystemTag, "HART scan: " + text, (short)pct, true)));
            return true;
        }

        public bool ValidateAddChild(string childSystemTag) => !_closed;

        public void OnAddChild(string childSystemTag)
        {
            lock (_sync) if (!_children.Contains(childSystemTag)) _children.Add(childSystemTag);
        }

        public bool ValidateRemoveChild(string childSystemTag) => true;

        public void OnRemoveChild(string childSystemTag)
        {
            lock (_sync)
            {
                _children.Remove(childSystemTag);
                foreach (var k in _connections.Where(c => c.Value.ClientTag == childSystemTag).Select(c => c.Key).ToList())
                    _connections.Remove(k);
            }
            Enqueue(CloseLinkIfIdle);
        }

        // ================================================================ uso interno (DTM e tela)

        /// <summary>Varredura somente leitura (comandos 0, 13, 74, 84). Os callbacks vêm na thread de E/S.</summary>
        internal void Scan(Action<List<HartScanEntry>, string> done, Action<int, string> progress = null)
        {
            Enqueue(() =>
            {
                List<HartScanEntry> found = null;
                string error = null;
                try
                {
                    if (!EnsureLink(out error)) { done(null, error); return; }
                    var s = _owner.Settings;
                    found = _master.Scan(s.ScanStart, s.ScanStop, _cts.Token, progress);
                }
                catch (OperationCanceledException) { error = "varredura cancelada"; }
                catch (Exception ex) { error = ex.Message; }
                finally { CloseLinkIfIdle(); }
                done(found, error);
            });
        }

        /// <summary>Abre e fecha o meio (porta serial / sessão HART-IP), sem enviar comandos aos instrumentos.</summary>
        internal void TestLink(Action<string> done)
        {
            Enqueue(() =>
            {
                string error;
                bool ok = EnsureLink(out error);
                CloseLinkIfIdle();
                done(ok ? null : error);
            });
        }

        /// <summary>A configuração mudou: fecha o meio; a próxima conexão usa a nova configuração.</summary>
        internal void Reconfigure() => Enqueue(() => CloseLink());

        internal void Shutdown()
        {
            if (_closed) return;
            _closed = true;
            lock (_sync) { _connections.Clear(); _children.Clear(); }
            _cts.Cancel();
            if (_worker == null) { _jobs.CompleteAdding(); return; }
            _jobs.Add(() => CloseLink());
            _jobs.CompleteAdding();
            if (!_worker.Join(5000)) Log.Error("thread de E/S não terminou em 5 s");
        }

        // ================================================================ thread de E/S

        private void Enqueue(Action job)
        {
            StartWorker();
            try { if (!_jobs.IsAddingCompleted) _jobs.Add(job); }
            catch (InvalidOperationException) { }
        }

        private void WorkerLoop()
        {
            foreach (var job in _jobs.GetConsumingEnumerable())
            {
                try { job(); }
                catch (OperationCanceledException) { }
                catch (Exception ex) { Log.Error("falha na thread de E/S: " + ex); }
            }
        }

        private bool EnsureLink(out string error)
        {
            error = null;
            if (_master != null && _master.Transport.IsOpen) return true;
            var s = _owner.Settings;
            string invalid = s.Validate();
            if (invalid != null) { error = "Configuração inválida: " + invalid; return false; }
            try
            {
                _master?.Dispose();
                _master = new HartMaster(HartTransports.Create(s, Log), s, Log);
                _master.Open();
                _owner.NotifyOnline(true);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Log.Error(error);
                _master = null;
                return false;
            }
        }

        private void CloseLinkIfIdle()
        {
            if (ConnectionCount == 0) CloseLink();
        }

        private void CloseLink()
        {
            if (_master == null) return;
            bool wasOpen = _master.Transport.IsOpen;
            try { _master.Dispose(); } catch (Exception ex) { Log.Error("fechando o meio: " + ex.Message); }
            _master = null;
            if (wasOpen) _owner.NotifyOnline(false);
        }

        private bool IsActive(Connection c)
        {
            lock (_sync) return !_closed && _connections.TryGetValue(c.CommRef, out var cur) && ReferenceEquals(cur, c);
        }

        private static string CommRefOf(string xml, string element) =>
            DtmXml.Elements(DtmXml.Load(xml), element).FirstOrDefault()?.GetAttribute("communicationReference");
    }

    /// <summary>Coleção de canais devolvida por IDtmChannel::GetChannels (um canal HART).</summary>
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public sealed class HartChannelCollection : StandardOleMarshalObject, IFdtChannelCollection
    {
        private readonly IFdtChannel[] _items;

        internal HartChannelCollection(params IFdtChannel[] items) { _items = items; }

        public int Count => _items.Length;

        public IFdtChannel get_Item(ref object pvarIndex)
        {
            if (pvarIndex is string path)
                return _items.FirstOrDefault(c => string.Equals(c.GetChannelPath(), path, StringComparison.OrdinalIgnoreCase));
            int i = Convert.ToInt32(pvarIndex);
            if (i >= 0 && i < _items.Length) return _items[i];
            // Frames que contam a partir de 1 (estilo coleção VB): Item(Count) é o último canal.
            return i == _items.Length && i > 0 ? _items[i - 1] : null;
        }

        public IEnumerator GetEnumerator() => _items.GetEnumerator();
    }
}
