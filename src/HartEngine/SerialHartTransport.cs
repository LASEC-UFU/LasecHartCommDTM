using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace HartEngine
{
    /// <summary>
    /// Modem HART numa porta COM (RS-232, USB ou Bluetooth SPP), pela API Win32 — System.IO.Ports não controla o
    /// RTS durante a transmissão e não aceita nomes de porta fora do padrão "COMx".
    ///
    /// Linha HART: 1200 bit/s, 8 bits, paridade ímpar, 1 stop bit; half-duplex. Em modems RS-232 o RTS liga o
    /// transmissor FSK e o DTR costuma alimentar o modem.
    /// </summary>
    public sealed class SerialHartTransport : IHartTransport
    {
        private readonly HartLinkSettings _s;
        private readonly HartCommLog _log;
        private SafeFileHandle _h;
        private readonly HartFrameParser _parser = new HartFrameParser();

        public SerialHartTransport(HartLinkSettings settings, HartCommLog log)
        {
            _s = settings.Clone();
            _log = log ?? new HartCommLog();
        }

        public string Description => _s.Describe();
        public bool IsOpen => _h != null && !_h.IsInvalid && !_h.IsClosed;

        /// <summary>Tempo máximo sem bytes no meio de um quadro (USB e Bluetooth entregam em rajadas).</summary>
        public int InterByteTimeoutMs { get; set; } = 250;

        public void Open()
        {
            if (IsOpen) return;
            string path = _s.Port.StartsWith(@"\\.\", StringComparison.Ordinal) ? _s.Port : @"\\.\" + _s.Port.Trim();
            var h = CreateFile(path, GENERIC_READ | GENERIC_WRITE, 0, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            if (h.IsInvalid)
            {
                int err = Marshal.GetLastWin32Error();
                throw new HartTransportException($"Não foi possível abrir {_s.Port}: {new Win32Exception(err).Message}" +
                    (err == 5 ? " (a porta está em uso por outro programa)" : err == 2 ? " (a porta não existe)" : ""));
            }
            try
            {
                SetupComm(h, 4096, 4096);
                var dcb = new DCB { DCBlength = (uint)Marshal.SizeOf(typeof(DCB)) };
                if (!GetCommState(h, ref dcb)) throw new Win32Exception();
                dcb.BaudRate = (uint)_s.Baudrate;
                dcb.ByteSize = 8;
                dcb.Parity = (byte)_s.Parity;
                dcb.StopBits = (byte)(_s.StopBits == 2 ? 2 : 0);
                uint flags = 0x0001;                                     // fBinary
                if (_s.Parity != SerialParity.None) flags |= 0x0002;     // fParity
                if (_s.DtrEnabled) flags |= 0x0010;                      // fDtrControl = ENABLE
                switch (_s.RtsControl)
                {
                    case RtsControl.On: flags |= 0x1000; break;          // RTS_CONTROL_ENABLE
                    case RtsControl.DriverToggle: flags |= 0x3000; break; // RTS_CONTROL_TOGGLE
                    default: break;                                      // DISABLE (Toggle manual e Off)
                }
                dcb.Flags = flags;
                if (!SetCommState(h, ref dcb))
                {
                    int err = Marshal.GetLastWin32Error();
                    if (_s.RtsControl == RtsControl.DriverToggle)
                        throw new HartTransportException($"O driver de {_s.Port} não aceita RTS_CONTROL_TOGGLE ({new Win32Exception(err).Message}). Use o controle de RTS \"Toggle\".");
                    throw new Win32Exception(err);
                }
                // ReadFile devolve na hora o que houver; sem nada, espera até 10 ms.
                var to = new COMMTIMEOUTS { ReadIntervalTimeout = uint.MaxValue, ReadTotalTimeoutMultiplier = uint.MaxValue, ReadTotalTimeoutConstant = 10, WriteTotalTimeoutConstant = 5000 };
                if (!SetCommTimeouts(h, ref to)) throw new Win32Exception();
                PurgeComm(h, PURGE_RXCLEAR | PURGE_TXCLEAR);
            }
            catch (Exception ex) when (!(ex is HartTransportException))
            {
                h.Dispose();
                throw new HartTransportException($"Falha ao configurar {_s.Port}: {ex.Message}", ex);
            }
            catch { h.Dispose(); throw; }
            _h = h;
            _log.Info($"Porta aberta: {Description}");
        }

        public void Close()
        {
            if (_h == null) return;
            try { if (_s.RtsControl == RtsControl.Toggle) EscapeCommFunction(_h, CLRRTS); } catch { }
            _h.Dispose();
            _h = null;
            _log.Info($"Porta fechada: {_s.Port}");
        }

        public void Dispose() => Close();

        private double CharTimeMs => (1 + 8 + (_s.Parity != SerialParity.None ? 1 : 0) + _s.StopBits) * 1000.0 / _s.Baudrate;

        public HartFrame Transact(HartFrame request, int preambles, int responseTimeoutMs, Func<HartFrame, bool> accept, CancellationToken ct)
        {
            if (!IsOpen) throw new HartTransportException("Porta serial fechada.");
            var raw = request.EncodeWithPreambles(preambles);
            PurgeComm(_h, PURGE_RXCLEAR);
            _parser.Clear();
            Send(raw);
            _log.Tx(request.Encode(), $"cmd {request.Command,3} → {request.Address}");

            var buf = new byte[512];
            var sw = Stopwatch.StartNew();
            long lastByte = -1;
            int maxFrameMs = (int)(CharTimeMs * (30 + 8 + 255 + 2)) + 500; // preâmbulos + cabeçalho + dados
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                if (!ReadFile(_h, buf, buf.Length, out int n, IntPtr.Zero))
                {
                    int err = Marshal.GetLastWin32Error();
                    ClearCommError(_h, out _, IntPtr.Zero);
                    throw new HartTransportException($"Erro de leitura em {_s.Port}: {new Win32Exception(err).Message}");
                }
                long now = sw.ElapsedMilliseconds;
                if (n > 0)
                {
                    lastByte = now;
                    _parser.Append(buf, n);
                    HartFrame f;
                    while ((f = _parser.TryRead()) != null)
                    {
                        if (f.Type == HartFrameType.Stx) { _log.Info("eco do pedido descartado"); continue; }
                        if (f.Type == HartFrameType.Back) { _log.Rx(f.Encode(), $"burst cmd {f.Command} (ignorado)"); continue; }
                        if (accept(f)) { _log.Rx(f.Encode(), $"cmd {f.Command,3} ← rc {f.ResponseCode} st {f.DeviceStatus:X2}"); return f; }
                        _log.Rx(f.Encode(), "resposta de outro endereço/comando (descartada)");
                    }
                }
                else
                {
                    bool inFrame = _parser.InFrame && lastByte >= 0;
                    if (inFrame && now - lastByte > InterByteTimeoutMs)
                    {
                        _log.Error($"quadro incompleto descartado ({_parser.Buffered} bytes; erros de checksum: {_parser.ChecksumErrors})");
                        _parser.Clear();
                        inFrame = false;
                    }
                    if (!inFrame && now > responseTimeoutMs) return null;
                    if (now > responseTimeoutMs + maxFrameMs) return null;
                }
            }
        }

        private void Send(byte[] raw)
        {
            bool manual = _s.RtsControl == RtsControl.Toggle;
            if (manual)
            {
                EscapeCommFunction(_h, SETRTS);
                if (_s.RtsDelayMs > 0) Thread.Sleep(_s.RtsDelayMs);
            }
            var sw = Stopwatch.StartNew();
            try
            {
                if (!WriteFile(_h, raw, raw.Length, out int written, IntPtr.Zero))
                    throw new HartTransportException($"Erro de escrita em {_s.Port}: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
                if (written != raw.Length)
                    throw new HartTransportException($"Escrita incompleta em {_s.Port}: {written} de {raw.Length} bytes.");
                if (manual)
                {
                    // Desliga o RTS só depois do último bit sair do UART (o WriteFile pode voltar antes).
                    double txMs = raw.Length * CharTimeMs + 2;
                    while (sw.Elapsed.TotalMilliseconds < txMs) Thread.Sleep(1);
                }
            }
            finally
            {
                if (manual) EscapeCommFunction(_h, CLRRTS);
            }
        }

        // ------------------------------------------------------------ Win32

        private const uint GENERIC_READ = 0x80000000, GENERIC_WRITE = 0x40000000, OPEN_EXISTING = 3;
        private const uint PURGE_TXCLEAR = 0x0004, PURGE_RXCLEAR = 0x0008;
        private const uint SETRTS = 3, CLRRTS = 4;

        [StructLayout(LayoutKind.Sequential)]
        private struct DCB
        {
            public uint DCBlength, BaudRate, Flags;
            public ushort wReserved, XonLim, XoffLim;
            public byte ByteSize, Parity, StopBits;
            public sbyte XonChar, XoffChar, ErrorChar, EofChar, EvtChar;
            public ushort wReserved1;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct COMMTIMEOUTS { public uint ReadIntervalTimeout, ReadTotalTimeoutMultiplier, ReadTotalTimeoutConstant, WriteTotalTimeoutMultiplier, WriteTotalTimeoutConstant; }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr sec, uint disp, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetupComm(SafeFileHandle h, uint inQueue, uint outQueue);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetCommState(SafeFileHandle h, ref DCB dcb);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetCommState(SafeFileHandle h, ref DCB dcb);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetCommTimeouts(SafeFileHandle h, ref COMMTIMEOUTS t);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool PurgeComm(SafeFileHandle h, uint flags);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool EscapeCommFunction(SafeFileHandle h, uint func);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool ClearCommError(SafeFileHandle h, out uint errors, IntPtr stat);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool ReadFile(SafeFileHandle h, byte[] buf, int len, out int read, IntPtr ov);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool WriteFile(SafeFileHandle h, byte[] buf, int len, out int written, IntPtr ov);
    }
}
