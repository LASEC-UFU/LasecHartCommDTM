using System;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using LasecHartCommDTM.FdtInterfaces;  // IDtmView only

namespace LasecHartCommDTM
{
    [Guid("B4B6B3E7-639D-460B-B9A0-6C7F7EB20020")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    public class DtmView : Form, IDtmView
    {
        private readonly CommDtm _dtm;

        // Protocol selection (first field)
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
        private ComboBox _cmbIpProtocol;     // "udp" / "tcp"

        // Common HART fields
        private CheckBox _chkPrimaryMaster;
        private NumericUpDown _numPreambleCount;
        private NumericUpDown _numRetryCount;
        private NumericUpDown _numScanStart;
        private NumericUpDown _numScanStop;
        private CheckBox _chkBurstMode;
        private NumericUpDown _numTimeout;
        private Button _btnApply;

        public DtmView()
        {
            _dtm = CommDtm.Current;
            if (_dtm == null)
            {
                _dtm = new CommDtm();
                _dtm.InitNew("CommDTM");
            }
            InitializeComponents();
            LoadCurrentValues();
        }

        private void InitializeComponents()
        {
            Text = "Lasec HART Communication DTM - Configuration";
            Width = 420;
            Height = 480;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;

            int y = 15;
            int lblX = 15, ctrlX = 160, lblW = 135, ctrlW = 200;
            int rowH = 30;

            // ---- Mode (first field) ----
            Controls.Add(new Label { Left = lblX, Top = y + 3, Width = lblW, Text = "Communication:" });
            _cmbMode = new ComboBox { Left = ctrlX, Top = y, Width = ctrlW, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbMode.Items.AddRange(new object[] { "Serial", "TCP/IP" });
            _cmbMode.SelectedIndex = 0;
            _cmbMode.SelectedIndexChanged += CmbMode_SelectedIndexChanged;
            Controls.Add(_cmbMode);
            y += rowH;

            // ---- Serial fields ----
            _lblComPort = new Label { Left = lblX, Top = y + 3, Width = lblW, Text = "COM Port:" };
            Controls.Add(_lblComPort);
            _cmbComPort = new ComboBox { Left = ctrlX, Top = y, Width = ctrlW, DropDownStyle = ComboBoxStyle.DropDownList };
            try { _cmbComPort.Items.AddRange(SerialPort.GetPortNames()); } catch { }
            if (_cmbComPort.Items.Count == 0) _cmbComPort.Items.Add("COM1");
            _cmbComPort.SelectedIndex = 0;
            Controls.Add(_cmbComPort);
            y += rowH;

            _lblBaudRate = new Label { Left = lblX, Top = y + 3, Width = lblW, Text = "Baud Rate:" };
            Controls.Add(_lblBaudRate);
            _cmbBaudRate = new ComboBox { Left = ctrlX, Top = y, Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbBaudRate.Items.AddRange(new object[] { "1200", "2400", "4800", "9600", "19200" });
            _cmbBaudRate.SelectedIndex = 0;  // 1200 = HART default
            Controls.Add(_cmbBaudRate);
            y += rowH;

            // ---- TCP/IP fields (initially hidden) ----
            _lblIpAddress = new Label { Left = lblX, Top = y + 3, Width = lblW, Text = "IP Address:", Visible = false };
            Controls.Add(_lblIpAddress);
            _txtIpAddress = new TextBox { Left = ctrlX, Top = y, Width = ctrlW, Text = "127.0.0.1", Visible = false };
            Controls.Add(_txtIpAddress);

            _lblIpPort = new Label { Left = lblX, Top = y + rowH + 3, Width = lblW, Text = "IP Port:", Visible = false };
            Controls.Add(_lblIpPort);
            _numIpPort = new NumericUpDown { Left = ctrlX, Top = y + rowH, Width = 100, Minimum = 1, Maximum = 65535, Value = 5094, Visible = false };
            Controls.Add(_numIpPort);

            _lblIpProtocol = new Label { Left = lblX, Top = y + rowH * 2 + 3, Width = lblW, Text = "IP Protocol:", Visible = false };
            Controls.Add(_lblIpProtocol);
            _cmbIpProtocol = new ComboBox { Left = ctrlX, Top = y + rowH * 2, Width = 100, DropDownStyle = ComboBoxStyle.DropDownList, Visible = false };
            _cmbIpProtocol.Items.AddRange(new object[] { "udp", "tcp" });
            _cmbIpProtocol.SelectedIndex = 0;
            Controls.Add(_cmbIpProtocol);

            // Skip 3 rows for TCP fields (same space as serial 2 rows + 1 extra)
            y += rowH * 3;

            // ---- Common HART fields ----
            _chkPrimaryMaster = new CheckBox { Left = ctrlX, Top = y, Width = ctrlW, Text = "Primary Master", Checked = true };
            Controls.Add(_chkPrimaryMaster);
            y += rowH;

            Controls.Add(new Label { Left = lblX, Top = y + 3, Width = lblW, Text = "Preamble Count:" });
            _numPreambleCount = new NumericUpDown { Left = ctrlX, Top = y, Width = 100, Minimum = 5, Maximum = 20, Value = 5 };
            Controls.Add(_numPreambleCount);
            y += rowH;

            Controls.Add(new Label { Left = lblX, Top = y + 3, Width = lblW, Text = "Retry Count:" });
            _numRetryCount = new NumericUpDown { Left = ctrlX, Top = y, Width = 100, Minimum = 1, Maximum = 10, Value = 3 };
            Controls.Add(_numRetryCount);
            y += rowH;

            Controls.Add(new Label { Left = lblX, Top = y + 3, Width = lblW, Text = "Scan Start Address:" });
            _numScanStart = new NumericUpDown { Left = ctrlX, Top = y, Width = 100, Minimum = 0, Maximum = 63, Value = 0 };
            Controls.Add(_numScanStart);
            y += rowH;

            Controls.Add(new Label { Left = lblX, Top = y + 3, Width = lblW, Text = "Scan Stop Address:" });
            _numScanStop = new NumericUpDown { Left = ctrlX, Top = y, Width = 100, Minimum = 0, Maximum = 63, Value = 0 };
            Controls.Add(_numScanStop);
            y += rowH;

            _chkBurstMode = new CheckBox { Left = ctrlX, Top = y, Width = ctrlW, Text = "Burst Mode" };
            Controls.Add(_chkBurstMode);
            y += rowH;

            Controls.Add(new Label { Left = lblX, Top = y + 3, Width = lblW, Text = "Timeout (ms):" });
            _numTimeout = new NumericUpDown { Left = ctrlX, Top = y, Width = 100, Minimum = 500, Maximum = 60000, Value = 5000, Increment = 500 };
            Controls.Add(_numTimeout);
            y += rowH + 5;

            _btnApply = new Button { Left = ctrlX, Top = y, Width = 100, Height = 28, Text = "Apply" };
            _btnApply.Click += BtnApply_Click;
            Controls.Add(_btnApply);
        }

        private void CmbMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool serial = _cmbMode.SelectedIndex == 0; // 0 = Serial, 1 = TCP/IP

            // Serial fields
            _lblComPort.Visible = serial;
            _cmbComPort.Visible = serial;
            _lblBaudRate.Visible = serial;
            _cmbBaudRate.Visible = serial;

            // TCP/IP fields
            _lblIpAddress.Visible = !serial;
            _txtIpAddress.Visible = !serial;
            _lblIpPort.Visible = !serial;
            _numIpPort.Visible = !serial;
            _lblIpProtocol.Visible = !serial;
            _cmbIpProtocol.Visible = !serial;
        }

        private void LoadCurrentValues()
        {
            if (_dtm == null) return;

            // Protocol mode
            if (_dtm._protocol == "serial")
                _cmbMode.SelectedIndex = 0;
            else
                _cmbMode.SelectedIndex = 1;

            // Serial
            int comIdx = _cmbComPort.FindStringExact(_dtm._comPort);
            if (comIdx >= 0) _cmbComPort.SelectedIndex = comIdx;
            int baudIdx = _cmbBaudRate.FindStringExact(_dtm._baudRate.ToString());
            if (baudIdx >= 0) _cmbBaudRate.SelectedIndex = baudIdx;

            // TCP/IP
            _txtIpAddress.Text = _dtm._ipAddress;
            _numIpPort.Value = Math.Max(_numIpPort.Minimum, Math.Min(_numIpPort.Maximum, _dtm._ipPort));
            if (_dtm._protocol == "tcp" || _dtm._protocol == "udp")
                _cmbIpProtocol.SelectedItem = _dtm._protocol;

            // Common
            _chkPrimaryMaster.Checked = _dtm._primaryMaster;
            _numPreambleCount.Value = Math.Max(_numPreambleCount.Minimum, Math.Min(_numPreambleCount.Maximum, _dtm._preambleCount));
            _numRetryCount.Value = Math.Max(_numRetryCount.Minimum, Math.Min(_numRetryCount.Maximum, _dtm._retryCount));
            _numScanStart.Value = Math.Max(_numScanStart.Minimum, Math.Min(_numScanStart.Maximum, _dtm._scanStart));
            _numScanStop.Value = Math.Max(_numScanStop.Minimum, Math.Min(_numScanStop.Maximum, _dtm._scanStop));
            _chkBurstMode.Checked = _dtm._burstMode;
            _numTimeout.Value = Math.Max(_numTimeout.Minimum, Math.Min(_numTimeout.Maximum, _dtm._timeout));
        }

        private void BtnApply_Click(object sender, EventArgs e)
        {
            try
            {
                string protocol;
                if (_cmbMode.SelectedIndex == 0)
                    protocol = "serial";
                else
                    protocol = _cmbIpProtocol.SelectedItem.ToString();

                _dtm.ApplyConfiguration(
                    protocol,
                    _cmbComPort.SelectedItem.ToString(),
                    int.Parse(_cmbBaudRate.SelectedItem.ToString()),
                    _txtIpAddress.Text,
                    (int)_numIpPort.Value,
                    _chkPrimaryMaster.Checked,
                    (int)_numPreambleCount.Value,
                    (int)_numRetryCount.Value,
                    (int)_numScanStart.Value,
                    (int)_numScanStop.Value,
                    _chkBurstMode.Checked,
                    (int)_numTimeout.Value);

                MessageBox.Show("Configuration applied.", "Lasec HART Communication DTM",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error applying configuration:\n" + ex.Message,
                    "Lasec HART Communication DTM", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void IDtmView.Show()
        {
            if (!Visible)
                base.Show();
            else
                Activate();
        }

        void IDtmView.Hide()
        {
            base.Hide();
        }
    }
}