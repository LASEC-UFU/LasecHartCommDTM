using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace CWHartSpy
{
    // IClassFactory — create real CWHart from the OCX
    [ComImport, Guid("00000001-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IClassFactory
    {
        [PreserveSig]
        int CreateInstance(
            [MarshalAs(UnmanagedType.IUnknown)] object pUnkOuter,
            ref Guid riid,
            [MarshalAs(UnmanagedType.IUnknown)] out object ppvObject);
        [PreserveSig]
        int LockServer([MarshalAs(UnmanagedType.Bool)] bool fLock);
    }

    /// <summary>
    /// Full-interception COM proxy for CWHart.
    /// Implements ALL FDT interfaces, logs every method call with params/returns,
    /// then forwards to the real CWHart VB6 object loaded from OCX.
    /// Also wraps PACTware's IFdtContainer and IFdtCommunicationEvents callbacks
    /// to capture CWHart→PACTware calls.
    /// </summary>
    [Guid("6358CCBF-AA0E-4633-9564-9EEFB7FDE86D")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    [ProgId("CWHartFdt.clsDTM")]
    public class CWHartProxy :
        IDtm, IDtmInformation, IDtmParameter, IDtmChannel,
        IFdtChannel, IFdtCommunication, IFdtChannelSubTopology,
        IDtmActiveXInformation, IFdtEvents, IDtmDocumentation,
        IPersistStreamInit,
        ICustomQueryInterface, IConnectionPointContainer, IDisposable
    {
        private object _real;           // Real CWHart COM object (System.__ComObject)
        private IntPtr _hModule;        // OCX module handle

        // Typed RCW handles to the real CWHart — each obtained via cast from _real
        private IDtm _rDtm;
        private IDtmInformation _rInfo;
        private IDtmParameter _rParam;
        private IDtmChannel _rChan;
        private IFdtChannel _rFdtChan;
        private IFdtCommunication _rComm;
        private IFdtChannelSubTopology _rSubTopo;
        private IDtmActiveXInformation _rAxInfo;
        private IFdtEvents _rEvents;
        private IDtmDocumentation _rDoc;
        private IPersistStreamInit _rPSI;

        // Prevent GC of spy wrappers passed to CWHart/PACTware
        private readonly List<object> _prevent = new List<object>();

        private static readonly string OcxPath = @"C:\Windows\SysWow64\CWHARTFDT.ocx";
        internal static readonly string LogPath = @"C:\Temp\CWHartSpy.log";

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DllGetClassObjectDelegate(ref Guid clsid, ref Guid riid, out IntPtr ppv);

        // ================================================================
        // Well-known GUIDs
        // ================================================================
        private static readonly Guid IID_IDtm                   = new Guid("036D1481-387B-11D4-86E1-00E0987270B9");
        private static readonly Guid IID_IDtmInformation        = new Guid("036D147F-387B-11D4-86E1-00E0987270B9");
        private static readonly Guid IID_IDtmParameter          = new Guid("036D147D-387B-11D4-86E1-00E0987270B9");
        private static readonly Guid IID_IDtmChannel            = new Guid("036D1489-387B-11D4-86E1-00E0987270B9");
        private static readonly Guid IID_IFdtChannel            = new Guid("036D1488-387B-11D4-86E1-00E0987270B9");
        private static readonly Guid IID_IFdtCommunication      = new Guid("039ECFC4-9CA8-44E6-944D-B37F288A34D8");
        private static readonly Guid IID_IFdtChannelSubTopology = new Guid("036D1484-387B-11D4-86E1-00E0987270B9");
        private static readonly Guid IID_IDtmActiveXInformation = new Guid("036D1480-387B-11D4-86E1-00E0987270B9");
        private static readonly Guid IID_IFdtEvents             = new Guid("036D1478-387B-11D4-86E1-00E0987270B9");
        private static readonly Guid IID_IDtmDocumentation      = new Guid("036D147C-387B-11D4-86E1-00E0987270B9");
        private static readonly Guid IID_IPersistStreamInit     = new Guid("7FD52380-4E07-101B-AE2D-08002B2EC713");
        private static readonly Guid IID_IConnPtContainer       = new Guid("B196B284-BAB4-101A-B69C-00AA00341D07");
        private static readonly Guid IID_IUnknown               = new Guid("00000000-0000-0000-C000-000000000046");

        // ================================================================
        // Constructor — load real CWHart from OCX
        // ================================================================
        public CWHartProxy()
        {
            Log("============================================================");
            Log("CWHartProxy() — FULL INTERCEPTION SPY — loading real CWHart");
            try
            {
                _hModule = LoadLibrary(OcxPath);
                if (_hModule == IntPtr.Zero)
                    throw new COMException("LoadLibrary failed for " + OcxPath, Marshal.GetLastWin32Error());
                Log("Loaded OCX at 0x" + _hModule.ToString("X"));

                IntPtr pDGCO = GetProcAddress(_hModule, "DllGetClassObject");
                if (pDGCO == IntPtr.Zero)
                    throw new COMException("DllGetClassObject not found");

                var dgco = (DllGetClassObjectDelegate)
                    Marshal.GetDelegateForFunctionPointer(pDGCO, typeof(DllGetClassObjectDelegate));

                Guid clsid = new Guid("6358CCBF-AA0E-4633-9564-9EEFB7FDE86D");
                Guid iidFact = new Guid("00000001-0000-0000-C000-000000000046");
                IntPtr pFactory;
                int hr = dgco(ref clsid, ref iidFact, out pFactory);
                if (hr != 0) throw new COMException("DllGetClassObject hr=0x" + hr.ToString("X8"), hr);

                var factory = (IClassFactory)Marshal.GetObjectForIUnknown(pFactory);
                Marshal.Release(pFactory);

                Guid iidUnk = IID_IUnknown;
                hr = factory.CreateInstance(null, ref iidUnk, out _real);
                if (hr != 0 || _real == null)
                    throw new COMException("CreateInstance hr=0x" + hr.ToString("X8"), hr);
                Log("Real CWHart created OK: " + _real.GetType().FullName);

                InitTypedRCWs();
            }
            catch (Exception ex)
            {
                Log("CONSTRUCTOR ERROR: " + ex);
                throw;
            }
        }

        private void InitTypedRCWs()
        {
            TryCast(ref _rDtm, "IDtm");
            TryCast(ref _rInfo, "IDtmInformation");
            TryCast(ref _rParam, "IDtmParameter");
            TryCast(ref _rChan, "IDtmChannel");
            TryCast(ref _rFdtChan, "IFdtChannel");
            TryCast(ref _rComm, "IFdtCommunication");
            TryCast(ref _rSubTopo, "IFdtChannelSubTopology");
            TryCast(ref _rAxInfo, "IDtmActiveXInformation");
            TryCast(ref _rEvents, "IFdtEvents");
            TryCast(ref _rDoc, "IDtmDocumentation");
            TryCast(ref _rPSI, "IPersistStreamInit");
            Log("Typed RCW init complete");
        }

        private void TryCast<T>(ref T field, string name) where T : class
        {
            try { field = (T)_real; Log("  Cast " + name + " OK"); }
            catch { Log("  Cast " + name + " FAILED"); }
        }

        // ================================================================
        // ICustomQueryInterface — route QIs
        // IMPORTANT: Do NOT call Marshal.GetComInterfaceForObject(this, ...)
        // inside GetInterface — it triggers recursive GetInterface calls!
        // For interfaces we implement, return NotHandled so the CLR's
        // standard CCW mechanism exposes them. For everything else,
        // forward to real CWHart.
        // ================================================================
        public CustomQueryInterfaceResult GetInterface(ref Guid iid, out IntPtr ppv)
        {
            ppv = IntPtr.Zero;
            string name = IdentifyInterface(iid);
            Log("QI " + name + " " + iid.ToString("B"));

            // Interfaces we implement on this class → NotHandled = CLR creates CCW vtable
            if (GetProxiedType(iid) != null || iid == IID_IConnPtContainer)
            {
                Log("  -> PROXIED (CCW NotHandled)");
                return CustomQueryInterfaceResult.NotHandled;
            }

            // Everything else — forward raw pointer from real CWHart
            IntPtr pUnk = Marshal.GetIUnknownForObject(_real);
            try
            {
                Guid g = iid;
                IntPtr pItf;
                int hr = Marshal.QueryInterface(pUnk, ref g, out pItf);
                if (hr == 0)
                {
                    ppv = pItf;
                    Log("  -> FORWARDED to real CWHart");
                    return CustomQueryInterfaceResult.Handled;
                }
                Log("  -> E_NOINTERFACE");
                return CustomQueryInterfaceResult.Failed;
            }
            finally { Marshal.Release(pUnk); }
        }

        private static Type GetProxiedType(Guid iid)
        {
            if (iid == IID_IDtm) return typeof(IDtm);
            if (iid == IID_IDtmInformation) return typeof(IDtmInformation);
            if (iid == IID_IDtmParameter) return typeof(IDtmParameter);
            if (iid == IID_IDtmChannel) return typeof(IDtmChannel);
            if (iid == IID_IFdtChannel) return typeof(IFdtChannel);
            if (iid == IID_IFdtCommunication) return typeof(IFdtCommunication);
            if (iid == IID_IFdtChannelSubTopology) return typeof(IFdtChannelSubTopology);
            if (iid == IID_IDtmActiveXInformation) return typeof(IDtmActiveXInformation);
            if (iid == IID_IFdtEvents) return typeof(IFdtEvents);
            if (iid == IID_IDtmDocumentation) return typeof(IDtmDocumentation);
            if (iid == IID_IPersistStreamInit) return typeof(IPersistStreamInit);
            return null;
        }

        // ================================================================
        // IDtm — the most critical interface
        // ================================================================
        bool IDtm.Environment(string systemTag, IFdtContainer container)
        {
            Log(">> IDtm.Environment(tag=" + Q(systemTag) + ", container=" + (container == null ? "null" : "obj") + ")");
            try
            {
                // Pass PACTware's real container directly to CWHart (no wrapping)
                // ContainerSpy was causing VB6 to hang — COM marshaling issue
                // We'll log container callbacks via a separate mechanism later
                bool r = _rDtm.Environment(systemTag, container);
                Log("<< IDtm.Environment -> " + r);
                return r;
            }
            catch (Exception ex)
            {
                Log("<< IDtm.Environment EXCEPTION: " + ex);
                throw;
            }
        }

        bool IDtm.InitNew(string deviceType)
        {
            Log(">> IDtm.InitNew(type=" + Q(deviceType) + ")");
            bool r = _rDtm.InitNew(deviceType);
            Log("<< IDtm.InitNew -> " + r);
            return r;
        }

        bool IDtm.Config(string userInfo)
        {
            Log(">> IDtm.Config(info=" + Trunc(userInfo, 200) + ")");
            bool r = _rDtm.Config(userInfo);
            Log("<< IDtm.Config -> " + r);
            return r;
        }

        bool IDtm.SetCommunication(object communication)
        {
            string commInfo = "NULL";
            if (communication != null)
            {
                commInfo = communication.GetType().FullName;
                // Try to identify what interfaces the communication object supports
                try
                {
                    IntPtr pUnk = Marshal.GetIUnknownForObject(communication);
                    Guid gComm = new Guid("039ECFC4-9CA8-44E6-944D-B37F288A34D8"); // IFdtCommunication
                    IntPtr pItf;
                    int hr = Marshal.QueryInterface(pUnk, ref gComm, out pItf);
                    if (hr == 0) { commInfo += " [IFdtCommunication]"; Marshal.Release(pItf); }
                    Marshal.Release(pUnk);
                }
                catch { }
            }
            Log(">> IDtm.SetCommunication(comm=" + commInfo + ") <<<< KEY");
            bool r = _rDtm.SetCommunication(communication);
            Log("<< IDtm.SetCommunication -> " + r);
            return r;
        }

        bool IDtm.PrepareToRelease()
        {
            Log(">> IDtm.PrepareToRelease()");
            bool r = _rDtm.PrepareToRelease();
            Log("<< IDtm.PrepareToRelease -> " + r);
            return r;
        }

        bool IDtm.PrepareToReleaseCommunication()
        {
            Log(">> IDtm.PrepareToReleaseCommunication()");
            bool r = _rDtm.PrepareToReleaseCommunication();
            Log("<< IDtm.PrepareToReleaseCommunication -> " + r);
            return r;
        }

        bool IDtm.ReleaseCommunication()
        {
            Log(">> IDtm.ReleaseCommunication()");
            bool r = _rDtm.ReleaseCommunication();
            Log("<< IDtm.ReleaseCommunication -> " + r);
            return r;
        }

        bool IDtm.PrepareToDelete()
        {
            Log(">> IDtm.PrepareToDelete()");
            bool r = _rDtm.PrepareToDelete();
            Log("<< IDtm.PrepareToDelete -> " + r);
            return r;
        }

        bool IDtm.SetLanguage(int languageId)
        {
            Log(">> IDtm.SetLanguage(" + languageId + ")");
            bool r = _rDtm.SetLanguage(languageId);
            Log("<< IDtm.SetLanguage -> " + r);
            return r;
        }

        string IDtm.GetFunctions(string operationState)
        {
            Log(">> IDtm.GetFunctions(state=" + Q(operationState) + ")");
            string r = _rDtm.GetFunctions(operationState);
            Log("<< IDtm.GetFunctions -> " + (r != null ? r.Length + " chars" : "null"));
            if (r != null) Log("   XML: " + Trunc(r, 2000));
            return r;
        }

        bool IDtm.InvokeFunctionRequest(string invokeId, string functionCall)
        {
            Log(">> IDtm.InvokeFunctionRequest(id=" + Q(invokeId) + ", func=" + Trunc(functionCall, 500) + ")");
            bool r = _rDtm.InvokeFunctionRequest(invokeId, functionCall);
            Log("<< IDtm.InvokeFunctionRequest -> " + r);
            return r;
        }

        bool IDtm.PrivateDialogEnabled(bool enabled)
        {
            Log(">> IDtm.PrivateDialogEnabled(" + enabled + ")");
            bool r = _rDtm.PrivateDialogEnabled(enabled);
            Log("<< IDtm.PrivateDialogEnabled -> " + r);
            return r;
        }

        // ================================================================
        // IDtmInformation
        // ================================================================
        string IDtmInformation.GetInformation()
        {
            Log(">> IDtmInformation.GetInformation()");
            string r = _rInfo.GetInformation();
            Log("<< IDtmInformation.GetInformation -> " + (r != null ? r.Length + " chars" : "null"));
            if (r != null) Log("   XML: " + Trunc(r, 2000));
            return r;
        }

        // ================================================================
        // IDtmParameter
        // ================================================================
        string IDtmParameter.GetParameters(string parameterPath)
        {
            Log(">> IDtmParameter.GetParameters(path=" + Q(parameterPath) + ")");
            string r = _rParam.GetParameters(parameterPath);
            Log("<< IDtmParameter.GetParameters -> " + (r != null ? r.Length + " chars" : "null"));
            if (r != null) Log("   XML: " + Trunc(r, 2000));
            return r;
        }

        bool IDtmParameter.SetParameters(string parameterPath, string fdtXmlDocument)
        {
            Log(">> IDtmParameter.SetParameters(path=" + Q(parameterPath) + ", xml=" + Trunc(fdtXmlDocument, 500) + ")");
            bool r = _rParam.SetParameters(parameterPath, fdtXmlDocument);
            Log("<< IDtmParameter.SetParameters -> " + r);
            return r;
        }

        // ================================================================
        // IDtmChannel
        // ================================================================
        object IDtmChannel.GetChannels()
        {
            Log(">> IDtmChannel.GetChannels()");
            object r = _rChan.GetChannels();
            string desc = "null";
            if (r != null)
            {
                desc = r.GetType().FullName;
                // Try to get collection count
                try
                {
                    var coll = (IFdtChannelCollection)r;
                    desc += " Count=" + coll.Count;
                }
                catch { }
            }
            Log("<< IDtmChannel.GetChannels -> " + desc);
            return r;
        }

        // ================================================================
        // IFdtChannel
        // ================================================================
        string IFdtChannel.GetChannelPath()
        {
            Log(">> IFdtChannel.GetChannelPath()");
            string r = _rFdtChan.GetChannelPath();
            Log("<< IFdtChannel.GetChannelPath -> " + Q(r));
            return r;
        }

        string IFdtChannel.GetChannelParameters(string parameterPath, string protocolId)
        {
            Log(">> IFdtChannel.GetChannelParameters(path=" + Q(parameterPath) + ", proto=" + Q(protocolId) + ")");
            string r = _rFdtChan.GetChannelParameters(parameterPath, protocolId);
            Log("<< IFdtChannel.GetChannelParameters -> " + (r != null ? r.Length + " chars" : "null"));
            if (r != null) Log("   XML: " + Trunc(r, 2000));
            return r;
        }

        bool IFdtChannel.SetChannelParameters(string parameterPath, string protocolId, string xmlDocument)
        {
            Log(">> IFdtChannel.SetChannelParameters(path=" + Q(parameterPath) + ", proto=" + Q(protocolId) + ", xml=" + Trunc(xmlDocument, 500) + ")");
            bool r = _rFdtChan.SetChannelParameters(parameterPath, protocolId, xmlDocument);
            Log("<< IFdtChannel.SetChannelParameters -> " + r);
            return r;
        }

        // ================================================================
        // IFdtCommunication
        // ================================================================
        void IFdtCommunication.Abort(string fieldbusFrame)
        {
            Log(">> IFdtCommunication.Abort(frame=" + Trunc(fieldbusFrame, 200) + ")");
            _rComm.Abort(fieldbusFrame);
            Log("<< IFdtCommunication.Abort done");
        }

        bool IFdtCommunication.ConnectRequest(IFdtCommunicationEvents callBack, string invokeId, string protocolId, string fieldbusFrame)
        {
            Log(">> IFdtCommunication.ConnectRequest(cb=" + (callBack == null ? "null" : "obj") +
                ", id=" + Q(invokeId) + ", proto=" + Q(protocolId) + ") <<<< KEY");
            IFdtCommunicationEvents wrapped = callBack;
            if (callBack != null)
            {
                var spy = new CommEventsSpy(callBack);
                _prevent.Add(spy);
                wrapped = spy;
                Log("   Wrapped callback in CommEventsSpy");
            }
            bool r = _rComm.ConnectRequest(wrapped, invokeId, protocolId, fieldbusFrame);
            Log("<< IFdtCommunication.ConnectRequest -> " + r);
            return r;
        }

        bool IFdtCommunication.DisconnectRequest(string invokeId, string fieldbusFrame)
        {
            Log(">> IFdtCommunication.DisconnectRequest(id=" + Q(invokeId) + ")");
            bool r = _rComm.DisconnectRequest(invokeId, fieldbusFrame);
            Log("<< IFdtCommunication.DisconnectRequest -> " + r);
            return r;
        }

        bool IFdtCommunication.TransactionRequest(string invokeId, string fieldbusFrame)
        {
            Log(">> IFdtCommunication.TransactionRequest(id=" + Q(invokeId) + ", frame=" + Trunc(fieldbusFrame, 200) + ")");
            bool r = _rComm.TransactionRequest(invokeId, fieldbusFrame);
            Log("<< IFdtCommunication.TransactionRequest -> " + r);
            return r;
        }

        string IFdtCommunication.GetSupportedProtocols()
        {
            Log(">> IFdtCommunication.GetSupportedProtocols()");
            string r = _rComm.GetSupportedProtocols();
            Log("<< IFdtCommunication.GetSupportedProtocols -> " + Trunc(r, 500));
            return r;
        }

        bool IFdtCommunication.SequenceBegin(string fieldbusFrame)
        {
            Log(">> IFdtCommunication.SequenceBegin()");
            bool r = _rComm.SequenceBegin(fieldbusFrame);
            Log("<< IFdtCommunication.SequenceBegin -> " + r);
            return r;
        }

        bool IFdtCommunication.SequenceStart(string fieldbusFrame)
        {
            Log(">> IFdtCommunication.SequenceStart()");
            bool r = _rComm.SequenceStart(fieldbusFrame);
            Log("<< IFdtCommunication.SequenceStart -> " + r);
            return r;
        }

        bool IFdtCommunication.SequenceEnd(string fieldbusFrame)
        {
            Log(">> IFdtCommunication.SequenceEnd()");
            bool r = _rComm.SequenceEnd(fieldbusFrame);
            Log("<< IFdtCommunication.SequenceEnd -> " + r);
            return r;
        }

        // ================================================================
        // IFdtChannelSubTopology
        // ================================================================
        bool IFdtChannelSubTopology.ScanRequest(string invokeId)
        {
            Log(">> IFdtChannelSubTopology.ScanRequest(id=" + Q(invokeId) + ")");
            bool r = _rSubTopo.ScanRequest(invokeId);
            Log("<< IFdtChannelSubTopology.ScanRequest -> " + r);
            return r;
        }

        bool IFdtChannelSubTopology.ValidateAddChild(string childSystemTag)
        {
            Log(">> IFdtChannelSubTopology.ValidateAddChild(tag=" + Q(childSystemTag) + ")");
            bool r = _rSubTopo.ValidateAddChild(childSystemTag);
            Log("<< IFdtChannelSubTopology.ValidateAddChild -> " + r);
            return r;
        }

        bool IFdtChannelSubTopology.ValidateRemoveChild(string childSystemTag)
        {
            Log(">> IFdtChannelSubTopology.ValidateRemoveChild(tag=" + Q(childSystemTag) + ")");
            bool r = _rSubTopo.ValidateRemoveChild(childSystemTag);
            Log("<< IFdtChannelSubTopology.ValidateRemoveChild -> " + r);
            return r;
        }

        void IFdtChannelSubTopology.OnAddChild(string childSystemTag)
        {
            Log(">> IFdtChannelSubTopology.OnAddChild(tag=" + Q(childSystemTag) + ")");
            _rSubTopo.OnAddChild(childSystemTag);
            Log("<< IFdtChannelSubTopology.OnAddChild done");
        }

        void IFdtChannelSubTopology.OnRemoveChild(string childSystemTag)
        {
            Log(">> IFdtChannelSubTopology.OnRemoveChild(tag=" + Q(childSystemTag) + ")");
            _rSubTopo.OnRemoveChild(childSystemTag);
            Log("<< IFdtChannelSubTopology.OnRemoveChild done");
        }

        // ================================================================
        // IDtmActiveXInformation
        // ================================================================
        string IDtmActiveXInformation.GetActiveXGuid(string functionCall)
        {
            Log(">> IDtmActiveXInformation.GetActiveXGuid(func=" + Trunc(functionCall, 200) + ")");
            string r = _rAxInfo.GetActiveXGuid(functionCall);
            Log("<< IDtmActiveXInformation.GetActiveXGuid -> " + Q(r));
            return r;
        }

        string IDtmActiveXInformation.GetActiveXProgId(string functionCall)
        {
            Log(">> IDtmActiveXInformation.GetActiveXProgId(func=" + Trunc(functionCall, 200) + ")");
            string r = _rAxInfo.GetActiveXProgId(functionCall);
            Log("<< IDtmActiveXInformation.GetActiveXProgId -> " + Q(r));
            return r;
        }

        // ================================================================
        // IFdtEvents (container → DTM notifications)
        // ================================================================
        void IFdtEvents.OnChildParameterChanged(string systemTag)
        {
            Log(">> IFdtEvents.OnChildParameterChanged(tag=" + Q(systemTag) + ")");
            _rEvents.OnChildParameterChanged(systemTag);
            Log("<< IFdtEvents.OnChildParameterChanged done");
        }

        void IFdtEvents.OnParameterChanged(string systemTag, string parameter)
        {
            Log(">> IFdtEvents.OnParameterChanged(tag=" + Q(systemTag) + ", param=" + Q(parameter) + ")");
            _rEvents.OnParameterChanged(systemTag, parameter);
            Log("<< IFdtEvents.OnParameterChanged done");
        }

        void IFdtEvents.OnLockDataSet(string systemTag, string userName)
        {
            Log(">> IFdtEvents.OnLockDataSet(tag=" + Q(systemTag) + ", user=" + Q(userName) + ")");
            _rEvents.OnLockDataSet(systemTag, userName);
            Log("<< IFdtEvents.OnLockDataSet done");
        }

        bool IFdtEvents.OnUnlockDataSet(string systemTag, string userName)
        {
            Log(">> IFdtEvents.OnUnlockDataSet(tag=" + Q(systemTag) + ", user=" + Q(userName) + ")");
            bool r = _rEvents.OnUnlockDataSet(systemTag, userName);
            Log("<< IFdtEvents.OnUnlockDataSet -> " + r);
            return r;
        }

        // ================================================================
        // IDtmDocumentation
        // ================================================================
        string IDtmDocumentation.GetDocumentation(string functionCall)
        {
            Log(">> IDtmDocumentation.GetDocumentation(func=" + Trunc(functionCall, 200) + ")");
            string r = _rDoc.GetDocumentation(functionCall);
            Log("<< IDtmDocumentation.GetDocumentation -> " + Trunc(r, 200));
            return r;
        }

        // ================================================================
        // IPersistStreamInit
        // ================================================================
        void IPersistStreamInit.GetClassID(out Guid pClassID)
        {
            Log(">> IPersistStreamInit.GetClassID()");
            _rPSI.GetClassID(out pClassID);
            Log("<< IPersistStreamInit.GetClassID -> " + pClassID.ToString("B"));
        }

        int IPersistStreamInit.IsDirty()
        {
            int hr = _rPSI.IsDirty();
            Log("   IPersistStreamInit.IsDirty -> 0x" + hr.ToString("X8") + (hr == 0 ? " (DIRTY)" : " (clean)"));
            return hr;
        }

        void IPersistStreamInit.Load(IntPtr pStm)
        {
            Log(">> IPersistStreamInit.Load(pStm=0x" + pStm.ToString("X") + ")");
            _rPSI.Load(pStm);
            Log("<< IPersistStreamInit.Load done");
        }

        void IPersistStreamInit.Save(IntPtr pStm, bool fClearDirty)
        {
            Log(">> IPersistStreamInit.Save(clearDirty=" + fClearDirty + ")");
            _rPSI.Save(pStm, fClearDirty);
            Log("<< IPersistStreamInit.Save done");
        }

        void IPersistStreamInit.GetSizeMax(out long pcbSize)
        {
            Log(">> IPersistStreamInit.GetSizeMax()");
            _rPSI.GetSizeMax(out pcbSize);
            Log("<< IPersistStreamInit.GetSizeMax -> " + pcbSize);
        }

        void IPersistStreamInit.InitNew()
        {
            Log(">> IPersistStreamInit.InitNew()");
            _rPSI.InitNew();
            Log("<< IPersistStreamInit.InitNew done");
        }

        // ================================================================
        // IConnectionPointContainer — wrap real CWHart's CPC in SpyCPC
        // ================================================================
        void IConnectionPointContainer.EnumConnectionPoints(out IEnumConnectionPoints ppEnum)
        {
            Log(">> IConnectionPointContainer.EnumConnectionPoints()");
            var realCPC = (IConnectionPointContainer)_real;
            realCPC.EnumConnectionPoints(out ppEnum);
        }

        void IConnectionPointContainer.FindConnectionPoint(ref Guid riid, out IConnectionPoint ppCP)
        {
            string name = IdentifyInterface(riid);
            Log(">> IConnectionPointContainer.FindConnectionPoint(" + name + " " + riid.ToString("B") + ")");
            var realCPC = (IConnectionPointContainer)_real;
            IConnectionPoint realCP;
            realCPC.FindConnectionPoint(ref riid, out realCP);
            if (realCP != null)
            {
                var spy = new SpyConnectionPoint(realCP, riid, name);
                _prevent.Add(spy);
                ppCP = spy;
                Log("<< FindConnectionPoint -> wrapped in SpyConnectionPoint");
            }
            else
            {
                ppCP = null;
                Log("<< FindConnectionPoint -> null");
            }
        }

        // ================================================================
        // Helpers
        // ================================================================
        internal static string IdentifyInterface(Guid iid)
        {
            if (iid == new Guid("00000000-0000-0000-C000-000000000046")) return "IUnknown";
            if (iid == new Guid("00020400-0000-0000-C000-000000000046")) return "IDispatch";
            if (iid == new Guid("036D1481-387B-11D4-86E1-00E0987270B9")) return "IDtm";
            if (iid == new Guid("036D147F-387B-11D4-86E1-00E0987270B9")) return "IDtmInformation";
            if (iid == new Guid("039ECFC4-9CA8-44E6-944D-B37F288A34D8")) return "IFdtCommunication";
            if (iid == new Guid("036D147D-387B-11D4-86E1-00E0987270B9")) return "IDtmParameter";
            if (iid == new Guid("036D1480-387B-11D4-86E1-00E0987270B9")) return "IDtmActiveXInformation";
            if (iid == new Guid("036D1486-387B-11D4-86E1-00E0987270B9")) return "IDtmActiveXControl";
            if (iid == new Guid("036D1484-387B-11D4-86E1-00E0987270B9")) return "IFdtChannelSubTopology";
            if (iid == new Guid("036D1489-387B-11D4-86E1-00E0987270B9")) return "IDtmChannel";
            if (iid == new Guid("036D1488-387B-11D4-86E1-00E0987270B9")) return "IFdtChannel";
            if (iid == new Guid("036D1478-387B-11D4-86E1-00E0987270B9")) return "IFdtEvents";
            if (iid == new Guid("036D147C-387B-11D4-86E1-00E0987270B9")) return "IDtmDocumentation";
            if (iid == new Guid("F15BA42E-BBF1-42ED-8009-7F664A002CFB")) return "IDtmEvents";
            if (iid == new Guid("B196B284-BAB4-101A-B69C-00AA00341D07")) return "IConnectionPointContainer";
            if (iid == new Guid("B196B286-BAB4-101A-B69C-00AA00341D07")) return "IConnectionPoint";
            if (iid == new Guid("7FD52380-4E07-101B-AE2D-08002B2EC713")) return "IPersistStreamInit";
            if (iid == new Guid("37D84F60-42CB-11CE-8135-00AA004BB851")) return "IPersistPropertyBag";
            if (iid == new Guid("0000010A-0000-0000-C000-000000000046")) return "IPersistStorage";
            if (iid == new Guid("0000010C-0000-0000-C000-000000000046")) return "IPersist";
            if (iid == new Guid("00000112-0000-0000-C000-000000000046")) return "IOleObject";
            if (iid == new Guid("00000113-0000-0000-C000-000000000046")) return "IOleInPlaceObject";
            if (iid == new Guid("00000114-0000-0000-C000-000000000046")) return "IOleWindow";
            if (iid == new Guid("B196B288-BAB4-101A-B69C-00AA00341D07")) return "IOleControl";
            if (iid == new Guid("CF51ED10-62FE-11CF-BF86-00A0C9034836")) return "IQuickActivate";
            if (iid == new Guid("036D1487-387B-11D4-86E1-00E0987270B9")) return "IFdtContainer";
            if (iid == new Guid("036D1485-387B-11D4-86E1-00E0987270B9")) return "IFdtCommunicationEvents";
            if (iid == new Guid("E4F31A10-45BF-11D4-BBB3-0060080993FF")) return "IFdtChannelCollection";
            if (iid == new Guid("036D1475-387B-11D4-86E1-00E0987270B9")) return "IFdtFunctionBlockData";
            if (iid == new Guid("D67240E4-664B-44B0-B692-A1D1ED3FB8F8")) return "IDtmSingleDeviceDataAccess";
            if (iid == new Guid("96341E37-9611-46BA-80ED-A85BD73BF518")) return "IBtm";
            if (iid == new Guid("51E1F44B-D6A1-423D-B11F-AD38EDE78047")) return "IDtm2";
            return "unknown_" + iid.ToString("B");
        }

        internal static void Log(string msg)
        {
            try
            {
                string dir = Path.GetDirectoryName(LogPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string line = DateTime.Now.ToString("HH:mm:ss.fff") + " [SPY] " + msg;
                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
            catch { }
        }

        private static string Q(string s) { return s ?? "null"; }
        private static string Trunc(string s, int max)
        {
            if (s == null) return "null";
            return s.Length <= max ? s : s.Substring(0, max) + "...(" + s.Length + " total)";
        }

        public void Dispose()
        {
            Log("CWHartProxy.Dispose()");
            if (_real != null)
            {
                try { Marshal.ReleaseComObject(_real); } catch { }
                _real = null;
            }
        }
    }

    // ================================================================
    // ContainerSpy — wraps PACTware's IFdtContainer to log DTM→container calls
    // ================================================================
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    internal class ContainerSpy : IFdtContainer
    {
        private readonly IFdtContainer _real;
        public ContainerSpy(IFdtContainer real) { _real = real; }

        bool IFdtContainer.SaveRequest(string systemTag)
        {
            CWHartProxy.Log(">>> CONTAINER.SaveRequest(tag=" + systemTag + ") <<<< KEY");
            bool r = _real.SaveRequest(systemTag);
            CWHartProxy.Log("<<< CONTAINER.SaveRequest -> " + r);
            return r;
        }

        bool IFdtContainer.LockDataSet(string systemTag)
        {
            CWHartProxy.Log(">>> CONTAINER.LockDataSet(tag=" + systemTag + ")");
            bool r = _real.LockDataSet(systemTag);
            CWHartProxy.Log("<<< CONTAINER.LockDataSet -> " + r);
            return r;
        }

        bool IFdtContainer.UnlockDataSet(string systemTag)
        {
            CWHartProxy.Log(">>> CONTAINER.UnlockDataSet(tag=" + systemTag + ")");
            bool r = _real.UnlockDataSet(systemTag);
            CWHartProxy.Log("<<< CONTAINER.UnlockDataSet -> " + r);
            return r;
        }

        string IFdtContainer.GetXmlSchemaPath()
        {
            CWHartProxy.Log(">>> CONTAINER.GetXmlSchemaPath()");
            string r = _real.GetXmlSchemaPath();
            CWHartProxy.Log("<<< CONTAINER.GetXmlSchemaPath -> " + (r ?? "null"));
            return r;
        }
    }

    // ================================================================
    // CommEventsSpy — wraps PACTware's IFdtCommunicationEvents callback
    // to log CWHart→PACTware async responses (OnConnectResponse etc.)
    // ================================================================
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    internal class CommEventsSpy : IFdtCommunicationEvents
    {
        private readonly IFdtCommunicationEvents _real;
        public CommEventsSpy(IFdtCommunicationEvents real) { _real = real; }

        void IFdtCommunicationEvents.OnAbort(string communicationReference)
        {
            CWHartProxy.Log(">>> CALLBACK.OnAbort(ref=" + communicationReference + ")");
            _real.OnAbort(communicationReference);
            CWHartProxy.Log("<<< CALLBACK.OnAbort forwarded");
        }

        void IFdtCommunicationEvents.OnConnectResponse(string invokeId, string response)
        {
            CWHartProxy.Log(">>> CALLBACK.OnConnectResponse(id=" + invokeId + ", resp=" +
                (response != null ? response.Length + "ch" : "null") + ") <<<< KEY");
            if (response != null) CWHartProxy.Log("    RESP XML: " + (response.Length > 2000 ? response.Substring(0, 2000) + "..." : response));
            _real.OnConnectResponse(invokeId, response);
            CWHartProxy.Log("<<< CALLBACK.OnConnectResponse forwarded");
        }

        void IFdtCommunicationEvents.OnDisconnectResponse(string invokeId, string response)
        {
            CWHartProxy.Log(">>> CALLBACK.OnDisconnectResponse(id=" + invokeId + ")");
            _real.OnDisconnectResponse(invokeId, response);
            CWHartProxy.Log("<<< CALLBACK.OnDisconnectResponse forwarded");
        }

        void IFdtCommunicationEvents.OnTransactionResponse(string invokeId, string response)
        {
            CWHartProxy.Log(">>> CALLBACK.OnTransactionResponse(id=" + invokeId + ")");
            _real.OnTransactionResponse(invokeId, response);
            CWHartProxy.Log("<<< CALLBACK.OnTransactionResponse forwarded");
        }
    }
}
