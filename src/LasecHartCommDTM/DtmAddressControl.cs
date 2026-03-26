using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LasecHartCommDTM
{
    /// <summary>
    /// ActiveX control for Change DTM Address (functionId=30).
    /// Changes the DTM's scan address range (poll address start/stop).
    /// Equivalent to CWHart's ctrlDtmAddress / ctrlSetDtmAddress.
    /// Available when not connected.
    /// </summary>
    [Guid("B4B6B3E7-639D-460B-B9A0-6C7F7EB20060")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    [ProgId("LasecHartCommDTM.DtmAddressControl")]
    public class DtmAddressControl : ActiveXControlBase
    {
        private NumericUpDown _numScanStart;
        private NumericUpDown _numScanStop;
        private Button _btnApply;
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
                Text = "Change DTM Address",
                Font = new Font(Font.FontFamily, 10, FontStyle.Bold)
            };
            Controls.Add(title);
            y += 30;

            // Description
            var desc = new Label
            {
                Left = lblX, Top = y, Width = 400, Height = 40,
                Text = "Set the HART polling address range used by this DTM\nfor device scanning and communication."
            };
            Controls.Add(desc);
            y += 50;

            // Scan Start
            Controls.Add(new Label { Left = lblX, Top = y + 3, Width = lblW, Text = "Scan Start Address:" });
            _numScanStart = new NumericUpDown
            {
                Left = ctrlX, Top = y, Width = 80,
                Minimum = 0, Maximum = 63, Value = 0
            };
            Controls.Add(_numScanStart);
            y += 35;

            // Scan Stop
            Controls.Add(new Label { Left = lblX, Top = y + 3, Width = lblW, Text = "Scan Stop Address:" });
            _numScanStop = new NumericUpDown
            {
                Left = ctrlX, Top = y, Width = 80,
                Minimum = 0, Maximum = 63, Value = 0
            };
            Controls.Add(_numScanStop);
            y += 35;

            // Apply button
            _btnApply = new Button
            {
                Left = ctrlX, Top = y, Width = 100, Height = 28,
                Text = "Apply"
            };
            _btnApply.Click += BtnApply_Click;
            Controls.Add(_btnApply);
            y += 40;

            // Status label
            _lblStatus = new Label
            {
                Left = lblX, Top = y, Width = 400, Height = 20,
                Text = "", ForeColor = Color.DarkBlue
            };
            Controls.Add(_lblStatus);

            ResumeLayout(false);

            // Load current values
            var dtm = CommDtm.Current;
            if (dtm != null)
            {
                _numScanStart.Value = Math.Max(0, Math.Min(63, dtm._scanStart));
                _numScanStop.Value = Math.Max(0, Math.Min(63, dtm._scanStop));
            }
        }

        private void BtnApply_Click(object sender, EventArgs e)
        {
            var dtm = CommDtm.Current;
            if (dtm == null)
            {
                _lblStatus.Text = "Error: DTM not available.";
                _lblStatus.ForeColor = Color.Red;
                return;
            }

            int start = (int)_numScanStart.Value;
            int stop = (int)_numScanStop.Value;

            if (stop < start)
            {
                _lblStatus.Text = "Error: Stop address must be >= Start address.";
                _lblStatus.ForeColor = Color.Red;
                return;
            }

            dtm._scanStart = start;
            dtm._scanStop = stop;

            _lblStatus.Text = "DTM address range set to " + start + " - " + stop + ".";
            _lblStatus.ForeColor = Color.DarkGreen;
            CommDtm.Log("DtmAddressControl: scan range set to " + start + "-" + stop);
        }

        [ComRegisterFunction]
        public static void Register(Type t) { RegisterActiveXControl(t); }

        [ComUnregisterFunction]
        public static void Unregister(Type t) { UnregisterActiveXControl(t); }
    }
}
