using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace CWHartSpy
{
    // ================================================================
    // SpyConnectionPointContainer — wraps real IConnectionPointContainer
    // Logs all FindConnectionPoint / EnumConnectionPoints calls
    // ================================================================
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public class SpyConnectionPointContainer : IConnectionPointContainer
    {
        private readonly IConnectionPointContainer _realCPC;
        private readonly List<SpyConnectionPoint> _spyCPs = new List<SpyConnectionPoint>();

        public SpyConnectionPointContainer(IConnectionPointContainer realCPC)
        {
            _realCPC = realCPC;
            CWHartProxy.Log("SpyCPC created, wrapping real CPC");
        }

        public void EnumConnectionPoints(out IEnumConnectionPoints ppEnum)
        {
            CWHartProxy.Log("SpyCPC.EnumConnectionPoints() called");
            _realCPC.EnumConnectionPoints(out ppEnum);

            // Log what connection points exist
            if (ppEnum != null)
            {
                IConnectionPoint[] cps = new IConnectionPoint[10];
                IntPtr pFetched = Marshal.AllocCoTaskMem(sizeof(int));
                ppEnum.Next(cps.Length, cps, pFetched);
                int fetched = Marshal.ReadInt32(pFetched);
                Marshal.FreeCoTaskMem(pFetched);
                CWHartProxy.Log("  EnumConnectionPoints: " + fetched + " connection point(s)");
                for (int i = 0; i < fetched; i++)
                {
                    Guid iid;
                    cps[i].GetConnectionInterface(out iid);
                    string name = CWHartProxy.IdentifyInterface(iid);
                    CWHartProxy.Log("  CP[" + i + "] IID=" + iid.ToString("B") + " (" + name + ")");
                }
                ppEnum.Reset(); // reset for caller
            }
        }

        public void FindConnectionPoint(ref Guid riid, out IConnectionPoint ppCP)
        {
            string name = CWHartProxy.IdentifyInterface(riid);
            CWHartProxy.Log("SpyCPC.FindConnectionPoint(" + name + " " + riid.ToString("B") + ")");

            try
            {
                IConnectionPoint realCP;
                _realCPC.FindConnectionPoint(ref riid, out realCP);

                if (realCP != null)
                {
                    CWHartProxy.Log("  FindConnectionPoint -> real CWHart has this CP, wrapping in spy");
                    var spy = new SpyConnectionPoint(realCP, riid, name);
                    _spyCPs.Add(spy); // prevent GC
                    ppCP = spy;
                }
                else
                {
                    CWHartProxy.Log("  FindConnectionPoint -> real CWHart returned null CP");
                    ppCP = null;
                }
            }
            catch (Exception ex)
            {
                CWHartProxy.Log("  FindConnectionPoint -> ERROR: " + ex.Message + " (0x" +
                    Marshal.GetHRForException(ex).ToString("X8") + ")");
                throw;
            }
        }
    }

    // ================================================================
    // SpyConnectionPoint — wraps real IConnectionPoint
    // Logs Advise/Unadvise calls and creates SpyEventSink for
    // intercepting events fired by CWHart
    // ================================================================
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public class SpyConnectionPoint : IConnectionPoint
    {
        private readonly IConnectionPoint _realCP;
        private readonly Guid _iid;
        private readonly string _iidName;

        // Track spy sinks by cookie so we can clean up on Unadvise
        private readonly Dictionary<int, SpyEventSink> _spySinks =
            new Dictionary<int, SpyEventSink>();

        public SpyConnectionPoint(IConnectionPoint realCP, Guid iid, string iidName)
        {
            _realCP = realCP;
            _iid = iid;
            _iidName = iidName;
            CWHartProxy.Log("SpyCP created for " + iidName + " " + iid.ToString("B"));
        }

        public void GetConnectionInterface(out Guid pIID)
        {
            _realCP.GetConnectionInterface(out pIID);
            CWHartProxy.Log("SpyCP.GetConnectionInterface() -> " + pIID.ToString("B") +
                " (" + CWHartProxy.IdentifyInterface(pIID) + ")");
        }

        public void GetConnectionPointContainer(out IConnectionPointContainer ppCPC)
        {
            CWHartProxy.Log("SpyCP.GetConnectionPointContainer()");
            _realCP.GetConnectionPointContainer(out ppCPC);
        }

        public void Advise(object pUnkSink, out int pdwCookie)
        {
            CWHartProxy.Log("*** SpyCP.Advise() — PACTware IS SUBSCRIBING to " + _iidName + " events! ***");
            CWHartProxy.Log("  PACTware sink object: " + (pUnkSink != null ? pUnkSink.GetType().FullName : "null"));

            try
            {
                // Log what interfaces PACTware's sink supports
                if (pUnkSink != null)
                {
                    IntPtr pUnk = Marshal.GetIUnknownForObject(pUnkSink);
                    try
                    {
                        Guid gDisp = new Guid("00020400-0000-0000-C000-000000000046");
                        Guid gEvt = new Guid("F15BA42E-BBF1-42ED-8009-7F664A002CFB");
                        IntPtr p;
                        int hr;

                        hr = Marshal.QueryInterface(pUnk, ref gDisp, out p);
                        CWHartProxy.Log("  Sink supports IDispatch? " + (hr == 0 ? "YES" : "NO"));
                        if (hr == 0) Marshal.Release(p);

                        hr = Marshal.QueryInterface(pUnk, ref gEvt, out p);
                        CWHartProxy.Log("  Sink supports IDtmEvents {F15BA42E}? " + (hr == 0 ? "YES" : "NO"));
                        if (hr == 0) Marshal.Release(p);
                    }
                    finally
                    {
                        Marshal.Release(pUnk);
                    }
                }

                // Create spy event sink that wraps PACTware's sink
                var spySink = new SpyEventSink(pUnkSink);

                // Subscribe our spy to the real CWHart's connection point
                _realCP.Advise(spySink, out pdwCookie);
                _spySinks[pdwCookie] = spySink;

                CWHartProxy.Log("  Advise -> OK, cookie=" + pdwCookie +
                    " (spy sink subscribed to real CWHart, will forward events)");
            }
            catch (Exception ex)
            {
                CWHartProxy.Log("  Advise -> ERROR: " + ex.Message);
                throw;
            }
        }

        public void Unadvise(int dwCookie)
        {
            CWHartProxy.Log("SpyCP.Unadvise(cookie=" + dwCookie + ") — PACTware unsubscribing from " + _iidName);
            try
            {
                _realCP.Unadvise(dwCookie);
                _spySinks.Remove(dwCookie);
                CWHartProxy.Log("  Unadvise -> OK");
            }
            catch (Exception ex)
            {
                CWHartProxy.Log("  Unadvise -> ERROR: " + ex.Message);
                throw;
            }
        }

        public void EnumConnections(out IEnumConnections ppEnum)
        {
            CWHartProxy.Log("SpyCP.EnumConnections()");
            _realCP.EnumConnections(out ppEnum);
        }
    }

    // ================================================================
    // IDtmEvents — source interface definition for the event sink.
    // Same GUID as CWHart's/FDT100's IDtmEvents.
    // CWHart fires events via IDispatch.Invoke on these DispIds.
    // ================================================================
    [Guid("F15BA42E-BBF1-42ED-8009-7F664A002CFB")]
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    [ComVisible(true)]
    public interface IDtmEventsSink
    {
        [DispId(1)]  void OnParameterChanged([MarshalAs(UnmanagedType.BStr)] string systemTag, [MarshalAs(UnmanagedType.BStr)] string parameter);
        [DispId(2)]  void OnErrorMessage([MarshalAs(UnmanagedType.BStr)] string systemTag, [MarshalAs(UnmanagedType.BStr)] string errorMessage);
        [DispId(3)]  void OnProgress([MarshalAs(UnmanagedType.BStr)] string systemTag, [MarshalAs(UnmanagedType.BStr)] string title, short percent, [MarshalAs(UnmanagedType.Bool)] bool show);
        [DispId(4)]  void OnUploadFinished([MarshalAs(UnmanagedType.BStr)] string invokeId, [MarshalAs(UnmanagedType.Bool)] bool success);
        [DispId(5)]  void OnDownloadFinished([MarshalAs(UnmanagedType.BStr)] string invokeId, [MarshalAs(UnmanagedType.Bool)] bool success);
        [DispId(6)]  void OnApplicationClosed([MarshalAs(UnmanagedType.BStr)] string invokeId);
        // DispId 7 does not exist (gap in FDT spec)
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

    // ================================================================
    // SpyEventSink — sits between CWHart and PACTware for events.
    // When CWHart fires an event, it arrives here. We log it and
    // forward to PACTware's original sink via dynamic dispatch.
    // ================================================================
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public class SpyEventSink : IDtmEventsSink
    {
        private readonly object _pactwareSink;  // PACTware's original event sink
        private readonly dynamic _sinkDynamic;  // for forwarding via IDispatch

        public SpyEventSink(object pactwareSink)
        {
            _pactwareSink = pactwareSink;
            _sinkDynamic = pactwareSink;
            CWHartProxy.Log("SpyEventSink created, wrapping PACTware's sink");
        }

        // Helper: log event and forward to PACTware's sink
        private void Forward(string eventName, string methodName, object[] args)
        {
            CWHartProxy.Log(">>> EVENT from CWHart: " + eventName);
            try
            {
                // Forward via late-binding (IDispatch.Invoke on PACTware's sink)
                _pactwareSink.GetType().InvokeMember(
                    methodName,
                    System.Reflection.BindingFlags.InvokeMethod,
                    null, _pactwareSink, args);
                CWHartProxy.Log("  -> forwarded to PACTware OK");
            }
            catch (Exception ex)
            {
                CWHartProxy.Log("  -> FORWARD ERROR: " + ex.GetType().Name + ": " + ex.Message);
                // Event still logged even if forwarding fails
            }
        }

        public void OnParameterChanged(string systemTag, string parameter)
        {
            Forward("OnParameterChanged(tag=" + systemTag + ", param=" + parameter + ")",
                "OnParameterChanged", new object[] { systemTag, parameter });
        }

        public void OnErrorMessage(string systemTag, string errorMessage)
        {
            Forward("OnErrorMessage(tag=" + systemTag + ", msg=" + errorMessage + ")",
                "OnErrorMessage", new object[] { systemTag, errorMessage });
        }

        public void OnProgress(string systemTag, string title, short percent, bool show)
        {
            Forward("OnProgress(tag=" + systemTag + ", title=" + title + ", pct=" + percent + ", show=" + show + ")",
                "OnProgress", new object[] { systemTag, title, percent, show });
        }

        public void OnUploadFinished(string invokeId, bool success)
        {
            Forward("OnUploadFinished(id=" + invokeId + ", success=" + success + ")",
                "OnUploadFinished", new object[] { invokeId, success });
        }

        public void OnDownloadFinished(string invokeId, bool success)
        {
            Forward("OnDownloadFinished(id=" + invokeId + ", success=" + success + ")",
                "OnDownloadFinished", new object[] { invokeId, success });
        }

        public void OnApplicationClosed(string invokeId)
        {
            Forward("OnApplicationClosed(id=" + invokeId + ")",
                "OnApplicationClosed", new object[] { invokeId });
        }

        public void OnFunctionChanged(string systemTag)
        {
            Forward("OnFunctionChanged(tag=" + systemTag + ")",
                "OnFunctionChanged", new object[] { systemTag });
        }

        public void OnChannelFunctionChanged(string systemTag, string channelPath)
        {
            Forward("OnChannelFunctionChanged(tag=" + systemTag + ", ch=" + channelPath + ")",
                "OnChannelFunctionChanged", new object[] { systemTag, channelPath });
        }

        public void OnPrint(string systemTag, string functionCall)
        {
            Forward("OnPrint(tag=" + systemTag + ")",
                "OnPrint", new object[] { systemTag, functionCall });
        }

        public void OnNavigation(string systemTag)
        {
            Forward("OnNavigation(tag=" + systemTag + ")",
                "OnNavigation", new object[] { systemTag });
        }

        public void OnOnlineStateChanged(string systemTag, bool onlineState)
        {
            Forward("OnOnlineStateChanged(tag=" + systemTag + ", ONLINE=" + onlineState + ") <<<< KEY EVENT",
                "OnOnlineStateChanged", new object[] { systemTag, onlineState });
        }

        public void OnPreparedToRelease(string systemTag)
        {
            Forward("OnPreparedToRelease(tag=" + systemTag + ")",
                "OnPreparedToRelease", new object[] { systemTag });
        }

        public void OnPreparedToReleaseCommunication(string systemTag)
        {
            Forward("OnPreparedToReleaseCommunication(tag=" + systemTag + ")",
                "OnPreparedToReleaseCommunication", new object[] { systemTag });
        }

        public void OnInvokedFunctionFinished(string invokeId, bool success)
        {
            Forward("OnInvokedFunctionFinished(id=" + invokeId + ", success=" + success + ")",
                "OnInvokedFunctionFinished", new object[] { invokeId, success });
        }

        public void OnScanResponse(string invokeId, string response)
        {
            Forward("OnScanResponse(id=" + invokeId + ", resp=" + (response != null ? response.Length + "chars" : "null") + ")",
                "OnScanResponse", new object[] { invokeId, response });
        }
    }
}
