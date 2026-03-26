using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LasecHartCommDTM
{
    /// <summary>
    /// ActiveX control for fdtConfiguration (functionId=1).
    /// PACTware embeds this in its MDI child window when the user opens "Parameter".
    /// Equivalent to CWHart's ctrlConfiguration.
    /// </summary>
    [Guid("B4B6B3E7-639D-460B-B9A0-6C7F7EB20030")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    [ProgId("LasecHartCommDTM.ConfigControl")]
    public class ConfigControl : ActiveXControlBase
    {
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

        protected override void CreateUI()
        {
            SuspendLayout();
            AutoScroll = true;
            Dock = DockStyle.Fill;
            BackColor = SystemColors.Control;

            int y = 15;
            int lblW = 155;
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
            _numTimeout = new NumericUpDown { Left = 180, Top = y, Width = 100, Minimum = 500, Maximum = 60000, Value = 5000, Increment = 500 };
            Controls.Add(_numTimeout);
            y += rowH + 10;

            // Apply button
            _btnApply = new Button { Left = 180, Top = y, Width = 100, Height = 28, Text = "Apply" };
            _btnApply.Click += BtnApply_Click;
            Controls.Add(_btnApply);

            ResumeLayout(false);

            // Load current values from CommDtm
            LoadCurrentValues();
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

        [ComRegisterFunction]
        public static void Register(Type t) { RegisterActiveXControl(t); }

        [ComUnregisterFunction]
        public static void Unregister(Type t) { UnregisterActiveXControl(t); }
    }
}
