using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace LasecHartCommDTM
{
    /// <summary>
    /// Base class for all ActiveX controls that PACTware embeds in its MDI child windows.
    /// Provides:
    /// - ICustomQueryInterface with detailed OLE QI logging
    /// - Deferred UI creation (child controls created in OnHandleCreated, not constructor)
    /// - Common COM registration helpers for ActiveX control registry entries
    ///
    /// Each derived class must:
    /// - Have [Guid("...")] with a unique GUID
    /// - Have [ClassInterface(ClassInterfaceType.None)] to avoid masking OLE interfaces
    /// - Have [ComVisible(true)] and [ProgId("...")]
    /// - Override CreateUI() to build its user interface
    /// - Call RegisterActiveXControl/UnregisterActiveXControl in [ComRegisterFunction]
    /// </summary>
    [ComVisible(true)]
    public abstract class ActiveXControlBase : UserControl, ICustomQueryInterface
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

        private bool _uiCreated;

        // ----------------------------------------------------------------
        // ICustomQueryInterface — log all QI from PACTware
        // Returns NotHandled so the CCW's normal QI handles everything
        // (OLE interfaces from base Control are exposed automatically).
        // ----------------------------------------------------------------
        public CustomQueryInterfaceResult GetInterface(ref Guid iid, out IntPtr ppv)
        {
            ppv = IntPtr.Zero;
            string name = IdentifyInterface(iid);
            CommDtm.Log(GetType().Name + ".QI " + iid.ToString("B") + " (" + name + ")");
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
            return "unknown";
        }

        protected ActiveXControlBase()
        {
            CommDtm.Log(GetType().Name + "() constructor");
            // Do NOT create child controls here — deferred to OnHandleCreated
            // to avoid premature HWND creation before OLE InPlace activation.
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            CommDtm.Log(GetType().Name + ".OnHandleCreated() hwnd=0x" + Handle.ToString("X"));
            if (!_uiCreated)
            {
                _uiCreated = true;
                try
                {
                    CreateUI();
                }
                catch (Exception ex)
                {
                    CommDtm.Log(GetType().Name + ".CreateUI error: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Override to create the control's user interface.
        /// Called once from OnHandleCreated after OLE activation sets up the correct parent window.
        /// </summary>
        protected abstract void CreateUI();

        // ----------------------------------------------------------------
        // COM Registration helpers for ActiveX controls
        // ----------------------------------------------------------------

        /// <summary>
        /// Registers the type as an ActiveX control in the Windows registry.
        /// Creates: Control, Insertable, MiscStatus, TypeLib, VERSION, Implemented Categories.
        /// Call from [ComRegisterFunction].
        /// </summary>
        protected static void RegisterActiveXControl(Type t)
        {
            try
            {
                string keyPath = @"CLSID\" + t.GUID.ToString("B");
                using (var key = Registry.ClassesRoot.OpenSubKey(keyPath, true))
                {
                    if (key == null) return;

                    key.CreateSubKey("Control");
                    key.CreateSubKey("Insertable");

                    // OLEMISC flags: RECOMPOSEONRESIZE | CANTLINKINSIDE | INSIDEOUT |
                    //                ACTIVATEWHENVISIBLE | SETCLIENTSITEFIRST
                    using (var ms = key.CreateSubKey("MiscStatus"))
                        ms?.SetValue("", "0");
                    using (var ms1 = key.CreateSubKey(@"MiscStatus\1"))
                        ms1?.SetValue("", "131473");

                    using (var tlb = key.CreateSubKey("TypeLib"))
                        tlb?.SetValue("", "{40498B38-0A79-3F68-905F-7954AA76AEA6}");

                    using (var ver = key.CreateSubKey("VERSION"))
                        ver?.SetValue("", "1.0");

                    using (var ic = key.CreateSubKey("Implemented Categories"))
                    {
                        ic?.CreateSubKey("{40FC6ED4-2438-11CF-A3DB-080036F12502}"); // Controls
                        ic?.CreateSubKey("{40FC6ED5-2438-11CF-A3DB-080036F12502}"); // Automation Objects
                    }
                }
            }
            catch (Exception ex)
            {
                CommDtm.Log("RegisterActiveXControl(" + t.Name + ") error: " + ex.Message);
            }
        }

        /// <summary>
        /// Unregisters the ActiveX control entries. Call from [ComUnregisterFunction].
        /// </summary>
        protected static void UnregisterActiveXControl(Type t)
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
