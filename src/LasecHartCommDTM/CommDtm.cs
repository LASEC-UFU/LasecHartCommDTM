using System;
using System.Runtime.InteropServices;
using System.Xml;
using Microsoft.Win32;
using Jigfdt.Fdt100;
using HartEngine;

namespace LasecHartCommDTM
{
    // IPersistStreamInit — required by PACTware for DTM state persistence
    // Vtable: IUnknown(3) + IPersist::GetClassID(1) + IsDirty,Load,Save,GetSizeMax,InitNew(5)
    [Guid("7FD52380-4E07-101B-AE2D-08002B2EC713")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComVisible(true)]
    public interface IPersistStreamInit
    {
        void GetClassID(out Guid pClassID);
        [PreserveSig] int IsDirty();
        void Load(IntPtr pStm);
        void Save(IntPtr pStm, [MarshalAs(UnmanagedType.Bool)] bool fClearDirty);
        void GetSizeMax(out long pcbSize);
        void InitNew();
    }

    // IPersistPropertyBag — alternative persistence requested by PACTware
    // Vtable: IUnknown(3) + IPersist::GetClassID(1) + InitNew,Load,Save(3)
    [Guid("37D84F60-42CB-11CE-8135-00AA004BB851")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComVisible(true)]
    public interface IPersistPropertyBag
    {
        void GetClassID(out Guid pClassID);
        void InitNew();
        void Load(IntPtr pPropBag, IntPtr pErrorLog);
        void Save(IntPtr pPropBag, [MarshalAs(UnmanagedType.Bool)] bool fClearDirty, [MarshalAs(UnmanagedType.Bool)] bool fSaveAllProperties);
    }

    // IDtmActiveXInformation — PACTware queries CLSID/ProgId of ActiveX controls
    // to embed (in-place) in its MDI child window.
    [Guid("036D1480-387B-11D4-86E1-00E0987270B9")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    [ComVisible(true)]
    public interface IDtmActiveXInformation
    {
        [return: MarshalAs(UnmanagedType.BStr)]
        string QueryActiveXGuid(
            [MarshalAs(UnmanagedType.BStr)] string functionCall);

        [return: MarshalAs(UnmanagedType.BStr)]
        string QueryActiveXProgId(
            [MarshalAs(UnmanagedType.BStr)] string functionCall);
    }

    // IDtmActiveXControl — PACTware calls Init() on the embedded ActiveX control
    // to pass the DTM reference and functionCall. Without this, PACTware considers
    // the OLE embedding incomplete and destroys the control.
    // This is the CRITICAL missing interface that caused the "double creation" pattern.
    [Guid("036D1486-387B-11D4-86E1-00E0987270B9")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    [ComVisible(true)]
    public interface IDtmActiveXControl
    {
        [return: MarshalAs(UnmanagedType.Bool)]
        bool Init(
            [MarshalAs(UnmanagedType.IDispatch)] object pDtm,
            [MarshalAs(UnmanagedType.BStr)] string functionCall);

        [return: MarshalAs(UnmanagedType.Bool)]
        bool PrepareToRelease();
    }

    [Guid("B4B6B3E7-639D-460B-B9A0-6C7F7EB20010")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    public class CommDtm : IDtmInformation, IDtm, IFdtCommunication, IDtmParameter, IDtmActiveXInformation, IPersistStreamInit, IPersistPropertyBag, ICustomQueryInterface
    {
        private static CommDtm _current;
        internal static CommDtm Current => _current;

        private ChannelManager _manager;
        private IFdtCommunicationEvents _callback;
        private bool _connected;
        private int _commRef;

        // ---- Parâmetros de comunicação (equivalente ao DTMPARAMETER.XML do CWHart) ----
        internal string _ipAddress    = "127.0.0.1";
        internal int    _ipPort       = 5094;
        internal string _protocol     = "udp";       // "udp" ou "tcp"
        internal bool   _primaryMaster = true;        // PrimaryMaster (0/1)
        internal int    _preambleCount = 5;           // 5-20
        internal int    _retryCount    = 3;           // 1-10
        internal int    _scanStart     = 0;           // 0-63  (poll address inicio)
        internal int    _scanStop      = 0;           // 0-63  (poll address fim)
        internal bool   _burstMode     = false;       // BurstMode (0/1)
        internal int    _timeout       = 5000;        // timeout em ms

        // Known FDT interface GUIDs (from Jigfdt.fdt100.dll)
        private static readonly Guid IID_IDtmInformation      = new Guid("036D147F-387B-11D4-86E1-00E0987270B9");
        private static readonly Guid IID_IDtm                 = new Guid("036D1481-387B-11D4-86E1-00E0987270B9");
        private static readonly Guid IID_IFdtCommunication    = new Guid("039ECFC4-9CA8-44E6-944D-B37F288A34D8");
        private static readonly Guid IID_IDtmParameter       = new Guid("036D147D-387B-11D4-86E1-00E0987270B9");
        private static readonly Guid IID_IPersistStreamInit   = new Guid("7FD52380-4E07-101B-AE2D-08002B2EC713");
        private static readonly Guid IID_IPersistPropertyBag  = new Guid("37D84F60-42CB-11CE-8135-00AA004BB851");
        private static readonly Guid IID_IDtmActiveXInfo      = new Guid("036D1480-387B-11D4-86E1-00E0987270B9");
        private static readonly Guid IID_IDispatch            = new Guid("00020400-0000-0000-C000-000000000046");
        private static readonly Guid IID_IUnknown             = new Guid("00000000-0000-0000-C000-000000000046");

        // Guard against recursion in ICustomQueryInterface
        [ThreadStatic]
        private static bool _inGetInterface;

        static CommDtm()
        {
            Log("CommDtm STATIC constructor — class loaded into AppDomain: " + AppDomain.CurrentDomain.FriendlyName);
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                Log("UNHANDLED EXCEPTION: " + e.ExceptionObject);
            };
        }

        // ICustomQueryInterface — manually provide interface pointers for FDT interfaces
        // This ensures PACTware (native COM caller) gets valid interface pointers
        // even if CLR's standard type equivalence lookup fails with embedded interop types.
        public CustomQueryInterfaceResult GetInterface(ref Guid iid, out IntPtr ppv)
        {
            ppv = IntPtr.Zero;

            if (_inGetInterface)
                return CustomQueryInterfaceResult.NotHandled;

            string name = "unknown";
            if (iid == IID_IDtmInformation) name = "IDtmInformation";
            else if (iid == IID_IDtm) name = "IDtm";
            else if (iid == IID_IFdtCommunication) name = "IFdtCommunication";
            else if (iid == IID_IDtmParameter) name = "IDtmParameter";
            else if (iid == IID_IPersistStreamInit) name = "IPersistStreamInit";
            else if (iid == IID_IPersistPropertyBag) name = "IPersistPropertyBag";
            else if (iid == IID_IDtmActiveXInfo) name = "IDtmActiveXInformation";
            else if (iid == IID_IDispatch) name = "IDispatch";
            else if (iid == IID_IUnknown) name = "IUnknown";

            Log("QI for " + iid.ToString("B") + " (" + name + ")");

            // Manually provide interface pointers for FDT + ActiveX persistence interfaces
            _inGetInterface = true;
            try
            {
                if (iid == IID_IDtmInformation)
                {
                    ppv = Marshal.GetComInterfaceForObject(this, typeof(IDtmInformation));
                    Log("QI -> HANDLED IDtmInformation");
                    return CustomQueryInterfaceResult.Handled;
                }
                if (iid == IID_IDtm)
                {
                    ppv = Marshal.GetComInterfaceForObject(this, typeof(IDtm));
                    Log("QI -> HANDLED IDtm");
                    return CustomQueryInterfaceResult.Handled;
                }
                if (iid == IID_IFdtCommunication)
                {
                    ppv = Marshal.GetComInterfaceForObject(this, typeof(IFdtCommunication));
                    Log("QI -> HANDLED IFdtCommunication");
                    return CustomQueryInterfaceResult.Handled;
                }
                if (iid == IID_IDtmParameter)
                {
                    ppv = Marshal.GetComInterfaceForObject(this, typeof(IDtmParameter));
                    Log("QI -> HANDLED IDtmParameter");
                    return CustomQueryInterfaceResult.Handled;
                }
                if (iid == IID_IPersistStreamInit)
                {
                    ppv = Marshal.GetComInterfaceForObject(this, typeof(IPersistStreamInit));
                    Log("QI -> HANDLED IPersistStreamInit");
                    return CustomQueryInterfaceResult.Handled;
                }
                if (iid == IID_IPersistPropertyBag)
                {
                    ppv = Marshal.GetComInterfaceForObject(this, typeof(IPersistPropertyBag));
                    Log("QI -> HANDLED IPersistPropertyBag");
                    return CustomQueryInterfaceResult.Handled;
                }
                if (iid == IID_IDtmActiveXInfo)
                {
                    ppv = Marshal.GetComInterfaceForObject(this, typeof(IDtmActiveXInformation));
                    Log("QI -> HANDLED IDtmActiveXInformation");
                    return CustomQueryInterfaceResult.Handled;
                }
            }
            catch (Exception ex)
            {
                Log("QI HANDLE ERROR: " + ex.Message);
            }
            finally
            {
                _inGetInterface = false;
            }

            return CustomQueryInterfaceResult.NotHandled;
        }

        public CommDtm()
        {
            Log("CommDtm() constructor called");
            _manager = new ChannelManager();
            _current = this;

            // Diagnóstico: verificar GUIDs reais das interfaces embedded
            try
            {
                Log("typeof(IDtmInformation).GUID = " + typeof(IDtmInformation).GUID.ToString("B"));
                Log("typeof(IDtm).GUID = " + typeof(IDtm).GUID.ToString("B"));
                Log("typeof(IFdtCommunication).GUID = " + typeof(IFdtCommunication).GUID.ToString("B"));

                // Verificar todas as interfaces implementadas
                var ifaces = GetType().GetInterfaces();
                foreach (var iface in ifaces)
                {
                    Log("  implements: " + iface.FullName + " GUID=" + iface.GUID.ToString("B"));
                }

                // Testar se Marshal.QueryInterface funciona para IDtm
                IntPtr pUnk = Marshal.GetIUnknownForObject(this);
                try
                {
                    Guid iidDtm = typeof(IDtm).GUID;
                    IntPtr pDtm;
                    int hr = Marshal.QueryInterface(pUnk, ref iidDtm, out pDtm);
                    Log("Marshal.QI(IDtm " + iidDtm.ToString("B") + ") hr=0x" + hr.ToString("X8"));
                    if (hr == 0) Marshal.Release(pDtm);

                    Guid iidInfo = typeof(IDtmInformation).GUID;
                    IntPtr pInfo;
                    hr = Marshal.QueryInterface(pUnk, ref iidInfo, out pInfo);
                    Log("Marshal.QI(IDtmInformation " + iidInfo.ToString("B") + ") hr=0x" + hr.ToString("X8"));
                    if (hr == 0) Marshal.Release(pInfo);

                    Guid iidComm = typeof(IFdtCommunication).GUID;
                    IntPtr pComm;
                    hr = Marshal.QueryInterface(pUnk, ref iidComm, out pComm);
                    Log("Marshal.QI(IFdtCommunication " + iidComm.ToString("B") + ") hr=0x" + hr.ToString("X8"));
                    if (hr == 0) Marshal.Release(pComm);
                }
                finally
                {
                    Marshal.Release(pUnk);
                }
            }
            catch (Exception ex)
            {
                Log("Diagnostic error: " + ex.ToString());
            }

            // Autodiagnóstico: confirmar que GetInformation funciona
            try
            {
                string xml = GetInformation();
                Log("GetInformation() self-test OK, " + xml.Length + " chars");
            }
            catch (Exception ex)
            {
                Log("GetInformation() self-test FAILED: " + ex.Message);
            }
            Log("CommDtm() constructor returning normally");
        }

        internal static void Log(string msg)
        {
            try
            {
                var dir = @"C:\ProgramData\PACTware Consortium e.V\PACTware 5.0";
                if (!System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);
                var path = System.IO.Path.Combine(dir, "LasecHartDTM.log");
                var line = DateTime.Now.ToString("HH:mm:ss.fff") + " [" + System.Diagnostics.Process.GetCurrentProcess().ProcessName + "] " + msg;
                System.IO.File.AppendAllText(path, line + System.Environment.NewLine);
            }
            catch { }
        }

        // ----------------------------------------------------------------
        // IDtmInformation — chamado pelo PACTware durante o scan do catálogo
        // Formato compatível com CWCommDTMHART DTMINFORMATION.XML
        // ----------------------------------------------------------------
        public string GetInformation()
        {
            Log("GetInformation() called");
            // busCategory UUID uppercase sem braces conforme CWHart
            const string hartBusUuid = "036D1498-387B-11D4-86E1-00E0987270B9";

            // XML conforme DTMInformationSchema.xml + FDTDataTypesSchema.xml
            // Schema root: <FDT><DtmInfo><FDTVersion/><VersionInformation/><DtmDeviceTypes>...</DtmDeviceTypes></DtmInfo></FDT>
            return
                "<FDT xmlns=\"x-schema:DTMInformationSchema.xml\" xmlns:fdt=\"x-schema:FDTDataTypesSchema.xml\">" +
                  "<DtmInfo>" +
                    "<FDTVersion major=\"1\" minor=\"2\" />" +
                    "<fdt:VersionInformation" +
                    " name=\"Lasec HART Communication DTM\"" +
                    " readAccess=\"1\"" +
                    " writeAccess=\"0\"" +
                    " vendor=\"JosueLab\"" +
                    " version=\"1.0.0\"" +
                    " date=\"2024-01-01\" />" +
                    "<DtmDeviceTypes>" +
                      "<fdt:DtmDeviceType" +
                      " readAccess=\"1\"" +
                      " writeAccess=\"0\"" +
                      " deviceTypeInformation=\"Lasec HART Communication FDT 1.2 DTM\">" +
                        "<fdt:VersionInformation" +
                        " name=\"Lasec HART Communication DTM\"" +
                        " vendor=\"JosueLab\"" +
                        " version=\"1.0.0\"" +
                        " date=\"2024-01-01\" />" +
                        "<fdt:SupportedLanguages>" +
                          "<fdt:LanguageId languageId=\"1033\" />" +
                        "</fdt:SupportedLanguages>" +
                        "<fdt:BusCategories>" +
                          "<fdt:BusCategory" +
                          " busCategory=\"" + hartBusUuid + "\"" +
                          " busCategoryName=\"HART\">" +
                            "<fdt:CommunicationTypeEntry communicationType=\"supported\" />" +
                          "</fdt:BusCategory>" +
                        "</fdt:BusCategories>" +
                      "</fdt:DtmDeviceType>" +
                    "</DtmDeviceTypes>" +
                  "</DtmInfo>" +
                "</FDT>";
        }

        // ----------------------------------------------------------------
        // IDtm — interface base FDT 1.x
        // ----------------------------------------------------------------
        public bool Environment(string systemTag, IFdtContainer container)
        {
            Log("Environment(tag=" + (systemTag ?? "null") + ")");
            return true;
        }

        public bool InitNew(string deviceType)
        {
            Log("InitNew(deviceType=" + (deviceType ?? "null") + ")");
            return true;
        }

        public bool Config(string userInfo)
        {
            Log("Config() called");
            // Restaura parâmetros do XML persistido pelo frame FDT
            if (!string.IsNullOrEmpty(userInfo))
            {
                try
                {
                    var doc = new XmlDocument();
                    doc.LoadXml(userInfo);
                    var ns = new XmlNamespaceManager(doc.NameTable);
                    ns.AddNamespace("fdt", "x-schema:FDTDataTypesSchema.xml");

                    var paramNode = doc.SelectSingleNode("//DtmParameter | //fdt:DtmParameter", ns);
                    if (paramNode != null)
                    {
                        var attr = paramNode.Attributes;
                        if (attr["ipAddress"] != null) _ipAddress = attr["ipAddress"].Value;
                        if (attr["ipPort"] != null) _ipPort = int.Parse(attr["ipPort"].Value);
                        if (attr["protocol"] != null) _protocol = attr["protocol"].Value;
                        if (attr["primaryMaster"] != null) _primaryMaster = attr["primaryMaster"].Value == "1";
                        if (attr["preambleCount"] != null) _preambleCount = int.Parse(attr["preambleCount"].Value);
                        if (attr["retryCount"] != null) _retryCount = int.Parse(attr["retryCount"].Value);
                        if (attr["scanStart"] != null) _scanStart = int.Parse(attr["scanStart"].Value);
                        if (attr["scanStop"] != null) _scanStop = int.Parse(attr["scanStop"].Value);
                        if (attr["burstMode"] != null) _burstMode = attr["burstMode"].Value == "1";
                        if (attr["timeout"] != null) _timeout = int.Parse(attr["timeout"].Value);
                    }
                    Log("Config() restored parameters: ip=" + _ipAddress + ":" + _ipPort + " proto=" + _protocol);
                }
                catch (Exception ex)
                {
                    Log("Config() parse error: " + ex.Message);
                }
            }
            return true;
        }

        public bool SetCommunication(IFdtCommunication communication)
        {
            Log("SetCommunication()");
            return true;
        }

        public bool PrepareToRelease()
        {
            Log("PrepareToRelease()");
            _connected = false;
            return true;
        }

        public bool PrepareToReleaseCommunication()
        {
            Log("PrepareToReleaseCommunication()");
            _connected = false;
            return true;
        }

        public bool ReleaseCommunication()
        {
            Log("ReleaseCommunication()");
            _manager?.Dispose();
            _connected = false;
            return true;
        }

        public bool PrepareToDelete()
        {
            Log("PrepareToDelete()");
            return true;
        }

        public bool SetLanguage(int languageId)
        {
            Log("SetLanguage(" + languageId + ")");
            return true;
        }

        // GetFunctions: retorna funções FDT para CommDTM
        // Formato idêntico ao CWHart DTMFUNCTIONS.XML:
        //   <FDT xmlns="x-schema:DTMFunctionsSchema.xml" ...>
        //     <Functions> com <StandardFunction> + <Function> + <Document>
        // PACTware chama com operationPhase="notSupported" para CommDTMs
        public string GetFunctions(string operationState)
        {
            Log("GetFunctions(state=" + (operationState ?? "null") + ")");

            string xml =
                "<?xml version=\"1.0\"?>" +
                "<FDT xmlns=\"x-schema:DTMFunctionsSchema.xml\"" +
                " xmlns:fdt=\"x-schema:FDTDataTypesSchema.xml\"" +
                " xmlns:appId=\"x-schema:FDTApplicationIdSchema.xml\">" +
                  "<Functions label=\"Functions\" fdt:name=\"\" help=\"\">" +

                    // StandardFunction fdtConfiguration → "parâmetro" no PACTware
                    "<StandardFunction fdt:name=\"Configuration\" help=\"\" functionId=\"1\"" +
                    " resizableStandardFunction=\"1\" printableStandardFunction=\"1\">" +
                      "<Status toggle=\"0\" checked=\"0\" enabled=\"1\" hidden=\"0\" separator=\"0\"/>" +
                      "<appId:ApplicationId applicationId=\"fdtConfiguration\"/>" +
                    "</StandardFunction>" +

                    // Custom: Change device address
                    "<Function label=\"Change device address\" fdt:name=\"mnChangeDeviceAddress\"" +
                    " help=\"Change device address\" functionId=\"20\" hasGUI=\"1\" resizable=\"1\">" +
                      "<Status toggle=\"1\" checked=\"0\" enabled=\"1\" hidden=\"0\" separator=\"0\"/>" +
                    "</Function>" +

                    // Custom: Change DTM address
                    "<Function label=\"Change DTM address\" fdt:name=\"mnChangeDtmAddress\"" +
                    " help=\"Change DTM address\" functionId=\"30\" hasGUI=\"1\" resizable=\"1\">" +
                      "<Status toggle=\"1\" checked=\"0\" enabled=\"1\" hidden=\"0\" separator=\"0\"/>" +
                    "</Function>" +

                    // Custom: Communication log
                    "<Function label=\"Communication log\" fdt:name=\"mnLog\"" +
                    " help=\"Communication log\" functionId=\"10\" hasGUI=\"1\" resizable=\"1\">" +
                      "<Status toggle=\"1\" checked=\"0\" enabled=\"1\" hidden=\"0\" separator=\"0\"/>" +
                    "</Function>" +

                    // Custom: About
                    "<Function label=\"About\" fdt:name=\"mnAbout\"" +
                    " help=\"About this DTM\" functionId=\"100\" hasGUI=\"1\" resizable=\"0\">" +
                      "<Status toggle=\"1\" checked=\"0\" enabled=\"1\" hidden=\"0\" separator=\"0\"/>" +
                    "</Function>" +

                  "</Functions>" +
                "</FDT>";

            Log("GetFunctions() returning " + xml.Length + " chars");
            return xml;
        }

        public bool InvokeFunctionRequest(string invokeId, string functionCall)
        {
            Log("InvokeFunctionRequest(id=" + invokeId + ", func=" + (functionCall ?? "null") + ")");
            try
            {
                // PACTware sends: <FDT xmlns="x-schema:DTMFunctionCallSchema.xml"
                //   xmlns:func="x-schema:DTMFunctionsSchema.xml">
                //   <FDTFunctionCall func:functionId="1"/>
                // </FDT>
                int funcId = 0;
                if (functionCall != null)
                {
                    var doc = new XmlDocument();
                    doc.LoadXml(functionCall);
                    var nsMgr = new XmlNamespaceManager(doc.NameTable);
                    nsMgr.AddNamespace("func", "x-schema:DTMFunctionsSchema.xml");
                    var attr = doc.SelectSingleNode("//*/@func:functionId", nsMgr);
                    if (attr != null) int.TryParse(attr.Value, out funcId);
                }

                Log("InvokeFunctionRequest -> functionId=" + funcId);

                switch (funcId)
                {
                    case 1:  // fdtConfiguration → "parâmetro"
                        OpenConfigDialog();
                        break;
                    case 10: // Communication log
                        Log("InvokeFunctionRequest -> Communication log (not yet implemented)");
                        break;
                    case 20: // Change device address
                        Log("InvokeFunctionRequest -> Change device address (not yet implemented)");
                        break;
                    case 30: // Change DTM address
                        Log("InvokeFunctionRequest -> Change DTM address (not yet implemented)");
                        break;
                    case 100: // About
                        OpenAboutDialog();
                        break;
                    default:
                        Log("InvokeFunctionRequest -> unknown functionId " + funcId);
                        break;
                }
            }
            catch (Exception ex)
            {
                Log("InvokeFunctionRequest error: " + ex.Message);
            }
            return true;
        }

        private void OpenConfigDialog()
        {
            Log("Opening configuration dialog...");
            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    var dlg = new DtmView();
                    dlg.ShowDialog();
                    dlg.Dispose();
                }
                catch (Exception ex)
                {
                    Log("Config dialog error: " + ex.Message);
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
        }

        private void OpenAboutDialog()
        {
            Log("Opening About dialog...");
            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    System.Windows.Forms.MessageBox.Show(
                        "Lasec HART Communication DTM\nVersion 1.0.0\n\nJosueLab",
                        "About",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    Log("About dialog error: " + ex.Message);
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
        }

        public bool PrivateDialogEnabled(bool enabled)
        {
            Log("PrivateDialogEnabled(" + enabled + ")");
            return true;
        }

        // ----------------------------------------------------------------
        // IFdtCommunication — canal de comunicação HART sobre IP
        // Formato XML compatível com CWCommDTMHART
        // ----------------------------------------------------------------
        public void Abort(string fieldbusFrame)
        {
            Log("Abort()");
            _manager?.Dispose();
            _connected = false;
        }

        public bool ConnectRequest(IFdtCommunicationEvents callBack, string invokeId, string protocolId, string fieldbusFrame)
        {
            Log("ConnectRequest(invokeId=" + invokeId + ")");
            try
            {
                _callback = callBack;
                _commRef++;

                // Configura o canal IP (TCP ou UDP)
                _manager.Configure(_protocol, _ipAddress, _ipPort, _timeout);
                _connected = true;

                // Resposta no formato CWHart: HartConnectResp.xml
                string tag = "HART";
                try
                {
                    var doc = new XmlDocument();
                    doc.LoadXml(fieldbusFrame);
                    var tagNode = doc.SelectSingleNode("//*[@tag]");
                    if (tagNode != null) tag = tagNode.Attributes["tag"].Value;
                }
                catch { }

                string connectResponse =
                    "<ConnectResponse" +
                    " xmlns=\"x-schema:HartCommunicationSchema.xml\"" +
                    " xmlns:fdt=\"x-schema:FDTDataTypesSchema.xml\"" +
                    " fdt:tag=\"" + HartXmlHelper.XmlEscape(tag) + "\"" +
                    " preambleCount=\"" + _preambleCount + "\"" +
                    " primaryMaster=\"" + (_primaryMaster ? "1" : "0") + "\"" +
                    " communicationReference=\"" + _commRef + "\">" +
                      "<ShortAddress shortAddress=\"" + _scanStart + "\"/>" +
                    "</ConnectResponse>";

                Log("ConnectRequest -> success, commRef=" + _commRef);
                callBack?.OnConnectResponse(invokeId, connectResponse);
                return true;
            }
            catch (Exception ex)
            {
                Log("ConnectRequest ERROR: " + ex.Message);
                // Resposta de erro no formato CWHart: HartErrorResp.xml
                string errorXml =
                    "<fdt:CommunicationError" +
                    " xmlns:fdt=\"x-schema:FDTDataTypesSchema.xml\"" +
                    " communicationError=\"connectionFailed\"" +
                    " tag=\"HART\"/>";

                callBack?.OnConnectResponse(invokeId, errorXml);
                return false;
            }
        }

        public bool DisconnectRequest(string invokeId, string fieldbusFrame)
        {
            Log("DisconnectRequest(invokeId=" + invokeId + ")");
            try
            {
                _manager?.Dispose();
                _connected = false;

                // Resposta no formato CWHart: HartDisConnectResp.xml
                string disconnectResponse =
                    "<DisconnectResponse" +
                    " xmlns=\"x-schema:HartCommunicationSchema.xml\"" +
                    " communicationReference=\"" + _commRef + "\"/>";

                _callback?.OnDisconnectResponse(invokeId, disconnectResponse);
                return true;
            }
            catch (Exception ex)
            {
                Log("DisconnectRequest ERROR: " + ex.Message);
                return false;
            }
        }

        public bool TransactionRequest(string invokeId, string fieldbusFrame)
        {
            Log("TransactionRequest(invokeId=" + invokeId + ")");
            try
            {
                if (!_connected)
                {
                    Log("TransactionRequest: not connected");
                    return false;
                }

                // Parse request XML no formato CWHart: HartTransactionReq.xml
                int commandNumber = 0;
                string commReference = _commRef.ToString();
                byte[] requestData = new byte[0];

                HartXmlHelper.ParseTransactionRequest(fieldbusFrame,
                    out commandNumber, out commReference, out requestData);

                Log("TransactionRequest: cmd=" + commandNumber + " data=" + HartXmlHelper.BytesToHex(requestData));

                // Envia pelo canal IP com retry
                byte[] responseData = null;
                Exception lastEx = null;

                for (int attempt = 0; attempt < _retryCount; attempt++)
                {
                    try
                    {
                        responseData = _manager.SendAndReceive(requestData, _timeout);
                        if (responseData != null && responseData.Length > 0)
                            break;
                    }
                    catch (Exception ex)
                    {
                        lastEx = ex;
                        Log("TransactionRequest: attempt " + (attempt + 1) + " failed: " + ex.Message);
                    }
                }

                if (responseData == null || responseData.Length == 0)
                {
                    // Resposta de erro (timeout/falha)
                    string errorXml = HartXmlHelper.BuildErrorResponse(
                        "communicationError", "HART");
                    _callback?.OnTransactionResponse(invokeId, errorXml);
                    Log("TransactionRequest: all retries failed");
                    return true;
                }

                // Monta resposta no formato CWHart: HartTransactionResp.xml
                string responseXml = HartXmlHelper.BuildTransactionResponse(
                    commandNumber, commReference, responseData);

                Log("TransactionRequest: response OK, " + responseData.Length + " bytes");
                _callback?.OnTransactionResponse(invokeId, responseXml);
                return true;
            }
            catch (Exception ex)
            {
                Log("TransactionRequest ERROR: " + ex.Message);
                string errorXml = HartXmlHelper.BuildErrorResponse(
                    "communicationError", "HART");
                _callback?.OnTransactionResponse(invokeId, errorXml);
                return false;
            }
        }

        public string GetSupportedProtocols()
        {
            Log("GetSupportedProtocols()");
            // uuid MSXML sem braces, lowercase
            const string hartBusUuid = "036d1498-387b-11d4-86e1-00e0987270b9";
            return
                "<FDTProtocols xmlns=\"x-schema:DTMProtocolsSchema.xml\" xmlns:fdt=\"x-schema:FDTDataTypesSchema.xml\">" +
                  "<fdt:Protocol protocolId=\"" + hartBusUuid + "\"/>" +
                "</FDTProtocols>";
        }

        public bool SequenceBegin(string fieldbusFrame)
        {
            Log("SequenceBegin()");
            return true;
        }

        public bool SequenceStart(string fieldbusFrame)
        {
            Log("SequenceStart()");
            return true;
        }

        public bool SequenceEnd(string fieldbusFrame)
        {
            Log("SequenceEnd()");
            return true;
        }

        // ----------------------------------------------------------------
        // IDtmParameter — habilita menu "parâmetro" no PACTware
        // ----------------------------------------------------------------
        public string GetParameters(string parameterPath)
        {
            Log("IDtmParameter.GetParameters(path=" + (parameterPath ?? "null") + ")");
            // Retorna XML com os parâmetros atuais do CommDTM
            string xml =
                "<?xml version=\"1.0\"?>" +
                "<FDT xmlns=\"x-schema:DTMParameterSchema.xml\" xmlns:fdt=\"x-schema:FDTDataTypesSchema.xml\">" +
                  "<DtmDevice>" +
                    "<fdt:DtmVariables>" +
                      "<fdt:DtmVariable name=\"ipAddress\"><fdt:Value><fdt:Variant><fdt:StringData string=\"" + HartXmlHelper.XmlEscape(_ipAddress) + "\"/></fdt:Variant></fdt:Value></fdt:DtmVariable>" +
                      "<fdt:DtmVariable name=\"ipPort\"><fdt:Value><fdt:Variant><fdt:NumberData number=\"" + _ipPort + "\"/></fdt:Variant></fdt:Value></fdt:DtmVariable>" +
                      "<fdt:DtmVariable name=\"protocol\"><fdt:Value><fdt:Variant><fdt:StringData string=\"" + HartXmlHelper.XmlEscape(_protocol) + "\"/></fdt:Variant></fdt:Value></fdt:DtmVariable>" +
                      "<fdt:DtmVariable name=\"primaryMaster\"><fdt:Value><fdt:Variant><fdt:NumberData number=\"" + (_primaryMaster ? "1" : "0") + "\"/></fdt:Variant></fdt:Value></fdt:DtmVariable>" +
                      "<fdt:DtmVariable name=\"preambleCount\"><fdt:Value><fdt:Variant><fdt:NumberData number=\"" + _preambleCount + "\"/></fdt:Variant></fdt:Value></fdt:DtmVariable>" +
                      "<fdt:DtmVariable name=\"retryCount\"><fdt:Value><fdt:Variant><fdt:NumberData number=\"" + _retryCount + "\"/></fdt:Variant></fdt:Value></fdt:DtmVariable>" +
                      "<fdt:DtmVariable name=\"scanStart\"><fdt:Value><fdt:Variant><fdt:NumberData number=\"" + _scanStart + "\"/></fdt:Variant></fdt:Value></fdt:DtmVariable>" +
                      "<fdt:DtmVariable name=\"scanStop\"><fdt:Value><fdt:Variant><fdt:NumberData number=\"" + _scanStop + "\"/></fdt:Variant></fdt:Value></fdt:DtmVariable>" +
                      "<fdt:DtmVariable name=\"burstMode\"><fdt:Value><fdt:Variant><fdt:NumberData number=\"" + (_burstMode ? "1" : "0") + "\"/></fdt:Variant></fdt:Value></fdt:DtmVariable>" +
                      "<fdt:DtmVariable name=\"timeout\"><fdt:Value><fdt:Variant><fdt:NumberData number=\"" + _timeout + "\"/></fdt:Variant></fdt:Value></fdt:DtmVariable>" +
                    "</fdt:DtmVariables>" +
                  "</DtmDevice>" +
                "</FDT>";
            Log("IDtmParameter.GetParameters() returning " + xml.Length + " chars");
            return xml;
        }

        public bool SetParameters(string parameterPath, string fdtXmlDocument)
        {
            Log("IDtmParameter.SetParameters(path=" + (parameterPath ?? "null") + ", xml=" + (fdtXmlDocument ?? "null") + ")");
            try
            {
                if (string.IsNullOrEmpty(fdtXmlDocument)) return true;
                var doc = new XmlDocument();
                doc.LoadXml(fdtXmlDocument);
                var nsMgr = new XmlNamespaceManager(doc.NameTable);
                nsMgr.AddNamespace("fdt", "x-schema:FDTDataTypesSchema.xml");

                var vars = doc.SelectNodes("//fdt:DtmVariable", nsMgr);
                if (vars != null)
                {
                    foreach (XmlNode v in vars)
                    {
                        string name = v.Attributes?["name"]?.Value;
                        var strNode = v.SelectSingleNode(".//fdt:StringData/@string", nsMgr);
                        var numNode = v.SelectSingleNode(".//fdt:NumberData/@number", nsMgr);
                        string strVal = strNode?.Value;
                        string numVal = numNode?.Value;

                        if (name == "ipAddress" && strVal != null) _ipAddress = strVal;
                        else if (name == "ipPort" && numVal != null) int.TryParse(numVal, out _ipPort);
                        else if (name == "protocol" && strVal != null) _protocol = strVal;
                        else if (name == "primaryMaster" && numVal != null) _primaryMaster = numVal != "0";
                        else if (name == "preambleCount" && numVal != null) int.TryParse(numVal, out _preambleCount);
                        else if (name == "retryCount" && numVal != null) int.TryParse(numVal, out _retryCount);
                        else if (name == "scanStart" && numVal != null) int.TryParse(numVal, out _scanStart);
                        else if (name == "scanStop" && numVal != null) int.TryParse(numVal, out _scanStop);
                        else if (name == "burstMode" && numVal != null) _burstMode = numVal != "0";
                        else if (name == "timeout" && numVal != null) int.TryParse(numVal, out _timeout);
                    }
                }
                Log("IDtmParameter.SetParameters() applied: " + _protocol + "://" + _ipAddress + ":" + _ipPort);
            }
            catch (Exception ex)
            {
                Log("IDtmParameter.SetParameters() ERROR: " + ex.Message);
            }
            return true;
        }

        // Persistência de parâmetros para XML (chamado pelo frame FDT via Config/SaveRequest)
        internal string GetParameterXml()
        {
            return
                "<DtmParameter" +
                " ipAddress=\"" + HartXmlHelper.XmlEscape(_ipAddress) + "\"" +
                " ipPort=\"" + _ipPort + "\"" +
                " protocol=\"" + _protocol + "\"" +
                " primaryMaster=\"" + (_primaryMaster ? "1" : "0") + "\"" +
                " preambleCount=\"" + _preambleCount + "\"" +
                " retryCount=\"" + _retryCount + "\"" +
                " scanStart=\"" + _scanStart + "\"" +
                " scanStop=\"" + _scanStop + "\"" +
                " burstMode=\"" + (_burstMode ? "1" : "0") + "\"" +
                " timeout=\"" + _timeout + "\"/>";
        }

        // Chamado pela UI (DtmView) para aplicar configuração
        internal void ApplyConfiguration(string ipAddress, int ipPort, string protocol,
            bool primaryMaster, int preambleCount, int retryCount,
            int scanStart, int scanStop, bool burstMode, int timeout)
        {
            _ipAddress     = string.IsNullOrEmpty(ipAddress) ? "127.0.0.1" : ipAddress.Trim();
            _ipPort        = ipPort <= 0 ? 5094 : ipPort;
            _protocol      = (protocol ?? "udp").ToLowerInvariant();
            _primaryMaster = primaryMaster;
            _preambleCount = Math.Max(5, Math.Min(20, preambleCount));
            _retryCount    = Math.Max(1, Math.Min(10, retryCount));
            _scanStart     = Math.Max(0, Math.Min(63, scanStart));
            _scanStop      = Math.Max(0, Math.Min(63, scanStop));
            _burstMode     = burstMode;
            _timeout       = timeout <= 0 ? 5000 : timeout;

            Log("ApplyConfiguration: " + _protocol + "://" + _ipAddress + ":" + _ipPort +
                " master=" + _primaryMaster + " preamble=" + _preambleCount +
                " retry=" + _retryCount + " scan=" + _scanStart + "-" + _scanStop +
                " burst=" + _burstMode + " timeout=" + _timeout);
        }

        // ----------------------------------------------------------------
        // IPersistStreamInit — PACTware requires this for DTM state persistence
        // ----------------------------------------------------------------
        void IPersistStreamInit.GetClassID(out Guid pClassID)
        {
            pClassID = typeof(CommDtm).GUID;
            Log("IPersistStreamInit.GetClassID()");
        }

        int IPersistStreamInit.IsDirty()
        {
            Log("IPersistStreamInit.IsDirty() -> S_FALSE");
            return 1; // S_FALSE = not dirty
        }

        void IPersistStreamInit.Load(IntPtr pStm)
        {
            Log("IPersistStreamInit.Load()");
        }

        void IPersistStreamInit.Save(IntPtr pStm, bool fClearDirty)
        {
            Log("IPersistStreamInit.Save(clearDirty=" + fClearDirty + ")");
        }

        void IPersistStreamInit.GetSizeMax(out long pcbSize)
        {
            pcbSize = 0;
            Log("IPersistStreamInit.GetSizeMax()");
        }

        void IPersistStreamInit.InitNew()
        {
            Log("IPersistStreamInit.InitNew()");
        }

        // ----------------------------------------------------------------
        // IPersistPropertyBag — alternative persistence for PACTware
        // ----------------------------------------------------------------
        void IPersistPropertyBag.GetClassID(out Guid pClassID)
        {
            pClassID = typeof(CommDtm).GUID;
            Log("IPersistPropertyBag.GetClassID()");
        }

        void IPersistPropertyBag.InitNew()
        {
            Log("IPersistPropertyBag.InitNew()");
        }

        void IPersistPropertyBag.Load(IntPtr pPropBag, IntPtr pErrorLog)
        {
            Log("IPersistPropertyBag.Load()");
        }

        void IPersistPropertyBag.Save(IntPtr pPropBag, bool fClearDirty, bool fSaveAllProperties)
        {
            Log("IPersistPropertyBag.Save()");
        }

        // ----------------------------------------------------------------
        // IDtmActiveXInformation — returns CLSID/ProgId of the ActiveX
        // control that PACTware embeds in its MDI child window.
        // PACTware sends functionCall XML with functionId to identify
        // which function the user wants to open. We return different
        // ActiveX controls per functionId (like CWHart does).
        // ----------------------------------------------------------------

        public string QueryActiveXGuid(string functionCall)
        {
            int funcId = ParseFunctionId(functionCall);
            string guid;
            switch (funcId)
            {
                case 1:   guid = typeof(ConfigControl).GUID.ToString("B"); break;
                case 10:  guid = typeof(LogControl).GUID.ToString("B"); break;
                case 20:  guid = typeof(DeviceAddressControl).GUID.ToString("B"); break;
                case 30:  guid = typeof(DtmAddressControl).GUID.ToString("B"); break;
                case 100: guid = typeof(AboutControl).GUID.ToString("B"); break;
                default:  guid = typeof(ConfigControl).GUID.ToString("B"); break;
            }
            Log("IDtmActiveXInformation.QueryActiveXGuid(funcId=" + funcId + ") -> " + guid);
            return guid;
        }

        public string QueryActiveXProgId(string functionCall)
        {
            int funcId = ParseFunctionId(functionCall);
            string progId;
            switch (funcId)
            {
                case 1:   progId = "LasecHartCommDTM.ConfigControl"; break;
                case 10:  progId = "LasecHartCommDTM.LogControl"; break;
                case 20:  progId = "LasecHartCommDTM.DeviceAddressControl"; break;
                case 30:  progId = "LasecHartCommDTM.DtmAddressControl"; break;
                case 100: progId = "LasecHartCommDTM.AboutControl"; break;
                default:  progId = "LasecHartCommDTM.ConfigControl"; break;
            }
            Log("IDtmActiveXInformation.QueryActiveXProgId(funcId=" + funcId + ") -> " + progId);
            return progId;
        }

        /// <summary>
        /// Parse functionId from PACTware's functionCall XML.
        /// Format: &lt;FDT xmlns="x-schema:DTMFunctionCallSchema.xml"
        ///           xmlns:func="x-schema:DTMFunctionsSchema.xml"&gt;
        ///           &lt;FDTFunctionCall func:functionId="1"/&gt;
        ///         &lt;/FDT&gt;
        /// </summary>
        private static int ParseFunctionId(string functionCall)
        {
            if (string.IsNullOrEmpty(functionCall)) return 1;
            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(functionCall);
                var nsMgr = new XmlNamespaceManager(doc.NameTable);
                nsMgr.AddNamespace("func", "x-schema:DTMFunctionsSchema.xml");
                var attr = doc.SelectSingleNode("//*/@func:functionId", nsMgr);
                if (attr != null)
                {
                    int id;
                    if (int.TryParse(attr.Value, out id)) return id;
                }
                // Fallback: try without namespace
                var node = doc.SelectSingleNode("//*/@functionId");
                if (node != null)
                {
                    int id;
                    if (int.TryParse(node.Value, out id)) return id;
                }
            }
            catch (Exception ex)
            {
                Log("ParseFunctionId error: " + ex.Message);
            }
            return 1; // default to Configuration
        }

        // ----------------------------------------------------------------
        // Registro FDT DTM no COM (32-bit)
        // ----------------------------------------------------------------
        private const string FdtDtmCategoryId  = "{036D1490-387B-11D4-86E1-00E0987270B9}";
        private const string HartBusCategoryId  = "{036D1498-387B-11D4-86E1-00E0987270B9}";
        private const string FdtDtm12CategoryId = "{036D1493-387B-11D4-86E1-00E0987270B9}";
        private const string FdtCommDtmCatId    = "{036D1494-387B-11D4-86E1-00E0987270B9}";
        private const string FdtCompatCatId     = "{036D1495-387B-11D4-86E1-00E0987270B9}";
        // ActiveX categories (required by PACTware, present on CWHart)
        private const string PersistStreamInitCatId  = "{0DE86A53-2BAA-11CF-A229-00AA003D7352}";
        private const string PersistPropertyBagCatId = "{0DE86A57-2BAA-11CF-A229-00AA003D7352}";
        private const string AutomationObjectsCatId  = "{40FC6ED5-2438-11CF-A3DB-080036F12502}";

        [ComRegisterFunction]
        public static void Register(Type t)
        {
            try
            {
                string clsidKeyPath = @"CLSID\" + t.GUID.ToString("B");

                using (var clsidKey = Registry.ClassesRoot.OpenSubKey(clsidKeyPath, true))
                {
                    if (clsidKey != null)
                    {
                        // Implemented Categories — FDT standard
                        clsidKey.CreateSubKey(@"Implemented Categories\" + FdtDtmCategoryId);
                        clsidKey.CreateSubKey(@"Implemented Categories\" + HartBusCategoryId);
                        clsidKey.CreateSubKey(@"Implemented Categories\" + FdtDtm12CategoryId);
                        clsidKey.CreateSubKey(@"Implemented Categories\" + FdtCommDtmCatId);
                        clsidKey.CreateSubKey(@"Implemented Categories\" + FdtCompatCatId);
                        // Implemented Categories — ActiveX (required by PACTware)
                        clsidKey.CreateSubKey(@"Implemented Categories\" + PersistStreamInitCatId);
                        clsidKey.CreateSubKey(@"Implemented Categories\" + PersistPropertyBagCatId);
                        clsidKey.CreateSubKey(@"Implemented Categories\" + AutomationObjectsCatId);

                        // TypeLib — needed by native COM callers for interface marshaling
                        // GUID from regasm /tlb output for LasecHartCommDTM
                        using (var tlbKey = clsidKey.CreateSubKey("TypeLib"))
                            tlbKey?.SetValue(null, "{40498B38-0A79-3F68-905F-7954AA76AEA6}");

                        // Programmable — indicates this is a programmable COM object
                        clsidKey.CreateSubKey("Programmable");

                        // VERSION — FDT version
                        using (var verKey = clsidKey.CreateSubKey("VERSION"))
                            verKey?.SetValue(null, "1.0");
                    }
                }

                using (var catKey = Registry.ClassesRoot.CreateSubKey(@"Component Categories\" + FdtDtmCategoryId))
                    if (catKey != null) catKey.SetValue(null, "FDT DTM");

                using (var catKey = Registry.ClassesRoot.CreateSubKey(@"Component Categories\" + HartBusCategoryId))
                    if (catKey != null) catKey.SetValue(null, "HART");

                // NOTE: ActiveX controls (ConfigControl, LogControl, DeviceAddressControl,
                // DtmAddressControl, AboutControl) each have their own [ComRegisterFunction]
                // that registers their Control/MiscStatus/TypeLib entries via
                // ActiveXControlBase.RegisterActiveXControl(). No need to do it here.
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Erro ao registrar categorias FDT: " + ex.Message);
            }
        }

        [ComUnregisterFunction]
        public static void Unregister(Type t)
        {
            try
            {
                string clsidKeyPath = @"CLSID\" + t.GUID.ToString("B");

                using (var clsidKey = Registry.ClassesRoot.OpenSubKey(clsidKeyPath, true))
                {
                    if (clsidKey != null)
                    {
                        foreach (var cat in new[] { FdtDtmCategoryId, HartBusCategoryId, FdtDtm12CategoryId, FdtCommDtmCatId, FdtCompatCatId, PersistStreamInitCatId, PersistPropertyBagCatId, AutomationObjectsCatId })
                            try { clsidKey.DeleteSubKeyTree(@"Implemented Categories\" + cat); } catch { }
                    }
                }
            }
            catch { }
        }
    }
}
