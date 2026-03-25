using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace LasecHartCommDTM
{
    /// <summary>
    /// ActiveX UserControl embedded by PACTware for fdtConfiguration (functionId=1).
    /// PACTware creates this via CoCreateInstance using the ProgId from IDtmActiveXInformation.
    ///
    /// KEY CHANGES for OLE embedding:
    /// 1. ClassInterfaceType.None — prevents the auto-generated dual interface from masking
    ///    the OLE embedding interfaces (IOleObject, IOleInPlaceObject, etc.) that are
    ///    implemented internally by System.Windows.Forms.Control via ActiveXImpl.
    ///    With AutoDual, the CCW's default interface would shadow IDispatch from IOleObject.
    ///
    /// 2. ICustomQueryInterface — logs all QueryInterface calls from PACTware so we can
    ///    diagnose exactly which interfaces are being requested and whether they succeed.
    ///
    /// 3. Deferred UI creation — child controls are NOT created in the constructor.
    ///    They are created in OnHandleCreated, which fires AFTER the OLE container
    ///    (PACTware) has called IOleObject.SetClientSite() and IOleObject.DoVerb().
    ///    This ensures the HWND is created with the correct parent window from PACTware's
    ///    MDI child, rather than as a premature top-level window.
    /// </summary>
    [Guid("B4B6B3E7-639D-460B-B9A0-6C7F7EB20030")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    [ProgId("LasecHartCommDTM.ConfigControl")]
    public class ConfigControl : UserControl, ICustomQueryInterface
    {
        // Well-known OLE interface GUIDs for QI logging
        private static readonly Guid IID_IOleObject              = new Guid("00000112-0000-0000-C000-000000000046");
        private static readonly Guid IID_IOleInPlaceObject       = new Guid("00000113-0000-0000-C000-000000000046");
        private static readonly Guid IID_IOleWindow              = new Guid("00000114-0000-0000-C000-000000000046");
        private static readonly Guid IID_IOleInPlaceActiveObject = new Guid("00000115-0000-0000-C000-000000000046");
        private static readonly Guid IID_IOleControl             = new Guid("B196B288-BAB4-101A-B69C-00AA00341D07");
        private static readonly Guid IID_IViewObject             = new Guid("0000010D-0000-0000-C000-000000000046");
        private static readonly Guid IID_IViewObject2            = new Guid("00000127-0000-0000-C000-000000000046");
        private static readonly Guid IID_IDataObject             = new Guid("0000010E-0000-0000-C000-000000000046");
        private static readonly Guid IID_IPersistStreamInit      = new Guid("7FD52380-4E07-101B-AE2D-08002B2EC713");
        private static readonly Guid IID_IPersistStorage         = new Guid("0000010A-0000-0000-C000-000000000046");
        private static readonly Guid IID_IPersistPropertyBag     = new Guid("37D84F60-42CB-11CE-8135-00AA004BB851");
        private static readonly Guid IID_IQuickActivate          = new Guid("CF51ED10-62FE-11CF-BF86-00A0C9034836");
        private static readonly Guid IID_IDispatch               = new Guid("00020400-0000-0000-C000-000000000046");
        private static readonly Guid IID_IConnectionPointContainer = new Guid("B196B284-BAB4-101A-B69C-00AA00341D07");
        private static readonly Guid IID_IProvideClassInfo       = new Guid("B196B283-BAB4-101A-B69C-00AA00341D07");
        private static readonly Guid IID_ISpecifyPropertyPages   = new Guid("B196B28B-BAB4-101A-B69C-00AA00341D07");

        private bool _uiCreated;

        private TextBox _txtIpAddress;
        private NumericUpDown _numIpPort;
        private ComboBox _cmbProtocol;
        private CheckBox _chkPrimaryMaster;
        private NumericUpDown _numPreambleCount;
        private NumericUpDown _numRetryCount;
        private NumericUpDown _numScanStart;
        private NumericUpDown _numScanStop;
        private CheckBox _chkBurstMode;
        private NumericUpDown _numTimeout;
        private Button _btnApply;

        // ----------------------------------------------------------------
        // ICustomQueryInterface — intercept and log all QueryInterface calls
        // from PACTware to understand which OLE interfaces are being requested.
        // Returns NotHandled for all interfaces so the CCW's normal QI logic
        // handles them (which includes OLE interfaces from base Control).
        // ----------------------------------------------------------------
        public CustomQueryInterfaceResult GetInterface(ref Guid iid, out IntPtr ppv)
        {
            ppv = IntPtr.Zero;

            string name = IdentifyInterface(iid);
            CommDtm.Log("ConfigControl.QI " + iid.ToString("B") + " (" + name + ")");

            // Let the CCW handle all interfaces normally.
            // The base Control class implements IOleObject, IOleInPlaceObject, etc.
            // internally via ActiveXImpl — the CCW exposes them automatically.
            return CustomQueryInterfaceResult.NotHandled;
        }

        private static string IdentifyInterface(Guid iid)
        {
            if (iid == IID_IOleObject) return "IOleObject";
            if (iid == IID_IOleInPlaceObject) return "IOleInPlaceObject";
            if (iid == IID_IOleWindow) return "IOleWindow";
            if (iid == IID_IOleInPlaceActiveObject) return "IOleInPlaceActiveObject";
            if (iid == IID_IOleControl) return "IOleControl";
            if (iid == IID_IViewObject) return "IViewObject";
            if (iid == IID_IViewObject2) return "IViewObject2";
            if (iid == IID_IDataObject) return "IDataObject";
            if (iid == IID_IPersistStreamInit) return "IPersistStreamInit";
            if (iid == IID_IPersistStorage) return "IPersistStorage";
            if (iid == IID_IPersistPropertyBag) return "IPersistPropertyBag";
            if (iid == IID_IQuickActivate) return "IQuickActivate";
            if (iid == IID_IDispatch) return "IDispatch";
            if (iid == IID_IConnectionPointContainer) return "IConnectionPointContainer";
            if (iid == IID_IProvideClassInfo) return "IProvideClassInfo";
            if (iid == IID_ISpecifyPropertyPages) return "ISpecifyPropertyPages";
            return "unknown";
        }

        public ConfigControl()
        {
            CommDtm.Log("ConfigControl() constructor START");
            // IMPORTANT: Do NOT create child controls here!
            // In OLE hosting, the sequence is:
            //   1. CoCreateInstance → constructor (no HWND yet)
            //   2. IOleObject.SetClientSite(pClientSite) → stores OLE client site
            //   3. IOleObject.DoVerb(OLEIVERB_INPLACEACTIVATE) → creates HWND
            //      with correct parent (PACTware's MDI child window)
            //   4. OnHandleCreated fires → we create child controls here
            //
            // If we create child controls in the constructor, they may force
            // premature HWND creation (top-level, wrong parent), which prevents
            // OLE InPlace activation from working correctly.
            CommDtm.Log("ConfigControl() constructor END");
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            CommDtm.Log("ConfigControl.OnHandleCreated() hwnd=0x" + Handle.ToString("X"));

            if (!_uiCreated)
            {
                _uiCreated = true;
                try
                {
                    InitializeComponents();
                    LoadCurrentValues();
                }
                catch (Exception ex)
                {
                    CommDtm.Log("ConfigControl.OnHandleCreated UI error: " + ex.Message);
                }
            }
        }

        private void InitializeComponents()
        {
            SuspendLayout();
            AutoScroll = true;
            Dock = DockStyle.Fill;
            BackColor = SystemColors.Control;

            int y = 15;
            int lblW = 155;
            int ctrlX = 180;
            int rowH = 30;

            // --- Communication Interface ---
            var grpComm = new GroupBox
            {
                Left = 10, Top = y, Width = 400, Height = 105,
                Text = "Communication Interface"
            };

            int gy = 20;
            grpComm.Controls.Add(new Label { Left = 10, Top = gy + 3, Width = lblW, Text = "IP Address:" });
            _txtIpAddress = new TextBox { Left = 170, Top = gy, Width = 180, Text = "127.0.0.1" };
            grpComm.Controls.Add(_txtIpAddress);
            gy += rowH;

            grpComm.Controls.Add(new Label { Left = 10, Top = gy + 3, Width = lblW, Text = "IP Port:" });
            _numIpPort = new NumericUpDown { Left = 170, Top = gy, Width = 100, Minimum = 1, Maximum = 65535, Value = 5094 };
            grpComm.Controls.Add(_numIpPort);
            gy += rowH;

            grpComm.Controls.Add(new Label { Left = 10, Top = gy + 3, Width = lblW, Text = "Protocol:" });
            _cmbProtocol = new ComboBox { Left = 170, Top = gy, Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
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

            ResumeLayout(false);
            CommDtm.Log("ConfigControl.InitializeComponents() OK");
        }

        private void LoadCurrentValues()
        {
            var dtm = CommDtm.Current;
            if (dtm == null) return;

            try
            {
                _txtIpAddress.Text = dtm._ipAddress;
                _numIpPort.Value = Clamp(_numIpPort, dtm._ipPort);
                _cmbProtocol.SelectedItem = dtm._protocol;
                if (_cmbProtocol.SelectedIndex < 0) _cmbProtocol.SelectedIndex = 0;
                _chkPrimaryMaster.Checked = dtm._primaryMaster;
                _numPreambleCount.Value = Clamp(_numPreambleCount, dtm._preambleCount);
                _numRetryCount.Value = Clamp(_numRetryCount, dtm._retryCount);
                _numScanStart.Value = Clamp(_numScanStart, dtm._scanStart);
                _numScanStop.Value = Clamp(_numScanStop, dtm._scanStop);
                _chkBurstMode.Checked = dtm._burstMode;
                _numTimeout.Value = Clamp(_numTimeout, dtm._timeout);
                CommDtm.Log("ConfigControl.LoadCurrentValues() OK");
            }
            catch (Exception ex)
            {
                CommDtm.Log("ConfigControl.LoadCurrentValues() error: " + ex.Message);
            }
        }

        private static decimal Clamp(NumericUpDown nud, int value)
        {
            return Math.Max(nud.Minimum, Math.Min(nud.Maximum, value));
        }

        private void BtnApply_Click(object sender, EventArgs e)
        {
            var dtm = CommDtm.Current;
            if (dtm == null) return;
            try
            {
                dtm.ApplyConfiguration(
                    _txtIpAddress.Text,
                    (int)_numIpPort.Value,
                    _cmbProtocol.SelectedItem?.ToString() ?? "udp",
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

        // ----------------------------------------------------------------
        // ActiveX COM Registration — required for PACTware to embed this
        // as an in-place OLE control in its MDI child window.
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

                    // Implemented Categories — CATID_Control + Automation Objects
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
