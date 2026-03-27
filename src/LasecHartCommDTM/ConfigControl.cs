using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Xml;
using Microsoft.Win32;
using Jigfdt.Fdt100;

namespace LasecHartCommDTM
{
    /// <summary>
    /// ActiveX UserControl embedded by PACTware for fdtConfiguration (functionId=1).
    /// PACTware creates this via CoCreateInstance using the CLSID from IDtmActiveXInformation.
    /// 
    /// KEY: We implement ICustomQueryInterface so that when PACTware asks for
    /// IOleObject, IOleInPlaceObject, IOleControl, etc., we forward these to
    /// the base Control class's internal implementation (ActiveXImpl).
    /// Without this, the CCW does not expose the OLE interfaces that PACTware
    /// needs for in-place activation / embedding.
    /// </summary>
    [Guid("B4B6B3E7-639D-460B-B9A0-6C7F7EB20030")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    [ProgId("LasecHartCommDTM.ConfigControl")]
    public class ConfigControl : UserControl, ICustomQueryInterface, IDtmActiveXControl
    {
        private IDtm _dtm;
        private string _invokeId;
        private string _functionCall;

        // OLE interface GUIDs that PACTware needs for in-place embedding
        private static readonly Guid IID_IOleObject           = new Guid("00000112-0000-0000-C000-000000000046");
        private static readonly Guid IID_IOleInPlaceObject    = new Guid("00000113-0000-0000-C000-000000000046");
        private static readonly Guid IID_IOleInPlaceActiveObj = new Guid("00000117-0000-0000-C000-000000000046");
        private static readonly Guid IID_IOleWindow           = new Guid("00000114-0000-0000-C000-000000000046");
        private static readonly Guid IID_IOleControl          = new Guid("B196B288-BAB4-101A-B69C-00AA00341D07");
        private static readonly Guid IID_IViewObject          = new Guid("0000010D-0000-0000-C000-000000000046");
        private static readonly Guid IID_IViewObject2         = new Guid("00000127-0000-0000-C000-000000000046");
        private static readonly Guid IID_IDataObject          = new Guid("0000010E-0000-0000-C000-000000000046");
        private static readonly Guid IID_IPersistStreamInit   = new Guid("7FD52380-4E07-101B-AE2D-08002B2EC713");
        private static readonly Guid IID_IPersistStorage      = new Guid("0000010A-0000-0000-C000-000000000046");
        private static readonly Guid IID_IPersist             = new Guid("0000010C-0000-0000-C000-000000000046");
        private static readonly Guid IID_IQuickActivate       = new Guid("CF51ED10-62FE-11CF-BF86-00A0C9034836");
        private static readonly Guid IID_IPersistPropertyBag  = new Guid("37D84F60-42CB-11CE-8135-00AA004BB851");
        private static readonly Guid IID_IDispatch            = new Guid("00020400-0000-0000-C000-000000000046");
        private static readonly Guid IID_IDtmActiveXControl  = new Guid("036D1486-387B-11D4-86E1-00E0987270B9");

        [ThreadStatic]
        private static bool _inQI;

        public CustomQueryInterfaceResult GetInterface(ref Guid iid, out IntPtr ppv)
        {
            ppv = IntPtr.Zero;

            if (_inQI)
                return CustomQueryInterfaceResult.NotHandled;

            // Log the QI
            string name = "unknown";
            if (iid == IID_IOleObject) name = "IOleObject";
            else if (iid == IID_IOleInPlaceObject) name = "IOleInPlaceObject";
            else if (iid == IID_IOleInPlaceActiveObj) name = "IOleInPlaceActiveObject";
            else if (iid == IID_IOleWindow) name = "IOleWindow";
            else if (iid == IID_IOleControl) name = "IOleControl";
            else if (iid == IID_IViewObject) name = "IViewObject";
            else if (iid == IID_IViewObject2) name = "IViewObject2";
            else if (iid == IID_IDataObject) name = "IDataObject";
            else if (iid == IID_IPersistStreamInit) name = "IPersistStreamInit";
            else if (iid == IID_IPersistStorage) name = "IPersistStorage";
            else if (iid == IID_IPersist) name = "IPersist";
            else if (iid == IID_IQuickActivate) name = "IQuickActivate";
            else if (iid == IID_IPersistPropertyBag) name = "IPersistPropertyBag";
            else if (iid == IID_IDispatch) name = "IDispatch";
            else if (iid == IID_IDtmActiveXControl) name = "IDtmActiveXControl";

            CommDtm.Log("ConfigControl.QI for " + iid.ToString("B") + " (" + name + ")");

            // Guard against recursion — Marshal calls below re-enter GetInterface
            _inQI = true;
            try
            {
                // For OLE interfaces, forward to base Control's internal ActiveXImpl.
                // For IDtmActiveXControl, the CCW finds it in the interface map
                // after we return NotHandled on the recursive call (_inQI=true).
                IntPtr pUnk = Marshal.GetIUnknownForObject(this);
                try
                {
                    Guid iidCopy = iid;
                    int hr = Marshal.QueryInterface(pUnk, ref iidCopy, out ppv);
                    if (hr == 0 && ppv != IntPtr.Zero)
                    {
                        CommDtm.Log("ConfigControl.QI -> HANDLED " + name + " (from base)");
                        // ppv already has AddRef from QueryInterface, return Handled
                        return CustomQueryInterfaceResult.Handled;
                    }
                }
                finally
                {
                    Marshal.Release(pUnk);
                }
            }
            catch (Exception ex)
            {
                CommDtm.Log("ConfigControl.QI ERROR for " + name + ": " + ex.Message);
            }
            finally
            {
                _inQI = false;
            }

            CommDtm.Log("ConfigControl.QI -> NOT HANDLED " + name);
            return CustomQueryInterfaceResult.NotHandled;
        }
        // Mode selection
        private ComboBox _cmbMode;           // "Serial" / "TCP/IP"

        // Serial fields
        private Label _lblComPort;
        private ComboBox _cmbComPort;
        private Label _lblBaudRate;
        private ComboBox _cmbBaudRate;

        // TCP/IP fields
        private Label _lblIpAddress;
        private TextBox _txtIpAddress;
        private Label _lblIpPort;
        private NumericUpDown _numIpPort;
        private Label _lblIpProtocol;
        private ComboBox _cmbProtocol;       // "udp" / "tcp"

        // Common HART fields
        private CheckBox _chkPrimaryMaster;
        private NumericUpDown _numPreambleCount;
        private NumericUpDown _numRetryCount;
        private NumericUpDown _numScanStart;
        private NumericUpDown _numScanStop;
        private CheckBox _chkBurstMode;
        private NumericUpDown _numTimeout;
        private Button _btnApply;

        // Communication Log
        private TextBox _txtLog;
        private Button _btnClearLog;

        private int _functionId;
        private bool _uiBuilt;

        public ConfigControl()
        {
            CommDtm.Log("ConfigControl() constructor");
            AutoScroll = true;
            Dock = DockStyle.Fill;
            BackColor = SystemColors.Control;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            CommDtm.Log("ConfigControl.OnHandleCreated()");
        }

        // ----------------------------------------------------------------
        // IDtmActiveXControl
        // ----------------------------------------------------------------

        public bool Init(string invokeId, string functionCall, IDtm dtm)
        {
            CommDtm.Log("ConfigControl.Init(invokeId=" + (invokeId ?? "null") +
                ", functionCall=" + (functionCall ?? "null") +
                ", dtm=" + (dtm != null ? "OK" : "null") + ")");
            _invokeId = invokeId;
            _functionCall = functionCall;
            _dtm = dtm;

            // Parse functionId from the functionCall XML
            _functionId = ParseFunctionId(functionCall);
            CommDtm.Log("ConfigControl.Init -> functionId=" + _functionId);

            if (!_uiBuilt)
            {
                _uiBuilt = true;
                BuildUI();
            }

            return true;
        }

        public bool PrepareToRelease()
        {
            CommDtm.Log("ConfigControl.PrepareToRelease()");
            _dtm = null;
            return true;
        }

        private static int ParseFunctionId(string functionCall)
        {
            if (string.IsNullOrEmpty(functionCall)) return 0;
            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(functionCall);
                var nsMgr = new XmlNamespaceManager(doc.NameTable);
                nsMgr.AddNamespace("func", "x-schema:DTMFunctionsSchema.xml");
                var attr = doc.SelectSingleNode("//*/@func:functionId", nsMgr);
                if (attr != null)
                {
                    int id;
                    if (int.TryParse(attr.Value, out id)) return id;
                }
            }
            catch { }
            return 0;
        }

        private void BuildUI()
        {
            SuspendLayout();
            Controls.Clear();

            switch (_functionId)
            {
                case 1:  // fdtConfiguration — "parâmetro"
                    BuildConfigurationUI();
                    break;
                case 10: // Communication log
                    BuildCommLogUI();
                    break;
                case 20: // Change device address
                    BuildChangeDeviceAddressUI();
                    break;
                case 30: // Change DTM address
                    BuildChangeDtmAddressUI();
                    break;
                case 100: // About
                    BuildAboutUI();
                    break;
                default:
                    Controls.Add(new Label
                    {
                        Text = "Unknown function: " + _functionId,
                        Dock = DockStyle.Fill,
                        TextAlign = ContentAlignment.MiddleCenter
                    });
                    break;
            }

            ResumeLayout(false);
        }

        // ================================================================
        // UI: Configuration (functionId=1) — "parâmetro"
        // ================================================================
        private void BuildConfigurationUI()
        {
            int y = 15;
            int lblW = 155, ctrlX = 180;
            int rowH = 30;

            // --- Communication Interface ---
            var grpComm = new GroupBox
            {
                Left = 10, Top = y, Width = 400, Height = 140,
                Text = "Communication Interface"
            };

            int gy = 20;

            // Mode (first row)
            grpComm.Controls.Add(new Label { Left = 10, Top = gy + 3, Width = lblW, Text = "Communication:" });
            _cmbMode = new ComboBox { Left = 170, Top = gy, Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbMode.Items.AddRange(new object[] { "Serial", "TCP/IP" });
            _cmbMode.SelectedIndex = 0;
            _cmbMode.SelectedIndexChanged += CmbMode_SelectedIndexChanged;
            grpComm.Controls.Add(_cmbMode);
            gy += rowH;

            // Serial fields (row 2 and 3)
            _lblComPort = new Label { Left = 10, Top = gy + 3, Width = lblW, Text = "COM Port:" };
            grpComm.Controls.Add(_lblComPort);
            _cmbComPort = new ComboBox { Left = 170, Top = gy, Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
            try { _cmbComPort.Items.AddRange(SerialPort.GetPortNames()); } catch { }
            if (_cmbComPort.Items.Count == 0) _cmbComPort.Items.Add("COM1");
            _cmbComPort.SelectedIndex = 0;
            grpComm.Controls.Add(_cmbComPort);
            gy += rowH;

            _lblBaudRate = new Label { Left = 10, Top = gy + 3, Width = lblW, Text = "Baud Rate:" };
            grpComm.Controls.Add(_lblBaudRate);
            _cmbBaudRate = new ComboBox { Left = 170, Top = gy, Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbBaudRate.Items.AddRange(new object[] { "1200", "2400", "4800", "9600", "19200" });
            _cmbBaudRate.SelectedIndex = 0;
            grpComm.Controls.Add(_cmbBaudRate);

            // TCP/IP fields (same rows, initially hidden)
            _lblIpAddress = new Label { Left = 10, Top = gy - rowH + 3, Width = lblW, Text = "IP Address:", Visible = false };
            grpComm.Controls.Add(_lblIpAddress);
            _txtIpAddress = new TextBox { Left = 170, Top = gy - rowH, Width = 180, Text = "127.0.0.1", Visible = false };
            grpComm.Controls.Add(_txtIpAddress);

            _lblIpPort = new Label { Left = 10, Top = gy + 3, Width = lblW, Text = "IP Port:", Visible = false };
            grpComm.Controls.Add(_lblIpPort);
            _numIpPort = new NumericUpDown { Left = 170, Top = gy, Width = 100, Minimum = 1, Maximum = 65535, Value = 5094, Visible = false };
            grpComm.Controls.Add(_numIpPort);
            gy += rowH;

            _lblIpProtocol = new Label { Left = 10, Top = gy + 3, Width = lblW, Text = "IP Protocol:", Visible = false };
            grpComm.Controls.Add(_lblIpProtocol);
            _cmbProtocol = new ComboBox { Left = 170, Top = gy, Width = 100, DropDownStyle = ComboBoxStyle.DropDownList, Visible = false };
            _cmbProtocol.Items.AddRange(new object[] { "udp", "tcp" });
            _cmbProtocol.SelectedIndex = 0;
            grpComm.Controls.Add(_cmbProtocol);

            Controls.Add(grpComm);
            y += grpComm.Height + 10;

            // --- HART Protocol ---
            var grpHart = new GroupBox
            {
                Left = 10, Top = y, Width = 400, Height = 105,
                Text = "HART Protocol"
            };

            gy = 20;
            _chkPrimaryMaster = new CheckBox { Left = 10, Top = gy, Width = 200, Text = "Primary Master", Checked = true };
            grpHart.Controls.Add(_chkPrimaryMaster);
            gy += rowH;

            grpHart.Controls.Add(new Label { Left = 10, Top = gy + 3, Width = 150, Text = "Preamble Count:" });
            _numPreambleCount = new NumericUpDown { Left = 170, Top = gy, Width = 80, Minimum = 5, Maximum = 20, Value = 5 };
            grpHart.Controls.Add(_numPreambleCount);
            gy += rowH;

            grpHart.Controls.Add(new Label { Left = 10, Top = gy + 3, Width = 150, Text = "Retry Count:" });
            _numRetryCount = new NumericUpDown { Left = 170, Top = gy, Width = 80, Minimum = 1, Maximum = 10, Value = 3 };
            grpHart.Controls.Add(_numRetryCount);

            Controls.Add(grpHart);
            y += grpHart.Height + 10;

            // --- Address Scan ---
            var grpAddr = new GroupBox
            {
                Left = 10, Top = y, Width = 400, Height = 75,
                Text = "Address Scan"
            };

            gy = 20;
            grpAddr.Controls.Add(new Label { Left = 10, Top = gy + 3, Width = 80, Text = "Start:" });
            _numScanStart = new NumericUpDown { Left = 90, Top = gy, Width = 70, Minimum = 0, Maximum = 63, Value = 0 };
            grpAddr.Controls.Add(_numScanStart);
            grpAddr.Controls.Add(new Label { Left = 180, Top = gy + 3, Width = 80, Text = "Stop:" });
            _numScanStop = new NumericUpDown { Left = 260, Top = gy, Width = 70, Minimum = 0, Maximum = 63, Value = 0 };
            grpAddr.Controls.Add(_numScanStop);
            gy += rowH;
            _chkBurstMode = new CheckBox { Left = 10, Top = gy, Width = 200, Text = "Burst Mode" };
            grpAddr.Controls.Add(_chkBurstMode);

            Controls.Add(grpAddr);
            y += grpAddr.Height + 10;

            // --- Communication Timeout ---
            Controls.Add(new Label { Left = 25, Top = y + 3, Width = lblW, Text = "Communication Timeout (ms):" });
            _numTimeout = new NumericUpDown { Left = ctrlX, Top = y, Width = 100, Minimum = 500, Maximum = 60000, Value = 5000, Increment = 500 };
            Controls.Add(_numTimeout);
            y += rowH + 10;

            // Apply button
            _btnApply = new Button { Left = ctrlX, Top = y, Width = 100, Height = 28, Text = "Apply" };
            _btnApply.Click += BtnApply_Click;
            Controls.Add(_btnApply);

            LoadConfigValues();
        }

        private void LoadConfigValues()
        {
            var dtm = CommDtm.Current;
            if (dtm == null) return;

            try
            {
                // Mode
                if (dtm._protocol == "serial")
                    _cmbMode.SelectedIndex = 0;
                else
                    _cmbMode.SelectedIndex = 1;

                // Serial
                int comIdx = _cmbComPort.FindStringExact(dtm._comPort);
                if (comIdx >= 0) _cmbComPort.SelectedIndex = comIdx;
                int baudIdx = _cmbBaudRate.FindStringExact(dtm._baudRate.ToString());
                if (baudIdx >= 0) _cmbBaudRate.SelectedIndex = baudIdx;

                // TCP/IP
                _txtIpAddress.Text = dtm._ipAddress;
                _numIpPort.Value = Clamp(_numIpPort, dtm._ipPort);
                if (dtm._protocol == "tcp" || dtm._protocol == "udp")
                    _cmbProtocol.SelectedItem = dtm._protocol;

                // Common
                _chkPrimaryMaster.Checked = dtm._primaryMaster;
                _numPreambleCount.Value = Clamp(_numPreambleCount, dtm._preambleCount);
                _numRetryCount.Value = Clamp(_numRetryCount, dtm._retryCount);
                _numScanStart.Value = Clamp(_numScanStart, dtm._scanStart);
                _numScanStop.Value = Clamp(_numScanStop, dtm._scanStop);
                _chkBurstMode.Checked = dtm._burstMode;
                _numTimeout.Value = Clamp(_numTimeout, dtm._timeout);
                CommDtm.Log("ConfigControl.LoadConfigValues() OK");
            }
            catch (Exception ex)
            {
                CommDtm.Log("ConfigControl.LoadConfigValues() error: " + ex.Message);
            }
        }

        private static decimal Clamp(NumericUpDown nud, int value)
        {
            return Math.Max(nud.Minimum, Math.Min(nud.Maximum, value));
        }

        private void CmbMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool serial = _cmbMode.SelectedIndex == 0;

            _lblComPort.Visible = serial;
            _cmbComPort.Visible = serial;
            _lblBaudRate.Visible = serial;
            _cmbBaudRate.Visible = serial;

            _lblIpAddress.Visible = !serial;
            _txtIpAddress.Visible = !serial;
            _lblIpPort.Visible = !serial;
            _numIpPort.Visible = !serial;
            _lblIpProtocol.Visible = !serial;
            _cmbProtocol.Visible = !serial;
        }

        private void BtnApply_Click(object sender, EventArgs e)
        {
            var dtm = CommDtm.Current;
            if (dtm == null) return;
            try
            {
                string protocol;
                if (_cmbMode.SelectedIndex == 0)
                    protocol = "serial";
                else
                    protocol = _cmbProtocol.SelectedItem?.ToString() ?? "udp";

                dtm.ApplyConfiguration(
                    protocol,
                    _cmbComPort.SelectedItem?.ToString() ?? "COM1",
                    int.Parse(_cmbBaudRate.SelectedItem?.ToString() ?? "1200"),
                    _txtIpAddress.Text,
                    (int)_numIpPort.Value,
                    _chkPrimaryMaster.Checked,
                    (int)_numPreambleCount.Value,
                    (int)_numRetryCount.Value,
                    (int)_numScanStart.Value,
                    (int)_numScanStop.Value,
                    _chkBurstMode.Checked,
                    (int)_numTimeout.Value);

                CommDtm.Log("ConfigControl: configuration applied");
            }
            catch (Exception ex)
            {
                CommDtm.Log("ConfigControl.Apply error: " + ex.Message);
                MessageBox.Show("Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ================================================================
        // UI: Communication Log (functionId=10)
        // ================================================================
        private void BuildCommLogUI()
        {
            var lbl = new Label
            {
                Text = "Communication Log",
                Font = new Font(Font.FontFamily, 10, FontStyle.Bold),
                Left = 10, Top = 10, Width = 300, Height = 22
            };
            Controls.Add(lbl);

            _txtLog = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Font = new Font("Consolas", 9),
                Left = 10, Top = 35,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            Controls.Add(_txtLog);

            _btnClearLog = new Button
            {
                Text = "Clear",
                Left = 10, Top = 10, Width = 70, Height = 25,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            _btnClearLog.Click += (s, ev) =>
            {
                CommDtm.ClearCommLog();
                _txtLog.Text = "";
            };
            Controls.Add(_btnClearLog);

            // Position after layout
            Resize += (s, ev) => LayoutCommLog();
            LayoutCommLog();

            // Load existing log entries
            _txtLog.Text = string.Join(Environment.NewLine, CommDtm.GetCommLog());

            // Subscribe to new log entries
            CommDtm.CommLogAdded += OnCommLogEntry;
        }

        private void LayoutCommLog()
        {
            if (_txtLog == null) return;
            _txtLog.Width = Math.Max(100, ClientSize.Width - 20);
            _txtLog.Height = Math.Max(50, ClientSize.Height - 75);
            _btnClearLog.Top = ClientSize.Height - 30;
        }

        private void OnCommLogEntry(string entry)
        {
            if (_txtLog == null || _txtLog.IsDisposed) return;
            if (_txtLog.InvokeRequired)
            {
                try { _txtLog.BeginInvoke(new Action<string>(OnCommLogEntry), entry); }
                catch { }
                return;
            }
            _txtLog.AppendText(entry + Environment.NewLine);
        }

        // ================================================================
        // UI: Change Device Address (functionId=20)
        // ================================================================
        private void BuildChangeDeviceAddressUI()
        {
            int y = 20;
            Controls.Add(new Label
            {
                Text = "Change Device Address",
                Font = new Font(Font.FontFamily, 10, FontStyle.Bold),
                Left = 15, Top = y, Width = 300, Height = 22
            });
            y += 35;

            Controls.Add(new Label { Left = 15, Top = y + 3, Width = 120, Text = "New Poll Address:" });
            var numAddr = new NumericUpDown { Left = 145, Top = y, Width = 80, Minimum = 0, Maximum = 63, Value = 0 };
            Controls.Add(numAddr);
            y += 40;

            var btnSet = new Button { Left = 145, Top = y, Width = 120, Height = 28, Text = "Set Address" };
            btnSet.Click += (s, ev) =>
            {
                int addr = (int)numAddr.Value;
                CommDtm.Log("Change device address to " + addr + " (not connected to HART engine yet)");
                MessageBox.Show("Device address set to " + addr + "\n(Will take effect when connected)",
                    "Change Device Address", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            Controls.Add(btnSet);
        }

        // ================================================================
        // UI: Change DTM Address (functionId=30)
        // ================================================================
        private void BuildChangeDtmAddressUI()
        {
            int y = 20;
            Controls.Add(new Label
            {
                Text = "Change DTM Address",
                Font = new Font(Font.FontFamily, 10, FontStyle.Bold),
                Left = 15, Top = y, Width = 300, Height = 22
            });
            y += 35;

            Controls.Add(new Label { Left = 15, Top = y + 3, Width = 120, Text = "DTM Address:" });
            var numAddr = new NumericUpDown { Left = 145, Top = y, Width = 80, Minimum = 0, Maximum = 63, Value = 0 };
            Controls.Add(numAddr);
            y += 40;

            var btnSet = new Button { Left = 145, Top = y, Width = 120, Height = 28, Text = "Set Address" };
            btnSet.Click += (s, ev) =>
            {
                int addr = (int)numAddr.Value;
                CommDtm.Log("Change DTM address to " + addr);
                MessageBox.Show("DTM address set to " + addr,
                    "Change DTM Address", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            Controls.Add(btnSet);
        }

        // ================================================================
        // UI: About (functionId=100)
        // ================================================================
        private void BuildAboutUI()
        {
            var lbl = new Label
            {
                Text = "Lasec HART Communication DTM\n\n" +
                       "Version 1.0.0\n\n" +
                       "HART Communication DTM for TCP/IP\n" +
                       "(UDP/TCP gateway to HART modem)\n\n" +
                       "LASEC - Universidade Federal de Uberlândia\n\n" +
                       "Based on FDT/DTM Standard 1.2",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font(Font.FontFamily, 10)
            };
            Controls.Add(lbl);
        }

        // ----------------------------------------------------------------
        // ActiveX COM Registration — required for PACTware to embed this
        // as an in-place OLE control in its MDI child window
        // ----------------------------------------------------------------
        [ComRegisterFunction]
        public static void Register(Type t)
        {
            try
            {
                string keyPath = @"CLSID\" + t.GUID.ToString("B");
                using (var key = Registry.ClassesRoot.OpenSubKey(keyPath, true))
                {
                    if (key == null) return;

                    // "Control" subkey marks this as an ActiveX control
                    key.CreateSubKey("Control");

                    // "Insertable" — required by some ActiveX containers
                    key.CreateSubKey("Insertable");

                    // MiscStatus — OLEMISC flags for in-place activation
                    // 131473 = RECOMPOSEONRESIZE | CANTLINKINSIDE | INSIDEOUT |
                    //          ACTIVATEWHENVISIBLE | SETCLIENTSITEFIRST
                    using (var ms = key.CreateSubKey("MiscStatus"))
                        ms?.SetValue("", "0");
                    using (var ms1 = key.CreateSubKey(@"MiscStatus\1"))
                        ms1?.SetValue("", "131473");

                    // TypeLib for interface marshaling
                    using (var tlb = key.CreateSubKey("TypeLib"))
                        tlb?.SetValue("", "{40498B38-0A79-3F68-905F-7954AA76AEA6}");

                    // Version
                    using (var ver = key.CreateSubKey("VERSION"))
                        ver?.SetValue("", "1.0");

                    // Implemented Categories — CATID_Control
                    using (var ic = key.CreateSubKey("Implemented Categories"))
                    {
                        ic?.CreateSubKey("{40FC6ED4-2438-11CF-A3DB-080036F12502}"); // Controls
                        ic?.CreateSubKey("{40FC6ED5-2438-11CF-A3DB-080036F12502}"); // Automation Objects
                    }
                }
            }
            catch (Exception ex)
            {
                CommDtm.Log("ConfigControl.Register error: " + ex.Message);
            }
        }

        [ComUnregisterFunction]
        public static void Unregister(Type t)
        {
            try
            {
                string keyPath = @"CLSID\" + t.GUID.ToString("B");
                using (var key = Registry.ClassesRoot.OpenSubKey(keyPath, true))
                {
                    if (key == null) return;
                    try { key.DeleteSubKeyTree("Control"); } catch { }
                    try { key.DeleteSubKeyTree("MiscStatus"); } catch { }
                }
            }
            catch { }
        }
    }
}
