using System;
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
            Height = 430;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;

            int y = 15;
            int lblX = 15, ctrlX = 160, lblW = 135, ctrlW = 200;
            int rowH = 30;

            // IP Address
            Controls.Add(new Label { Left = lblX, Top = y + 3, Width = lblW, Text = "IP Address:" });
            _txtIpAddress = new TextBox { Left = ctrlX, Top = y, Width = ctrlW, Text = "127.0.0.1" };
            Controls.Add(_txtIpAddress);
            y += rowH;

            // IP Port
            Controls.Add(new Label { Left = lblX, Top = y + 3, Width = lblW, Text = "IP Port:" });
            _numIpPort = new NumericUpDown { Left = ctrlX, Top = y, Width = 100, Minimum = 1, Maximum = 65535, Value = 5094 };
            Controls.Add(_numIpPort);
            y += rowH;

            // Protocol
            Controls.Add(new Label { Left = lblX, Top = y + 3, Width = lblW, Text = "Protocol:" });
            _cmbProtocol = new ComboBox { Left = ctrlX, Top = y, Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbProtocol.Items.AddRange(new object[] { "udp", "tcp" });
            _cmbProtocol.SelectedIndex = 0;
            Controls.Add(_cmbProtocol);
            y += rowH;

            // Primary Master
            _chkPrimaryMaster = new CheckBox { Left = ctrlX, Top = y, Width = ctrlW, Text = "Primary Master", Checked = true };
            Controls.Add(_chkPrimaryMaster);
            y += rowH;

            // Preamble Count
            Controls.Add(new Label { Left = lblX, Top = y + 3, Width = lblW, Text = "Preamble Count:" });
            _numPreambleCount = new NumericUpDown { Left = ctrlX, Top = y, Width = 100, Minimum = 5, Maximum = 20, Value = 5 };
            Controls.Add(_numPreambleCount);
            y += rowH;

            // Retry Count
            Controls.Add(new Label { Left = lblX, Top = y + 3, Width = lblW, Text = "Retry Count:" });
            _numRetryCount = new NumericUpDown { Left = ctrlX, Top = y, Width = 100, Minimum = 1, Maximum = 10, Value = 3 };
            Controls.Add(_numRetryCount);
            y += rowH;

            // Scan Start
            Controls.Add(new Label { Left = lblX, Top = y + 3, Width = lblW, Text = "Scan Start Address:" });
            _numScanStart = new NumericUpDown { Left = ctrlX, Top = y, Width = 100, Minimum = 0, Maximum = 63, Value = 0 };
            Controls.Add(_numScanStart);
            y += rowH;

            // Scan Stop
            Controls.Add(new Label { Left = lblX, Top = y + 3, Width = lblW, Text = "Scan Stop Address:" });
            _numScanStop = new NumericUpDown { Left = ctrlX, Top = y, Width = 100, Minimum = 0, Maximum = 63, Value = 0 };
            Controls.Add(_numScanStop);
            y += rowH;

            // Burst Mode
            _chkBurstMode = new CheckBox { Left = ctrlX, Top = y, Width = ctrlW, Text = "Burst Mode" };
            Controls.Add(_chkBurstMode);
            y += rowH;

            // Timeout
            Controls.Add(new Label { Left = lblX, Top = y + 3, Width = lblW, Text = "Timeout (ms):" });
            _numTimeout = new NumericUpDown { Left = ctrlX, Top = y, Width = 100, Minimum = 500, Maximum = 60000, Value = 5000, Increment = 500 };
            Controls.Add(_numTimeout);
            y += rowH + 5;

            // Apply button
            _btnApply = new Button { Left = ctrlX, Top = y, Width = 100, Height = 28, Text = "Apply" };
            _btnApply.Click += BtnApply_Click;
            Controls.Add(_btnApply);
        }

        private void LoadCurrentValues()
        {
            if (_dtm == null) return;
            _txtIpAddress.Text = _dtm._ipAddress;
            _numIpPort.Value = Math.Max(_numIpPort.Minimum, Math.Min(_numIpPort.Maximum, _dtm._ipPort));
            _cmbProtocol.SelectedItem = _dtm._protocol;
            if (_cmbProtocol.SelectedIndex < 0) _cmbProtocol.SelectedIndex = 0;
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
                _dtm.ApplyConfiguration(
                    _txtIpAddress.Text,
                    (int)_numIpPort.Value,
                    _cmbProtocol.SelectedItem.ToString(),
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