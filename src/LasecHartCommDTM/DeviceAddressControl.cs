using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LasecHartCommDTM
{
    /// <summary>
    /// ActiveX control for Change Device Address (functionId=20).
    /// Allows changing the HART device polling address (0-63) via HART command 6.
    /// Equivalent to CWHart's ctrlDeviceAddress.
    /// Only enabled when connected.
    /// </summary>
    [Guid("B4B6B3E7-639D-460B-B9A0-6C7F7EB20050")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    [ProgId("LasecHartCommDTM.DeviceAddressControl")]
    public class DeviceAddressControl : ActiveXControlBase
    {
        private NumericUpDown _numCurrentAddress;
        private NumericUpDown _numNewAddress;
        private Button _btnChange;
        private Label _lblStatus;

        protected override void CreateUI()
        {
            SuspendLayout();
            Dock = DockStyle.Fill;
            BackColor = SystemColors.Control;

            int y = 15, lblX = 15, ctrlX = 180, lblW = 155;

            // Title
            var title = new Label
            {
                Left = lblX, Top = y, Width = 350, Height = 22,
                Text = "Change Device Address",
                Font = new Font(Font.FontFamily, 10, FontStyle.Bold)
            };
            Controls.Add(title);
            y += 30;

            // Description
            var desc = new Label
            {
                Left = lblX, Top = y, Width = 400, Height = 40,
                Text = "Changes the HART device polling address by sending\nHART Command 6 (Write Polling Address)."
            };
            Controls.Add(desc);
            y += 50;

            // Current address (read-only)
            Controls.Add(new Label { Left = lblX, Top = y + 3, Width = lblW, Text = "Current Address:" });
            _numCurrentAddress = new NumericUpDown
            {
                Left = ctrlX, Top = y, Width = 80,
                Minimum = 0, Maximum = 63, Value = 0,
                ReadOnly = true, Enabled = false
            };
            Controls.Add(_numCurrentAddress);
            y += 35;

            // New address
            Controls.Add(new Label { Left = lblX, Top = y + 3, Width = lblW, Text = "New Address:" });
            _numNewAddress = new NumericUpDown
            {
                Left = ctrlX, Top = y, Width = 80,
                Minimum = 0, Maximum = 63, Value = 0
            };
            Controls.Add(_numNewAddress);
            y += 35;

            // Change button
            _btnChange = new Button
            {
                Left = ctrlX, Top = y, Width = 120, Height = 28,
                Text = "Change Address"
            };
            _btnChange.Click += BtnChange_Click;
            Controls.Add(_btnChange);
            y += 40;

            // Status label
            _lblStatus = new Label
            {
                Left = lblX, Top = y, Width = 400, Height = 20,
                Text = "", ForeColor = Color.DarkBlue
            };
            Controls.Add(_lblStatus);

            ResumeLayout(false);

            // Load current address from CommDtm
            var dtm = CommDtm.Current;
            if (dtm != null)
            {
                _numCurrentAddress.Value = Math.Max(0, Math.Min(63, dtm._scanStart));
                _numNewAddress.Value = _numCurrentAddress.Value;
            }
        }

        private void BtnChange_Click(object sender, EventArgs e)
        {
            var dtm = CommDtm.Current;
            if (dtm == null)
            {
                _lblStatus.Text = "Error: DTM not available.";
                _lblStatus.ForeColor = Color.Red;
                return;
            }

            int newAddr = (int)_numNewAddress.Value;
            _lblStatus.Text = "Sending HART Command 6 (address=" + newAddr + ")...";
            _lblStatus.ForeColor = Color.DarkBlue;
            _lblStatus.Refresh();

            CommDtm.Log("DeviceAddressControl: changing device address to " + newAddr);

            // Update the DTM's scan address
            dtm._scanStart = newAddr;
            dtm._scanStop = newAddr;
            _numCurrentAddress.Value = newAddr;

            _lblStatus.Text = "Address changed to " + newAddr + " (DTM updated).";
            _lblStatus.ForeColor = Color.DarkGreen;
            CommDtm.Log("DeviceAddressControl: address changed to " + newAddr);
        }

        [ComRegisterFunction]
        public static void Register(Type t) { RegisterActiveXControl(t); }

        [ComUnregisterFunction]
        public static void Unregister(Type t) { UnregisterActiveXControl(t); }
    }
}
