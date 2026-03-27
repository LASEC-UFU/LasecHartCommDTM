// FDT 1.x COM interface definitions used by the spy to intercept method calls.
// Declared as [ComImport] so we can:
//   1) Cast the real CWHart RCW to call its methods (consume)
//   2) Implement on CWHartProxy to expose to PACTware (produce CCW)
// Method order MUST match FDT100.dll TypeLib vtable layout exactly.

using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace CWHartSpy
{
    [ComImport, Guid("036D1487-387B-11D4-86E1-00E0987270B9")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IFdtContainer
    {
        [DispId(1)] [return: MarshalAs(UnmanagedType.Bool)]
        bool SaveRequest([MarshalAs(UnmanagedType.BStr)] string systemTag);

        [DispId(2)] [return: MarshalAs(UnmanagedType.Bool)]
        bool LockDataSet([MarshalAs(UnmanagedType.BStr)] string systemTag);

        [DispId(3)] [return: MarshalAs(UnmanagedType.Bool)]
        bool UnlockDataSet([MarshalAs(UnmanagedType.BStr)] string systemTag);

        [DispId(4)] [return: MarshalAs(UnmanagedType.BStr)]
        string GetXmlSchemaPath();
    }

    [ComImport, Guid("036D1481-387B-11D4-86E1-00E0987270B9")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IDtm
    {
        [DispId(1)] [return: MarshalAs(UnmanagedType.Bool)]
        bool Environment(
            [MarshalAs(UnmanagedType.BStr)] string systemTag,
            [MarshalAs(UnmanagedType.Interface)] IFdtContainer container);

        [DispId(2)] [return: MarshalAs(UnmanagedType.Bool)]
        bool InitNew([MarshalAs(UnmanagedType.BStr)] string deviceType);

        [DispId(3)] [return: MarshalAs(UnmanagedType.Bool)]
        bool Config([MarshalAs(UnmanagedType.BStr)] string userInfo);

        [DispId(4)] [return: MarshalAs(UnmanagedType.Bool)]
        bool SetCommunication([MarshalAs(UnmanagedType.Interface)] object communication);

        [DispId(5)] [return: MarshalAs(UnmanagedType.Bool)]
        bool PrepareToRelease();

        [DispId(6)] [return: MarshalAs(UnmanagedType.Bool)]
        bool PrepareToReleaseCommunication();

        [DispId(7)] [return: MarshalAs(UnmanagedType.Bool)]
        bool ReleaseCommunication();

        [DispId(8)] [return: MarshalAs(UnmanagedType.Bool)]
        bool PrepareToDelete();

        [DispId(9)] [return: MarshalAs(UnmanagedType.Bool)]
        bool SetLanguage(int languageId);

        [DispId(10)] [return: MarshalAs(UnmanagedType.BStr)]
        string GetFunctions([MarshalAs(UnmanagedType.BStr)] string operationState);

        [DispId(11)] [return: MarshalAs(UnmanagedType.Bool)]
        bool InvokeFunctionRequest(
            [MarshalAs(UnmanagedType.BStr)] string invokeId,
            [MarshalAs(UnmanagedType.BStr)] string functionCall);

        [DispId(12)] [return: MarshalAs(UnmanagedType.Bool)]
        bool PrivateDialogEnabled([MarshalAs(UnmanagedType.Bool)] bool enabled);
    }

    [ComImport, Guid("036D147F-387B-11D4-86E1-00E0987270B9")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IDtmInformation
    {
        [DispId(1)] [return: MarshalAs(UnmanagedType.BStr)]
        string GetInformation();
    }

    [ComImport, Guid("036D147D-387B-11D4-86E1-00E0987270B9")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IDtmParameter
    {
        [DispId(1)] [return: MarshalAs(UnmanagedType.BStr)]
        string GetParameters([MarshalAs(UnmanagedType.BStr)] string parameterPath);

        [DispId(2)] [return: MarshalAs(UnmanagedType.Bool)]
        bool SetParameters(
            [MarshalAs(UnmanagedType.BStr)] string parameterPath,
            [MarshalAs(UnmanagedType.BStr)] string fdtXmlDocument);
    }

    [ComImport, Guid("036D1489-387B-11D4-86E1-00E0987270B9")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IDtmChannel
    {
        [DispId(1)] [return: MarshalAs(UnmanagedType.Interface)]
        object GetChannels();
    }

    [ComImport, Guid("036D1488-387B-11D4-86E1-00E0987270B9")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IFdtChannel
    {
        [DispId(1)] [return: MarshalAs(UnmanagedType.BStr)]
        string GetChannelPath();

        [DispId(2)] [return: MarshalAs(UnmanagedType.BStr)]
        string GetChannelParameters(
            [MarshalAs(UnmanagedType.BStr)] string parameterPath,
            [MarshalAs(UnmanagedType.BStr)] string protocolId);

        [DispId(3)] [return: MarshalAs(UnmanagedType.Bool)]
        bool SetChannelParameters(
            [MarshalAs(UnmanagedType.BStr)] string parameterPath,
            [MarshalAs(UnmanagedType.BStr)] string protocolId,
            [MarshalAs(UnmanagedType.BStr)] string xmlDocument);
    }

    [ComImport, Guid("036D1485-387B-11D4-86E1-00E0987270B9")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IFdtCommunicationEvents
    {
        [DispId(1)] void OnAbort([MarshalAs(UnmanagedType.BStr)] string communicationReference);

        [DispId(2)] void OnConnectResponse(
            [MarshalAs(UnmanagedType.BStr)] string invokeId,
            [MarshalAs(UnmanagedType.BStr)] string response);

        [DispId(3)] void OnDisconnectResponse(
            [MarshalAs(UnmanagedType.BStr)] string invokeId,
            [MarshalAs(UnmanagedType.BStr)] string response);

        [DispId(4)] void OnTransactionResponse(
            [MarshalAs(UnmanagedType.BStr)] string invokeId,
            [MarshalAs(UnmanagedType.BStr)] string response);
    }

    [ComImport, Guid("039ECFC4-9CA8-44E6-944D-B37F288A34D8")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IFdtCommunication
    {
        [DispId(1)] void Abort([MarshalAs(UnmanagedType.BStr)] string fieldbusFrame);

        [DispId(2)] [return: MarshalAs(UnmanagedType.Bool)]
        bool ConnectRequest(
            [MarshalAs(UnmanagedType.Interface)] IFdtCommunicationEvents callBack,
            [MarshalAs(UnmanagedType.BStr)] string invokeId,
            [MarshalAs(UnmanagedType.BStr)] string protocolId,
            [MarshalAs(UnmanagedType.BStr)] string fieldbusFrame);

        [DispId(3)] [return: MarshalAs(UnmanagedType.Bool)]
        bool DisconnectRequest(
            [MarshalAs(UnmanagedType.BStr)] string invokeId,
            [MarshalAs(UnmanagedType.BStr)] string fieldbusFrame);

        [DispId(4)] [return: MarshalAs(UnmanagedType.Bool)]
        bool TransactionRequest(
            [MarshalAs(UnmanagedType.BStr)] string invokeId,
            [MarshalAs(UnmanagedType.BStr)] string fieldbusFrame);

        [DispId(5)] [return: MarshalAs(UnmanagedType.BStr)]
        string GetSupportedProtocols();

        [DispId(6)] [return: MarshalAs(UnmanagedType.Bool)]
        bool SequenceBegin([MarshalAs(UnmanagedType.BStr)] string fieldbusFrame);

        [DispId(7)] [return: MarshalAs(UnmanagedType.Bool)]
        bool SequenceStart([MarshalAs(UnmanagedType.BStr)] string fieldbusFrame);

        [DispId(8)] [return: MarshalAs(UnmanagedType.Bool)]
        bool SequenceEnd([MarshalAs(UnmanagedType.BStr)] string fieldbusFrame);
    }

    [ComImport, Guid("036D1484-387B-11D4-86E1-00E0987270B9")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IFdtChannelSubTopology
    {
        [DispId(1)] [return: MarshalAs(UnmanagedType.Bool)]
        bool ScanRequest([MarshalAs(UnmanagedType.BStr)] string invokeId);

        [DispId(2)] [return: MarshalAs(UnmanagedType.Bool)]
        bool ValidateAddChild([MarshalAs(UnmanagedType.BStr)] string childSystemTag);

        [DispId(4)] [return: MarshalAs(UnmanagedType.Bool)]
        bool ValidateRemoveChild([MarshalAs(UnmanagedType.BStr)] string childSystemTag);

        [DispId(5)] void OnAddChild([MarshalAs(UnmanagedType.BStr)] string childSystemTag);

        [DispId(6)] void OnRemoveChild([MarshalAs(UnmanagedType.BStr)] string childSystemTag);
    }

    [ComImport, Guid("036D1480-387B-11D4-86E1-00E0987270B9")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IDtmActiveXInformation
    {
        [DispId(1)] [return: MarshalAs(UnmanagedType.BStr)]
        string GetActiveXGuid([MarshalAs(UnmanagedType.BStr)] string functionCall);

        [DispId(2)] [return: MarshalAs(UnmanagedType.BStr)]
        string GetActiveXProgId([MarshalAs(UnmanagedType.BStr)] string functionCall);
    }

    [ComImport, Guid("036D1486-387B-11D4-86E1-00E0987270B9")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IDtmActiveXControl
    {
        [DispId(1)] [return: MarshalAs(UnmanagedType.Bool)]
        bool Init(
            [MarshalAs(UnmanagedType.BStr)] string invokeId,
            [MarshalAs(UnmanagedType.BStr)] string functionCall,
            [MarshalAs(UnmanagedType.Interface)] IDtm dtm);

        [DispId(2)] [return: MarshalAs(UnmanagedType.Bool)]
        bool PrepareToRelease();
    }

    [ComImport, Guid("036D1478-387B-11D4-86E1-00E0987270B9")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IFdtEvents
    {
        [DispId(1)] void OnChildParameterChanged([MarshalAs(UnmanagedType.BStr)] string systemTag);

        [DispId(2)] void OnParameterChanged(
            [MarshalAs(UnmanagedType.BStr)] string systemTag,
            [MarshalAs(UnmanagedType.BStr)] string parameter);

        [DispId(3)] void OnLockDataSet(
            [MarshalAs(UnmanagedType.BStr)] string systemTag,
            [MarshalAs(UnmanagedType.BStr)] string userName);

        [DispId(4)] [return: MarshalAs(UnmanagedType.Bool)]
        bool OnUnlockDataSet(
            [MarshalAs(UnmanagedType.BStr)] string systemTag,
            [MarshalAs(UnmanagedType.BStr)] string userName);
    }

    [ComImport, Guid("036D147C-387B-11D4-86E1-00E0987270B9")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IDtmDocumentation
    {
        [DispId(1)] [return: MarshalAs(UnmanagedType.BStr)]
        string GetDocumentation([MarshalAs(UnmanagedType.BStr)] string functionCall);
    }

    // IPersistStreamInit — IUnknown-based persistence interface
    // Vtable: IUnknown(3) + GetClassID(1) + IsDirty(1) + Load(1) + Save(1) + GetSizeMax(1) + InitNew(1)
    [ComImport, Guid("7FD52380-4E07-101B-AE2D-08002B2EC713")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPersistStreamInit
    {
        void GetClassID(out Guid pClassID);
        [PreserveSig] int IsDirty();
        void Load(IntPtr pStm);
        void Save(IntPtr pStm, [MarshalAs(UnmanagedType.Bool)] bool fClearDirty);
        void GetSizeMax(out long pcbSize);
        void InitNew();
    }

    // E4F31A10 — IFdtChannelCollection
    [ComImport, Guid("E4F31A10-45BF-11D4-BBB3-0060080993FF")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IFdtChannelCollection
    {
        [DispId(1)] [return: MarshalAs(UnmanagedType.Interface)]
        object get_Item([In] ref object pvarIndex);

        [DispId(2)] int Count { [return: MarshalAs(UnmanagedType.I4)] get; }

        [DispId(-4)] [return: MarshalAs(UnmanagedType.Interface)]
        IEnumerator GetEnumerator();
    }
}
