using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using LasecHartCommDTM.FdtInterfaces;

namespace LasecHartCommDTM
{
    [Guid("B4B6B3E7-639D-460B-B9A0-6C7F7EB20020")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    public class DtmView : Form, IDtmView
    {
        private readonly CommDtm _dtm;

        private ComboBox _cmbMode;
        private ComboBox _cmbSerialPort;
        private TextBox _txtUdpHost;
        private NumericUpDown _numUdpPort;
        private NumericUpDown _numBaud;
        private Button _btnApply;

        public DtmView()
        {
            _dtm = CommDtm.Current;
            if (_dtm == null)
            {
                _dtm = new CommDtm();
                _dtm.Initialize(IntPtr.Zero);
            }
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            Text = "Lasec HART Communication DTM - Config";
            Width = 400;
            Height = 250;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;

            var lblMode = new Label { Left = 10, Top = 20, Width = 100, Text = "Mode:" };
            _cmbMode = new ComboBox { Left = 120, Top = 18, Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbMode.Items.AddRange(new object[] { "udp", "serial" });
            _cmbMode.SelectedIndex = 0;

            var lblSerialPort = new Label { Left = 10, Top = 50, Width = 100, Text = "Serial Port:" };
            _cmbSerialPort = new ComboBox { Left = 120, Top = 48, Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
            try
            {
                var ports = System.IO.Ports.SerialPort.GetPortNames().OrderBy(p => p).ToArray();
                if (ports.Length == 0) ports = new[] { "COM1" };
                _cmbSerialPort.Items.AddRange(ports);
                _cmbSerialPort.SelectedIndex = 0;
            }
            catch
            {
                _cmbSerialPort.Items.Add("COM1");
                _cmbSerialPort.SelectedIndex = 0;
            }

            var lblBaud = new Label { Left = 10, Top = 80, Width = 100, Text = "Baud rate:" };
            _numBaud = new NumericUpDown { Left = 120, Top = 78, Width = 100, Minimum = 120, Maximum = 115200, Value = 1200, Increment = 120 };

            var lblUdpHost = new Label { Left = 10, Top = 110, Width = 100, Text = "UDP Host:" };
            _txtUdpHost = new TextBox { Left = 120, Top = 108, Width = 150, Text = "127.0.0.1" };

            var lblUdpPort = new Label { Left = 10, Top = 140, Width = 100, Text = "UDP Port:" };
            _numUdpPort = new NumericUpDown { Left = 120, Top = 138, Width = 100, Minimum = 1, Maximum = 65535, Value = 5094 };

            _btnApply = new Button { Left = 120, Top = 170, Width = 100, Text = "Apply" };
            _btnApply.Click += BtnApply_Click;

            Controls.Add(lblMode);
            Controls.Add(_cmbMode);
            Controls.Add(lblSerialPort);
            Controls.Add(_cmbSerialPort);
            Controls.Add(lblBaud);
            Controls.Add(_numBaud);
            Controls.Add(lblUdpHost);
            Controls.Add(_txtUdpHost);
            Controls.Add(lblUdpPort);
            Controls.Add(_numUdpPort);
            Controls.Add(_btnApply);
        }

        private void BtnApply_Click(object sender, EventArgs e)
        {
            try
            {
                string mode = _cmbMode.SelectedItem.ToString();
                string serialPort = _cmbSerialPort.SelectedItem.ToString();
                string host = _txtUdpHost.Text;
                int port = (int)_numUdpPort.Value;
                int baud = (int)_numBaud.Value;

                _dtm.Configure(mode, serialPort, host, port, baud);
                MessageBox.Show("Configuration applied.", "Lasec HART Communication DTM", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error applying configuration:\n" + ex.Message, "Lasec HART Communication DTM", MessageBoxButtons.OK, MessageBoxIcon.Error);
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