using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
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
        [DispId(1)]
        [return: MarshalAs(UnmanagedType.BStr)]
        string QueryActiveXGuid(
            [In, MarshalAs(UnmanagedType.BStr)] string functionCall);

        [DispId(2)]
        [return: MarshalAs(UnmanagedType.BStr)]
        string QueryActiveXProgId(
            [In, MarshalAs(UnmanagedType.BStr)] string functionCall);
    }

    // IDtmActiveXControl — PACTware calls Init() on the embedded ActiveX control
    // to pass the DTM reference and function call information.
    // Without this interface, PACTware cannot connect the control to the DTM.
    [Guid("036D1486-387B-11D4-86E1-00E0987270B9")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    [ComVisible(true)]
    public interface IDtmActiveXControl
    {
        [DispId(1)]
        bool Init(
            [In, MarshalAs(UnmanagedType.BStr)] string invokeId,
            [In, MarshalAs(UnmanagedType.BStr)] string functionCall,
            [In, MarshalAs(UnmanagedType.Interface)] IDtm dtm);

        [DispId(2)]
        bool PrepareToRelease();
    }

    // ----------------------------------------------------------------
    // Local channel interface definitions — Dual layout (IDispatch vtable)
    // Replaces Jigfdt.Fdt100 ComImport types to ensure correct CCW vtable.
    // PACTware expects Dual interfaces (custom methods at slot 7+).
    // ComImport embedded types may produce IUnknown layout (slot 3+), causing
    // PACTware to call the wrong vtable slot and silently fail.
    // ----------------------------------------------------------------

    [Guid("036D1489-387B-11D4-86E1-00E0987270B9")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    [ComVisible(true)]
    public interface IDtmChannel
    {
        [DispId(1)]
        [return: MarshalAs(UnmanagedType.Interface)]
        IFdtChannelCollection GetChannels();
    }

    [Guid("036D1484-387B-11D4-86E1-00E0987270B9")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    [ComVisible(true)]
    public interface IFdtChannelSubTopology
    {
        [DispId(1)]
        bool ScanRequest([In, MarshalAs(UnmanagedType.BStr)] string invokeId);
        [DispId(2)]
        bool ValidateAddChild([In, MarshalAs(UnmanagedType.BStr)] string childsystemTag);
        [DispId(4)]
        bool ValidateRemoveChild([In, MarshalAs(UnmanagedType.BStr)] string childsystemTag);
        [DispId(5)]
        void OnAddChild([In, MarshalAs(UnmanagedType.BStr)] string childsystemTag);
        [DispId(6)]
        void OnRemoveChild([In, MarshalAs(UnmanagedType.BStr)] string childsystemTag);
    }

    [Guid("036D1488-387B-11D4-86E1-00E0987270B9")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    [ComVisible(true)]
    public interface IFdtChannel
    {
        [DispId(1)]
        [return: MarshalAs(UnmanagedType.BStr)]
        string GetChannelPath();

        [DispId(2)]
        [return: MarshalAs(UnmanagedType.BStr)]
        string GetChannelParameters(
            [In, MarshalAs(UnmanagedType.BStr)] string parameterPath,
            [In, MarshalAs(UnmanagedType.BStr)] string protocolId);

        [DispId(3)]
        bool SetChannelParameters(
            [In, MarshalAs(UnmanagedType.BStr)] string parameterPath,
            [In, MarshalAs(UnmanagedType.BStr)] string protocolId,
            [In, MarshalAs(UnmanagedType.BStr)] string XmlDocument);
    }

    // ----------------------------------------------------------------
    // Local IFdtCommunicationEvents — Dual layout with correct DispIds
    // Replaces Jigfdt.Fdt100.IFdtCommunicationEvents (ComImport).
    // ----------------------------------------------------------------
    [Guid("036D1485-387B-11D4-86E1-00E0987270B9")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    [ComVisible(true)]
    public interface IFdtCommunicationEvents
    {
        [DispId(1)]
        void OnAbort([In, MarshalAs(UnmanagedType.BStr)] string communicationReference);

        [DispId(2)]
        void OnConnectResponse(
            [In, MarshalAs(UnmanagedType.BStr)] string invokeId,
            [In, MarshalAs(UnmanagedType.BStr)] string response);

        [DispId(3)]
        void OnDisconnectResponse(
            [In, MarshalAs(UnmanagedType.BStr)] string invokeId,
            [In, MarshalAs(UnmanagedType.BStr)] string response);

        [DispId(4)]
        void OnTransactionResponse(
            [In, MarshalAs(UnmanagedType.BStr)] string invokeId,
            [In, MarshalAs(UnmanagedType.BStr)] string response);
    }

    // ----------------------------------------------------------------
    // Local IFdtCommunication — Dual layout with correct DispIds
    // Replaces Jigfdt.Fdt100.IFdtCommunication (ComImport) to ensure
    // PACTware can call ConnectRequest via IDispatch.
    // ----------------------------------------------------------------
    [Guid("039ECFC4-9CA8-44E6-944D-B37F288A34D8")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    [ComVisible(true)]
    public interface IFdtCommunication
    {
        [DispId(1)]
        void Abort([In, MarshalAs(UnmanagedType.BStr)] string fieldbusFrame);

        [DispId(2)]
        bool ConnectRequest(
            [In, MarshalAs(UnmanagedType.Interface)] IFdtCommunicationEvents callBack,
            [In, MarshalAs(UnmanagedType.BStr)] string invokeId,
            [In, MarshalAs(UnmanagedType.BStr)] string protocolId,
            [In, MarshalAs(UnmanagedType.BStr)] string fieldbusFrame);

        [DispId(3)]
        bool DisconnectRequest(
            [In, MarshalAs(UnmanagedType.BStr)] string invokeId,
            [In, MarshalAs(UnmanagedType.BStr)] string fieldbusFrame);

        [DispId(4)]
        bool TransactionRequest(
            [In, MarshalAs(UnmanagedType.BStr)] string invokeId,
            [In, MarshalAs(UnmanagedType.BStr)] string fieldbusFrame);

        [DispId(5)]
        [return: MarshalAs(UnmanagedType.BStr)]
        string GetSupportedProtocols();

        [DispId(6)]
        bool SequenceBegin([In, MarshalAs(UnmanagedType.BStr)] string fieldbusFrame);

        [DispId(7)]
        bool SequenceStart([In, MarshalAs(UnmanagedType.BStr)] string fieldbusFrame);

        [DispId(8)]
        bool SequenceEnd([In, MarshalAs(UnmanagedType.BStr)] string fieldbusFrame);
    }

    [Guid("E4F31A10-45BF-11D4-BBB3-0060080993FF")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    [ComVisible(true)]
    public interface IFdtChannelCollection
    {
        [DispId(1)]
        [return: MarshalAs(UnmanagedType.Interface)]
        IFdtChannel get_Item([In] ref object pvarIndex);

        [DispId(2)]
        int Count { [return: MarshalAs(UnmanagedType.I4)] get; }

        [DispId(-4)]
        [return: MarshalAs(UnmanagedType.Interface)]
        IEnumerator GetEnumerator();
    }

    // ----------------------------------------------------------------
    // IFdtEvents — Container→DTM event sink (CWHart implements this)
    // PACTware calls these methods to notify the DTM of state changes.
    // ----------------------------------------------------------------
    [Guid("036D1478-387B-11D4-86E1-00E0987270B9")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    [ComVisible(true)]
    public interface IFdtEvents
    {
        [DispId(1)]
        void OnChildParameterChanged(
            [In, MarshalAs(UnmanagedType.BStr)] string systemTag);

        [DispId(2)]
        void OnParameterChanged(
            [In, MarshalAs(UnmanagedType.BStr)] string systemTag,
            [In, MarshalAs(UnmanagedType.BStr)] string parameter);

        [DispId(3)]
        void OnLockDataSet(
            [In, MarshalAs(UnmanagedType.BStr)] string systemTag,
            [In, MarshalAs(UnmanagedType.BStr)] string userName);

        [DispId(4)]
        bool OnUnlockDataSet(
            [In, MarshalAs(UnmanagedType.BStr)] string systemTag,
            [In, MarshalAs(UnmanagedType.BStr)] string userName);
    }

    // ----------------------------------------------------------------
    // IDtmDocumentation — CWHart implements this for help files
    // ----------------------------------------------------------------
    [Guid("036D147C-387B-11D4-86E1-00E0987270B9")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    [ComVisible(true)]
    public interface IDtmDocumentation
    {
        [DispId(1)]
        [return: MarshalAs(UnmanagedType.BStr)]
        string GetDocumentation(
            [In, MarshalAs(UnmanagedType.BStr)] string functionCall);
    }

    // ----------------------------------------------------------------
    // IDtmEvents — outgoing event source interface (COM connection points)
    // PACTware subscribes via IConnectionPointContainer → Advise().
    // The DTM fires events to notify PACTware of state changes.
    // GUID must match Jigfdt.Fdt100.IDtmEvents exactly.
    // ----------------------------------------------------------------
    [Guid("F15BA42E-BBF1-42ED-8009-7F664A002CFB")]
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    [ComVisible(true)]
    public interface IDtmEventsSource
    {
        [DispId(1)]  void OnParameterChanged([MarshalAs(UnmanagedType.BStr)] string systemTag, [MarshalAs(UnmanagedType.BStr)] string parameter);
        [DispId(2)]  void OnErrorMessage([MarshalAs(UnmanagedType.BStr)] string systemTag, [MarshalAs(UnmanagedType.BStr)] string errorMessage);
        [DispId(3)]  void OnProgress([MarshalAs(UnmanagedType.BStr)] string systemTag, [MarshalAs(UnmanagedType.BStr)] string title, short percent, [MarshalAs(UnmanagedType.Bool)] bool show);
        [DispId(4)]  void OnUploadFinished([MarshalAs(UnmanagedType.BStr)] string invokeId, [MarshalAs(UnmanagedType.Bool)] bool success);
        [DispId(5)]  void OnDownloadFinished([MarshalAs(UnmanagedType.BStr)] string invokeId, [MarshalAs(UnmanagedType.Bool)] bool success);
        [DispId(6)]  void OnApplicationClosed([MarshalAs(UnmanagedType.BStr)] string invokeId);
        [DispId(8)]  void OnFunctionChanged([MarshalAs(UnmanagedType.BStr)] string systemTag);
        [DispId(9)]  void OnChannelFunctionChanged([MarshalAs(UnmanagedType.BStr)] string systemTag, [MarshalAs(UnmanagedType.BStr)] string channelPath);
        [DispId(10)] void OnPrint([MarshalAs(UnmanagedType.BStr)] string systemTag, [MarshalAs(UnmanagedType.BStr)] string functionCall);
        [DispId(11)] void OnNavigation([MarshalAs(UnmanagedType.BStr)] string systemTag);
        [DispId(12)] void OnOnlineStateChanged([MarshalAs(UnmanagedType.BStr)] string systemTag, [MarshalAs(UnmanagedType.Bool)] bool onlineState);
        [DispId(13)] void OnPreparedToRelease([MarshalAs(UnmanagedType.BStr)] string systemTag);
        [DispId(14)] void OnPreparedToReleaseCommunication([MarshalAs(UnmanagedType.BStr)] string systemTag);
        [DispId(15)] void OnInvokedFunctionFinished([MarshalAs(UnmanagedType.BStr)] string invokeId, [MarshalAs(UnmanagedType.Bool)] bool success);
        [DispId(16)] void OnScanResponse([MarshalAs(UnmanagedType.BStr)] string invokeId, [MarshalAs(UnmanagedType.BStr)] string response);
    }

    // Delegate types for COM connection point events
    [ComVisible(false)] public delegate void OnParameterChangedEventHandler(string systemTag, string parameter);
    [ComVisible(false)] public delegate void OnErrorMessageEventHandler(string systemTag, string errorMessage);
    [ComVisible(false)] public delegate void OnProgressEventHandler(string systemTag, string title, short percent, bool show);
    [ComVisible(false)] public delegate void OnUploadFinishedEventHandler(string invokeId, bool success);
    [ComVisible(false)] public delegate void OnDownloadFinishedEventHandler(string invokeId, bool success);
    [ComVisible(false)] public delegate void OnApplicationClosedEventHandler(string invokeId);
    [ComVisible(false)] public delegate void OnFunctionChangedEventHandler(string systemTag);
    [ComVisible(false)] public delegate void OnChannelFunctionChangedEventHandler(string systemTag, string channelPath);
    [ComVisible(false)] public delegate void OnPrintEventHandler(string systemTag, string functionCall);
    [ComVisible(false)] public delegate void OnNavigationEventHandler(string systemTag);
    [ComVisible(false)] public delegate void OnOnlineStateChangedEventHandler(string systemTag, bool onlineState);
    [ComVisible(false)] public delegate void OnPreparedToReleaseEventHandler(string systemTag);
    [ComVisible(false)] public delegate void OnPreparedToReleaseCommunicationEventHandler(string systemTag);
    [ComVisible(false)] public delegate void OnInvokedFunctionFinishedEventHandler(string invokeId, bool success);
    [ComVisible(false)] public delegate void OnScanResponseEventHandler(string invokeId, string response);

    [Guid("B4B6B3E7-639D-460B-B9A0-6C7F7EB20010")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComSourceInterfaces(typeof(IDtmEventsSource))]
    [ComVisible(true)]
    public class CommDtm : IDtmInformation, IDtm, IFdtCommunication, IDtmParameter, IDtmActiveXInformation, IDtmChannel, IFdtChannel, IFdtChannelSubTopology, IFdtEvents, IDtmDocumentation, IPersistStreamInit, IPersistPropertyBag, ICustomQueryInterface, IConnectionPointContainer
    {
        private static CommDtm _current;
        internal static CommDtm Current => _current;

        private ChannelManager _manager;
        private IFdtCommunicationEvents _callback;
        private IFdtContainer _fdtContainer;
        private string _systemTag;
        private bool _connected;
        private int _commRef;

        // COM connection point events — PACTware subscribes via IConnectionPointContainer.
        // CLR auto-generates IConnectionPointContainer from [ComSourceInterfaces].
        // Event names MUST match method names in IDtmEventsSource interface exactly.
        // Using custom accessors to log subscription attempts.

        private OnParameterChangedEventHandler _evParameterChanged;
        public event OnParameterChangedEventHandler OnParameterChanged {
            add { Log("EVENT +subscribe OnParameterChanged"); _evParameterChanged += value; }
            remove { Log("EVENT -unsubscribe OnParameterChanged"); _evParameterChanged -= value; }
        }

        private OnErrorMessageEventHandler _evErrorMessage;
        public event OnErrorMessageEventHandler OnErrorMessage {
            add { Log("EVENT +subscribe OnErrorMessage"); _evErrorMessage += value; }
            remove { Log("EVENT -unsubscribe OnErrorMessage"); _evErrorMessage -= value; }
        }

        private OnProgressEventHandler _evProgress;
        public event OnProgressEventHandler OnProgress {
            add { Log("EVENT +subscribe OnProgress"); _evProgress += value; }
            remove { Log("EVENT -unsubscribe OnProgress"); _evProgress -= value; }
        }

        private OnUploadFinishedEventHandler _evUploadFinished;
        public event OnUploadFinishedEventHandler OnUploadFinished {
            add { Log("EVENT +subscribe OnUploadFinished"); _evUploadFinished += value; }
            remove { Log("EVENT -unsubscribe OnUploadFinished"); _evUploadFinished -= value; }
        }

        private OnDownloadFinishedEventHandler _evDownloadFinished;
        public event OnDownloadFinishedEventHandler OnDownloadFinished {
            add { Log("EVENT +subscribe OnDownloadFinished"); _evDownloadFinished += value; }
            remove { Log("EVENT -unsubscribe OnDownloadFinished"); _evDownloadFinished -= value; }
        }

        private OnApplicationClosedEventHandler _evApplicationClosed;
        public event OnApplicationClosedEventHandler OnApplicationClosed {
            add { Log("EVENT +subscribe OnApplicationClosed"); _evApplicationClosed += value; }
            remove { Log("EVENT -unsubscribe OnApplicationClosed"); _evApplicationClosed -= value; }
        }

        private OnFunctionChangedEventHandler _evFunctionChanged;
        public event OnFunctionChangedEventHandler OnFunctionChanged {
            add { Log("EVENT +subscribe OnFunctionChanged"); _evFunctionChanged += value; }
            remove { Log("EVENT -unsubscribe OnFunctionChanged"); _evFunctionChanged -= value; }
        }

        private OnChannelFunctionChangedEventHandler _evChannelFunctionChanged;
        public event OnChannelFunctionChangedEventHandler OnChannelFunctionChanged {
            add { Log("EVENT +subscribe OnChannelFunctionChanged"); _evChannelFunctionChanged += value; }
            remove { Log("EVENT -unsubscribe OnChannelFunctionChanged"); _evChannelFunctionChanged -= value; }
        }

        private OnPrintEventHandler _evPrint;
        public event OnPrintEventHandler OnPrint {
            add { Log("EVENT +subscribe OnPrint"); _evPrint += value; }
            remove { Log("EVENT -unsubscribe OnPrint"); _evPrint -= value; }
        }

        private OnNavigationEventHandler _evNavigation;
        public event OnNavigationEventHandler OnNavigation {
            add { Log("EVENT +subscribe OnNavigation"); _evNavigation += value; }
            remove { Log("EVENT -unsubscribe OnNavigation"); _evNavigation -= value; }
        }

        private OnOnlineStateChangedEventHandler _evOnlineStateChanged;
        public event OnOnlineStateChangedEventHandler OnOnlineStateChanged {
            add { Log("EVENT +subscribe OnOnlineStateChanged"); _evOnlineStateChanged += value; }
            remove { Log("EVENT -unsubscribe OnOnlineStateChanged"); _evOnlineStateChanged -= value; }
        }

        private OnPreparedToReleaseEventHandler _evPreparedToRelease;
        public event OnPreparedToReleaseEventHandler OnPreparedToRelease {
            add { Log("EVENT +subscribe OnPreparedToRelease"); _evPreparedToRelease += value; }
            remove { Log("EVENT -unsubscribe OnPreparedToRelease"); _evPreparedToRelease -= value; }
        }

        private OnPreparedToReleaseCommunicationEventHandler _evPreparedToReleaseCommunication;
        public event OnPreparedToReleaseCommunicationEventHandler OnPreparedToReleaseCommunication {
            add { Log("EVENT +subscribe OnPreparedToReleaseCommunication"); _evPreparedToReleaseCommunication += value; }
            remove { Log("EVENT -unsubscribe OnPreparedToReleaseCommunication"); _evPreparedToReleaseCommunication -= value; }
        }

        private OnInvokedFunctionFinishedEventHandler _evInvokedFunctionFinished;
        public event OnInvokedFunctionFinishedEventHandler OnInvokedFunctionFinished {
            add { Log("EVENT +subscribe OnInvokedFunctionFinished"); _evInvokedFunctionFinished += value; }
            remove { Log("EVENT -unsubscribe OnInvokedFunctionFinished"); _evInvokedFunctionFinished -= value; }
        }

        private OnScanResponseEventHandler _evScanResponse;
        public event OnScanResponseEventHandler OnScanResponse {
            add { Log("EVENT +subscribe OnScanResponse"); _evScanResponse += value; }
            remove { Log("EVENT -unsubscribe OnScanResponse"); _evScanResponse -= value; }
        }

        // ---- Explicit IConnectionPointContainer implementation ----
        // CLR's [ComSourceInterfaces] auto-generates IConnectionPointContainer, but PACTware
        // may not discover it. By implementing explicitly, we ensure it's available and can log.
        private DtmEventsConnectionPoint _eventsCP;

        private DtmEventsConnectionPoint GetOrCreateCP()
        {
            if (_eventsCP == null)
                _eventsCP = new DtmEventsConnectionPoint(this);
            return _eventsCP;
        }

        void IConnectionPointContainer.EnumConnectionPoints(out IEnumConnectionPoints ppEnum)
        {
            Log("IConnectionPointContainer.EnumConnectionPoints()");
            ppEnum = null; // PACTware typically uses FindConnectionPoint, not Enum
        }

        void IConnectionPointContainer.FindConnectionPoint(ref Guid riid, out IConnectionPoint ppCP)
        {
            Log("IConnectionPointContainer.FindConnectionPoint(riid=" + riid.ToString("B") + ")");
            Guid eventsIID = new Guid("F15BA42E-BBF1-42ED-8009-7F664A002CFB");
            if (riid == eventsIID)
            {
                ppCP = GetOrCreateCP();
                Log("FindConnectionPoint -> returned DtmEventsConnectionPoint OK");
                return;
            }
            Log("FindConnectionPoint -> CONNECT_E_NOCONNECTION for " + riid.ToString("B"));
            ppCP = null;
            Marshal.ThrowExceptionForHR(unchecked((int)0x80040200)); // CONNECT_E_NOCONNECTION
        }

        /// <summary>Fire event to all COM sinks registered via IConnectionPoint.Advise</summary>
        private void FireCPEvent(int dispId, string name, params object[] args)
        {
            var cp = _eventsCP;
            if (cp != null)
                cp.FireEvent(dispId, name, args);
        }

        // ---- Parâmetros de comunicação (equivalente ao DTMPARAMETER.XML do CWHart) ----
        internal string _protocol     = "serial";    // "serial", "tcp" ou "udp"
        internal string _comPort      = "COM1";       // Porta serial (modo serial)
        internal int    _baudRate     = 1200;          // Baud rate HART padrão
        internal string _ipAddress    = "127.0.0.1";
        internal int    _ipPort       = 5094;
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
        private static readonly Guid IID_IFdtChannelSubTopo  = new Guid("036D1484-387B-11D4-86E1-00E0987270B9");
        private static readonly Guid IID_IDtmChannel          = new Guid("036D1489-387B-11D4-86E1-00E0987270B9");
        private static readonly Guid IID_IFdtChannel          = new Guid("036D1488-387B-11D4-86E1-00E0987270B9");
        private static readonly Guid IID_IDispatch            = new Guid("00020400-0000-0000-C000-000000000046");
        private static readonly Guid IID_IUnknown             = new Guid("00000000-0000-0000-C000-000000000046");
        private static readonly Guid IID_IFdtEvents           = new Guid("036D1478-387B-11D4-86E1-00E0987270B9");
        private static readonly Guid IID_IDtmDocumentation    = new Guid("036D147C-387B-11D4-86E1-00E0987270B9");
        private static readonly Guid IID_IConnPtContainer     = new Guid("B196B284-BAB4-101A-B69C-00AA00341D07");
        private static readonly Guid IID_IBtm                 = new Guid("96341E37-9611-46BA-80ED-A85BD73BF518");
        private static readonly Guid IID_IDtm2                = new Guid("51E1F44B-D6A1-423D-B11F-AD38EDE78047");

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
            AppDomain.CurrentDomain.FirstChanceException += (s, e) =>
            {
                // Log ALL exceptions including caught ones — helps detect silent COM failures
                Log("FIRST-CHANCE EXCEPTION: " + e.Exception.GetType().Name + ": " + e.Exception.Message
                    + " at " + e.Exception.TargetSite?.Name);
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
            else if (iid == IID_IFdtChannelSubTopo) name = "IFdtChannelSubTopology";
            else if (iid == IID_IDtmChannel) name = "IDtmChannel";
            else if (iid == IID_IFdtChannel) name = "IFdtChannel";
            else if (iid == IID_IFdtEvents) name = "IFdtEvents";
            else if (iid == IID_IDtmDocumentation) name = "IDtmDocumentation";
            else if (iid == IID_IConnPtContainer) name = "IConnectionPointContainer";
            else if (iid == IID_IBtm) name = "IBtm";
            else if (iid == IID_IDtm2) name = "IDtm2";
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
                if (iid == IID_IFdtChannelSubTopo)
                {
                    ppv = Marshal.GetComInterfaceForObject(this, typeof(IFdtChannelSubTopology));
                    Log("QI -> HANDLED IFdtChannelSubTopology");
                    return CustomQueryInterfaceResult.Handled;
                }
                if (iid == IID_IDtmChannel)
                {
                    ppv = Marshal.GetComInterfaceForObject(this, typeof(IDtmChannel));
                    Log("QI -> HANDLED IDtmChannel");
                    return CustomQueryInterfaceResult.Handled;
                }
                if (iid == IID_IFdtChannel)
                {
                    ppv = Marshal.GetComInterfaceForObject(this, typeof(IFdtChannel));
                    Log("QI -> HANDLED IFdtChannel");
                    return CustomQueryInterfaceResult.Handled;
                }
                if (iid == IID_IFdtEvents)
                {
                    ppv = Marshal.GetComInterfaceForObject(this, typeof(IFdtEvents));
                    Log("QI -> HANDLED IFdtEvents");
                    return CustomQueryInterfaceResult.Handled;
                }
                if (iid == IID_IDtmDocumentation)
                {
                    ppv = Marshal.GetComInterfaceForObject(this, typeof(IDtmDocumentation));
                    Log("QI -> HANDLED IDtmDocumentation");
                    return CustomQueryInterfaceResult.Handled;
                }
                if (iid == IID_IConnPtContainer)
                {
                    ppv = Marshal.GetComInterfaceForObject(this, typeof(IConnectionPointContainer));
                    Log("QI -> HANDLED IConnectionPointContainer (explicit)");
                    return CustomQueryInterfaceResult.Handled;
                }
            }
            catch (Exception ex)
            {
                Log("QI HANDLE ERROR: " + ex.Message + " stack: " + ex.StackTrace);
            }
            finally
            {
                _inGetInterface = false;
            }

            // Log unhandled QIs with detailed info
            Log("QI -> NOT HANDLED " + name + " (falls through to CLR)");
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

            // Self-test: IConnectionPointContainer
            try
            {
                IntPtr pUnk2 = Marshal.GetIUnknownForObject(this);
                try
                {
                    Guid iidCPC = new Guid("B196B284-BAB4-101A-B69C-00AA00341D07");
                    IntPtr pCPC;
                    int hrCPC = Marshal.QueryInterface(pUnk2, ref iidCPC, out pCPC);
                    Log("Self-test IConnectionPointContainer QI hr=0x" + hrCPC.ToString("X8"));
                    if (hrCPC == 0)
                    {
                        Log("Self-test IConnectionPointContainer -> AVAILABLE");
                        // Try FindConnectionPoint for IDtmEventsSource
                        try
                        {
                            var cpc = (IConnectionPointContainer)Marshal.GetObjectForIUnknown(pCPC);
                            Guid iidEvents = new Guid("F15BA42E-BBF1-42ED-8009-7F664A002CFB");
                            IConnectionPoint cp;
                            cpc.FindConnectionPoint(ref iidEvents, out cp);
                            if (cp != null)
                            {
                                Guid cpIID;
                                cp.GetConnectionInterface(out cpIID);
                                Log("Self-test FindConnectionPoint(IDtmEventsSource) OK, CP interface=" + cpIID.ToString("B"));
                            }
                            else
                            {
                                Log("Self-test FindConnectionPoint(IDtmEventsSource) returned null");
                            }
                        }
                        catch (Exception ex2)
                        {
                            Log("Self-test FindConnectionPoint FAILED: " + ex2.GetType().Name + ": " + ex2.Message);
                        }
                        Marshal.Release(pCPC);
                    }
                    else
                    {
                        Log("Self-test IConnectionPointContainer -> NOT AVAILABLE (hr=0x" + hrCPC.ToString("X8") + ")");
                    }
                }
                finally
                {
                    Marshal.Release(pUnk2);
                }
            }
            catch (Exception ex)
            {
                Log("Self-test IConnectionPointContainer ERROR: " + ex.ToString());
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
                var path = @"C:\Temp\LasecHartDTM.log";
                var dir = System.IO.Path.GetDirectoryName(path);
                if (!System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);
                var line = DateTime.Now.ToString("HH:mm:ss.fff") + " [" + System.Diagnostics.Process.GetCurrentProcess().ProcessName + "] " + msg;
                System.IO.File.AppendAllText(path, line + System.Environment.NewLine);
            }
            catch { }
        }

        // ---- Communication Log (visible to user via functionId=10) ----
        private static readonly List<string> _commLog = new List<string>();
        private static readonly object _commLogLock = new object();
        internal static event Action<string> CommLogAdded;

        internal static void LogComm(string msg)
        {
            var line = DateTime.Now.ToString("HH:mm:ss.fff") + " " + msg;
            lock (_commLogLock) { _commLog.Add(line); }
            CommLogAdded?.Invoke(line);
        }

        internal static string[] GetCommLog()
        {
            lock (_commLogLock) { return _commLog.ToArray(); }
        }

        internal static void ClearCommLog()
        {
            lock (_commLogLock) { _commLog.Clear(); }
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
            Log("Environment(tag=" + (systemTag ?? "null") + ", container=" + (container == null ? "null" : "object") + ")");
            _systemTag = systemTag;
            _fdtContainer = container;
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
                        if (attr["comPort"] != null) _comPort = attr["comPort"].Value;
                        if (attr["baudRate"] != null) _baudRate = int.Parse(attr["baudRate"].Value);
                        if (attr["primaryMaster"] != null) _primaryMaster = attr["primaryMaster"].Value == "1";
                        if (attr["preambleCount"] != null) _preambleCount = int.Parse(attr["preambleCount"].Value);
                        if (attr["retryCount"] != null) _retryCount = int.Parse(attr["retryCount"].Value);
                        if (attr["scanStart"] != null) _scanStart = int.Parse(attr["scanStart"].Value);
                        if (attr["scanStop"] != null) _scanStop = int.Parse(attr["scanStop"].Value);
                        if (attr["burstMode"] != null) _burstMode = attr["burstMode"].Value == "1";
                        if (attr["timeout"] != null) _timeout = int.Parse(attr["timeout"].Value);
                    }
                    Log("Config() restored parameters: protocol=" + _protocol +
                        (_protocol == "serial" ? " port=" + _comPort + " baud=" + _baudRate
                                               : " ip=" + _ipAddress + ":" + _ipPort));
                }
                catch (Exception ex)
                {
                    Log("Config() parse error: " + ex.Message);
                }
            }
            return true;
        }

        // Open communication channel and notify PACTware via COM events.
        // CWHart opens the COM port here and fires OnOnlineStateChanged(true)
        // via IConnectionPointContainer. PACTware waits for this event to
        // mark the DTM as "green" (connected).
        bool IDtm.SetCommunication(Jigfdt.Fdt100.IFdtCommunication communication)
        {
            Log("SetCommunication(communication=" + (communication == null ? "null" : "object") + ")");

            if (communication == null)
            {
                // Top-level CommDTM: mark as ready for ConnectRequest.
                // Do NOT open TCP here — physical channel opens in ConnectRequest
                // when a Device DTM (or PACTware) actually needs communication.
                _connected = true;
                Log("SetCommunication: top-level CommDTM, marked ready, firing events");
                // PACTware subscribes to events via IConnectionPointContainer (exempt from
                // ICustomQueryInterface logging). Fire OnOnlineStateChanged so PACTware
                // marks us as "green" (online).
                FireOnlineStateChanged(true);
                FireFunctionChanged();
                return true;
            }

            // Non-top-level: communication from parent CommDTM
            _connected = true;
            Log("SetCommunication: received parent communication reference, firing events");
            FireOnlineStateChanged(true);
            FireFunctionChanged();
            return true;
        }

        private void FireOnlineStateChanged(bool online)
        {
            try
            {
                string tag = _systemTag ?? "";
                int sinkCount = _eventsCP != null ? _eventsCP.SinkCount : 0;
                Log("FireOnlineStateChanged(tag=" + tag + ", online=" + online + ") delegates=" + (_evOnlineStateChanged != null ? "YES" : "NO") + " cpSinks=" + sinkCount);
                _evOnlineStateChanged?.Invoke(tag, online);
                FireCPEvent(12, "OnOnlineStateChanged", tag, online);
                Log("FireOnlineStateChanged: done");
            }
            catch (Exception ex)
            {
                Log("FireOnlineStateChanged ERROR: " + ex.Message + " stack: " + ex.StackTrace);
            }
        }

        private void FireFunctionChanged()
        {
            try
            {
                string tag = _systemTag ?? "";
                int sinkCount = _eventsCP != null ? _eventsCP.SinkCount : 0;
                Log("FireFunctionChanged(tag=" + tag + ") delegates=" + (_evFunctionChanged != null ? "YES" : "NO") + " cpSinks=" + sinkCount);
                _evFunctionChanged?.Invoke(tag);
                FireCPEvent(8, "OnFunctionChanged", tag);
                Log("FireFunctionChanged: done");
            }
            catch (Exception ex)
            {
                Log("FireFunctionChanged ERROR: " + ex.Message + " stack: " + ex.StackTrace);
            }
        }

        public bool PrepareToRelease()
        {
            Log("PrepareToRelease()");
            _connected = false;
            try
            {
                string tag = _systemTag ?? "";
                int sinkCount = _eventsCP != null ? _eventsCP.SinkCount : 0;
                Log("Firing OnPreparedToRelease(tag=" + tag + ") delegates=" + (_evPreparedToRelease != null ? "YES" : "NO") + " cpSinks=" + sinkCount);
                _evPreparedToRelease?.Invoke(tag);
                FireCPEvent(13, "OnPreparedToRelease", tag);
            }
            catch (Exception ex) { Log("OnPreparedToRelease ERROR: " + ex.Message); }
            return true;
        }

        public bool PrepareToReleaseCommunication()
        {
            Log("PrepareToReleaseCommunication()");
            _manager?.Dispose();
            _manager = new ChannelManager();
            _connected = false;
            FireOnlineStateChanged(false);
            try
            {
                string tag = _systemTag ?? "";
                int sinkCount = _eventsCP != null ? _eventsCP.SinkCount : 0;
                Log("Firing OnPreparedToReleaseCommunication(tag=" + tag + ") delegates=" + (_evPreparedToReleaseCommunication != null ? "YES" : "NO") + " cpSinks=" + sinkCount);
                _evPreparedToReleaseCommunication?.Invoke(tag);
                FireCPEvent(14, "OnPreparedToReleaseCommunication", tag);
            }
            catch (Exception ex) { Log("OnPreparedToReleaseCommunication ERROR: " + ex.Message); }
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

            // CWHart resolves macros $(NOTCONNECTED)/$(CONNECTED) internally
            // before returning the XML. PACTware does NOT resolve these macros —
            // if returned as literal strings, PACTware ignores the function.
            string notConnected = _connected ? "0" : "1";
            string connected    = _connected ? "1" : "0";

            string xml =
                "<?xml version=\"1.0\"?>" +
                "<FDT xmlns=\"x-schema:DTMFunctionsSchema.xml\"" +
                " xmlns:fdt=\"x-schema:FDTDataTypesSchema.xml\"" +
                " xmlns:appId=\"x-schema:FDTApplicationIdSchema.xml\">" +
                  "<Functions label=\"Functions\" fdt:name=\"\" help=\"\">" +

                    // StandardFunction fdtConfiguration → "Parâmetro" no PACTware
                    "<StandardFunction fdt:name=\"Configuration\" help=\"\" functionId=\"1\"" +
                    " resizableStandardFunction=\"1\" printableStandardFunction=\"1\">" +
                      "<Status toggle=\"0\" checked=\"0\" enabled=\"" + notConnected + "\" hidden=\"0\" separator=\"0\"/>" +
                      "<appId:ApplicationId applicationId=\"fdtConfiguration\"/>" +
                    "</StandardFunction>" +

                    // Custom: Change device address — enabled only when CONNECTED
                    "<Function label=\"Change device address\" fdt:name=\"mnChangeDeviceAddress\"" +
                    " help=\"Change device address\" functionId=\"20\" hasGUI=\"1\" resizable=\"1\">" +
                      "<Status toggle=\"1\" checked=\"0\" enabled=\"" + connected + "\" hidden=\"0\" separator=\"0\"/>" +
                    "</Function>" +

                    // Custom: Change DTM address — enabled only when NOT CONNECTED
                    "<Function label=\"Change DTM address\" fdt:name=\"mnChangeDtmAddress\"" +
                    " help=\"Change DTM address\" functionId=\"30\" hasGUI=\"1\" resizable=\"1\">" +
                      "<Status toggle=\"1\" checked=\"0\" enabled=\"" + notConnected + "\" hidden=\"0\" separator=\"0\"/>" +
                    "</Function>" +

                    // Custom: Communication log — always enabled
                    "<Function label=\"Communication log\" fdt:name=\"mnLog\"" +
                    " help=\"Communication log\" functionId=\"10\" hasGUI=\"1\" resizable=\"1\">" +
                      "<Status toggle=\"1\" checked=\"0\" enabled=\"1\" hidden=\"0\" separator=\"0\"/>" +
                    "</Function>" +

                    // Custom: About — always enabled
                    "<Function label=\"About\" fdt:name=\"mnAbout\"" +
                    " help=\"About this DTM\" functionId=\"100\" hasGUI=\"1\" resizable=\"0\">" +
                      "<Status toggle=\"1\" checked=\"0\" enabled=\"1\" hidden=\"0\" separator=\"0\"/>" +
                    "</Function>" +

                  "</Functions>" +
                "</FDT>";

            Log("GetFunctions() returning " + xml.Length + " chars\n" + xml);
            return xml;
        }

        public bool InvokeFunctionRequest(string invokeId, string functionCall)
        {
            Log("InvokeFunctionRequest(id=" + invokeId + ", func=" + (functionCall ?? "null") + ")");
            Log($"[DEBUG] _ipAddress={_ipAddress}, _ipPort={_ipPort}, _protocol={_protocol}, _connected={_connected}");
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
                    case 1:  // fdtConfiguration → embedded ActiveX
                    case 10: // Communication log → embedded ActiveX
                    case 20: // Change device address → embedded ActiveX
                    case 30: // Change DTM address → embedded ActiveX
                    case 100: // About → embedded ActiveX
                        // PACTware will open the ActiveX control via IDtmActiveXInformation
                        Log("InvokeFunctionRequest -> ActiveX function " + funcId);
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

        private void VerifyTopology()
        {
            Log("VerifyTopology: checking communication channel...");
            LogComm("VerifyTopology initiated");

            if (_manager != null && _connected)
            {
                LogComm("VerifyTopology: channel is connected to " + _ipAddress + ":" + _ipPort);
                Log("VerifyTopology: OK - connected");
            }
            else
            {
                LogComm("VerifyTopology: channel is NOT connected");
                Log("VerifyTopology: NOT connected");
            }
        }

        // ----------------------------------------------------------------
        // IFdtChannelSubTopology — "Verificar Topologia" no PACTware
        // Envia HART Command 0 para cada endereço no range scanStart..scanStop
        // ----------------------------------------------------------------
        public bool ScanRequest(string invokeId)
        {
            Log("IFdtChannelSubTopology.ScanRequest(invokeId=" + invokeId + ")");
            LogComm("=== Topology scan started: addresses " + _scanStart + " to " + _scanStop + " ===");

            try
            {
                // Ensure channel manager exists
                if (_manager == null)
                    _manager = new ChannelManager();

                // Configure and connect if not already connected
                bool wasConnected = _connected;
                if (!_connected)
                {
                    Log("ScanRequest: connecting to " + _protocol + "://" + _ipAddress + ":" + _ipPort);
                    _manager.Configure(_protocol, _ipAddress, _ipPort, _timeout);
                    _connected = true;
                }

                int found = 0;
                for (int addr = _scanStart; addr <= _scanStop; addr++)
                {
                    LogComm("Scanning address " + addr + "...");
                    Log("ScanRequest: probing address " + addr);

                    try
                    {
                        byte[] frame = BuildHartCmd0Frame(addr);
                        LogComm("  TX: " + HartXmlHelper.BytesToHex(frame));

                        byte[] response = _manager.SendAndReceive(frame, _timeout);

                        if (response != null && response.Length > 0)
                        {
                            LogComm("  RX: " + HartXmlHelper.BytesToHex(response));
                            LogComm("  Address " + addr + ": DEVICE FOUND (" + response.Length + " bytes)");
                            Log("ScanRequest: FOUND device at address " + addr + " (" + response.Length + " bytes)");
                            found++;
                        }
                        else
                        {
                            LogComm("  Address " + addr + ": no response");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogComm("  Address " + addr + ": error - " + ex.Message);
                        Log("ScanRequest: error at address " + addr + ": " + ex.Message);
                    }
                }

                LogComm("=== Topology scan complete: " + found + " device(s) found ===");
                Log("ScanRequest complete: " + found + " device(s) in range " + _scanStart + "-" + _scanStop);

                // Disconnect if we connected just for the scan
                if (!wasConnected)
                {
                    _manager.Dispose();
                    _manager = new ChannelManager();
                    _connected = false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Log("ScanRequest ERROR: " + ex.Message);
                LogComm("Topology scan error: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Build a HART Command 0 (Read Unique Identifier) short frame request.
        /// Frame: [preambles] [delimiter=0x02] [address] [cmd=0x00] [byteCount=0x00] [checksum]
        /// </summary>
        private byte[] BuildHartCmd0Frame(int pollAddress)
        {
            int preambleLen = _preambleCount;
            byte[] frame = new byte[preambleLen + 5];

            int idx = 0;

            // Preamble bytes (0xFF)
            for (int i = 0; i < preambleLen; i++)
                frame[idx++] = 0xFF;

            // Start delimiter: 0x02 = Master to Slave, Short Frame
            byte delimiter = 0x02;
            frame[idx++] = delimiter;

            // Address byte: bit7 = primary master, bits 5-0 = poll address (0-63)
            byte address = (byte)(pollAddress & 0x3F);
            if (_primaryMaster)
                address |= 0x80;
            frame[idx++] = address;

            // Command 0
            frame[idx++] = 0x00;

            // Byte count 0 (no request data for Command 0)
            frame[idx++] = 0x00;

            // Checksum: XOR from delimiter through byte count
            byte chk = (byte)(delimiter ^ address ^ 0x00 ^ 0x00);
            frame[idx++] = chk;

            return frame;
        }

        public bool ValidateAddChild(string childXml)
        {
            Log("IFdtChannelSubTopology.ValidateAddChild()");
            return true;
        }

        public bool ValidateRemoveChild(string childXml)
        {
            Log("IFdtChannelSubTopology.ValidateRemoveChild()");
            return true;
        }

        public void OnAddChild(string childXml)
        {
            Log("IFdtChannelSubTopology.OnAddChild()");
        }

        public void OnRemoveChild(string childXml)
        {
            Log("IFdtChannelSubTopology.OnRemoveChild()");
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
            Log("ConnectRequest(invokeId=" + invokeId + ", protocolId=" + (protocolId ?? "null") + ")");
            Log("ConnectRequest fieldbusFrame=" + (fieldbusFrame ?? "null"));
            try
            {
                _callback = callBack;
                _commRef++;

                // Open the physical channel — this is where communication starts
                if (!_connected)
                {
                    if (_protocol == "serial")
                    {
                        _manager.ConfigureSerial(_comPort, _baudRate);
                        Log("ConnectRequest -> serial channel opened: " + _comPort + " @ " + _baudRate);
                    }
                    else
                    {
                        _manager.Configure(_protocol, _ipAddress, _ipPort, _timeout);
                        Log("ConnectRequest -> channel opened: " + _protocol + "://" + _ipAddress + ":" + _ipPort);
                    }
                    _connected = true;
                }
                else
                {
                    Log("ConnectRequest -> already connected, reusing channel");
                }

                // Resposta no formato CWHart: HartConnectResp.xml
                // <FDT xmlns="x-schema:FDTHARTCommunicationSchema.xml" ...>
                //   <ConnectResponse fdt:tag="..." preambleCount="..." ...>
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
                    "<?xml version=\"1.0\"?>" +
                    "<FDT xmlns=\"x-schema:FDTHARTCommunicationSchema.xml\"" +
                    " xmlns:fdt=\"x-schema:FDTDataTypesSchema.xml\">" +
                    "<ConnectResponse" +
                    " fdt:tag=\"" + HartXmlHelper.XmlEscape(tag) + "\"" +
                    " preambleCount=\"" + _preambleCount + "\"" +
                    " primaryMaster=\"" + (_primaryMaster ? "1" : "0") + "\"" +
                    " communicationReference=\"" + _commRef + "\">" +
                      "<ShortAddress shortAddress=\"" + _scanStart + "\"/>" +
                    "</ConnectResponse>" +
                    "</FDT>";

                Log("ConnectRequest -> success, commRef=" + _commRef);
                callBack?.OnConnectResponse(invokeId, connectResponse);
                return true;
            }
            catch (Exception ex)
            {
                Log("ConnectRequest ERROR: " + ex.Message);
                // Resposta de erro no formato CWHart: HartErrorResp.xml
                string errorXml =
                    "<?xml version=\"1.0\"?>" +
                    "<FDT xmlns=\"x-schema:FDTHARTCommunicationSchema.xml\"" +
                    " xmlns:fdt=\"x-schema:FDTDataTypesSchema.xml\">" +
                    "<fdt:CommunicationError" +
                    " communicationError=\"connectionFailed\"" +
                    " tag=\"HART\"/>" +
                    "</FDT>";

                callBack?.OnConnectResponse(invokeId, errorXml);
                return false;
            }
        }

        public bool DisconnectRequest(string invokeId, string fieldbusFrame)
        {
            Log("DisconnectRequest(invokeId=" + invokeId + ", connected=" + _connected + ", callback=" + (_callback != null ? "set" : "null") + ")");
            try
            {
                _manager?.Dispose();
                _manager = new ChannelManager();
                _connected = false;

                // CWHart HartDisConnectResp.xml format
                string disconnectResponse =
                    "<?xml version=\"1.0\"?>" +
                    "<FDT xmlns=\"x-schema:FDTHARTCommunicationSchema.xml\"" +
                    " xmlns:fdt=\"x-schema:FDTDataTypesSchema.xml\">" +
                    "<DisconnectResponse" +
                    " communicationReference=\"" + _commRef + "\"/>" +
                    "</FDT>";

                Log("DisconnectRequest -> sending OnDisconnectResponse, commRef=" + _commRef);
                if (_callback != null)
                {
                    _callback.OnDisconnectResponse(invokeId, disconnectResponse);
                    Log("DisconnectRequest -> OnDisconnectResponse sent OK");
                }
                else
                {
                    Log("DisconnectRequest -> WARNING: no callback, cannot send OnDisconnectResponse");
                }
                return true;
            }
            catch (Exception ex)
            {
                Log("DisconnectRequest ERROR: " + ex.ToString());
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
            // Formato idêntico ao CWHart HartProtocols.xml
            string xml =
                "<?xml version=\"1.0\"?>" +
                "<FDT xmlns=\"x-schema:DTMProtocolsSchema.xml\" xmlns:fdt=\"x-schema:FDTDataTypesSchema.xml\">" +
                  "<fdt:BusCategories>" +
                    "<fdt:BusCategory busCategory=\"036D1498-387B-11D4-86E1-00E0987270B9\" busCategoryName=\"HART\">" +
                      "<fdt:CommunicationTypeEntry communicationType=\"supported\"/>" +
                    "</fdt:BusCategory>" +
                  "</fdt:BusCategories>" +
                "</FDT>";
            Log("GetSupportedProtocols() -> " + xml);
            return xml;
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
            // Estrutura CWHart: DtmDeviceType + DtmDevice com ChannelReferences + ExportedVariables
            const string hartBusUuid = "036D1498-387B-11D4-86E1-00E0987270B9";
            string deviceTag = _protocol == "serial"
                ? _comPort.ToUpperInvariant()
                : _protocol.ToUpperInvariant() + " " + _ipAddress + ":" + _ipPort;
            string xml =
                "<?xml version=\"1.0\"?>" +
                "<FDT xmlns=\"x-schema:DTMParameterSchema.xml\" xmlns:fdt=\"x-schema:FDTDataTypesSchema.xml\">" +
                  "<fdt:DtmDeviceType readAccess=\"0\" writeAccess=\"0\">" +
                    "<fdt:VersionInformation name=\"Lasec HART Communication DTM\" vendor=\"JosueLab\" version=\"1.0.0\" date=\"2024-01-01\"/>" +
                    "<fdt:SupportedLanguages>" +
                      "<fdt:LanguageId languageId=\"1033\"/>" +
                    "</fdt:SupportedLanguages>" +
                    "<fdt:BusCategories>" +
                      "<fdt:BusCategory busCategory=\"" + hartBusUuid + "\" busCategoryName=\"HART\">" +
                        "<fdt:CommunicationTypeEntry communicationType=\"supported\"/>" +
                      "</fdt:BusCategory>" +
                    "</fdt:BusCategories>" +
                  "</fdt:DtmDeviceType>" +
                  "<DtmDevice tag=\"" + HartXmlHelper.XmlEscape(deviceTag) + "\">" +
                    "<fdt:ChannelReferences>" +
                      "<fdt:ChannelReference idref=\"HARTCH\"/>" +
                    "</fdt:ChannelReferences>" +
                    "<ExportedVariables>" +
                      "<fdt:DtmVariables name=\"Parameter\" descriptor=\"root of parameters\">" +
                        "<fdt:DtmVariable name=\"protocol\"><fdt:Value><fdt:Variant dataType=\"ascii\"><fdt:StringData string=\"" + HartXmlHelper.XmlEscape(_protocol) + "\"/></fdt:Variant></fdt:Value></fdt:DtmVariable>" +
                        "<fdt:DtmVariable name=\"comPort\"><fdt:Value><fdt:Variant dataType=\"ascii\"><fdt:StringData string=\"" + HartXmlHelper.XmlEscape(_comPort) + "\"/></fdt:Variant></fdt:Value></fdt:DtmVariable>" +
                        "<fdt:DtmVariable name=\"baudRate\"><fdt:Value><fdt:Variant dataType=\"int\"><fdt:NumberData number=\"" + _baudRate + "\"/></fdt:Variant></fdt:Value></fdt:DtmVariable>" +
                        "<fdt:DtmVariable name=\"ipAddress\"><fdt:Value><fdt:Variant dataType=\"ascii\"><fdt:StringData string=\"" + HartXmlHelper.XmlEscape(_ipAddress) + "\"/></fdt:Variant></fdt:Value></fdt:DtmVariable>" +
                        "<fdt:DtmVariable name=\"ipPort\"><fdt:Value><fdt:Variant dataType=\"int\"><fdt:NumberData number=\"" + _ipPort + "\"/></fdt:Variant></fdt:Value></fdt:DtmVariable>" +
                        "<fdt:DtmVariable name=\"PrimaryMaster\"><fdt:Value><fdt:Variant dataType=\"int\"><fdt:NumberData number=\"" + (_primaryMaster ? "1" : "0") + "\"/></fdt:Variant></fdt:Value></fdt:DtmVariable>" +
                        "<fdt:DtmVariable name=\"PreambleCount\"><fdt:Value><fdt:Variant dataType=\"int\"><fdt:NumberData number=\"" + _preambleCount + "\"/></fdt:Variant></fdt:Value></fdt:DtmVariable>" +
                        "<fdt:DtmVariable name=\"RetryCount\"><fdt:Value><fdt:Variant dataType=\"int\"><fdt:NumberData number=\"" + _retryCount + "\"/></fdt:Variant></fdt:Value></fdt:DtmVariable>" +
                        "<fdt:DtmVariable name=\"ScanStart\"><fdt:Value><fdt:Variant dataType=\"int\"><fdt:NumberData number=\"" + _scanStart + "\"/></fdt:Variant></fdt:Value></fdt:DtmVariable>" +
                        "<fdt:DtmVariable name=\"ScanStop\"><fdt:Value><fdt:Variant dataType=\"int\"><fdt:NumberData number=\"" + _scanStop + "\"/></fdt:Variant></fdt:Value></fdt:DtmVariable>" +
                        "<fdt:DtmVariable name=\"BurstMode\"><fdt:Value><fdt:Variant dataType=\"int\"><fdt:NumberData number=\"" + (_burstMode ? "1" : "0") + "\"/></fdt:Variant></fdt:Value></fdt:DtmVariable>" +
                        "<fdt:DtmVariable name=\"timeout\"><fdt:Value><fdt:Variant dataType=\"int\"><fdt:NumberData number=\"" + _timeout + "\"/></fdt:Variant></fdt:Value></fdt:DtmVariable>" +
                      "</fdt:DtmVariables>" +
                    "</ExportedVariables>" +
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

                        if (name == "protocol" && strVal != null) _protocol = strVal;
                        else if (name == "comPort" && strVal != null) _comPort = strVal;
                        else if (name == "baudRate" && numVal != null) int.TryParse(numVal, out _baudRate);
                        else if (name == "ipAddress" && strVal != null) _ipAddress = strVal;
                        else if (name == "ipPort" && numVal != null) int.TryParse(numVal, out _ipPort);
                        else if (name == "primaryMaster" && numVal != null) _primaryMaster = numVal != "0";
                        else if (name == "PrimaryMaster" && numVal != null) _primaryMaster = numVal != "0";
                        else if (name == "preambleCount" && numVal != null) int.TryParse(numVal, out _preambleCount);
                        else if (name == "PreambleCount" && numVal != null) int.TryParse(numVal, out _preambleCount);
                        else if (name == "retryCount" && numVal != null) int.TryParse(numVal, out _retryCount);
                        else if (name == "RetryCount" && numVal != null) int.TryParse(numVal, out _retryCount);
                        else if (name == "scanStart" && numVal != null) int.TryParse(numVal, out _scanStart);
                        else if (name == "ScanStart" && numVal != null) int.TryParse(numVal, out _scanStart);
                        else if (name == "scanStop" && numVal != null) int.TryParse(numVal, out _scanStop);
                        else if (name == "ScanStop" && numVal != null) int.TryParse(numVal, out _scanStop);
                        else if (name == "burstMode" && numVal != null) _burstMode = numVal != "0";
                        else if (name == "BurstMode" && numVal != null) _burstMode = numVal != "0";
                        else if (name == "timeout" && numVal != null) int.TryParse(numVal, out _timeout);
                    }
                }
                Log("IDtmParameter.SetParameters() applied: protocol=" + _protocol +
                    (_protocol == "serial" ? " port=" + _comPort + " baud=" + _baudRate
                                          : " " + _protocol + "://" + _ipAddress + ":" + _ipPort));
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
                " protocol=\"" + HartXmlHelper.XmlEscape(_protocol) + "\"" +
                " comPort=\"" + HartXmlHelper.XmlEscape(_comPort) + "\"" +
                " baudRate=\"" + _baudRate + "\"" +
                " ipAddress=\"" + HartXmlHelper.XmlEscape(_ipAddress) + "\"" +
                " ipPort=\"" + _ipPort + "\"" +
                " primaryMaster=\"" + (_primaryMaster ? "1" : "0") + "\"" +
                " preambleCount=\"" + _preambleCount + "\"" +
                " retryCount=\"" + _retryCount + "\"" +
                " scanStart=\"" + _scanStart + "\"" +
                " scanStop=\"" + _scanStop + "\"" +
                " burstMode=\"" + (_burstMode ? "1" : "0") + "\"" +
                " timeout=\"" + _timeout + "\"/>";
        }

        // Chamado pela UI (DtmView) para aplicar configuração
        internal void ApplyConfiguration(string protocol, string comPort, int baudRate,
            string ipAddress, int ipPort,
            bool primaryMaster, int preambleCount, int retryCount,
            int scanStart, int scanStop, bool burstMode, int timeout)
        {
            _protocol      = (protocol ?? "serial").ToLowerInvariant();
            _comPort       = string.IsNullOrEmpty(comPort) ? "COM1" : comPort.Trim().ToUpperInvariant();
            _baudRate      = baudRate <= 0 ? 1200 : baudRate;
            _ipAddress     = string.IsNullOrEmpty(ipAddress) ? "127.0.0.1" : ipAddress.Trim();
            _ipPort        = ipPort <= 0 ? 5094 : ipPort;
            _primaryMaster = primaryMaster;
            _preambleCount = Math.Max(5, Math.Min(20, preambleCount));
            _retryCount    = Math.Max(1, Math.Min(10, retryCount));
            _scanStart     = Math.Max(0, Math.Min(63, scanStart));
            _scanStop      = Math.Max(0, Math.Min(63, scanStop));
            _burstMode     = burstMode;
            _timeout       = timeout <= 0 ? 5000 : timeout;

            Log("ApplyConfiguration: protocol=" + _protocol +
                (_protocol == "serial" ? " port=" + _comPort + " baud=" + _baudRate
                                       : " " + _ipAddress + ":" + _ipPort) +
                " master=" + _primaryMaster + " preamble=" + _preambleCount +
                " retry=" + _retryCount + " scan=" + _scanStart + "-" + _scanStop +
                " burst=" + _burstMode + " timeout=" + _timeout);

            // Notifica PACTware que os parâmetros mudaram (como CWHart faz)
            // SaveRequest faz PACTware chamar GetParameters + SetCommunication
            try
            {
                if (_fdtContainer != null)
                {
                    Log("ApplyConfiguration: calling SaveRequest on container");
                    _fdtContainer.SaveRequest(_systemTag ?? "");
                }
            }
            catch (Exception ex)
            {
                Log("ApplyConfiguration SaveRequest error: " + ex.Message);
            }
        }

        // ----------------------------------------------------------------
        // IFdtEvents — Container→DTM notifications (CWHart implements this)
        // ----------------------------------------------------------------
        void IFdtEvents.OnChildParameterChanged(string systemTag)
        {
            Log("IFdtEvents.OnChildParameterChanged(tag=" + (systemTag ?? "null") + ")");
        }

        void IFdtEvents.OnParameterChanged(string systemTag, string parameter)
        {
            Log("IFdtEvents.OnParameterChanged(tag=" + (systemTag ?? "null") + ", param=" + (parameter ?? "null") + ")");
        }

        void IFdtEvents.OnLockDataSet(string systemTag, string userName)
        {
            Log("IFdtEvents.OnLockDataSet(tag=" + (systemTag ?? "null") + ", user=" + (userName ?? "null") + ")");
        }

        bool IFdtEvents.OnUnlockDataSet(string systemTag, string userName)
        {
            Log("IFdtEvents.OnUnlockDataSet(tag=" + (systemTag ?? "null") + ", user=" + (userName ?? "null") + ")");
            return true;
        }

        // ----------------------------------------------------------------
        // IDtmDocumentation — help/documentation (CWHart implements this)
        // ----------------------------------------------------------------
        string IDtmDocumentation.GetDocumentation(string functionCall)
        {
            Log("IDtmDocumentation.GetDocumentation(func=" + (functionCall ?? "null") + ")");
            return "";
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
        // ----------------------------------------------------------------
        private static readonly string ConfigControlGuid =
            typeof(ConfigControl).GUID.ToString("B");
        private const string ConfigControlProgId = "LasecHartCommDTM.ConfigControl";

        public string QueryActiveXGuid(string functionCall)
        {
            Log("IDtmActiveXInformation.QueryActiveXGuid(functionCall=" +
                (functionCall ?? "null") + ") -> " + ConfigControlGuid);
            return ConfigControlGuid;
        }

        public string QueryActiveXProgId(string functionCall)
        {
            Log("IDtmActiveXInformation.QueryActiveXProgId(functionCall=" +
                (functionCall ?? "null") + ") -> " + ConfigControlProgId);
            return ConfigControlProgId;
        }

        // ----------------------------------------------------------------
        // IFdtChannel — CommDtm itself acts as a channel (like CWHart clsDTM)
        // PACTware queries IFdtChannel on the DTM object directly.
        // ----------------------------------------------------------------
        string IFdtChannel.GetChannelPath()
        {
            Log("CommDtm.IFdtChannel.GetChannelPath() -> HARTCH");
            return "HARTCH";
        }

        string IFdtChannel.GetChannelParameters(string parameterPath, string protocolId)
        {
            Log("CommDtm.IFdtChannel.GetChannelParameters(path=" + parameterPath + ", proto=" + protocolId + ")");
            return GetParameterXml();
        }

        bool IFdtChannel.SetChannelParameters(string parameterPath, string protocolId, string XmlDocument)
        {
            Log("CommDtm.IFdtChannel.SetChannelParameters()");
            return true;
        }

        // ----------------------------------------------------------------
        // IDtmChannel — returns available communication channels
        // ----------------------------------------------------------------
        public IFdtChannelCollection GetChannels()
        {
            Log("IDtmChannel.GetChannels()");
            return new HartChannelCollection(this);
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
                        // ThreadingModel — CWHart uses "Apartment" (STA).
                        // .NET COM defaults to "Both", but PACTware (STA host) may need Apartment
                        // to avoid cross-apartment marshaling issues.
                        using (var ipsKey = clsidKey.OpenSubKey("InprocServer32", true))
                        {
                            if (ipsKey != null)
                                ipsKey.SetValue("ThreadingModel", "Apartment");
                        }

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

                // Register ConfigControl as ActiveX control
                string ctrlKeyPath = @"CLSID\{B4B6B3E7-639D-460B-B9A0-6C7F7EB20030}";
                using (var ctrlKey = Registry.ClassesRoot.OpenSubKey(ctrlKeyPath, true))
                {
                    if (ctrlKey != null)
                    {
                        ctrlKey.CreateSubKey("Control");
                        using (var ms = ctrlKey.CreateSubKey("MiscStatus"))
                            ms?.SetValue(null, "0");
                        using (var ms1 = ctrlKey.CreateSubKey(@"MiscStatus\1"))
                            ms1?.SetValue(null, "131473");
                        using (var tlb = ctrlKey.CreateSubKey("TypeLib"))
                            tlb?.SetValue(null, "{40498B38-0A79-3F68-905F-7954AA76AEA6}");
                        using (var ver = ctrlKey.CreateSubKey("VERSION"))
                            ver?.SetValue(null, "1.0");
                    }
                    else
                    {
                        // ConfigControl CLSID not found yet, try creating subkeys directly
                        using (var ctrlKey2 = Registry.ClassesRoot.CreateSubKey(ctrlKeyPath + @"\Control")) { }
                        using (var ms = Registry.ClassesRoot.CreateSubKey(ctrlKeyPath + @"\MiscStatus"))
                            ms?.SetValue(null, "0");
                        using (var ms1 = Registry.ClassesRoot.CreateSubKey(ctrlKeyPath + @"\MiscStatus\1"))
                            ms1?.SetValue(null, "131473");
                        using (var tlb = Registry.ClassesRoot.CreateSubKey(ctrlKeyPath + @"\TypeLib"))
                            tlb?.SetValue(null, "{40498B38-0A79-3F68-905F-7954AA76AEA6}");
                        using (var ver = Registry.ClassesRoot.CreateSubKey(ctrlKeyPath + @"\VERSION"))
                            ver?.SetValue(null, "1.0");
                    }
                }
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

    // ----------------------------------------------------------------
    // HartChannel — single HART communication channel (IP-based)
    // Implements IFdtChannel + IFdtChannelSubTopology so PACTware can
    // discover topology scan capability on the channel object itself.
    // ----------------------------------------------------------------
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public class HartChannel : IFdtChannel, IFdtChannelSubTopology, ICustomQueryInterface
    {
        private readonly CommDtm _dtm;

        private static readonly Guid IID_IFdtChannel = new Guid("036D1488-387B-11D4-86E1-00E0987270B9");
        private static readonly Guid IID_IFdtChannelSubTopo = new Guid("036D1484-387B-11D4-86E1-00E0987270B9");
        private static readonly Guid IID_IDispatch = new Guid("00020400-0000-0000-C000-000000000046");

        [ThreadStatic] private static bool _inGetInterface;

        internal HartChannel(CommDtm dtm)
        {
            _dtm = dtm;
        }

        public CustomQueryInterfaceResult GetInterface(ref Guid iid, out IntPtr ppv)
        {
            ppv = IntPtr.Zero;
            if (_inGetInterface) return CustomQueryInterfaceResult.NotHandled;

            string name = "unknown";
            if (iid == IID_IFdtChannel) name = "IFdtChannel";
            else if (iid == IID_IFdtChannelSubTopo) name = "IFdtChannelSubTopology";

            CommDtm.Log("HartChannel.QI for " + iid.ToString("B") + " (" + name + ")");

            _inGetInterface = true;
            try
            {
                if (iid == IID_IFdtChannel)
                {
                    ppv = Marshal.GetComInterfaceForObject(this, typeof(IFdtChannel));
                    CommDtm.Log("HartChannel.QI -> HANDLED IFdtChannel");
                    return CustomQueryInterfaceResult.Handled;
                }
                if (iid == IID_IFdtChannelSubTopo)
                {
                    ppv = Marshal.GetComInterfaceForObject(this, typeof(IFdtChannelSubTopology));
                    CommDtm.Log("HartChannel.QI -> HANDLED IFdtChannelSubTopology");
                    return CustomQueryInterfaceResult.Handled;
                }
            }
            catch (Exception ex)
            {
                CommDtm.Log("HartChannel.QI ERROR: " + ex.Message);
            }
            finally
            {
                _inGetInterface = false;
            }

            return CustomQueryInterfaceResult.NotHandled;
        }

        public string GetChannelPath()
        {
            CommDtm.Log("HartChannel.GetChannelPath() -> HARTCH");
            return "HARTCH";
        }

        public string GetChannelParameters(string parameterPath, string protocolId)
        {
            CommDtm.Log("HartChannel.GetChannelParameters(path=" + parameterPath + ")");
            return _dtm.GetParameterXml();
        }

        public bool SetChannelParameters(string parameterPath, string protocolId, string XmlDocument)
        {
            CommDtm.Log("HartChannel.SetChannelParameters()");
            return true;
        }

        // IFdtChannelSubTopology — delegate to CommDtm
        public bool ScanRequest(string invokeId)
        {
            CommDtm.Log("HartChannel.ScanRequest(invokeId=" + invokeId + ")");
            return _dtm.ScanRequest(invokeId);
        }

        public bool ValidateAddChild(string childsystemTag)
        {
            CommDtm.Log("HartChannel.ValidateAddChild()");
            return true;
        }

        public bool ValidateRemoveChild(string childsystemTag)
        {
            CommDtm.Log("HartChannel.ValidateRemoveChild()");
            return true;
        }

        public void OnAddChild(string childsystemTag)
        {
            CommDtm.Log("HartChannel.OnAddChild()");
        }

        public void OnRemoveChild(string childsystemTag)
        {
            CommDtm.Log("HartChannel.OnRemoveChild()");
        }
    }

    // ----------------------------------------------------------------
    // HartChannelCollection — returns the CommDtm itself as the channel
    // CWHart architecture: the DTM IS the channel object, so PACTware
    // finds IFdtChannel + IFdtCommunication + IFdtChannelSubTopology
    // all on the same COM identity.
    // ----------------------------------------------------------------
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public class HartChannelCollection : IFdtChannelCollection, IEnumerable, ICustomQueryInterface
    {
        private readonly CommDtm _dtm;

        private static readonly Guid IID_IFdtChannelCollection = new Guid("E4F31A10-45BF-11D4-BBB3-0060080993FF");

        [ThreadStatic] private static bool _inGetInterface;

        internal HartChannelCollection(CommDtm dtm)
        {
            _dtm = dtm;
        }

        public CustomQueryInterfaceResult GetInterface(ref Guid iid, out IntPtr ppv)
        {
            ppv = IntPtr.Zero;
            if (_inGetInterface) return CustomQueryInterfaceResult.NotHandled;

            CommDtm.Log("HartChannelCollection.QI for " + iid.ToString("B"));

            _inGetInterface = true;
            try
            {
                if (iid == IID_IFdtChannelCollection)
                {
                    ppv = Marshal.GetComInterfaceForObject(this, typeof(IFdtChannelCollection));
                    CommDtm.Log("HartChannelCollection.QI -> HANDLED IFdtChannelCollection");
                    return CustomQueryInterfaceResult.Handled;
                }
            }
            catch (Exception ex)
            {
                CommDtm.Log("HartChannelCollection.QI ERROR: " + ex.Message);
            }
            finally
            {
                _inGetInterface = false;
            }

            return CustomQueryInterfaceResult.NotHandled;
        }

        public IFdtChannel get_Item(ref object pvarIndex)
        {
            CommDtm.Log("HartChannelCollection.get_Item -> returning CommDtm as IFdtChannel");
            return _dtm;
        }

        public int Count
        {
            get
            {
                CommDtm.Log("HartChannelCollection.Count -> 1");
                return 1;
            }
        }

        public IEnumerator GetEnumerator()
        {
            return new IFdtChannel[] { _dtm }.GetEnumerator();
        }
    }

    // ----------------------------------------------------------------
    // DtmEventsConnectionPoint — explicit IConnectionPoint for IDtmEventsSource
    // Receives COM sinks from PACTware via Advise(), fires events via IDispatch.
    // ----------------------------------------------------------------
    internal class DtmEventsConnectionPoint : IConnectionPoint
    {
        private static readonly Guid IID_IDtmEventsSource = new Guid("F15BA42E-BBF1-42ED-8009-7F664A002CFB");
        private readonly CommDtm _owner;
        private readonly Dictionary<int, object> _sinks = new Dictionary<int, object>();
        private int _nextCookie = 1;

        internal int SinkCount { get { lock (_sinks) { return _sinks.Count; } } }

        internal DtmEventsConnectionPoint(CommDtm owner) { _owner = owner; }

        public void GetConnectionInterface(out Guid pIID)
        {
            pIID = IID_IDtmEventsSource;
            CommDtm.Log("IConnectionPoint.GetConnectionInterface() -> " + pIID.ToString("B"));
        }

        public void GetConnectionPointContainer(out IConnectionPointContainer ppCPC)
        {
            ppCPC = _owner;
            CommDtm.Log("IConnectionPoint.GetConnectionPointContainer()");
        }

        public void Advise(object pUnkSink, out int pdwCookie)
        {
            lock (_sinks)
            {
                pdwCookie = _nextCookie++;
                _sinks[pdwCookie] = pUnkSink;
            }
            CommDtm.Log("IConnectionPoint.Advise(sink=" + (pUnkSink?.GetType().FullName ?? "null") + ") -> cookie=" + pdwCookie + " totalSinks=" + SinkCount);

            // Probe sink capabilities
            try
            {
                IntPtr pUnk = Marshal.GetIUnknownForObject(pUnkSink);
                try
                {
                    Guid iidDisp = new Guid("00020400-0000-0000-C000-000000000046");
                    IntPtr pDisp;
                    int hr = Marshal.QueryInterface(pUnk, ref iidDisp, out pDisp);
                    CommDtm.Log("  Sink supports IDispatch? " + (hr == 0 ? "YES" : "NO (hr=0x" + hr.ToString("X8") + ")"));
                    if (hr == 0) Marshal.Release(pDisp);

                    Guid iidEvents = IID_IDtmEventsSource;
                    IntPtr pEv;
                    hr = Marshal.QueryInterface(pUnk, ref iidEvents, out pEv);
                    CommDtm.Log("  Sink supports IDtmEventsSource? " + (hr == 0 ? "YES" : "NO (hr=0x" + hr.ToString("X8") + ")"));
                    if (hr == 0) Marshal.Release(pEv);
                }
                finally { Marshal.Release(pUnk); }
            }
            catch (Exception ex)
            {
                CommDtm.Log("  Sink probe error: " + ex.Message);
            }
        }

        public void Unadvise(int dwCookie)
        {
            CommDtm.Log("IConnectionPoint.Unadvise(cookie=" + dwCookie + ")");
            lock (_sinks) { _sinks.Remove(dwCookie); }
            CommDtm.Log("  Remaining sinks: " + SinkCount);
        }

        public void EnumConnections(out IEnumConnections ppEnum)
        {
            CommDtm.Log("IConnectionPoint.EnumConnections()");
            ppEnum = null;
        }

        internal void FireEvent(int dispId, string name, params object[] args)
        {
            Dictionary<int, object> snapshot;
            lock (_sinks)
            {
                if (_sinks.Count == 0) return;
                snapshot = new Dictionary<int, object>(_sinks);
            }
            CommDtm.Log("CP.FireEvent(dispId=" + dispId + ", name=" + name + ", sinks=" + snapshot.Count + ")");
            foreach (var kv in snapshot)
            {
                try
                {
                    kv.Value.GetType().InvokeMember(
                        name,
                        BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance,
                        null, kv.Value, args);
                    CommDtm.Log("  -> sink " + kv.Key + " invoked OK");
                }
                catch (Exception ex)
                {
                    var inner = ex.InnerException ?? ex;
                    CommDtm.Log("  -> sink " + kv.Key + " invoke FAILED: " + inner.GetType().Name + ": " + inner.Message);
                }
            }
        }
    }
}
