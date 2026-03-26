using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LasecHartCommDTM
{
    /// <summary>
    /// ActiveX control for Communication Log (functionId=10).
    /// Displays HART communication frames in real time.
    /// Equivalent to CWHart's ctrlProtocol / Log function.
    /// </summary>
    [Guid("B4B6B3E7-639D-460B-B9A0-6C7F7EB20040")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    [ProgId("LasecHartCommDTM.LogControl")]
    public class LogControl : ActiveXControlBase
    {
        private TextBox _txtLog;
        private Button _btnClear;
        private Button _btnRefresh;
        private Timer _refreshTimer;

        protected override void CreateUI()
        {
            SuspendLayout();
            Dock = DockStyle.Fill;
            BackColor = SystemColors.Control;

            // Title
            var lbl = new Label
            {
                Left = 10, Top = 10, Width = 380, Height = 20,
                Text = "HART Communication Log",
                Font = new Font(Font.FontFamily, 9, FontStyle.Bold)
            };
            Controls.Add(lbl);

            // Log text box
            _txtLog = new TextBox
            {
                Left = 10, Top = 35, Width = 560, Height = 320,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 8.5f),
                WordWrap = false,
                BackColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            Controls.Add(_txtLog);

            // Buttons
            _btnRefresh = new Button { Left = 10, Top = 362, Width = 80, Height = 26, Text = "Refresh", Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
            _btnRefresh.Click += (s, e) => LoadLog();
            Controls.Add(_btnRefresh);

            _btnClear = new Button { Left = 100, Top = 362, Width = 80, Height = 26, Text = "Clear", Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
            _btnClear.Click += BtnClear_Click;
            Controls.Add(_btnClear);

            ResumeLayout(false);

            // Load initial log content
            LoadLog();

            // Auto-refresh every 2 seconds
            _refreshTimer = new Timer { Interval = 2000 };
            _refreshTimer.Tick += (s, e) => LoadLog();
            _refreshTimer.Start();
        }

        private void LoadLog()
        {
            try
            {
                var path = System.IO.Path.Combine(
                    @"C:\ProgramData\PACTware Consortium e.V\PACTware 5.0",
                    "LasecHartDTM.log");

                if (System.IO.File.Exists(path))
                {
                    string content = System.IO.File.ReadAllText(path);
                    if (content != _txtLog.Text)
                    {
                        _txtLog.Text = content;
                        _txtLog.SelectionStart = _txtLog.TextLength;
                        _txtLog.ScrollToCaret();
                    }
                }
                else
                {
                    _txtLog.Text = "(No log file found)";
                }
            }
            catch (Exception ex)
            {
                CommDtm.Log("LogControl.LoadLog error: " + ex.Message);
            }
        }

        private void BtnClear_Click(object sender, EventArgs e)
        {
            try
            {
                var path = System.IO.Path.Combine(
                    @"C:\ProgramData\PACTware Consortium e.V\PACTware 5.0",
                    "LasecHartDTM.log");
                System.IO.File.WriteAllText(path, "");
                _txtLog.Text = "";
                CommDtm.Log("LogControl: log cleared");
            }
            catch (Exception ex)
            {
                CommDtm.Log("LogControl.Clear error: " + ex.Message);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _refreshTimer?.Stop();
                _refreshTimer?.Dispose();
            }
            base.Dispose(disposing);
        }

        [ComRegisterFunction]
        public static void Register(Type t) { RegisterActiveXControl(t); }

        [ComUnregisterFunction]
        public static void Unregister(Type t) { UnregisterActiveXControl(t); }
    }
}
