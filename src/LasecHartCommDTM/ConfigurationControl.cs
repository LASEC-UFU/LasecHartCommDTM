using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using HartEngine;
using Jigfdt.Fdt100;
using Microsoft.Win32;

namespace LasecHartCommDTM
{
    /// <summary>
    /// Tela do DTM (presentation object, FDT 1.2 §4.4): controle ActiveX criado pelo Frame a partir de
    /// IDtmActiveXInformation::GetActiveXGuid e ligado ao DTM por IDtmActiveXControl::Init.
    /// Abas: Conexão, Mestre HART, Instrumentos (varredura somente leitura), Log e Sobre.
    /// </summary>
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    [Guid(ClassId)]
    [ProgId(ProgIdText)]
    public sealed class ConfigurationControl : UserControl, IDtmActiveXControl
    {
        public const string ClassId = "B4B6B3E7-639D-460B-B9A0-6C7F7EB20020";
        public const string ProgIdText = "JosueLab.LasecHartCommDTM.ConfigurationControl";

        private CommDtm _dtm;
        private string _invokeId;
        private bool _pt = true;
        private readonly Queue<LogEntry> _pendingLog = new Queue<LogEntry>();
        private Timer _logTimer;

        // controles
        private TabControl _tabs;
        private TabPage _tabConn, _tabMaster, _tabScan, _tabLog, _tabAbout;
        private ComboBox _cbInterface, _cbPort, _cbBaud, _cbParity, _cbStop, _cbRts;
        private CheckBox _chkDtr, _chkSubDevices;
        private NumericUpDown _numRtsDelay, _numHostPort, _numInactivity, _numPreambles, _numRetries, _numTimeout, _numDrTimeout, _numScanFrom, _numScanTo;
        private TextBox _txtHost, _txtLog;
        private ComboBox _cbMaster;
        private GroupBox _grpSerial, _grpNet;
        private Button _btnApply, _btnRevert, _btnTest, _btnScan, _btnRefreshPorts;
        private Label _lblStatus, _lblScan;
        private ListView _lvDevices;

        public ConfigurationControl()
        {
            Font = new Font("Segoe UI", 9f);
            MinimumSize = new Size(560, 430);
            Size = new Size(640, 470);
            Controls.Add(new Label { Text = "Lasec HART Communication DTM", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter });
        }

        private string T(string pt, string en) => _pt ? pt : en;

        // ================================================================ IDtmActiveXControl

        public bool Init(string invokeId, string functionCall, IDtm dtm)
        {
            _invokeId = invokeId;
            _dtm = dtm as CommDtm;
            if (_dtm == null) return false; // §4.4.1.1: o Frame libera o controle
            _pt = _dtm.Portuguese;
            BuildUi();
            LoadFromSettings(_dtm.Settings);
            switch (DtmXml.FunctionIdOf(functionCall))
            {
                case DtmXml.FunctionLog: _tabs.SelectedTab = _tabLog; break;
                case DtmXml.FunctionScan: _tabs.SelectedTab = _tabScan; break;
                case DtmXml.FunctionAbout: _tabs.SelectedTab = _tabAbout; break;
            }
            foreach (var e in _dtm.CommLog.Snapshot().Skip(Math.Max(0, _dtm.CommLog.Snapshot().Count - 500))) AppendLog(e);
            _dtm.CommLog.Added += OnLogAdded;
            _dtm.StateChanged += UpdateStatus;
            _logTimer = new Timer { Interval = 200 };
            _logTimer.Tick += (s, e) => FlushLog();
            _logTimer.Start();
            UpdateStatus();
            return true;
        }

        public bool PrepareToRelease()
        {
            Detach();
            _dtm?.NotifyApplicationClosed(_invokeId);
            return true;
        }

        private void Detach()
        {
            _logTimer?.Stop();
            if (_dtm != null)
            {
                _dtm.CommLog.Added -= OnLogAdded;
                _dtm.StateChanged -= UpdateStatus;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { Detach(); _logTimer?.Dispose(); }
            base.Dispose(disposing);
        }

        // ================================================================ montagem da tela

        private void BuildUi()
        {
            SuspendLayout();
            Controls.Clear();
            _tabs = new TabControl { Dock = DockStyle.Fill };
            _tabConn = new TabPage(T("Conexão", "Connection"));
            _tabMaster = new TabPage(T("Mestre HART", "HART master"));
            _tabScan = new TabPage(T("Instrumentos", "Devices"));
            _tabLog = new TabPage("Log");
            _tabAbout = new TabPage(T("Sobre", "About"));
            _tabs.TabPages.AddRange(new[] { _tabConn, _tabMaster, _tabScan, _tabLog, _tabAbout });

            BuildConnectionTab();
            BuildMasterTab();
            BuildScanTab();
            BuildLogTab();
            BuildAboutTab();

            var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 38, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(4) };
            _btnApply = new Button { Text = T("Aplicar", "Apply"), AutoSize = true };
            _btnRevert = new Button { Text = T("Desfazer", "Revert"), AutoSize = true };
            _lblStatus = new Label { AutoSize = true, Padding = new Padding(0, 7, 20, 0) };
            _btnApply.Click += (s, e) => Apply();
            _btnRevert.Click += (s, e) => LoadFromSettings(_dtm.Settings);
            bottom.Controls.AddRange(new Control[] { _btnApply, _btnRevert, _lblStatus });

            Controls.Add(_tabs);
            Controls.Add(bottom);
            ResumeLayout();
        }

        private static TableLayoutPanel Grid(int rows)
        {
            var g = new TableLayoutPanel { ColumnCount = 2, RowCount = rows, AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(6) };
            g.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            g.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            return g;
        }

        private static void Row(TableLayoutPanel g, string label, Control c)
        {
            int r = g.Controls.Count / 2;
            g.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 7, 3, 3) }, 0, r);
            c.Anchor = AnchorStyles.Left;
            g.Controls.Add(c, 1, r);
        }

        private static ComboBox Combo(params string[] items)
        {
            var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
            c.Items.AddRange(items);
            return c;
        }

        private static NumericUpDown Num(int min, int max, int width = 90) => new NumericUpDown { Minimum = min, Maximum = max, Width = width };

        private void BuildConnectionTab()
        {
            var top = Grid(1);
            _cbInterface = Combo(
                T("Modem HART serial / USB / Bluetooth", "HART modem serial / USB / Bluetooth"),
                T("HART-IP UDP (gateway WirelessHART)", "HART-IP UDP (WirelessHART gateway)"),
                "HART-IP TCP",
                T("Ponte UDP (quadro HART bruto)", "UDP bridge (raw HART frame)"));
            _cbInterface.Width = 320;
            _cbInterface.SelectedIndexChanged += (s, e) => UpdateGroups();
            Row(top, T("Interface", "Interface"), _cbInterface);

            _grpSerial = new GroupBox { Text = T("Modem HART (porta COM)", "HART modem (COM port)"), Dock = DockStyle.Top, AutoSize = true };
            var gs = Grid(7);
            var portRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            _cbPort = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Width = 260 };
            _btnRefreshPorts = new Button { Text = "↻", Width = 30, Height = 23 };
            _btnRefreshPorts.Click += (s, e) => FillPorts(PortName(_cbPort.Text));
            portRow.Controls.AddRange(new Control[] { _cbPort, _btnRefreshPorts });
            Row(gs, T("Porta", "Port"), portRow);
            _cbBaud = Combo("1200", "2400", "4800", "9600", "19200", "38400");
            _cbBaud.Width = 100;
            Row(gs, T("Velocidade (bit/s)", "Baud rate"), _cbBaud);
            _cbParity = Combo(T("Ímpar (padrão HART)", "Odd (HART default)"), T("Nenhuma", "None"), T("Par", "Even"));
            Row(gs, T("Paridade", "Parity"), _cbParity);
            _cbStop = Combo("1", "2");
            _cbStop.Width = 60;
            Row(gs, "Stop bits", _cbStop);
            _cbRts = Combo(
                T("Liga só na transmissão (DTM)", "On while transmitting (DTM)"),
                T("Liga só na transmissão (driver)", "On while transmitting (driver)"),
                T("Sempre ligado", "Always on"),
                T("Sempre desligado (USB/Bluetooth)", "Always off (USB/Bluetooth)"));
            Row(gs, "RTS", _cbRts);
            _chkDtr = new CheckBox { Text = T("DTR ligado (alimenta modems RS-232)", "DTR on (powers RS-232 modems)"), AutoSize = true };
            Row(gs, "DTR", _chkDtr);
            _numRtsDelay = Num(0, 100);
            Row(gs, T("Atraso RTS → dados (ms)", "RTS → data delay (ms)"), _numRtsDelay);
            _grpSerial.Controls.Add(gs);

            _grpNet = new GroupBox { Text = T("Rede (HART-IP / ponte UDP)", "Network (HART-IP / UDP bridge)"), Dock = DockStyle.Top, AutoSize = true };
            var gn = Grid(3);
            _txtHost = new TextBox { Width = 260 };
            Row(gn, T("Endereço do servidor", "Server address"), _txtHost);
            _numHostPort = Num(1, 65535);
            Row(gn, T("Porta", "Port"), _numHostPort);
            _numInactivity = Num(10000, 3600000, 110);
            _numInactivity.Increment = 10000;
            Row(gn, T("Inatividade da sessão (ms)", "Session inactivity (ms)"), _numInactivity);
            _grpNet.Controls.Add(gn);

            var testRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6) };
            _btnTest = new Button { Text = T("Testar meio (sem falar com instrumentos)", "Test link (no device traffic)"), AutoSize = true };
            var lblTest = new Label { AutoSize = true, Padding = new Padding(6, 7, 0, 0) };
            _btnTest.Click += (s, e) => TestLink(lblTest);
            testRow.Controls.AddRange(new Control[] { _btnTest, lblTest });

            _tabConn.AutoScroll = true;
            _tabConn.Controls.Add(testRow);
            _tabConn.Controls.Add(_grpNet);
            _tabConn.Controls.Add(_grpSerial);
            _tabConn.Controls.Add(top);
        }

        private void BuildMasterTab()
        {
            var g = Grid(8);
            _cbMaster = Combo(T("Primário", "Primary"), T("Secundário", "Secondary"));
            Row(g, T("Tipo de mestre", "Master type"), _cbMaster);
            _numPreambles = Num(2, 20);
            Row(g, T("Preâmbulos", "Preambles"), _numPreambles);
            _numRetries = Num(0, 10);
            Row(g, T("Repetições", "Retries"), _numRetries);
            _numTimeout = Num(0, 60000, 100);
            _numTimeout.Increment = 100;
            Row(g, T("Tempo de resposta (ms, 0 = auto)", "Response timeout (ms, 0 = auto)"), _numTimeout);
            _numDrTimeout = Num(1000, 600000, 100);
            _numDrTimeout.Increment = 1000;
            Row(g, T("Resposta atrasada até (ms)", "Delayed response up to (ms)"), _numDrTimeout);
            _numScanFrom = Num(0, 63);
            Row(g, T("Varredura: do endereço", "Scan: from address"), _numScanFrom);
            _numScanTo = Num(0, 63);
            Row(g, T("Varredura: até o endereço", "Scan: to address"), _numScanTo);
            _chkSubDevices = new CheckBox { Text = T("Listar sub-dispositivos de gateways e multiplexadores", "List sub-devices of gateways and multiplexers"), AutoSize = true };
            Row(g, "", _chkSubDevices);
            _tabMaster.AutoScroll = true;
            _tabMaster.Controls.Add(g);
        }

        private void BuildScanTab()
        {
            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4) };
            _btnScan = new Button { Text = T("Procurar instrumentos", "Search devices"), AutoSize = true };
            _lblScan = new Label { AutoSize = true, Padding = new Padding(6, 7, 0, 0), Text = T("Somente leitura: comandos 0, 13, 74 e 84.", "Read only: commands 0, 13, 74 and 84.") };
            _btnScan.Click += (s, e) => Scan();
            bar.Controls.AddRange(new Control[] { _btnScan, _lblScan });
            _lvDevices = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true };
            _lvDevices.Columns.Add(T("Endereço", "Address"), 80);
            _lvDevices.Columns.Add("Tag", 150);
            _lvDevices.Columns.Add(T("Fabr./tipo", "Mfr./type"), 90);
            _lvDevices.Columns.Add(T("ID do instrumento", "Device ID"), 100);
            _lvDevices.Columns.Add(T("Rev. HART", "HART rev."), 70);
            _lvDevices.Columns.Add(T("Via", "Via"), 120);
            _tabScan.Controls.Add(_lvDevices);
            _tabScan.Controls.Add(bar);
        }

        private void BuildLogTab()
        {
            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4) };
            var clear = new Button { Text = T("Limpar", "Clear"), AutoSize = true };
            var copy = new Button { Text = T("Copiar", "Copy"), AutoSize = true };
            clear.Click += (s, e) => { _dtm.CommLog.Clear(); _txtLog.Clear(); };
            copy.Click += (s, e) => { if (_txtLog.TextLength > 0) Clipboard.SetText(_txtLog.Text); };
            bar.Controls.AddRange(new Control[] { clear, copy, new Label { AutoSize = true, Padding = new Padding(6, 7, 0, 0), Text = _dtm.CommLog.FilePath ?? "" } });
            _txtLog = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 8.5f), BackColor = SystemColors.Window };
            _tabLog.Controls.Add(_txtLog);
            _tabLog.Controls.Add(bar);
        }

        private void BuildAboutTab()
        {
            var asm = typeof(CommDtm).Assembly.GetName();
            _tabAbout.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                Text = $"{DtmXml.DtmName} DTM {DtmXml.Version} ({DtmXml.Date})\n{DtmXml.Vendor}\n\n" +
                       T("DTM de comunicação FDT 1.2 (mestre HART).\n\nMeios suportados:\n" +
                         "  • modem HART em porta COM: RS-232, USB ou Bluetooth (SPP)\n" +
                         "  • HART-IP UDP/TCP, porta 5094: gateways WirelessHART, multiplexadores, instrumentos HART-IP\n" +
                         "  • ponte UDP com o quadro HART bruto\n\n",
                         "FDT 1.2 communication DTM (HART master).\n\nSupported links:\n" +
                         "  • HART modem on a COM port: RS-232, USB or Bluetooth (SPP)\n" +
                         "  • HART-IP UDP/TCP, port 5094: WirelessHART gateways, multiplexers, HART-IP devices\n" +
                         "  • UDP bridge carrying the raw HART frame\n\n") +
                       $"Assembly {asm.Name} {asm.Version}\nCLSID {{{CommDtm.ClassId}}}\nProgID {CommDtm.ProgIdText}",
            });
        }

        // ================================================================ dados ↔ tela

        private static readonly HartInterface[] Interfaces = { HartInterface.Serial, HartInterface.HartIpUdp, HartInterface.HartIpTcp, HartInterface.UdpBridge };
        private static readonly SerialParity[] Parities = { SerialParity.Odd, SerialParity.None, SerialParity.Even };
        private static readonly RtsControl[] RtsModes = { RtsControl.Toggle, RtsControl.DriverToggle, RtsControl.On, RtsControl.Off };

        private void LoadFromSettings(HartLinkSettings s)
        {
            _cbInterface.SelectedIndex = Array.IndexOf(Interfaces, s.Interface);
            FillPorts(s.Port);
            _cbBaud.SelectedItem = s.Baudrate.ToString(CultureInfo.InvariantCulture);
            if (_cbBaud.SelectedIndex < 0) { _cbBaud.Items.Add(s.Baudrate.ToString(CultureInfo.InvariantCulture)); _cbBaud.SelectedIndex = _cbBaud.Items.Count - 1; }
            _cbParity.SelectedIndex = Array.IndexOf(Parities, s.Parity);
            _cbStop.SelectedIndex = s.StopBits == 2 ? 1 : 0;
            _cbRts.SelectedIndex = Array.IndexOf(RtsModes, s.RtsControl);
            _chkDtr.Checked = s.DtrEnabled;
            Set(_numRtsDelay, s.RtsDelayMs);
            _txtHost.Text = s.HostAddress;
            Set(_numHostPort, s.HostPort);
            Set(_numInactivity, s.InactivityTimeoutMs);
            _cbMaster.SelectedIndex = s.PrimaryMaster ? 0 : 1;
            Set(_numPreambles, s.PreambleCount);
            Set(_numRetries, s.RetryCount);
            Set(_numTimeout, s.ResponseTimeoutMs);
            Set(_numDrTimeout, s.DelayedResponseTimeoutMs);
            Set(_numScanFrom, s.ScanStart);
            Set(_numScanTo, s.ScanStop);
            _chkSubDevices.Checked = s.ScanSubDevices;
            UpdateGroups();
        }

        private static void Set(NumericUpDown n, int v) => n.Value = Math.Max(n.Minimum, Math.Min(n.Maximum, v));

        private HartLinkSettings ReadFromUi()
        {
            var s = _dtm.Settings;
            s.Interface = Interfaces[Math.Max(0, _cbInterface.SelectedIndex)];
            s.Port = PortName(_cbPort.Text);
            s.Baudrate = int.Parse((string)_cbBaud.SelectedItem ?? "1200", CultureInfo.InvariantCulture);
            s.Parity = Parities[Math.Max(0, _cbParity.SelectedIndex)];
            s.StopBits = _cbStop.SelectedIndex == 1 ? 2 : 1;
            s.RtsControl = RtsModes[Math.Max(0, _cbRts.SelectedIndex)];
            s.DtrEnabled = _chkDtr.Checked;
            s.RtsDelayMs = (int)_numRtsDelay.Value;
            s.HostAddress = _txtHost.Text.Trim();
            s.HostPort = (int)_numHostPort.Value;
            s.InactivityTimeoutMs = (int)_numInactivity.Value;
            s.PrimaryMaster = _cbMaster.SelectedIndex == 0;
            s.PreambleCount = (int)_numPreambles.Value;
            s.RetryCount = (int)_numRetries.Value;
            s.ResponseTimeoutMs = (int)_numTimeout.Value;
            s.DelayedResponseTimeoutMs = (int)_numDrTimeout.Value;
            s.ScanStart = (int)_numScanFrom.Value;
            s.ScanStop = (int)_numScanTo.Value;
            s.ScanSubDevices = _chkSubDevices.Checked;
            return s;
        }

        private void UpdateGroups()
        {
            var i = Interfaces[Math.Max(0, _cbInterface.SelectedIndex)];
            _grpSerial.Enabled = i == HartInterface.Serial;
            _grpNet.Enabled = i != HartInterface.Serial;
            _numInactivity.Enabled = i == HartInterface.HartIpUdp || i == HartInterface.HartIpTcp;
            if (i != HartInterface.Serial && _numHostPort.Value == 0) _numHostPort.Value = HartIpTransport.DefaultPort;
        }

        /// <summary>Portas COM do sistema (HARDWARE\DEVICEMAP\SERIALCOMM), com o tipo do dispositivo quando conhecido.</summary>
        private void FillPorts(string current)
        {
            _cbPort.Items.Clear();
            var ports = new SortedDictionary<string, string>(Comparer<string>.Create((a, b) => PortNumber(a).CompareTo(PortNumber(b)) is int c && c != 0 ? c : string.CompareOrdinal(a, b)));
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DEVICEMAP\SERIALCOMM"))
                    if (k != null)
                        foreach (var n in k.GetValueNames())
                            if (k.GetValue(n) is string port)
                                ports[port] = n.IndexOf("BthModem", StringComparison.OrdinalIgnoreCase) >= 0 ? "Bluetooth"
                                            : n.IndexOf("USB", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("VCP", StringComparison.OrdinalIgnoreCase) >= 0 ? "USB"
                                            : n.IndexOf("com0com", StringComparison.OrdinalIgnoreCase) >= 0 ? T("virtual", "virtual") : "";
            }
            catch { }
            foreach (var p in ports) _cbPort.Items.Add(p.Value.Length > 0 ? $"{p.Key}  ({p.Value})" : p.Key);
            string cur = PortName(current);
            int idx = _cbPort.Items.Cast<string>().ToList().FindIndex(x => PortName(x) == cur);
            if (idx >= 0) _cbPort.SelectedIndex = idx;
            else _cbPort.Text = cur;
        }

        private static string PortName(string item) => (item ?? "").Split(' ')[0].Trim();
        private static int PortNumber(string p) => int.TryParse(new string(p.Where(char.IsDigit).ToArray()), out int n) ? n : int.MaxValue;

        // ================================================================ ações

        private void Apply()
        {
            string err = _dtm.ApplySettings(ReadFromUi());
            if (err != null) { SetStatus(err, true); return; }
            SetStatus(T("Configuração aplicada.", "Configuration applied."), false);
        }

        private void TestLink(Label result)
        {
            var s = ReadFromUi();
            string invalid = s.Validate();
            if (invalid != null) { result.Text = invalid; result.ForeColor = Color.Firebrick; return; }
            if (!SameLink(s, _dtm.Settings)) { result.Text = T("Aplique a configuração antes de testar.", "Apply the configuration before testing."); result.ForeColor = Color.DarkOrange; return; }
            result.Text = T("Testando…", "Testing…");
            result.ForeColor = SystemColors.ControlText;
            _btnTest.Enabled = false;
            _dtm.Channel.TestLink(err => UiPost(() =>
            {
                _btnTest.Enabled = true;
                result.Text = err == null ? T("Meio aberto e fechado com sucesso.", "Link opened and closed successfully.") : err;
                result.ForeColor = err == null ? Color.DarkGreen : Color.Firebrick;
            }));
        }

        private static bool SameLink(HartLinkSettings a, HartLinkSettings b) => a.ToText() == b.ToText();

        private void Scan()
        {
            if (!SameLink(ReadFromUi(), _dtm.Settings)) { _lblScan.Text = T("Aplique a configuração antes de procurar.", "Apply the configuration before searching."); return; }
            _btnScan.Enabled = false;
            _lvDevices.Items.Clear();
            _lblScan.Text = T("Procurando…", "Searching…");
            _dtm.Channel.Scan(
                (found, error) => UiPost(() =>
                {
                    _btnScan.Enabled = true;
                    foreach (var d in found ?? new List<HartScanEntry>())
                    {
                        var a = d.UniqueAddress;
                        var it = new ListViewItem(d.PollingAddress.HasValue ? d.PollingAddress.Value.ToString(CultureInfo.InvariantCulture) : "—");
                        it.SubItems.Add(d.Tag);
                        it.SubItems.Add($"{a.ManufacturerOrTypeHigh}/{a.DeviceType}");
                        it.SubItems.Add(a.DeviceId.ToString("X6"));
                        it.SubItems.Add((d.Identity?.UniversalRevision ?? d.SubDevice?.UniversalRevision ?? 0).ToString(CultureInfo.InvariantCulture));
                        it.SubItems.Add(d.SubDevice != null ? T($"sub-disp. {d.SubDevice.Index} (cartão {d.SubDevice.IoCard}, canal {d.SubDevice.Channel})", $"sub-device {d.SubDevice.Index} (card {d.SubDevice.IoCard}, channel {d.SubDevice.Channel})") : T("direto", "direct"));
                        _lvDevices.Items.Add(it);
                    }
                    _lblScan.Text = error ?? T($"{_lvDevices.Items.Count} instrumento(s) encontrado(s).", $"{_lvDevices.Items.Count} device(s) found.");
                }),
                (pct, text) => UiPost(() => _lblScan.Text = $"{pct}% — {text}"));
        }

        private void UpdateStatus()
        {
            if (_dtm == null || _lblStatus == null) return;
            UiPost(() =>
            {
                var s = _dtm.Settings;
                int n = _dtm.Channel.ConnectionCount;
                _lblStatus.ForeColor = _dtm.LinkOnline ? Color.DarkGreen : SystemColors.GrayText;
                _lblStatus.Text = $"{s.Describe()} — {(_dtm.LinkOnline ? "online" : "offline")}, {n} {T("instrumento(s) conectado(s)", "device(s) connected")}";
                _btnApply.Enabled = n == 0;
            });
        }

        private void SetStatus(string text, bool error)
        {
            _lblStatus.Text = text;
            _lblStatus.ForeColor = error ? Color.Firebrick : Color.DarkGreen;
        }

        // ================================================================ log ao vivo

        private void OnLogAdded(LogEntry e)
        {
            lock (_pendingLog) { _pendingLog.Enqueue(e); while (_pendingLog.Count > 2000) _pendingLog.Dequeue(); }
        }

        private void FlushLog()
        {
            LogEntry[] items;
            lock (_pendingLog) { items = _pendingLog.ToArray(); _pendingLog.Clear(); }
            if (items.Length == 0) return;
            var sb = new StringBuilder();
            foreach (var e in items) sb.AppendLine(e.ToString());
            _txtLog.AppendText(sb.ToString());
            if (_txtLog.Lines.Length > 3000) _txtLog.Lines = _txtLog.Lines.Skip(_txtLog.Lines.Length - 2000).ToArray();
            UpdateStatus();
        }

        private void AppendLog(LogEntry e) => _txtLog.AppendText(e + Environment.NewLine);

        private void UiPost(Action a)
        {
            if (IsDisposed) return;
            try
            {
                if (InvokeRequired || !IsHandleCreated) { if (IsHandleCreated) BeginInvoke(a); }
                else a();
            }
            catch (InvalidOperationException) { }
        }

        // ================================================================ registro COM (controle ActiveX)

        [ComRegisterFunction]
        internal static void Register(Type t)
        {
            if (t != typeof(ConfigurationControl)) return;
            using (var cls = Registry.ClassesRoot.OpenSubKey(@"CLSID\" + t.GUID.ToString("B"), writable: true))
            {
                if (cls == null) return;
                cls.SetValue(null, DtmXml.DtmName + " DTM — configuração");
                cls.CreateSubKey("Control")?.Dispose();
                using (var m = cls.CreateSubKey("MiscStatus")) m?.SetValue(null, "131457"); // RECOMPOSEONRESIZE|CANTLINKINSIDE|INSIDEOUT|ACTIVATEWHENVISIBLE|SETCLIENTSITEFIRST
                using (var c = cls.CreateSubKey(@"Implemented Categories\{40FC6ED4-2438-11CF-A3DB-080036F12502}")) { } // CATID_Control
            }
        }

        [ComUnregisterFunction]
        internal static void Unregister(Type t)
        {
            if (t != typeof(ConfigurationControl)) return;
            using (var cls = Registry.ClassesRoot.OpenSubKey(@"CLSID\" + t.GUID.ToString("B"), writable: true))
            {
                if (cls == null) return;
                cls.DeleteSubKeyTree("Control", false);
                cls.DeleteSubKeyTree("MiscStatus", false);
                cls.DeleteSubKeyTree(@"Implemented Categories\{40FC6ED4-2438-11CF-A3DB-080036F12502}", false);
            }
        }
    }
}
