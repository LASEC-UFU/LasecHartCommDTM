using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LasecHartCommDTM
{
    /// <summary>
    /// ActiveX control for About (functionId=100).
    /// Displays DTM information: name, version, vendor, copyright.
    /// Equivalent to CWHart's ctrlAbout.
    /// </summary>
    [Guid("B4B6B3E7-639D-460B-B9A0-6C7F7EB20070")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    [ProgId("LasecHartCommDTM.AboutControl")]
    public class AboutControl : ActiveXControlBase
    {
        protected override void CreateUI()
        {
            SuspendLayout();
            Dock = DockStyle.Fill;
            BackColor = SystemColors.Control;

            int y = 20, x = 20;

            // Title
            var title = new Label
            {
                Left = x, Top = y, Width = 400, Height = 28,
                Text = "Lasec HART Communication DTM",
                Font = new Font(Font.FontFamily, 14, FontStyle.Bold),
                ForeColor = Color.DarkBlue
            };
            Controls.Add(title);
            y += 40;

            // Version info
            var lines = new[]
            {
                "Version: 1.0.0",
                "FDT Version: 1.2",
                "",
                "Vendor: JosueLab / LASEC-UFU",
                "",
                "HART Communication DTM for PACTware 5.0",
                "Supports HART-over-IP (UDP/TCP)",
                "",
                "Compatible with FDT 1.2 specification.",
                "Based on CodeWrights CWHart reference architecture.",
                "",
                "Bus Category: HART",
                "Protocol: HART-IP (UDP port 5094 / TCP)",
                "",
                "Supported HART Commands:",
                "  Command 0 - Read Unique Identifier",
                "  Command 1 - Read Primary Variable",
                "  Command 2 - Read Loop Current",
                "  Command 3 - Read Dynamic Variables",
                "  Command 6 - Write Polling Address",
                "  All pass-through commands via TransactionRequest",
            };

            foreach (var line in lines)
            {
                var lbl = new Label
                {
                    Left = x, Top = y, Width = 450, Height = 18,
                    Text = line,
                    Font = string.IsNullOrEmpty(line) ? Font : new Font("Segoe UI", 9)
                };
                Controls.Add(lbl);
                y += string.IsNullOrEmpty(line) ? 8 : 18;
            }

            ResumeLayout(false);
            CommDtm.Log("AboutControl.CreateUI() OK");
        }

        [ComRegisterFunction]
        public static void Register(Type t) { RegisterActiveXControl(t); }

        [ComUnregisterFunction]
        public static void Unregister(Type t) { UnregisterActiveXControl(t); }
    }
}
